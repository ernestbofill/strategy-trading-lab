# Decisions

Decisions that outlive a PR, and why. One dated line each, newest first, with the PR number. The PR has the detail.

- 2026-10-21 · The admin page can switch the API between the dev and production databases and set their URLs (Ernest's ask). The choice is kept in an encrypted file in the service's state directory, not in the database it chooses; the server's own database comes only from `api.env` (the one the deploy migrates), and the page sets a URL for the other one only. Guards: each change is tested first (it answers, no pending migration), only Neon hosts, URLs are write-only (the page shows the host), and on the server it is refused while the admin password is the default. A DEV tag in the top bar shows the dev database is in use. (#30)
- 2026-10-21 · Accounts as designed in #25, all recommended answers: no email verification before use (only before going live), sign-up never reveals a taken email, log-in by email only, passwords of 10+ characters, 5 wrong tries lock an email for 15 minutes, 30-day sessions (90 at most), template endpoints public. (#25)
- 2026-10-21 · The seeded user (Ernest, `stl`) is removed entirely, on every database, in the same release as log-in. Local work signs up like anyone else. (#25)
- 2026-10-21 · Email goes through Gmail SMTP with an app password, as in the POC: no domain to set up and enough volume for now. Revisit before a public launch. (#25)
- 2026-10-21 · Accounts use server-side sessions in an `HttpOnly` cookie (not JWTs), the app and API share one origin. Design: backend/AUTH.md. (#25)
- 2026-10-21 · Settings like the email ones are set on an admin page (like the POC's), stored in the database with secrets encrypted, from an allow-list. Until accounts have an admin role the page has a fixed password, `STL` (Ernest's call); secrets are write-only, set only on the page (never from api.env), and sealed with the mail account, server and port they were saved with, so changing those can't send them elsewhere. (#26)
- 2026-10-20 · Sign-up and log-in is built right after #24, ahead of the rest of ROADMAP → Next. Until then `POST /strategies` stays open on production, accepted while there are no users. (#24)
- 2026-10-20 · Nobody applies a migration to the dev database by hand: the API migrates it on startup from a launch profile, and cloud sessions apply `migrations.sql` over HTTPS (script and session start hook). Production keeps the deploy's migrations bundle. (#24)
- 2026-10-20 · The dashboard card shows the template's worst year next to its past result, and the footer carries "Past results don't promise future ones." (#24)
- 2026-10-20 · Until sign-up exists, every request runs as one fixed user, Ernest, who holds the strategies he starts. His password is `stl`, for the log-in that doesn't exist yet: change it before there is a way to log in. (#24)
- 2026-10-20 · Tools come from the claude.ai connectors. A repo-defined set (`.mcp.json` with `disableClaudeAiConnectors`) was tried and dropped: the desktop app loads the claude.ai connectors anyway, so it only made duplicates and keys to manage. (#22)
- 2026-10-19 · Two disclaimer lengths: the short one on screens seen again and again (including Confirm a signal), the full one where the user commits (sign-up, above the Confirm button, the legal page). The user sees it over and over. (#21)
- 2026-10-19 · Spec copy is a current version, not final: agents may shorten it, except the disclaimers and "Past results don't promise future ones." (#20)
- 2026-10-19 · High risk means strategy and budget calculations, authentication and security. High-risk and docs-only PRs always wait for Ernest to merge, even in a loop. (#20, #22)
- 2026-10-19 · `ROADMAP.md` is the backlog; `TODO.md` holds every manual step for Ernest, so nothing waits only in a PR. (#20, #22)
- 2026-10-19 · Catalan is a priority: first in ROADMAP → Next, while there are few screens. (#22)
- 2026-10-20 · The project is low risk for now: a hobby with no active users, maybe a public launch later. Favour progress, but secrets, security and the product rules apply in full. (#22)
- 2026-10-20 · Claude's memory lives in the repo, in `.claude/memories/`, for transparency and so every environment shares it. Auto memory stays on, but is told to save there instead of its default folder. (#22)
