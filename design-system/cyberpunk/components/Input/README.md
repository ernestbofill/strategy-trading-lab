# Input

Text field with a neon 1px border and console-glass fill, plus the info / warning / error message boxes VS Code shows under it (`input.*`, `inputValidation.*`).

**Consumer provides:** a `<label class="cp-field">` wrapping its label text and an `<input class="cp-input">`; for validation, a sibling `<div class="cp-msg cp-msg--info|warning|error">` with the message, starting with the word Info, Warning or Error.

- Typed text is `input-ink` in the mono family; the caret is `cursor`; placeholder is `input-placeholder`.
- Focus adds `glow-primary`; the border stays `border-focus`.
- Messages: `ink` text on `info-surface`, `warning-surface` or `danger-surface`, bordered by `info`, `warning` or `danger`.

Don't rely on placeholder text for instructions: it sits below 4.5:1 contrast. Don't tell states apart by color alone; lead with the word.
