# Local browser test account

The dev database has a dedicated account for checking authenticated screens:

- Email: `playwright-test-20261024@stl.invalid`
- Password: the local-only `STL_BROWSER_TEST_PASSWORD` value in the repository-root `.env.local`

The account is on the Neon **dev** branch only. Its `.invalid` email is intentionally undeliverable; the account is unverified, but can log in and is suitable for checking the Account page and other user screens. Do not reuse these credentials on production or a public service.

## Sign in locally

1. Start the API and frontend as described in [frontend/README.md](../frontend/README.md).
2. Before entering credentials, open `http://localhost:5173/api/version` and confirm `"database":"dev"`.
3. Use the email above and the password in `.env.local` to sign in at `http://localhost:5173/log-in`.

The root `.env.local` is git-ignored. Keep the password there or in a password manager; do not copy it into this document, source control, a pull request, or a CI log.

If the password is lost, create a new dev-only account with a fresh `playwright-test-<date>@stl.invalid` address and store its credentials in `.env.local`. The `.invalid` address cannot receive verification or password-reset email.

GitHub Secrets are for credentials used by hosted automation. The local browser cannot read them, and no current workflow consumes a browser-account secret; add one only alongside a CI workflow that needs it.
