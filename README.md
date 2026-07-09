# Channel Chat — Real-Time WebSocket Messaging Platform

A full-stack real-time chat application built with ASP.NET Core Minimal API, JWT authentication with refresh token rotation, SignalR WebSocket messaging, and automated CI/CD deployment to Monster ASP.NET.

**Author:** Engineer Ayman Mohamed

---

## Table of Contents

- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Authentication Flow](#authentication-flow)
- [SignalR Flow](#signalr-flow)
- [REST API Endpoints](#rest-api-endpoints)
- [Project Architecture](#project-architecture)
- [Build Phases](#build-phases)
- [Deployment Architecture](#deployment-architecture)
- [Live URL & Test Credentials](#live-url--test-credentials)
- [Development Setup](#development-setup)

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
| Frontend | Vanilla JavaScript + SignalR JS client |

---

## Project Structure

```
EnexabitWebSocketProject.App/
├── Data/
│   ├── AppDbContext.cs          # EF Core DbContext
│   └── DbInitializer.cs         # Seeds channels + test users
├── DTOs/
│   ├── LoginRequest.cs
│   ├── LoginResponse.cs
│   ├── RegisterRequest.cs
│   └── SendMessageRequest.cs
├── Hubs/
│   └── ChannelHub.cs            # SignalR hub with [Authorize]
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
│   └── index.html               # Chat client UI
├── Program.cs                   # Entry point — all endpoints and middleware
└── EnexabitWebSocketProject.App.csproj
```

---

## Authentication Flow

```
  Client                    ASP.NET API                    SQL Server
    │                            │                            │
    │  POST /api/auth/register   │                            │
    │───────────────────────────>│                            │
    │                            │  BCrypt hash password      │
    │                            │───────────────────────────>│
    │                            │<───────────────────────────│
    │<───────────────────────────│  Created                    │
    │                            │                            │
    │  POST /api/auth/login      │                            │
    │───────────────────────────>│                            │
    │                            │  Verify BCrypt hash        │
    │                            │───────────────────────────>│
    │                            │<───────────────────────────│
    │                            │                            │
    │  { accessToken }           │  Generate JWT (15min)      │
    │  Set-Cookie: refreshToken  │  + refresh token (7 days)  │
    │  (HttpOnly, Secure,        │───────────────────────────>│
    │   SameSite=Strict)         │<───────────────────────────│
    │<───────────────────────────│                            │
    │                            │                            │
    │  POST /api/auth/refresh    │                            │
    │  Cookie: refreshToken      │                            │
    │───────────────────────────>│                            │
    │                            │  Validate + rotate token   │
    │                            │  (revoke old, issue new)   │
    │  { accessToken }           │───────────────────────────>│
    │  Set-Cookie: refreshToken  │<───────────────────────────│
    │<───────────────────────────│                            │
    │                            │                            │
    │  POST /api/auth/logout     │                            │
    │  Authorization: Bearer JWT │                            │
    │───────────────────────────>│                            │
    │                            │  Revoke ALL user tokens    │
    │  Clear-Cookie              │───────────────────────────>│
    │<───────────────────────────│                            │
```

### Security Features

- **Refresh token rotation:** Each refresh invalidates the old token and issues a new one
- **Theft detection:** If a revoked token is reused, all user sessions are invalidated
- **HttpOnly/Secure/SameSite=Strict** cookies for refresh tokens (inaccessible to JavaScript)
- **XSS prevention:** All message text runs through `Sanitizer.StripHtml()`
- **JWT stored in memory only** (not localStorage) to prevent XSS token theft

---

## SignalR Flow

```
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
    │  On: JoinedChannel([...])  │───────────────────────────>│
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
    │<───────────────────────────│  Remove from dictionary    │
    │                            │  Broadcast to group        │
```

### Connection Tracking

Connections are tracked via a `ConcurrentDictionary<string, UserConnection>` mapping SignalR connection IDs to their channel and display name. This enables proper cleanup and notifications when a user disconnects (SignalR groups lack reverse lookup on disconnect).

---

## REST API Endpoints

| Method | Path | Auth Required | Description |
|--------|------|:---:|------------|
| POST | `/api/auth/register` | ✗ | Create account (username, password, displayName) |
| POST | `/api/auth/login` | ✗ | Returns JWT + sets refresh cookie |
| POST | `/api/auth/refresh` | ✗ (cookie) | Rotates refresh token |
| POST | `/api/auth/logout` | ✓ JWT | Revokes all refresh tokens |
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

### Data Model

```
User (Id, Username, PasswordHash, DisplayName, CreatedAt)
  │
  ├── RefreshToken (Id, Token, UserId, CreatedAt, ExpiresAt, IsRevoked)
  │
  └── Message (Id, ChannelId, UserName, Text, CreatedAt)

Channel (Id, Name)
  │
  └── Message (via ChannelId)
```

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
- Refresh token stored as `HttpOnly` / `Secure` / `SameSite=Strict` cookie
- JWT signing key stored in `dotnet user-secrets` (local) / environment variables (production)

### Phase 3 — SignalR Messaging Hub

- `ChannelHub` with `[Authorize]` attribute for hub-level security
- `JoinChannel` — loads message history, adds connection to SignalR group
- `SendMessage` — sanitizes text, persists to database, broadcasts to group
- `OnDisconnectedAsync` — removes connection from tracking dictionary, notifies group
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
- Deploys to Monster ASP.NET via WebDeploy
- Environment variables configured in Control Panel for connection string and JWT key

---

## Deployment Architecture

```
GitHub Push (main branch)
       │
       ▼
GitHub Actions (windows-latest)
  │
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
```

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
- Visual Studio 2022+ / JetBrains Rider / VS Code

### Steps

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
```

### Local Configuration

File: `appsettings.json`

```json
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
```

---

## License

MIT
