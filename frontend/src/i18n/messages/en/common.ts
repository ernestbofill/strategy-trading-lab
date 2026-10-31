/** Copy used on several screens (spec section 3). Keys are common.*. */
export const common = {
  'common.disclaimer': 'Not financial advice. Signals follow your rules.',
  /** Where the user commits: accepted at sign-up, above the Confirm button and on the legal page. */
  'common.disclaimerFull': 'Not financial advice. Signals follow your rules; you decide and trade at your own broker.',
  /** Wherever past results appear. */
  'common.pastResults': "Past results don't promise future ones.",
  'common.indexTip.label': 'What is the S&P 500 fund?',
  'common.indexTip.text': 'Buy and hold: the same money put into an S&P 500 fund (SPY) on the same days, never sold.',
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

  // Legal footer and pages
  'legal.nav': 'Legal',
  'legal.notice.title': 'Legal notice',
  'legal.notice.lead': 'Information about the operator and contact details.',
  'legal.terms.title': 'Terms of use',
  'legal.terms.lead': 'Rules for using Strategy Trading Lab.',
  'legal.privacy.title': 'Privacy policy',
  'legal.privacy.lead': 'How data is stored and handled.',
  'legal.disclaimer.title': 'Disclaimer',
  'legal.disclaimer.content':
    'Strategy Trading Lab is a strategy lab and monitoring app for learning and following strategies with your own rules. The app never places orders and never connects to a broker.',
  'legal.about.title': 'About',
  'legal.about.lead': 'About Strategy Trading Lab.',
  'legal.about.content':
    'A strategy lab and monitoring side app. Pick a strategy template, tune its rules, and follow it as a simulation or live. The app never places orders and never connects to a broker.',
  'legal.topBar': 'STL // LEGAL',
  'legal.laterStep': 'This page comes in a later step.',
} as const
