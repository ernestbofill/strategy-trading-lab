# Backend

ASP.NET Core minimal API (.NET 10) with EF Core and PostgreSQL on Neon. The schema is [DATA-MODEL.md](DATA-MODEL.md).

- `src/StrategyTradingLab.Api`: the API. `Data/` has the entities, `AppDbContext`, the table configurations and the migrations.
- `src/StrategyTradingLab.Api/Templates`: the five strategy templates (code, not data). `TemplateCatalog` has each one's tested defaults (budget, params, starter list or fund) from the POC, `TemplateParams` the typed params records, `Texts/{locale}.json` the words, and `TemplateAssets` the stocks and funds they need (seeded by migration; tests check every template's assets are seeded). Served at `GET /templates` and `GET /templates/{key}`. How to change a template's stocks or fund: [template-assets.md](src/StrategyTradingLab.Api/Templates/template-assets.md).
- `src/StrategyTradingLab.Api/Strategies`: `POST /strategies` starts a strategy (checked by `NewStrategyValidation`: name, budget limits, stocks or fund) and `GET /dashboard` lists the user's running strategies. Until auth exists every request runs as the one seeded user, Ernest (`DevSeed`), whose password is `stl`.
- `src/StrategyTradingLab.Api/Admin`: the admin settings page's API (`/admin`): a fixed password (`ADMIN_PASSWORD` on the server, else `STL`) until accounts have an admin role, settings from an allow-list (`SettingDefinitions`), stored in `app_settings` with secrets encrypted (`SettingsStore`). Code that needs settings reads them with `SettingsStore.ValuesAsync` (all from one read; the email sender must take the password and its server from the same call). A setting falls back to the environment and then the default, except a secret: it is only ever set on the page, so `SMTP_PASSWORD` in `api.env` is ignored.
- `src/StrategyTradingLab.Api/Admin/DatabaseSwitch.cs`: which database the API uses, dev or production, switched on the admin page (`/admin/database`) and kept in `database.json` in the state directory (`STATE_DIRECTORY` on the server, else a per-user folder). `AppDbContext` takes its connection string from it for every new context, so a switch applies from the next request. Both URLs come only from the environment, never from the page: the host's own database (production on the server, dev locally) from `DATABASE_URL` or `ConnectionStrings:Default`, the one `dotnet ef`, local startup and the deploy's migrations bundle migrate, and the other from `DATABASE_URL_DEV` (server) or `DATABASE_URL_PRODUCTION` (local, optional, in `.env.local`). A switch is tested first (it answers and has no pending migration) and, on the server, refused while `ADMIN_PASSWORD` isn't set. At startup `CheckActiveAsync` checks a switched-to database again in the background and, if it misses a migration (a deploy migrates production only), uses the home one until the next start without saving that, and `Database__MigrateOnStartup` only migrates the home database, so a local API switched to production never migrates it. Only Postgres is switched: object storage stays on the home branch. Tests get their own `STATE_DIRECTORY` (`TestStateDirectory`), so they never follow a switch made on the local admin page. `GET /version` also says which database is in use (`"database": "dev"`), and the frontend shows a DEV tag for it.
- `src/StrategyTradingLab.Api/Email`: `IEmailSender`, sent by `SmtpEmailSender` over SMTP with STARTTLS (.NET's `SmtpClient`) with the admin page's settings, read in one `ValuesAsync` call. `POST /admin/test-email` sends a test to the account itself.
- [AUTH.md](AUTH.md): how sign-up, log-in and sessions will work (design, not built yet).
- `tests/StrategyTradingLab.Api.Tests`: xUnit. Tests marked `[NeonFact]` run against the linked Neon branch and skip without it (CI).
- `migrations.sql`: the idempotent SQL script of every migration, for review and for applying by hand.

## Neon

The repo root is linked to the Neon project (`.neon`, `neon.ts`). Local work uses the `dev` branch; `production` is the default branch.

`neon.ts` declares the services: Postgres and the private `assets` bucket (Object Storage). `neon deploy` applies it to the linked branch and writes the branch's variables to `.env.local` at the repo root (gitignored):

| Variable | Used for |
| --- | --- |
| `DATABASE_URL` | The API's queries (pooled, PgBouncer) |
| `DATABASE_URL_UNPOOLED` | Migrations (`dotnet ef`), direct |
| `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_ENDPOINT_URL_S3`, `AWS_REGION` | Object Storage |

In Development the API reads `.env.local` itself. `ConnectionStrings:Default` (user secrets or environment) overrides `DATABASE_URL` when set.

## Run

From `backend/`:

```bash
dotnet tool restore
```

```bash
dotnet run --project src/StrategyTradingLab.Api
```

`GET http://localhost:5184/health` reports the Postgres version and the bucket. In Development, Swagger UI is at `http://localhost:5184/swagger` (the OpenAPI document at `/openapi/v1.json`); F5 in VS Code opens it.

`GET /version` returns the version, the commit and when it was built (Release builds).

## Versions and deploy

The version is `<Version>` in [Directory.Build.props](Directory.Build.props), with the frontend's scheme (`0.1.0-alpha`). Bump it in every PR that changes the backend, minor for a feature and patch for a fix: CI fails otherwise. Merging to `main` deploys to the Oracle server and tags `backend-v<version>`; see [deploy/backend](../deploy/backend/README.md).

## Migrations

```bash
dotnet ef migrations add <Name> --project src/StrategyTradingLab.Api -o Data/Migrations
```

```bash
dotnet ef migrations script --idempotent --project src/StrategyTradingLab.Api -o migrations.sql
```

```bash
dotnet ef database update --project src/StrategyTradingLab.Api
```

You rarely need that one: the launch profiles (F5, `dotnet run`) set `Database__MigrateOnStartup`, so the API brings the dev database up to date when it starts, in Development only. In Claude Code cloud sessions, where port 5432 is blocked, `python3 backend/scripts/migrate-dev-https.py` applies `migrations.sql` to the dev branch over HTTPS, and the session start hook runs it. Production gets its migrations from the deploy.

The strategies' unique running name, `(user_id, lower(name)) WHERE status = 'running'`, is an expression index EF can't model; it's raw SQL in the initial migration.
