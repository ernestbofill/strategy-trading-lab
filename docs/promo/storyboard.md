# Promo video: treatment and storyboard

The creative plan for the first promo, built from [brief.md](brief.md). Where the two differ, this file wins (changes agreed with Ernest on 2026-11-11: five ideas instead of seven, no "next Nvidia", no "live with real money", real backtest figures, the short disclaimer on the end card). The code is in [`promo/`](../../promo/).

## Treatment

**Idea: nerves against rules.** The video does not explain the app. It shows a feeling everyone who has held a stock knows (3 a.m., a red chart, a racing pulse), then cuts it off with something calm, green and exact. The viewer should leave with one question: *what did he build?*

- **Two worlds.** *Emotion* is hot pink and red, handheld, noisy, glitching. *Rules* is the app's neon green on deep violet, locked off, snapping to the beat. Every cut from one to the other is a glitch.
- **The phone is the hero.** A 3D phone carries the real app's screens. It slams in, spins and tilts on the beat; the screens are real captures, never mock-ups.
- **Words are few and big.** Fira Code, uppercase, one short line at a time, 2 to 5 words. Each line can be read in under a second with no sound.
- **Music:** dark synthwave, 120 BPM (one beat = 15 frames, one bar = 2 s). A low drone under the hook, the drop at 2.0 s, a one-bar silence at 12.0 s, a last hit at 21.0 s.
- **Length:** 26 s, 780 frames at 30 fps, 1080 × 1920.

## Shot list

`MG` motion graphics in code · `APP` a real app screen · `CINE` a cinematic AI shot. Every `CINE` slot has a code-rendered fallback, so the video is complete without Higgsfield and the AI shots are an upgrade.

| # | Time | Kind | What we see | Line (EN) | Line (CA) |
| --- | --- | --- | --- | --- | --- |
| 1 | 0.0–2.0 | CINE A + MG | Extreme close-up of eyes lit by a flickering red chart, in the dark. The words type in over it. *Fallback: a red candlestick chart crashing behind heavy grain.* | I STOPPED STARING AT CHARTS. | HE DEIXAT DE MIRAR GRÀFICS. |
| 2 | 2.0–4.0 | MG + APP | Glitch cut on the drop. Black, then the phone slams in from the dark with a green light sweep, showing the dashboard. | I BUILT THIS INSTEAD. | I HE CREAT AIXÒ. |
| 3 | 4.0–8.0 | MG + APP | The five template cards fly out of the phone and fan around it, one per beat, each lighting up with its real name. Then they collapse back in. | FIVE STRATEGIES. ZERO GUT FEELING. | CINC ESTRATÈGIES. ZERO IMPULSOS. |
| 4 | 8.0–12.0 | CINE B + MG + APP | A fly-through over a neon city whose towers are price bars; the skyline becomes the backtest line, drawing itself against the index fund's line. The phone shows the real "How we tested" screen, worst year included. *Fallback: the bars rise in code.* | TESTED ON 25 YEARS OF REAL PRICES. | PROVADES AMB 25 ANYS DE PREUS REALS. |
| 5 | 12.0–18.0 | CINE C + MG + APP | Music cuts. A dark bedroom, someone asleep, a phone on the nightstand glowing green once. *Fallback: black, one green pulse.* Then the signal drops in with a pulse ring and the phone shows the real signal screen. | IT WATCHES THE MARKET. / YOU MAKE THE CALL. | VIGILA EL MERCAT. / TU DECIDEIXES. |
| 6 | 18.0–21.0 | MG + APP | The AI round screen; scores tick up fast and the top one lights up. | OR BRING YOUR OWN AI. | O PORTA LA TEVA IA. |
| 7 | 21.0–26.0 | MG | Last hit. Logo lockup, the app's own tagline, the URL, then the small print. | TRADE RULES, NOT EMOTIONS. · strategytradinglab.com · FREE EARLY ALPHA. TELL ME WHAT BREAKS. · Not financial advice. Signals follow your rules. | OPERA SEGONS REGLES, NO PER EMOCIONS. · strategytradinglab.com · ALFA GRATUÏTA. DIGUE'M QUÈ FALLA. · No és assessorament financer. Els senyals segueixen les teves regles. |

Notes:

- Shot 4's "25 years" is the backtests' 2000–2024 period. Check it against the captured screen before the final render.
- The taglines in shot 7 and the Catalan disclaimer are the app's own copy (`welcome.hero.title`, the site footer). The other Catalan lines are new: Ernest checks them.
- Captions (`.srt`) repeat the on-screen lines, one cue per line.

## Cinematic shots (Seedance, 9:16, ~5 s each, keep 1.5–2 s)

No text, numbers, logos or screens that can be read in any shot.

- **A, the hook.** "Extreme close-up of a tired person's eyes at night in a dark room, face lit only by a flickering red and magenta glow from a screen out of frame, reflections of jagged red chart lines moving across the eyes, shallow depth of field, slight handheld shake, film grain, anamorphic, cinematic, cyberpunk colour palette, vertical 9:16."
- **B, the city.** "Fast low aerial fly-through over a vast night city where the skyscrapers are glowing neon green and violet bars of different heights like a bar chart, deep violet haze, light trails, the camera rises above the skyline at the end, cyberpunk, cinematic, volumetric light, vertical 9:16."
- **C, the night.** "Static shot of a dark bedroom at night, a person asleep and out of focus in the background, a phone face-up on a nightstand in the foreground, its screen pulses once with a soft neon green glow that lights the room, calm, cinematic, shallow depth of field, film grain, vertical 9:16."

For A, make the still first (Nano Banana Pro) so the face can be chosen, then animate it.

## Posting checklist

- Tick the "AI-generated" label on Instagram and YouTube if a cinematic shot is used (shot 1 shows a realistic person).
- Check that sign-ups are open, or put an invite link in the post.
