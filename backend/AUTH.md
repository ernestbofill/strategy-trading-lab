# Accounts: how sign-up, log-in and sessions work

Status: **backend built** (#28); the screens are the next slice. The screens and copy are in the [app spec](../docs/strategy-trading-lab-app-spec.md) (*Sign-up*, *Log-in*, *Forgot and reset password*, *Account*). This is the technical side. Authentication is high risk, so it is built in slices, each reviewed (see ROADMAP).

## Choices

| Question | Choice | Why |
| --- | --- | --- |
| How is a request tied to a user? | A **server-side session**: a random token in a cookie, a row in `sessions` | Revocable at once (log out, password change, delete account), nothing to sign or rotate, and nothing readable on the client. A JWT can't be revoked without a table anyway. |
| The cookie | `stl_session`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, 30-day sliding expiry (90 days at most) | The app and the API share one origin (`/api` behind nginx), so there is no CORS and no token in JavaScript. |
| The token | 32 random bytes (`RandomNumberGenerator`), URL-safe base64 in the cookie. Only its SHA-256 is stored | A database leak doesn't give working sessions. |
| Password hashing | ASP.NET Core Identity's `PasswordHasher<User>` (PBKDF2-HMAC-SHA512, per-user salt, rehash when its settings change) | Already in the shared framework. No new package. |
| CSRF | `SameSite=Lax` cookies, and an `X-Requested-With: stl` header required on every POST, PUT, PATCH and DELETE, which the client always sends and a plain form or image on another site can't | No token plumbing, and a cross-site request can't change anything. |
| Email | An `IEmailSender` (SMTP). When email isn't set up, Development writes the email, link included, to the log; Production logs only that it failed. SMTP is Gmail with an app password, as in the POC (Ernest's choice): `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_HOST` (empty with a `@gmail.com` user: `smtp.gmail.com`), `SMTP_PORT` (587, STARTTLS), `SMTP_FROM` (empty: `SMTP_USER`). Sent with .NET's own `System.Net.Mail.SmtpClient`, so no new package. The values are set on the admin page (#26): everything but the app password defaults to Ernest's Gmail | No domain to set up, and Gmail's limit (about 500 emails a day) is far above what sign-up and reset need. Move to a sending service with its own domain before a public launch or when the daily overview grows past that limit. |
| Rate limits | ASP.NET Core's rate limiter per client address (IPv4, or IPv6 /64): sign-up 5 an hour, log-in 10 per 15 minutes, the email links 5 an hour, password changes and links 10 per 15 minutes. The per-email log-in lock (5 tries in 15 minutes, then 15 minutes' wait) is counted in memory for every email typed, with or without an account, so it never tells which emails have one; a restart forgets it. A try is counted before its password is checked (a good log-in clears the count), so tries sent all at once can't get past it. It tracks at most 100,000 emails; when full it drops expired entries, then the oldest counts, and locks only when nothing else is left | The app is behind one nginx, so the address comes from `X-Forwarded-For` (`ForwardedHeaders`, trusting only the local proxy). |
| Who is the current user? | `Sessions.CurrentAsync`, read from the session once per request. Endpoints that need a user add `RequireUser()`, which answers 401 without a valid session. The fixed user is gone | One place decides, and a screen can't forget to check. |
| Which endpoints are public? | `GET /templates`, `/templates/{key}`, `/health`, `/version`, and the account endpoints below. Everything else needs a session | The template pages hold no user data. Until the language choice exists they are served in English to visitors. |

## Data

No new columns on `users` (the log-in lock is in memory, see Rate limits).

```
sessions      id uuid PK · user_id FK → users (cascade) · token_hash bytea unique · created_at ·
              last_used_at · expires_at (30 days after last use, capped at created_at + 90 days) · user_agent text null
email_tokens  id uuid PK · user_id FK → users (cascade) · purpose ('verify_email' | 'reset_password') ·
              token_hash bytea unique · created_at · expires_at (verify 24 h, reset 1 h) · used_at null
```

Both are new tables, so the migration changes no existing data. Expired rows are deleted by a daily clean-up (later, with the other scheduled jobs). `last_used_at` is written at most once a minute per session. IP addresses are not stored (privacy).

## How it is built

