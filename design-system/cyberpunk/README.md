This design-system is AI generated from
[https://github.com/prometheux-ar/cyberpunk](https://github.com/prometheux-ar/cyberpunk)


Neon on deep violet. Cyberpunk is a dark-only system lifted from the "crazy cyberpunk theme" for VS Code and Visual Studio: a wide, deliberate rainbow on a midnight ground, so every kind of thing gets its own color and the eye finds structure before it reads.

> "I chose the cyberpunk theme because it allows me to use a very wide color palette … so our eye quickly detects color changes." — the theme's author

## Protocols (themes)

The system ships three themes, the theme's own variants. Set `data-theme` on the root.

| Theme | Ground | Signature | Mood |
| --- | --- | --- | --- |
| `cyberpunk` (default) | violet `surface-editor` #261d45 | mint `primary` #00ff9c | the original |
| `umbra` | near-black indigo #100d23 | the same mint | darker, calmer chrome |
| `scarlet` | graphite #101116 | hot red `primary` #ff0055 | aggressive, red-on-black |

Buttons (`action`, `action-rest`), states and all `syntax-*` colors are identical across protocols; only chrome and the signature hue change.

## Content fundamentals

- Voice: terse, technical, a little playful. Short imperative labels in uppercase: INSTALL, UPDATE, LATER.
- Sci-fi flavor goes in sample and empty-state copy ("Wake up, samurai"), never in errors or instructions.
- Address the reader as "you". No emoji in UI; use Unicode glyphs (⎇ ⟳ ▶ ×) or the host's icon font.
- Numbers and identifiers are set in the mono family.

## Visual foundations

**Color**
- Page ground is `surface-editor`. Side panels, tab strips and inactive tabs sit on `surface-sidebar`; title bars and wells on `surface-deep`; bottom panels on `surface-panel`.
- Floating things (status bar, notifications, hovers, inputs) use the translucent green-black console glass, `surface-console` / `surface-input`, with a 1px `primary` border.
- `primary` is the voice of the system: body text in the editor, active tab labels, status bar text, widget borders. `accent` is its brighter cousin for icons and the active line number. `hot-pink` is for progress and "new".
- Default text is `ink` on any surface. In side panels use `ink-sidebar`; for secondary labels `ink-muted` on `surface-deep`.
- States: `info` cyan, `warning` orange, `danger` red-pink, each with a `*-surface` fill that carries `ink` text. `success` is a bright green and `danger` a red, told apart by hue: always add a word or a + / − mark.
- Code uses the `syntax-*` set: comments `syntax-comment` (italic, deliberately dim), keywords `syntax-keyword`, functions and punctuation `syntax-function`, strings `syntax-string`, numbers and parameters `syntax-number`, classes `syntax-class`, tags `syntax-tag`, attributes `syntax-attribute`.
- Several source pairs fall under 4.5:1 and are kept exact; each token's note says where (`ink-muted` on `surface-sidebar`, `input-placeholder`, `link` in Cyberpunk/UMBRA, `syntax-comment`, `ink-line-number`). Never use them for essential UI text.

**Type**
- Mono family is Fira Code (load from Google Fonts), with ligatures on. Use it for code (`code`, 14/30, the theme's recommended line height), status items (`code-sm`) and all display text (`display`, `heading`, `title`).
- Interface text (`ui`, `ui-strong`, `caption`) uses the platform UI stack, as the editor chrome does. `ui-strong` labels are uppercase with 0.5px tracking.

**Space, shape, depth**
- Spacing on a 4px base: `space-1` 4, `space-2` 8, `space-3` 16, `space-4` 24.
- Chrome is square: panels, tabs and bars use `radius-none`; controls `radius-sm` (2px); only count badges are pills (`radius-pill`).
- Separate regions with 1px `border-hairline` lines and fill changes, not shadows. The only shadow is `shadow-widget` on floating panels.
- Glow (`glow-primary`) is the one effect: on the hovered or focused control only.
- Focus ring: 1px solid `border-focus`, 2px offset; at least 3:1 on every surface in every protocol.

**Motion**
- None defined by the source. Keep state changes instant or under 100ms.

## Iconography and imagery

- The source ships no icon set; it styles VS Code's own Codicons through `accent` (activity bar) and `primary`. Use the host's icon font or simple Unicode glyphs, tinted with `accent`.
- Logos (group Logos): `cyberpunk-mark.png`, the yellow chevron "λ" over a circuit pattern, and `cyberpunk-wordmark.png`, the stacked CYBER / PUNK lettering in mint with violet and pink offset shadows. Both are full-color PNGs on transparent; place them on `surface-editor` or darker, never recolor or rebuild them.
- Screenshots (group Screenshots) show each protocol in the editor; use them as references for how the colors combine.
