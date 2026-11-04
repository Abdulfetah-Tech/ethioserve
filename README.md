# EthioServe

EthioServe is a service-booking application with an Angular frontend and an ASP.NET Core API. The repository includes provider availability calendars, appointment workflows, authentication, and real-time notifications.

## Tech stack

- **Frontend:** Angular 22, TypeScript, and SignalR
- **Backend:** ASP.NET Core 10, Entity Framework Core, and PostgreSQL
- **Local deployment:** Docker Compose

## Run with Docker Compose

1. Copy the example environment file:

   ```bash
   cp .env.example .env
   ```

   On Windows PowerShell, use `Copy-Item .env.example .env`.

2. Edit `.env` and set a unique `POSTGRES_PASSWORD` and a `JWT_SIGNING_KEY` of at least 32 characters. Do not use the example credentials in a shared or production environment.
3. Build and start the services:

   ```bash
   docker compose up --build
   ```

4. Open the frontend at [http://localhost:4200](http://localhost:4200).

The API is available at `http://localhost:5102`; its readiness endpoint is `/health/ready`. Docker Compose starts PostgreSQL, applies database migrations on API startup, and uses named volumes for database data and request uploads. Ports can be changed in `.env`.

To stop the services, press `Ctrl+C`, then run `docker compose down`. To also delete the local database and uploaded files, run `docker compose down -v`.

## Local development

### Requirements

- .NET SDK 10.0.401 or a compatible later feature release
- Node.js and npm (the frontend declares npm 11.19.0)
- PostgreSQL, unless you use the Compose database service

### Start the backend

From the repository root, configure the API's database connection and JWT signing key using local configuration or .NET user secrets, then run:

```bash
dotnet run --project backend/EthioServe.API/EthioServe.API.csproj
```

### Start the frontend

In another terminal:

```bash
cd frontend/ethioserve
npm ci
npm start
```

The Angular development server runs at [http://localhost:4200](http://localhost:4200). The configured development proxy forwards API and hub requests to the backend. On Windows, `run-all.ps1` can start both projects; use `./run-all.ps1 -NoBackend` to start only the frontend.

## Tests

Run backend tests from the solution directory:

```bash
dotnet test backend/EthioServe.sln
```

Run frontend tests and build from `frontend/ethioserve`:

```bash
npm test
npm run build
```

## Documentation

- [Deployment guide](docs/deployment.md)
- [Authentication](docs/authentication.md)
- [Provider availability](docs/availability.md)

## Security

Never commit real passwords, signing keys, connection strings, or other secrets. Use separate, securely generated values for each environment and store production secrets in a secrets manager.