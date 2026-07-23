FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["EnexabitWebSocketProject.App.csproj", "./"]
RUN dotnet restore "EnexabitWebSocketProject.App.csproj"
COPY . .
WORKDIR "./"
RUN dotnet build "EnexabitWebSocketProject.App.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EnexabitWebSocketProject.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EnexabitWebSocketProject.App.dll"]