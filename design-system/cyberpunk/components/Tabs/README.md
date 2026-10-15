# Tabs

Editor tab strip (`tab.*`, `editorGroupHeader.tabsBackground`).

**Consumer provides:** a `<div class="cp-tabs">` container and one `<div class="cp-tab">` per item with its label; mark the current one `is-active`.

- Strip and inactive tabs: `surface-sidebar` with `ink-muted` labels and `border-tab` separators.
- Hover: `surface-hover` with an `accent-violet` bottom rule.
- Active: `surface-editor` (it merges into the content below) with a `primary` label.

Keep tabs square (`radius-none`). Inactive labels are low-contrast on purpose: the active tab is the one that must read.
