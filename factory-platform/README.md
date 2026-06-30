# Factory Digital Platform v0.1

First expandable foundation for an internal factory digital platform. This is intentionally not a full ERP. The project provides clean module, permission, dashboard, widget, audit, settings, file-storage, and API foundations that can grow into ERP, MES, CMMS, SCADA dashboards, document control, warehouse, quality, and production modules.

## Stack

- Frontend: React, TypeScript, Vite
- Backend: .NET 8 Web API
- Database: PostgreSQL
- Auth: JWT bearer tokens
- UI: modern dark industrial app shell

## Default Admin

- Email: `admin@factory.local`
- Password: `Admin123!`

Change this immediately outside local development.

## Structure

```text
factory-platform/
  frontend/
    src/
      app/
      modules/
      shared/
  backend/
    FactoryPlatform.Api/
    FactoryPlatform.Application/
    FactoryPlatform.Domain/
    FactoryPlatform.Infrastructure/
    FactoryPlatform.Modules/
```

## Setup

1. Configure backend for your local PostgreSQL installation:

```bash
cd backend/FactoryPlatform.Api
cp .env.example .env
```

The default local connection string points to `Host=localhost;Port=5432;Database=factory_platform;Username=postgres;Password=Admin123!`. If your local PostgreSQL password is different, update `appsettings.json`, `.env`, or use the `ConnectionStrings__DefaultConnection` environment variable.

2. Apply migrations and run API:

```bash
cd backend
dotnet restore
dotnet tool restore
dotnet ef database update --project FactoryPlatform.Infrastructure --startup-project FactoryPlatform.Api
dotnet run --project FactoryPlatform.Api
```

Swagger is available at `http://localhost:5080/swagger` depending on your local launch URL.

3. Run frontend:

```bash
cd frontend
cp .env.example .env
pnpm install
pnpm run dev
```

Open `http://localhost:5173`.

`npm install` / `npm run dev` should also work if your team standardizes on npm.

## Troubleshooting

- Login says `API is not reachable`: start PostgreSQL and the .NET API. The frontend calls `VITE_API_BASE_URL`, which defaults to `http://localhost:5080/api`.
- `dotnet --version` says no SDK found: install the .NET 8 SDK, then rerun the backend setup commands.
- Swagger does not open: confirm the API is running at `http://localhost:5080/swagger`.

## API Endpoints

- `POST /api/auth/login`
- `GET /api/users/me`
- `GET /api/modules`
- `GET /api/dashboard/layout`
- `PUT /api/dashboard/layout`
- `GET /api/settings`
- `PUT /api/settings`
- `GET /api/audit-logs`

## Extension Notes

- Add frontend navigation through `frontend/src/app/moduleRegistry.tsx`.
- Add dashboard widgets through `frontend/src/modules/dashboard/widgetRegistry.tsx`.
- Add permissions in `FactoryPlatform.Application/Common/SeedData.cs`.
- Keep future module rules inside `FactoryPlatform.Modules/*` and application services, not directly in pages or API endpoints.
- Keep the sidebar consuming a registry or API module manifest so navigation stays modular.
