# Promo video

The first promo video, built in code with [Remotion](https://www.remotion.dev/). The plan is in [docs/promo/brief.md](../docs/promo/brief.md) and the shots in [docs/promo/storyboard.md](../docs/promo/storyboard.md).

It is its own npm package, outside `frontend/` and `backend/`, so it never runs in CI.

## Run it

From `promo/`, after `npm install`:

| Command | What it does |
| --- | --- |
| `npm run capture` | Captures the app's screens in English and Catalan into `public/screens/` (run the app locally first; the first time, `npx playwright install chromium`). |
| `npm run studio` | Opens Remotion Studio to scrub through the video. |
| `npm run render` | Renders `out/promo-en-nomusic.mp4`, `out/promo-ca-nomusic.mp4` and the two `.srt` caption files. |

Remotion brings its own ffmpeg, so none needs installing.

## Where things are

- `src/strings/en.json`, `ca.json`: every word on screen. A language is a re-render, nothing else.
- `src/timeline.json`: when each line shows (frames at 30 fps). The captions come from it too.
- `src/Promo.tsx`: the seven scenes. `src/components/`: the phone, the kinetic type and the effects.
- `src/theme.ts`: colours from the Cyberpunk tokens, the safe zone, and the `CINE` switches.
- `public/screens/` and `out/` are not in git: capture and render again.

## The second video (Pulse)

A different angle, English only, with no app screens and no footage: `src/pulse/` draws every picture on a canvas, with a small 3D line renderer of its own (`engine.ts`). Its words are in `src/pulse/strings.json` and its cues in `src/pulse/timeline.json`. `npm run render:pulse` writes `out/pulse-en-nomusic.mp4`. It needs no capture and no running app.

## Cinematic shots

Drop the Higgsfield clips into `public/cine/` as `a.mp4`, `b.mp4` and `c.mp4` (9:16), then set that shot to `true` in `CINE` in `src/theme.ts`. Until then each shot plays its coded fallback.

## The demo account

The dashboard, the waiting signal and the AI round are captured from a demo account on the **dev** database (Ernest chose rows written by hand, #218):

1. `node scripts/seed.mjs signup` signs up `claude-promo-<date>@stl.invalid` and keeps its session in `.auth/state.json` (gitignored). Its password is random and kept nowhere.
2. Confirm its email in the dev database (docs/dev-browser-test-account.md), then `node scripts/seed.mjs strategies` starts three strategies through the app's API: a simulation of Winners at a discount, a manual The comeback and a manual AI strategy, whose first round is due at once.
3. Written by hand on dev, with SQL: the simulation is back-dated to 2025-10-13 with seven confirmed buys (four on 2025-10-13, three on 2026-01-12) and their trades at the next day's real opening price, so its result follows from the stored prices, but the days and stocks are picked, not what the rules found. The manual strategy has one invented waiting signal, for ServiceNow on the latest stored day. Each stock touched also has a daily evaluation on that day, which keeps the daily check from filling the past with its own signals.

The waiting signal turns into a missed one once newer prices arrive on dev, so capture soon after seeding. Without `.auth/state.json`, `npm run capture` falls back to guest pages for those three slots.

## Still to do

- The Catalan app still shows the strategy names in English (they are the demo account's own names) and English template texts (an API bug): capture again once that is fixed.
- Music, and the with-music renders.
- The cinematic shots.
