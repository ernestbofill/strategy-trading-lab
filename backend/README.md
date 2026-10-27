# Backend

ASP.NET Core minimal API (.NET 10) with EF Core and PostgreSQL on Neon. The schema is [DATA-MODEL.md](DATA-MODEL.md).

- `src/StrategyTradingLab.Api`: the API. `Data/` has the entities, `AppDbContext`, the table configurations and the migrations.
- `src/StrategyTradingLab.Api/Templates`: the five strategy templates (code, not data). `TemplateCatalog` has each one's tested defaults (budget, params, starter list or fund) from the POC, `TemplateParams` the typed params records, `Texts/{locale}.json` the words, and `TemplateAssets` the stocks and funds they need (seeded by migration; tests check every template's assets are seeded). Served at `GET /templates` and `GET /templates/{key}`. How to change a template's stocks or fund: [template-assets.md](src/StrategyTradingLab.Api/Templates/template-assets.md).
- `src/StrategyTradingLab.Api/Strategies`: `POST /strategies` starts a strategy (checked by `NewStrategyValidation`: name, budget limits, stocks or fund) and `GET /dashboard` lists the user's running strategies. They need a logged-in user.
- `src/StrategyTradingLab.Api/Admin`: the admin settings page's API (`/admin`): for the owner role only (401 without a log-in, 403 otherwise; see [AUTH.md](AUTH.md)), settings from an allow-list (`SettingDefinitions`), stored in `app_settings` with secrets encrypted (`SettingsStore`). Code that needs settings reads them with `SettingsStore.ValuesAsync` (all from one read; the email sender must take the password and its server from the same call). A setting falls back to the environment and then the default, except a secret: it is only ever set on the page, so `SMTP_PASSWORD` in `api.env` is ignored.
- `src/StrategyTradingLab.Api/Admin/DatabaseSwitch.cs`: which database the API uses, dev or production, switched on the admin page (`/admin/database`) and kept in `database.json` in the state directory (`STATE_DIRECTORY` on the server, else a per-user folder). `AppDbContext` takes its connection string from it for every new context, so a switch applies from the next request. Both URLs come only from the environment, never from the page: the host's own database (production on the server, dev locally) from `DATABASE_URL` or `ConnectionStrings:Default`, the one `dotnet ef`, local startup and the deploy's migrations bundle migrate, and the other from `DATABASE_URL_DEV` (server) or `DATABASE_URL_PRODUCTION` (local, optional, in `.env.local`). A switch is tested first (it answers and has no pending migration). At startup `CheckActiveAsync` checks a switched-to database again in the background and, if it misses a migration (a deploy migrates production only), uses the home one until the next start without saving that, and `Database__MigrateOnStartup` only migrates the home database, so a local API switched to production never migrates it. Only Postgres is switched: object storage stays on the home branch. Tests get their own `STATE_DIRECTORY` (`TestStateDirectory`), so they never follow a switch made on the local admin page. `GET /version` also says which database is in use (`"database": "dev"`), and the frontend shows a DEV tag for it.
- `src/StrategyTradingLab.Api/Email`: `IEmailSender`, sent by `SmtpEmailSender` over SMTP with STARTTLS (.NET's `SmtpClient`) with the admin page's settings, read in one `ValuesAsync` call. `POST /admin/test-email` sends a test to the account itself.
- `src/StrategyTradingLab.Api/Auth` and [AUTH.md](AUTH.md): sign-up, log-in, sessions in an HttpOnly cookie, email links and the CSRF header. `/dashboard` and `/strategies` need a logged-in user. Local work signs up like anyone else: without email set up, the API's log shows the verification link.
- `src/StrategyTradingLab.Api/MarketData`: daily prices and euro exchange rates from Yahoo's chart endpoint (the POC's source). `YahooClient` reads it (adjusted for splits and dividends, as `auto_adjust`), `PriceRefresher` brings `prices_daily` and `fx_rates` up to date (the last month, or the full history when nothing is stored or a stored close differs by over 0.1%), and `PriceRefreshWorker` runs it once a day and a minute after start. The schedule is three admin-page settings, `PRICES_SCHEDULE_ENABLED`, `PRICES_TIME_ZONE` and `PRICES_DAILY_TIME` (defaults on, Europe/Madrid, 22:30), read by the worker whenever it wakes; a save on the page wakes it (`PriceSchedule` computes the next run, DST included). `appsettings.Development.json` sets `PRICES_SCHEDULE_ENABLED` to false, so running or testing locally never calls Yahoo by itself. The page's Prices box (`/admin/prices`) shows the next and last runs, `POST /admin/prices/refresh` (Refresh now) runs it whatever the schedule says, and `POST /admin/prices/test` (Test Yahoo) asks Yahoo for SPY and EURUSD without storing anything.
- `src/StrategyTradingLab.Api/Engine`: the strategy rules, pure (bars in, evaluations out; no database). `Indicators` are the POC's trailing SMA, rolling high and low. `RulesRunner` is the POC's `Strategy.step`: it keeps the rules' own position and tells the rules about it, a buy or sell signal on day D is acted on at D+1 (the rules carry on as if the trade was made), a cooldown counts from the fill, and `Holding` once the steps are bought. The rules decide each day's status (`IBuyRules.Decide`, given the rules' own position in a `DayContext`); `BuyWindow` is the POC's `compose_status` they share. `DipRules` is *Dip in winners* (POC `stage2_pullback`), `ComebackRules` is *The comeback* (POC `turnaround`: the turn is the first step, milestones earn the later ones), `BearRules` is *The big market drop* (POC `bear_accumulation`: 20% drop from the 6-month high, first step at +5% off the low, then a step every 10 trading days, with recovery and rearm resets), and `TrendRules` is *Ride the long trend* (POC `index_trend`: buy above SMA 200 + 3%, sell below SMA 200 − 3%, all in and all out, 5-day cooldown). `tests/.../Golden` holds the POC's own results on real prices, which `EngineGoldenTests` must match day by day.
- `src/StrategyTradingLab.Api/Evaluation`: the daily check. `StrategyEvaluator` runs after each price refresh: for every running *Dip in winners*, *The comeback*, *The big market drop* and *Ride the long trend* strategy and each stock or fund it can signal for, it runs the engine over the stored prices up to `UsSession.LastComplete` (the last finished US session), from the strategy's start day. It keeps a `signals` row for each buy or sell the rules call for (simulation: confirmed at once; live: waiting, and `missed` once the signal window closes), each signal's `signal_trades` row at the next day's opening price once that day exists (the tested fee, the day's exchange rate through the euro; for *Ride the long trend* a buy reinvests what the previous sale returned), and the stock's status for the day in `daily_evaluations`. Idempotent. The admin Prices box shows how many new signals the last run made.
- `src/StrategyTradingLab.Api/Ai`: *Your AI's top picks*. The app runs no AI: `AiPrompt` writes the suggested prompt (date line, the instructions the user may edit, the answer table and the stocks; `POST /templates/ai/prompt` for the review and the editor), `AiAnswerReader` reads the scores block of the pasted answer (a Markdown table or JSON; pure, data only), `AiRoundRules` holds the rules (due every `EveryDays`, open up to 7 days, top N get a step, ties, the first open after the round), and `AiRounds` keeps each strategy's round due (`SweepAsync`, on reads and daily), turns a round into signals (`SubmitAsync`; a simulation buys at once at the latest price, which the endpoint refreshes first; an extra round before the scheduled one is due), skips a due round (`SkipAsync`), and in the daily check after the evaluator makes their signal trades, misses the live ones whose next round is due, and stores each stock's status (`RunDailyAsync`). `AiRoundEndpoints`: `GET/POST /strategies/{id}/ai-round`, `POST …/ai-round/read`, `POST …/ai-round/skip`, `GET …/ai-rounds`, `PUT …/ai-prompt` and `POST /account/ai-sharing`. A shared round (`shared_ai_rounds`) has no key to its user; `AiSharingScrub` removes emails, phone numbers and link parameters first.
- `src/StrategyTradingLab.Api/Notifications`: the emails. `EmailTexts` (pure, English, "describe, don't instruct") writes the live signal alert and the daily overview; `NotificationService` claims a `notifications` row first (its unique key is the dedupe contract) and then sends, so each goes out once, only to verified addresses. The alerts follow the daily check (`PriceRefreshWorker`: live signals of the last 3 days that are still waiting, strategy notifications on, and have no alert yet); `NotificationWorker` looks every hour for users whose morning (07:00 in their time zone) has begun and who haven't had today's overview (the account's switch, at least one running strategy). The admin setting `EMAILS_ENABLED` (Notifications group) turns both off (off in Development).
- `src/StrategyTradingLab.Api/Feedback`: in-app feedback (`feedback_items`). `POST /feedback` lets any logged-in user with a verified email (`Policies.VerifiedEmail`) submit a comment (`bug` | `idea` | `note` | `feature`, with optional route, DOM selector and selected text); `GET /feedback`, `PATCH /feedback/{id}` and `DELETE /feedback/{id}` are for the owner (`Policies.Owner`) to review, edit, close/reopen and delete items.
- `tests/StrategyTradingLab.Api.Tests`: xUnit. Tests marked `[NeonFact]` run against the linked Neon branch, and skip without a database and object storage. CI gives them a throwaway PostgreSQL and RustFS (`.github/workflows/ci-backend.yml`), so they always run there.
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