- `Auth/Sessions.cs`: the cookie and the `sessions` rows (logging in on a device ends the session it had); `CurrentAsync` answers "who is calling?" once per request, and `RequireUser()` is the endpoint filter that answers 401 without a session. `/dashboard` and `/strategies` use it.
- `Auth/AccountEndpoints.cs`: the endpoints below. Single-use links are claimed with one `UPDATE … WHERE used_at IS NULL`, so the same link can't be used twice at once; a reset runs in one transaction that first locks the user's row, so two reset links used at once can't deadlock. Changing or resetting the password uses up the user's other reset links. Forgot password finds the user and stores the link in one statement, so a known email costs the same round trips as an unknown one. Log-in refuses a password over 128 characters before hashing it.
- `Auth/AccountEmails.cs` and `Email/EmailQueue.cs`: account emails go through an in-memory queue that a background worker sends, so answers never wait on Gmail and their timing never tells whether an email went out. Without email set up, Development logs the email (link included) instead; Production logs only that it failed, never a link.
- `PublicUrl` (appsettings: the live address; `appsettings.Development.json`: `http://localhost:5173`) builds the links in emails. It is configuration, never taken from the request's Host header, so nobody can make a reset link point elsewhere.
- CSRF: a middleware refuses any POST, PUT, PATCH or DELETE without `X-Requested-With: stl`; the frontend's client sends it on every request.
- A wrong email takes as long as a wrong password: log-in checks a dummy hash when the email has no account.

**What sign-up can still tell.** The answer is the same either way, and a taken email does the same password hashing as a new one, but a new account is logged in at once (Ernest's choice) and an existing one isn't, so someone who signs up with an email can tell from the cookie (and roughly from the timing of the database writes) whether it already had an account. The sign-up rate limit (5 an hour per client) keeps that slow. Hiding it fully would mean not logging in at sign-up.

**Someone who signed up with your email first** keeps a working account until the email's owner acts. The confirmation email says to ignore it if you didn't sign up, and an unconfirmed account can't go live. If the owner clicks the link anyway, the account is confirmed, but the link logs no one in, so the other person still holds the password and their session. The owner takes the account back with "Forgot your password?" (also linked from the email they get when they try to sign up): a reset ends every other session. Closing this fully would mean choosing the password from the confirmation link, an open question for Ernest.

**The lock can be used to annoy.** Anyone can keep an email locked with 5 wrong tries every 15 minutes. A reset clears the lock; a lock per email and client would end that, if it is ever abused.

## Endpoints

All JSON, under `/api`. State-changing ones follow the CSRF rule above.

| Endpoint | Does |
| --- | --- |
| `POST /account/sign-up` | email, password, language, time zone, currency (optional, `EUR`), overview on/off, disclaimer accepted. Same answer whether or not the email exists; a new user is logged in |
| `POST /account/currency` | currency (`EUR`, `USD` or `GBP`) for the logged-in user's new strategies. Running ones keep theirs |
| `POST /account/log-in` | email, password. Same error for a wrong email and a wrong password; the lock counts tries per email |
| `POST /account/log-out` | ends this session |
| `GET /account/me` | the user (email, verified, language, time zone, currency) or 401. The app's start-up check |
| `POST /account/verify-email` | token from the email link. Confirms the email and logs no one in: a link that logged in whoever opened it would allow login CSRF and the takeover of an account someone pre-registered with a victim's email |
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
2. **Frontend**: Sign-up, Log-in, Forgot and Reset password, the check-your-email notice, the start-up check, a guard that sends visitors to Log-in and back, the 401 handling, and the account page's first version.
3. **Email**: the SMTP sender (Gmail, as in the POC). Ernest puts the five `SMTP_*` variables in `/etc/strategy-trading-lab/api.env` on the server and in `.env.local` for local work. Until then the links only reach the log. Small enough to ride with slice 1 if the variables are ready.

## The admin page

The admin settings page (#26) has its own fixed password, `STL` (or `ADMIN_PASSWORD` on the server), until accounts exist (Ernest's call). With accounts it moves behind an admin role on the user (Ernest's account), and the fixed password goes. The email sender reads its settings through `SettingsStore.ValuesAsync` (#26), all in one call.

## Not in this design

Social log-in, two-factor codes, "log out everywhere", the list of devices, and email change are left out until someone needs them.
