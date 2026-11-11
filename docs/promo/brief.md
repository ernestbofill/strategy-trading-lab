# Strategy Trading Lab: first promo video brief
 
Nov 11, 2026 · @Ernest
 
## Goal and audience
A first short promo to get attention from friends and followers. The audience is very small and the app is a free alpha with no users yet: the aim is curiosity and first sign-ups. Copy can be punchy; the regulation note's marketing constraints don't apply to this video.
 
**Direction:** a flashy, professional promo, not a screencast. Motion graphics in the app's Cyberpunk style carry it, with the real app's screens inside them and a few short cinematic AI shots for mood.
 
**Versions:** English and Catalan.
 
## Where and how it's built
- **Model:** Claude Fable 5.2, for creative direction and the build. Check that it is offered in Claude Code with `/model`.
- **Tool:** Claude Code on Ernest's machine, in the `strategy-trading-lab` repo, with this brief in `docs/promo/brief.md` and the code in `promo/`. Locally it can run the app with seeded demo data, download freely and render on the local CPU.
- **Needs:** ffmpeg installed. Remotion, Playwright or other npm packages are installed as needed.
## Three kinds of shot
1. **Motion graphics rendered in code** (HTML/CSS, Three.js or Remotion, at full 1080 × 1920): the backbone. Kinetic type, a floating 3D phone, neon charts drawing themselves, glitch and scanline transitions, a signal dropping in, AI scores ticking up, light sweeps, particles.
2. **The real app's screens**, captured with Playwright from the app running locally with a seeded demo account (good-looking strategies with results, one waiting signal, an AI round). Placed on the 3D phone and in the motion graphics. Captured twice, with the app set to English and to Catalan, same account and same data, so each version shows the app in its own language. No AI-generated app UI.
3. **Cinematic AI shots** (Higgsfield), a few, 1-3 s each, for mood and story: e.g. a late night lit by flickering charts, a neon city of stock prices. Marked as such in the storyboard. Shared by both versions.
## Cinematic shots (Higgsfield free trial)
- **Model:** Seedance 2.0 at 720p (unlimited in the trial) for the final shots. 720p vertical scales 1.5× to 1080 × 1920, which holds up on a phone. Seedance 2.5 at 480p only for quick tests of prompts and compositions.
- **Format:** generate in 9:16 from the start. Generate ~5 s clips and keep the best 1-3 s.
- **Consistency:** for a recurring person or place, first make a still in Nano Banana Pro at 2K, then animate it (image-to-video, if offered for Seedance on Higgsfield).
- **No text or UI** in AI shots: all words and app screens stay in the code layer, so the same clips serve both languages.
- **Blending:** the same colour grade, film grain and neon tint over AI shots and motion graphics, so it reads as one film and hides upscaling softness.
- **Trial:** €0 for 3 days, then €79 for the first month and €129/month after. Start it only once the storyboard and prompts are approved, and turn off auto-renewal right after starting. Day 1: stills and a first batch of clips. Day 2: redo the misses. Day 3: spare.
- **Fallback:** free stock footage (Pexels, Pixabay) for any mood shot that doesn't need to be specific.
## Sound
- An original or royalty-free track, with cuts on the beat: Higgsfield Seed Audio during the trial, or the YouTube Audio Library / Pixabay Music. The same track for both versions.
- Also a copy of each version with no music, so one of Instagram's own licensed tracks can be added in the app.
- No voiceover: most Reels are watched on mute, and the captions carry the message.
## English and Catalan
All words live in one strings file per language, `strings/en.json` and `strings/ca.json` (on-screen text and captions), never typed into a shot. Ernest checks the Catalan copy (native speaker); it should be written as Catalan, not translated word for word.
- **Shared:** the edit, timings, cinematic shots and music.
- **Per language:** the kinetic type and captions (rendered from the strings file) and the app screens (captured in that language). A language is a re-render of the code scenes only.
- **Timing:** Catalan lines run ~20-30% longer than English. Write the English short, and time each caption so the Catalan line can be read too; the two versions keep the same cut.
- **Outputs:**
  - `out/promo-en.mp4`, `out/promo-ca.mp4`: with music.
  - `out/promo-en-nomusic.mp4`, `out/promo-ca-nomusic.mp4`: for Instagram's licensed music.
  - `out/promo-en.srt`, `out/promo-ca.srt`: caption tracks for YouTube.
- **Posting:** Instagram gets the file for the post's language (or both, as two posts). YouTube Shorts gets one version, with both `.srt` files uploaded as caption tracks.
## Format
- 1080 × 1920 (9:16), 30 fps, H.264, ~20-30 s.
- Text inside the safe zone: clear of the top ~250 px, the bottom ~400 px and the right ~150 px (Reels and Shorts UI).
- The first 2 seconds must work on mute and stop the scroll.
- Platforms: Instagram Reels and Stories, and YouTube Shorts.
## Story beats (starting point for the storyboard)
Superseded by [storyboard.md](storyboard.md), which has the final shots and lines.

| Beat | Idea | Draft line (EN) |
| --- | --- | --- |
| Hook | Cinematic: a late night, a face lit by charts; glitch cut to the app | "I stopped staring at charts. I built this instead." |
| Pick | The five templates fan out as neon cards around the phone | "Pick a strategy. Find the next Nvidia." |
| Proof | The backtest line draws itself against the index | "See how it did in the past" (stronger claim only if the demo figures show it) |
| Mode | The phone flips between Simulation and Live | "Test it on paper, or go live with real money" |
| Signal | A signal drops in with a pulse: "Your rules are met" | "It watches the market. You get the signal." |
| AI | Scores tick up, the top one lights up | "Or let your AI pick the stocks" |
| End | Logo lockup, URL | "Free, early alpha. Try it and tell me what breaks." |
 
Catalan lines are written in the storyboard alongside the English ones.
 
## Process
1. **Treatment and storyboard (Fable):** concept, shot list with timings, each shot marked as motion graphics, app screen or cinematic, the Seedance prompt for each cinematic shot, the opening line in English and Catalan, and the music mood.
2. **Look frames:** a few stills at full size for approval, in both languages.
3. **App capture:** seed the demo account, capture the screens with Playwright in English and in Catalan.
4. **Cinematics:** start the Higgsfield trial, turn off renewal, generate stills and clips.
5. **Build:** motion scenes, grade, render the English version, then re-render the code scenes for Catalan; music, captions, no-music copies.
6. **Check:** score the cut with Higgsfield's virality predictor (during the trial); adjust the opening if needed.