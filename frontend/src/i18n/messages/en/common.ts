/** Copy used on several screens (spec section 3). Keys are common.*. */
export const common = {
  'common.disclaimer': 'Not financial advice. Signals follow your rules.',
  /** Where the user commits: accepted at sign-up, above the Confirm button and on the legal page. */
  'common.disclaimerFull': 'Not financial advice. Signals follow your rules; you decide and trade at your own broker.',
  /** Wherever past results appear. */
  'common.pastResults': "Past results don't promise future ones.",
  /** The top bar's link to the screen before. */
  'common.back': '\u2190 BACK',
  /** Tooltip of the DEV tag in the top bar. */
  'common.devDatabase': 'The API uses the dev database',
  /** The password toggle: the visible word, then the part only a screen reader hears ("Show password"). */
  'common.show': 'Show',
  'common.hide': 'Hide',
  'common.passwordSuffix': ' password',
  /** A screen's loading, error and not-found lines; `what` is the thing, already translated ("the template"). */
  'common.loading': 'Loading {what}…',
  'common.loadError': 'Could not load {what}. Try again later.',
  'common.notFound': "We couldn't find {what}.",
  /** Screen-reader names of a setting's minus / plus buttons; `label` is the setting's name in lower case. */
  'common.decrease': 'Decrease {label}',
  'common.increase': 'Increase {label}',
} as const
