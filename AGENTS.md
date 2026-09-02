# Rosterly

## Stack

- **Backend**: .NET 10 / ASP.NET Core Web API / Entity Framework Core / PostgreSQL
- **Frontend**: React 19 / TypeScript / Vite / Tailwind CSS v4 / shadcn/ui
- **Infra**: Docker Compose (PostgreSQL, API, frontend + nginx)

## Project layout

- `RosterlyApi/` — .NET API project
- `client/` — React frontend

## Git convention

- Use [gitmoji](https://gitmoji.dev) for commit types (e.g. `:sparkles:` for features, `:bug:` for fixes, `:recycle:` for refactors)

## Dev commands

**Frontend** (`client/`):
- `npm run dev` — Vite dev server (port 5173, proxies `/api` to `http://localhost:5000`)
- `npm run build` — Type-check then build
- `npm run lint` — ESLint
- `npm run format` — Prettier write
- `npm run typecheck` — `tsc --noEmit`
- Generate API types from OpenAPI spec (backend must be running): `npm run generate:types`
- Add shadcn component: `npx shadcn@latest add <component>`

**Backend** (`RosterlyApi/`):
- `dotnet restore` / `dotnet build` / `dotnet run`
- Migrations (EF Core tools installed): `dotnet ef migrations add <name>`, `dotnet ef database update`
- Scalar API docs: `http://localhost:5000/scalar/v1` (when running)
- Raw OpenAPI spec: `http://localhost:5000/openapi/v1.json`

**Docker Compose** (root):
- `docker compose up --build` — builds images, starts db, backend, frontend
- Backend binds to port 8080, frontend to port 80

## Quirks

- **Tailwind v4**: Uses `@tailwindcss/vite` plugin — no `tailwind.config.js`
- **Vite proxy only** in dev (`/api` -> `localhost:5000`); dev server runs on 5173
- **Docker vs local DB**: `appsettings.Development.json` has `localhost:5432`; `compose.yaml` overrides to `Host=db` — do not commit docker-specific connection strings to `appsettings.Development.json`
- **EF Core Design** package is included for migrations; migrations are auto-applied at API startup via `db.Database.MigrateAsync()`
- **Prettier**: `semi: false`, `singleQuote: false`, `endOfLine: lf`, `tailwindStylesheet: src/index.css`
- **ESLint**: ignores `dist` directory
- shadcn components live in `src/components/ui/`
- **Secrets via .env**: Resend API key and email config live in `.env` (loaded by `DotNetEnv`). Never commit `.env`; use `.env.example` as template.

## Status

MVP in progress. Auth (Clerk) and email (Resend) are implemented. Background jobs (Hangfire) are planned but not yet implemented.