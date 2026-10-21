# Accounts: how sign-up, log-in and sessions work

Status: **design, for review**. The screens and copy are in the [app spec](../docs/strategy-trading-lab-app-spec.md) (*Sign-up*, *Log-in*, *Forgot and reset password*, *Account*). This is the technical side. Authentication is high risk, so it is built in slices, each reviewed (see ROADMAP).

## Choices

| Question | Choice | Why |
| --- | --- | --- |
| How is a request tied to a user? | A **server-side session**: a random token in a cookie, a row in `sessions` | Revocable at once (log out, password change, delete account), nothing to sign or rotate, and nothing readable on the client. A JWT can't be revoked without a table anyway. |
| The cookie | `stl_session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, 30-day sliding expiry (90 days at most) | The app and the API share one origin (`/api` behind nginx), so there is no CORS and no token in JavaScript. |
| The token | 32 random bytes (`RandomNumberGenerator`), URL-safe base64 in the cookie. Only its SHA-256 is stored | A database leak doesn't give working sessions. |
| Password hashing | ASP.NET Core Identity's `PasswordHasher<User>` (PBKDF2-HMAC-SHA512, per-user salt, rehash when its settings change) | Already in the shared framework. No new package. |
| CSRF | `SameSite=Lax`, JSON bodies only (`Content-Type: application/json` required on every POST/PUT/PATCH/DELETE), and a `X-Requested-With: stl` header that the client always sends and a plain form can't | No token plumbing, and a cross-site form or image can't make a state-changing request. |
| Email | An `IEmailSender`: SMTP when `SMTP_USER` and `SMTP_PASSWORD` are set, else a log sender (development and tests; it writes the link to the log, never to a response). SMTP is Gmail with an app password, as in the POC (Ernest's choice): `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_HOST` (empty with a `@gmail.com` user: `smtp.gmail.com`), `SMTP_PORT` (587, STARTTLS), `SMTP_FROM` (empty: `SMTP_USER`). Sent with .NET's own `System.Net.Mail.SmtpClient`, so no new package. The values are set on the admin page (#26): everything but the app password defaults to Ernest's Gmail | No domain to set up, and Gmail's limit (about 500 emails a day) is far above what sign-up and reset need. Move to a sending service with its own domain before a public launch or when the daily overview grows past that limit. |
| Rate limits | ASP.NET Core's rate limiter per client address on sign-up, log-in, forgot password and "send the link again"; the per-email log-in lock is on the user row | The app is behind one nginx, so the address comes from `X-Forwarded-For` (`ForwardedHeaders`, trusting only the local proxy). |
| Who is the current user? | A small `ICurrentUser` read from the session. Endpoints that need a user take it; without a valid session they answer 401. The fixed user `DevSeed.DevUserId` goes away | One place decides, and a screen can't forget to check. |
| Which endpoints are public? | `GET /templates`, `/templates/{key}`, `/health`, `/version`, and the account endpoints below. Everything else needs a session | The template pages hold no user data. Until the language choice exists they are served in English to visitors. |

## Data

New columns on `users`: `failed_login_count smallint not null default 0`, `locked_until timestamptz null`.

```
sessions      id uuid PK · user_id FK → users (cascade) · token_hash bytea unique · created_at ·
              last_used_at · expires_at (30 days after last use, capped at created_at + 90 days) · user_agent text null
email_tokens  id uuid PK · user_id FK → users (cascade) · purpose ('verify_email' | 'reset_password') ·
              token_hash bytea unique · created_at · expires_at (verify 24 h, reset 1 h) · used_at null
```

Both are new tables, so the migration changes no existing data. Expired rows are deleted by a daily clean-up (later, with the other scheduled jobs). `last_used_at` is written at most once a minute per session. IP addresses are not stored (privacy).

## Endpoints

All JSON, under `/api`. State-changing ones follow the CSRF rule above.

| Endpoint | Does |
| --- | --- |
| `POST /account/sign-up` | email, password, language, time zone, overview on/off, disclaimer accepted. Same answer whether or not the email exists; a new user is logged in |
| `POST /account/log-in` | email, password. Same error for a wrong email and a wrong password; the lock counts failures per email |
| `POST /account/log-out` | ends this session |
| `GET /account/me` | the user (email, verified, language, time zone) or 401. The app's start-up check |
| `POST /account/verify-email` | token from the email link. Logs the user in |
| `POST /account/resend-verification` | for the logged-in user, rate-limited |
| `POST /account/forgot-password` | email. Always the same answer |
| `POST /account/reset-password` | token, new password. Ends all the user's sessions, then logs in this device |
| `POST /account/change-password` | current password, new password. Ends the other sessions |

Every token is single-use and checked in constant time through its hash. Passwords and tokens are never logged.

## The seeded user goes away

Ernest's call: the fixed user (`DevSeed`, `ernest@strategytradinglab.local`, password `stl`) is removed entirely, on every database. The backend slice's migration deletes the row; its strategies go with it (`users` cascades), which is only test data. `DevSeed` and `DevSeed.DevUserId` leave the code, and every endpoint reads the user from the session instead. It happens in the same release as log-in, because until then every request runs as that user.

Local work and the tests sign up like anyone else: with no `SMTP_*` variables the verification link is in the API's log. The endpoint tests create their own users.

## Slices

1. **Backend**: tables, `ICurrentUser`, the endpoints above, rate limits, forwarded headers, `LogEmailSender`, the removal of the seeded user, and tests (hash, token expiry and reuse, lock, enumeration-safe answers, 401 on every protected route). High risk: an `opus` review and a second round after any real finding.
2. **Frontend**: Sign-up, Log-in, Forgot and Reset password, the check-your-email notice, the start-up check, a guard that sends visitors to Log-in and back, the 401 handling, the mock backend, and the account page's first version.
3. **Email**: the SMTP sender (Gmail, as in the POC). Ernest puts the five `SMTP_*` variables in `/etc/strategy-trading-lab/api.env` on the server and in `.env.local` for local work. Until then the links only reach the log. Small enough to ride with slice 1 if the variables are ready.

## The admin page

The admin settings page (#26) has its own fixed password, `STL` (or `ADMIN_PASSWORD` on the server), until accounts exist (Ernest's call). With accounts it moves behind an admin role on the user (Ernest's account), and the fixed password goes. The email sender reads its settings through `SettingsStore.ValuesAsync` (#26), all in one call.

## Not in this design

Social log-in, two-factor codes, "log out everywhere", the list of devices, and email change are left out until someone needs them.
