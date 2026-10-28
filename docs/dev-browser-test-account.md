# Test accounts on dev

When a feature needs a signed-in check, Claude (or any agent) makes disposable accounts on the Neon **dev** branch. Never on production: there are no test accounts there.

## Make one

1. Start the API and frontend as described in [frontend/README.md](../frontend/README.md).
2. Open `http://localhost:5173/api/version` and confirm `"database":"dev"`. Stop if it says anything else.
3. Sign up at `http://localhost:5173/sign-up` with `claude-<feature>-<yyyymmdd>@stl.invalid` (for example `claude-add-stock-20261028@stl.invalid`) and a fresh password. Make as many as the check needs (a second user, a vip).
4. The password isn't kept anywhere: not in `.env.local`, chat, PRs or logs. If you need the account again later, make a new one.

## Give it what the check needs

The `.invalid` address can't receive email, so set the rest in the dev database (the Neon connector, or the HTTPS SQL endpoint in cloud sessions), on that row only:

- A confirmed email: `update users set email_verified_at = now() where email = '<address>';`
- The owner or vip role: `update users set role = 'owner' where email = '<address>';` (the role is only ever granted, so it stays).

## Clean up

Accounts starting with `claude-` on dev are disposable: anyone may delete them, and nothing should depend on them. The older fixed account `playwright-test-20261024@stl.invalid` (password `STL_BROWSER_TEST_PASSWORD` in `.env.local`) still works for quick checks of user screens.

A standing account on dev and production, owner included, belongs to the scheduled QA agent idea in [ROADMAP.md](../ROADMAP.md) (*Ideas*), not to the dev flow.
