# Notification

Floating console-glass panel used for notifications, hover cards and the suggest widget (`notifications.*`, `editorHoverWidget.*`, `editorSuggestWidget.*`).

**Consumer provides:** a `<div class="cp-widget">` with a `cp-widget__head` (title, close control), a `cp-widget__body` (one or two sentences, links allowed) and an optional `cp-widget__foot` holding Buttons.

- `surface-console` fill, 1px `primary` border, `shadow-widget`.
- Title in `primary`, uppercase `ui-strong`; body in `ink`; links in `link`, underlined.

One primary action at most, placed last (right). Don't stack more than three notifications.
