/**
 * The attributes of the panel a tab of `Tabs` shows: `id` is the one given to `Tabs`, and `shared` its `sharedPanel` (one panel
 * for every tab, labelled by the tab `key` shows).
 */
export const tabPanel = (id: string, key: string, shared?: boolean) => ({
  id: shared ? `${id}-panel` : `${id}-panel-${key}`,
  role: 'tabpanel',
  'aria-labelledby': `${id}-tab-${key}`,
})
