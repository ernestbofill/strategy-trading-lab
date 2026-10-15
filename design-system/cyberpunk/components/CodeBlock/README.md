# CodeBlock

Syntax-highlighted code with a line-number gutter, in the theme's token colors (`tokenColors`) and recommended editor settings: Fira Code, ligatures on, 30px line height.

**Consumer provides:** a `<pre class="cp-code">` with one `<span class="ln">` per line (add `cur` to the current line) and spans with the token classes from their highlighter: `t-com` comment, `t-kw` keyword, `t-fn` function/operator/punctuation, `t-me` method, `t-tag` tag/`this`, `t-num` number/parameter, `t-str` string, `t-cls` class/type, `t-attr` attribute, `t-var` variable.

- Ground `surface-editor`; unclassed text falls back to `primary`.
- Current line: `surface-deep` with an `accent` line number.

The rainbow is the point: the theme gives nearly every token kind its own hue so the eye spots structure fast. Don't remap token colors per protocol; they stay the same in all three.
