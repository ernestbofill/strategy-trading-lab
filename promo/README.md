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

## Cinematic shots

Drop the Higgsfield clips into `public/cine/` as `a.mp4`, `b.mp4` and `c.mp4` (9:16), then set that shot to `true` in `CINE` in `src/theme.ts`. Until then each shot plays its coded fallback.

## Still to do

- The signed-in screens (dashboard, a waiting signal, an AI round) need a seeded account on dev. For now those slots show guest pages: the welcome page, a template's review and the AI template.
- Music, and the with-music renders.
- The cinematic shots.
