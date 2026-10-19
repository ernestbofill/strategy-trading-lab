
## Todos from Claude to Ernest
- [ ] Confirm or reorder ROADMAP → Next.
- [ ] Decide: a fixed dev user to save strategies before sign-up exists, or build sign-up first? (ROADMAP → Open decisions)
- [ ] Decide: the price data provider (commercial terms, cost at many free users).
- [ ] Cloud environment (claude.ai → Claude Code → environment settings): make sure Node 24 and .NET 10 are available.
- [ ] Cloud dev DB (optional): add `DATABASE_URL` and `DATABASE_URL_UNPOOLED` from the Neon `dev` branch as environment variables, and allow outbound access to Neon in the network settings.
- [ ] Mac: install .NET 10 (`dotnet` isn't on the PATH, so backend work here can't build or test).
- [ ] Check the frontend deploy after #21 shows the new disclaimers. (#21)

## Mine
- [ ] Instructions from STAB
- [ ] Make a plan and then give more autonomy to AI. Right now it is too much human driven.
- [ ] Split FE and BE repos?
- [x] CI / CD on github

## Minor
- [ ] On Oracle Cloud review if it should be more isolated from the trading bot.
- [ ] Admin ssh
- [x] Fix git history 
- [x] worktrees template
- [x] Review the free domain and ssl certificate.
  - https://certbot.eff.org/
  - [sslip](https://nip.io/)

## Ideas
- [ ] AI automation for continuous improvement.
- [ ] User can give feedback
- [ ] Referral program
- [ ] Agent to test the live app and review it as a user
