
## Todos from Claude to Ernest
- [ ] Confirm or reorder ROADMAP → Next. (#20)
- [ ] Decide: a fixed dev user to save strategies before sign-up exists, or build sign-up first? (ROADMAP → Open decisions) (#20)
- [ ] Decide: the price data provider (commercial terms, cost at many free users). (#20)
- [ ] Cloud environment (claude.ai → Claude Code → environment settings): make sure Node 24 and .NET 10 are available. (#20)
- [ ] Cloud dev DB (optional): add `DATABASE_URL` and `DATABASE_URL_UNPOOLED` from the Neon `dev` branch as environment variables, and allow outbound access to Neon in the network settings. (#20)
- [ ] Mac: install .NET 10 (`dotnet` isn't on the PATH, so backend work here can't build or test). (#20)
- [ ] Project tools (#22): this repo now turns off your claude.ai connectors and uses `.mcp.json` instead. Set two keys on the Mac, the PC and in the cloud environment (Mac: `export` in `~/.zshrc`, then restart the Claude app; Windows: a user environment variable; cloud: the environment's variables). Then open a new session and ask Claude for the connector status: `github`, `neon` and `exa` should be connected.
  - [ ] `GITHUB_PERSONAL_ACCESS_TOKEN`: a fine-grained token for `ernestbofill/strategy-trading-lab` only. Read and write for Contents, Pull requests and Issues; read for Actions.
  - [ ] `NEON_API_KEY`: from the Neon console (Account → API keys). It can reach the production branch too; a project-scoped key, if your plan has them, limits it to STL.
- [ ] Install the Playwright plugin (claude.ai plugins), so Claude can check screens in cloud sessions and the live app after merges (ROADMAP → Next #2). (#22)
- [ ] Check the frontend deploy after #21 shows the new disclaimers. (#21)
- [ ] Decide who reviews the Catalan copy: you, or also a native speaker before release? (#22)
- [ ] Global `~/.claude/settings.json` (Mac): the auto-mode environment describes the trading bot repo (default branch `master`, trusted repo `trading-strategies-bot`), and the default model is Sonnet. Move the bot's environment into that repo's settings, and pick the default model for this repo. (#22)

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
