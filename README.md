# Channel Chat — Real-Time WebSocket Messaging Platform

A full-stack real-time chat application built with ASP.NET Core Minimal API, JWT authentication with dual-mode refresh token rotation (cookie + header), SignalR WebSocket messaging, Flutter Web UI, and automated CI/CD deployment to Monster ASP.NET.

**Author:** Engineer Ayman Mohamed

---

## Table of Contents

- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Authentication Flow](#authentication-flow)
- [SignalR Flow](#signalr-flow)
- [REST API Endpoints](#rest-api-endpoints)
- [Project Architecture](#project-architecture)
- [Client Libraries](#client-libraries)
- [Build Phases](#build-phases)
- [Deployment Architecture](#deployment-architecture)
- [Live URL & Test Credentials](#live-url--test-credentials)
- [Development Setup](#development-setup)
- [Documentation](#documentation)

---

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| API Style | Minimal API (C#) |
| Real-Time | SignalR with `[Authorize]` hub |
| ORM | Entity Framework Core 10 |
| Database | SQL Server |
| Authentication | JWT Bearer + refresh token rotation with theft detection |
| Password Hashing | BCrypt (BCrypt.Net-Next) |
| XSS Protection | Custom HTML sanitizer (`StripHtml`) |
| API Documentation | Swagger UI (Swashbuckle 10 + Microsoft OpenApi 2) |
| CI/CD | GitHub Actions → WebDeploy |
| Hosting | Monster ASP.NET |
| Frontend (Web) | Flutter Web |
| Frontend (Mobile) | Flutter (Android / iOS) |
| Web Client (JS) | Vanilla JavaScript + `@microsoft/signalr` |
| Dart Client | `signalr_netcore` |
| React Client | `@microsoft/signalr` |

---

## Project Structure

EnexabitWebSocketProject.App/
├── Data/
│   ├── AppDbContext.cs          # EF Core DbContext
│   └── DbInitializer.cs         # Seeds channels + test users
├── DTOs/
│   ├── LoginRequest.cs
│   ├── LoginResponse.cs         # +RefreshToken? (populated for mobile)
│   ├── RefreshRequest.cs        # Body fallback for mobile refresh
│   ├── RegisterRequest.cs
│   └── SendMessageRequest.cs
├── Hubs/
│   └── ChannelHub.cs            # SignalR hub with client type tracking
├── Migrations/                  # EF Core migrations
├── Models/
│   ├── Channel.cs
│   ├── Message.cs
│   ├── RefreshToken.cs
│   └── User.cs
├── Services/
│   ├── AuthService.cs           # Register + authenticate
│   ├── MessageServices.cs       # Persist + retrieve messages
│   ├── Sanitizer.cs             # StripHtml() XSS prevention
│   └── TokenService.cs          # JWT generation + refresh rotation
├── wwwroot/
│   ├── index.html               # Vanilla JS chat client (legacy)
│   └── flutterApp/              # Flutter Web build output
│       ├── index.html
│       ├── main.dart.js
│       ├── flutter_bootstrap.js
│       └── assets/
├── Program.cs                   # Entry point — all endpoints and middleware
└── EnexabitWebSocketProject.App.csproj

---

## Authentication Flow

### Dual-Mode Token Delivery

The refresh token is delivered via multiple channels, selected by priority. Web clients continue using cookies. Mobile clients opt into header/body delivery by sending `X-Client-Type: mobile`.

Source Priority:
1. Cookie         → web clients (unchanged)
2. X-Refresh-Token header → mobile clients (recommended)
3. JSON body      → mobile fallback

### Login (POST /api/auth/login)

  Client (web)                ASP.NET API                  Client (mobile)
    │                             │                            │
    │  POST /api/auth/login       │                            │
    │  (no X-Client-Type)         │  POST /api/auth/login      │
    │───────────────────────────> │  X-Client-Type: mobile     │
    │                             │<───────────────────────────│
    │                             │                            │
    │  Set-Cookie: refreshToken   │  { accessToken             │
    │  (HttpOnly, Secure,         │    displayName             │
    │   SameSite=Strict)          │    refreshToken  }         │
    │<───────────────────────────│───────────────────────────>│
    │  { accessToken              │                            │
    │    displayName              │                            │
    │    refreshToken: null }     │                            │

### Refresh (POST /api/auth/refresh)

  Client (web)                ASP.NET API                  Client (mobile)
    │                             │                            │
    │  POST /api/auth/refresh     │                            │
    │  Cookie: refreshToken       │  POST /api/auth/refresh    │
    │───────────────────────────> │  X-Refresh-Token: <token>  │
    │                             │<───────────────────────────│
    │                             │  Validate + rotate token   │
    │                             │                            │
    │  { accessToken }            │  { accessToken             │
    │<───────────────────────────│    refreshToken  }          │
    │                             │───────────────────────────>│

### Logout (POST /api/auth/logout)

  Client (web)                ASP.NET API                  Client (mobile)
    │                             │                            │
    │  POST /api/auth/logout      │                            │
    │  Authorization: Bearer JWT  │  Authorization: Bearer JWT │
    │───────────────────────────> │  X-Refresh-Token: <token>  │
    │                             │<───────────────────────────│
    │                             │  Revoke ALL user tokens    │
    │                             │  + specific token by value │
    │  Clear-Cookie               │                            │
    │<───────────────────────────│───────────────────────────>│

### Security Features

- **Refresh token rotation:** Each refresh invalidates the old token and issues a new one
- **Theft detection:** If a revoked token is reused, all user sessions are invalidated
- **HttpOnly/Secure cookie** for web clients (inaccessible to JavaScript)
- **Dual-mode delivery:** Mobile receives token in response body via `X-Refresh-Token` header
- **XSS prevention:** All message text runs through `Sanitizer.StripHtml()`
- **JWT stored in memory only** (web) or secure storage (mobile)

---

## SignalR Flow

  Client                    SignalR Hub                      SQL Server
    │                            │                            │
    │  GET /channelHub           │                            │
    │  ?access_token=<JWT>       │                            │
    │───────────────────────────>│                            │
    │                            │  OnMessageReceived event   │
    │                            │  validates JWT             │
    │                            │                            │
    │  Invoke: JoinChannel(1)    │                            │
    │───────────────────────────>│                            │
    │                            │  Fetch recent messages     │
    │  On: JoinedChannel(...)  │───────────────────────────>│
    │<───────────────────────────│<───────────────────────────│
    │                            │  Add connection to group   │
    │  On: UserJoined("Alice")   │  Broadcast to group        │
    │<───────────────────────────│                            │
    │                            │                            │
    │  Invoke: SendMessage(1,    │                            │
    │    "Hello!")               │                            │
    │───────────────────────────>│                            │
    │                            │  Get displayName from JWT  │
    │                            │  Sanitize message text     │
    │  On: NewMessage({          │  Save to DB                │
    │    userName, text,         │───────────────────────────>│
    │    createdAt })            │  Broadcast to group        │
    │<───────────────────────────│                            │
    │                            │                            │
    │  Connection lost           │                            │
    │───────────────────────────>│                            │
    │  On: UserLeft("Alice")     │  OnDisconnectedAsync       │
    │<───────────────────────────│  Cleanup client type       │
    │                            │  Broadcast to group        │

### Client Type Tracking

On connection, the hub reads the `X-Client-Type` header (`mobile` / `web`) from the HTTP context and stores it per connection. The type is logged on connect and cleaned up on disconnect. This enables platform-specific analytics and behavior adjustments.

### Connection Tracking

Connections are tracked via a `ConcurrentDictionary<string, UserConnection>` mapping SignalR connection IDs to their display name, joined channels, and client type. This enables proper cleanup and notifications when a user disconnects.

---

## REST API Endpoints

| Method | Path | Auth Required | Description |
|--------|------|:---:|------------|
| POST | `/api/auth/register` | ✗ | Create account (username, password, displayName) |
| POST | `/api/auth/login` | ✗ | Returns JWT + refresh token (cookie for web, body for mobile) |
| POST | `/api/auth/refresh` | ✗ | Rotates refresh token (from cookie / X-Refresh-Token header / body) |
| POST | `/api/auth/logout` | ✓ JWT | Revokes all refresh tokens (+ specific token from `X-Refresh-Token` header) |
| GET | `/api/channels` | ✓ JWT | List all channels |
| GET | `/api/channels/{id}/messages` | ✓ JWT | Get recent messages (last 50) |
| POST | `/api/channels/{id}/messages` | ✓ JWT | Send a message via REST |
| GET | `/swagger` | ✗ | Interactive API documentation |

All JWT-protected endpoints require header: `Authorization: Bearer <token>`

---

## Project Architecture

### Design Patterns

| Pattern | Usage |
|---|---|
| Repository via Services | `AuthService`, `TokenService`, `MessageServices` encapsulate data access |
| Dependency Injection | All services registered with scoped lifetime via `AddScoped` |
| CQRS-lite | Separate services for authentication, token management, and messaging |
| Middleware Pipeline | CSP, auth, CORS, Swagger, and routing composed in `Program.cs` |
| Dual-Mode Auth | Cookie-based for web, header/body-based for mobile (transparent to services) |

### Data Model

User (Id, Username, PasswordHash, DisplayName, CreatedAt)
  │
  ├── RefreshToken (Id, Token, UserId, CreatedAt, ExpiresAt, RevokedAt, ReplacedByToken)
  │
  └── Message (Id, ChannelId, UserName, Text, CreatedAt)
Channel (Id, Name)
  │
  └── Message (via ChannelId)

---

## Client Libraries

### Flutter / Dart

| Package | Version | Usage |
|---------|---------|-------|
| `signalr_netcore` | ^1.3.7 | SignalR HubConnection with `accessTokenFactory` |
| `flutter_secure_storage` | ^9.x | Secure token persistence on Android / iOS |

### React / TypeScript

| Package | Version | Usage |
|---------|---------|-------|
| `@microsoft/signalr` | ^8.0.0 | Official SignalR client with automatic reconnect |

### Common Requirements

- Send `X-Client-Type: mobile` header on all requests
- Use `X-Refresh-Token` header for refresh and logout (mobile)
- Persist refresh token from login response body into secure storage
- Re-join channel in `onreconnected` callback

---

## Build Phases

### Phase 1 — Database Foundation

- Models: `User`, `RefreshToken`, `Channel`, `Message`
- `AppDbContext` with Entity Framework Core + SQL Server
- Initial migration and seed data
  - 5 channels: general, random, tech, support, off-topic
  - 2 test users: alice / bob (password: `pass123`)
- `Sanitizer` service for XSS prevention (`StripHtml`)

### Phase 2 — JWT Authentication

- `AuthService`: BCrypt-based registration and authentication
- `TokenService`: JWT generation with refresh token rotation and theft detection
- 4 authentication endpoints: register, login, refresh, logout
- Refresh token stored as `HttpOnly` / `Secure` cookie (web) or response body (mobile)
- JWT signing key stored in `dotnet user-secrets` (local) / environment variables (production)

### Phase 3 — SignalR Messaging Hub

- `ChannelHub` with `[Authorize]` attribute for hub-level security
- `JoinChannel` — loads message history, adds connection to SignalR group
- `SendMessage` — sanitizes text, persists to database, broadcasts to group
- `OnConnectedAsync` / `OnDisconnectedAsync` — client type tracking and cleanup
- Connection tracking via `ConcurrentDictionary<string, UserConnection>`
- REST fallback endpoints for message retrieval and creation

### Phase 4 — Security & Polish

- CORS policy with `AllowCredentials` for local and production origins
- Content-Security-Policy header on every response
- Empty text validation in hub + REST endpoints
- Channel existence validation (404 if channel not found)
- Zero build warnings, zero `Console.WriteLine` calls

### Phase 5 — API Documentation

- Swagger UI with Bearer JWT "Authorize" button
- Swagger enabled in development and production
- Security scheme configured globally for all endpoints

### Phase 6 — CI/CD Deployment

- GitHub Actions workflow triggers on push to `main`
- Builds and publishes on `windows-latest` runner
- Flutter Web build step copies output to `wwwroot/flutterApp/`
- Deploys to Monster ASP.NET via WebDeploy
- Environment variables configured in Control Panel for connection string and JWT key

### Phase 7 — Mobile & Cross-Platform Support

- Dual-mode refresh token delivery (cookie + header/body)
- `X-Client-Type` header for client platform detection
- `X-Refresh-Token` header for mobile refresh and logout
- Refresh token returned in login response body for mobile clients
- Backward compatible — web clients continue using cookies unchanged

### Phase 8 — Flutter Web UI

- Replace vanilla JS UI with Flutter Web application
- Hosted in `wwwroot/flutterApp/` with `<base href="/flutterApp/">`
- Full chat experience with auto-reconnect and session persistence
- Rebuilt via `flutter build web --base-href=/flutterApp/ --release`

---

## Deployment Architecture

GitHub Push (main branch)
       │
       ▼
GitHub Actions (windows-latest)
  │
  ├── Setup Flutter SDK
  ├── flutter build web --base-href=/flutterApp/ --release
  ├── Copy build output to wwwroot/flutterApp/
  ├── Setup .NET 10 SDK
  ├── dotnet restore
  ├── dotnet build --configuration Release
  ├── dotnet publish --configuration Release
  │
  └── WebDeploy ──────────────────► Monster ASP.NET
                                        │
                                   ┌────┴────┐
                                   │  IIS    │
                                   │ Site    │
                                   └────┬────┘
                                        │
                              Environment Variables
                    ┌────────────────────┼────────────────────┐
                    ▼                    ▼                    ▼
          ConnectionStrings__         Jwt__Key          https://
          DefaultConnection                              enexabitwebsocket.
                                                         runasp.net

### Environment Variables (Monster ASP.NET Control Panel)

| Name | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Key` | 256-bit JWT signing key |
| `Jwt__Issuer` | `EnexabitWebSocketProject` |
| `Jwt__Audience` | `EnexabitWebSocketProject` |

---

## Live URL & Test Credentials

**Live Application:** [https://enexabitwebsocket.runasp.net](https://enexabitwebsocket.runasp.net)

**Flutter Web App:** [https://enexabitwebsocket.runasp.net/flutterApp/](https://enexabitwebsocket.runasp.net/flutterApp/)

**Swagger UI:** [https://enexabitwebsocket.runasp.net/swagger](https://enexabitwebsocket.runasp.net/swagger)

### Test Accounts

| Username | Password | Display Name |
|---|---|---|
| alice | pass123 | Alice |
| bob | pass123 | Bob |

---

## Development Setup

### Prerequisites

- .NET 10 SDK
- SQL Server (local instance)
- Flutter SDK (optional — for Flutter Web development)
- Visual Studio 2022+ / JetBrains Rider / VS Code

### Backend Setup

```bash
# Clone the repository
git clone https://github.com/Engineer-Ayman-Mohamed/Enexabit-WebSocket-Project.git
cd Enexabit-WebSocket-Project/EnexabitWebSocketProject/EnexabitWebSocketProject.App

# Initialize user secrets
dotnet user-secrets init

# Set JWT signing key
dotnet user-secrets set "Jwt:Key" "<your-256-bit-base64-key>"

# Update connection string in appsettings.json
# Server=.\SQLSERVER_2025;Database=EnexabitWebSocketDb;Trusted_Connection=True;TrustServerCertificate=True;

# Run database migrations (auto-applied on startup)
dotnet run

# Open browser at https://localhost:5001
Flutter Web Development
cd channels_ws

# Build with correct base href
flutter build web --base-href=/flutterApp/ --release

# Copy to ASP.NET wwwroot
xcopy /E /I build\web\* ..\EnexabitWebSocketProject.App\wwwroot\flutterApp\*
Local Configuration
File: appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLSERVER_2025;Database=EnexabitWebSocketDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "EnexabitWebSocketProject",
    "Audience": "EnexabitWebSocketProject",
    "ExpiryInMinutes": "15"
  }
}
