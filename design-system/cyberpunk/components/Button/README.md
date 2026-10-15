# Button

Square-cornered neon command button, taken from VS Code's `button.*` and `extensionButton.*` colors.

**Consumer provides:** a `<button class="cp-btn">` with a short uppercase verb as its text; add a modifier for other emphasis.

- `cp-btn` — primary: `action-rest` fill (65% neon), `on-action` label, brightening to `action` with `glow-primary` on hover.
- `cp-btn--ghost` — secondary: `primary` text and 1px border on a transparent ground.
- `cp-btn--danger` — destructive: `danger-surface` fill, `danger` border, `ink` label.
- `disabled` — 40% opacity, no glow.

Do: one primary button per view; keep labels to one or two words in `ui-strong`.
Don't: round the corners past `radius-sm`, or put the glow on more than the hovered/focused control.
