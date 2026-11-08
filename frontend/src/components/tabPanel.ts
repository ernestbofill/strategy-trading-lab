/** The attributes of the panel a tab of `Tabs` shows: `id` is the one given to `Tabs`. */
export const tabPanel = (id: string, key: string) => ({
  id: `${id}-panel-${key}`,
  role: 'tabpanel',
  'aria-labelledby': `${id}-tab-${key}`,
})
