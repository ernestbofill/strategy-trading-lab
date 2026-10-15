# StatusBar

The 22px bottom bar (`statusBar.*`): console-glass fill, a neon top rule and mono status items.

**Consumer provides:** a `<div class="cp-status">` with one `<span>` per item; put a `<span class="sp"></span>` where items should split left/right. Add `cp-status--debug` while a debug session runs.

- Default: `surface-console`, `primary` text and border, `code-sm` type.
- Debug: `debug` text and rule.

Keep items terse (branch, position, encoding, language). Use Unicode glyphs or the host's icon font, not emoji.
