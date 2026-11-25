# What a cloud session's network reaches (for data work)

2026-11-25, PR #283 (backtest data).

- **Yahoo Finance: no.** `query1/query2.finance.yahoo.com` answer 429 to the cloud container, and retrying with a browser user agent was refused by the permission check. Anything that needs Yahoo prices runs on the server (an owner-started job), not in the session.
- **raw.githubusercontent.com: yes** (github.com and api.github.com pages: 403).
- **Wikipedia: yes, but rate-limited.** `w/api.php` answers 429 from the shared address after a few calls; `w/rest.php/v1/page/<title>` gives the wikitext and its revision id in one call and worked, with a retry honouring `Retry-After`.

**Why:** the item 52 data download had to be designed around it (membership committed to the repo, prices fetched on the server).

**How to apply:** for a task that needs market data, plan for the server or mocked data from the start; test a host with one `curl` before building on it.
