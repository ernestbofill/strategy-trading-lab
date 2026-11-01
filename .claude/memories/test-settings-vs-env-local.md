# A test's setting loses to .env.local locally

In API tests, `WithWebHostBuilder(b => b.UseSetting(key, value))` is overridden by the same key in the developer's `.env.local`, which `Program.cs` adds as a later configuration source in Development. CI has no `.env.local`, so such a test passes there and fails locally.

**Why:** `FeedbackEndpointTests.New_feedback_is_emailed_to_the_owner…` failed locally for this reason: `OWNER_EMAIL` from `.env.local` won over the test's address (2026-11-01, #110).

**How to apply:** to set a value a developer's `.env.local` may also hold (`OWNER_EMAIL`, mail or Neon settings), add it last instead: `b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(...))`. `UseSetting` is fine for keys `.env.local` never sets (the rate limits).
