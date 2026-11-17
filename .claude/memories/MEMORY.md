# Claude's memory for STL

Loaded into every Claude Code session through `CLAUDE.md`. One line per memory, linking to its file. Each file holds one fact, with a short **Why** and **How to apply**. Committed with the session's PR, so Ernest sees every change.

- [Ernest's quoted replies](ernest-quoted-replies.md) — he answers a review point by point, quoting each point with his decision
- [Neon over HTTPS in cloud sessions](neon-over-https-in-cloud.md) — dev through `DATABASE_URL` and the HTTPS SQL endpoint (port 5432 blocked); production only through the Neon connector, read-only
- [.NET on the Mac](dotnet-on-the-mac.md) — installed in `~/.dotnet` without sudo; set PATH per command, `[NeonFact]` tests skip locally
- [Backend tests in cloud sessions](backend-tests-in-cloud.md) — install .NET in `~/.dotnet` and run the `[NeonFact]` tests against a local PostgreSQL 16, as CI does
- [Merge main, don't rebase](merge-main-not-rebase.md) — force-push is refused; merge `origin/main` into pushed branches, replay unpushed stacked ones
- [A test's setting loses to .env.local](test-settings-vs-env-local.md) — `UseSetting` is overridden locally by `.env.local`; add test config with `ConfigureAppConfiguration` instead
- [Browser checks in a cloud session](cloud-browser-check.md) — headless Playwright with mocked `/api` replies, since the local API can't reach the dev database
