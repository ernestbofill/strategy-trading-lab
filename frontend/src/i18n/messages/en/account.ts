/** The account screens: sign-up, log-in, password reset, email check and the Account page. Keys are account.*. */
export const account = {
  // Shared by the screens
  'account.field.email': 'Email',
  'account.field.password': 'Password',
  'account.field.newPassword': 'New password',
  'account.passwordHint': 'At least 10 characters.',
  'account.saving': 'Saving…',
  'account.expiredLink': 'This link has expired. Ask for a new one.',
  'account.error.tooManyTries': 'Too many tries. Wait a few minutes and try again.',
  'account.error.generic': 'Something went wrong. Try again.',
  'account.overviewEmail': 'Email me a daily overview, early in the morning.',
  'account.currencyOption': '{code} · {name}',
  'account.currencyName.EUR': 'Euro',
  'account.currencyName.USD': 'US dollar',
  'account.currencyName.GBP': 'British pound',

  // Sign-up
  'account.signUp.topBar': 'STL // SIGN UP',
  'account.signUp.title': 'Create your account',
  'account.signUp.lead': 'Follow strategies with your own rules. The app never trades for you.',
  'account.signUp.language': 'Language',
  'account.signUp.currency': 'Currency',
  'account.signUp.timeZone': 'Time zone',
  'account.signUp.accept': '{disclaimer} The app is for learning, testing and following strategies; it never trades for you.',
  'account.signUp.checkEmail': 'Check your email.',
  'account.signUp.submit': 'Create my account',
  'account.signUp.submitting': 'Creating it…',
  'account.signUp.note': 'Tick the box above to go on.',
  'account.signUp.haveAccount': 'I already have an account',

  // Log-in
  'account.logIn.topBar': 'STL // LOG IN',
  'account.logIn.title': 'Log in',
  'account.logIn.submit': 'Log in',
  'account.logIn.submitting': 'Logging in…',
  'account.logIn.mismatch': "That email and password don't match.",
  'account.logIn.tooManyTries': 'Too many tries. Wait a few minutes, or reset your password.',
  'account.logIn.forgot': 'Forgot your password?',
  'account.logIn.create': 'Create an account',

  // Forgot password
  'account.forgot.topBar': 'STL // PASSWORD',
  'account.forgot.title': 'Forgot your password?',
  'account.forgot.lead': "We'll email you a link to choose a new one.",
  'account.forgot.sent': 'If that email has an account, we sent a link. It works for 1 hour.',
  'account.forgot.submit': 'Send the link',
  'account.forgot.submitting': 'Sending…',
  'account.forgot.logIn': 'Log in',

  // Reset password
  'account.reset.topBar': 'STL // PASSWORD',
  'account.reset.title': 'Choose a new password',
  'account.reset.lead': "You'll be logged out everywhere else.",
  'account.reset.submit': 'Save password',
  'account.reset.forgot': 'Forgot your password?',

  // Verify email (the link in the email)
  'account.verify.topBar': 'STL // EMAIL',
  'account.verify.titleConfirmed': 'Your email is confirmed',
  'account.verify.title': 'Confirm your email',
  'account.verify.checking': 'Checking the link…',
  'account.verify.expiredLoggedIn': 'Your account page sends it again.',
  'account.verify.expiredLoggedOut': 'Log in to have it sent again.',
  'account.verify.failed': "Couldn't check the link. Reload to try again.",
  'account.verify.logIn': 'Log in',
  'account.verify.goAccount': 'Go to your account',
  'account.verify.goDashboard': 'Go to the dashboard',

  // Check-your-email notice and the resend link
  'account.notice.title': 'Check your email',
  'account.notice.body': 'We sent a link to {email} to confirm it.',
  'account.notice.resend': 'Send it again',
  'account.resend.label': 'Send the link again',
  'account.resend.sent': 'Sent. Check your email.',

  // Sign-in gate
  'account.gate.loading': 'Loading…',
  'account.gate.unreachable': 'Could not reach Strategy Trading Lab.',
  'account.gate.retry': 'Try again',

  // Install the app
  'account.install.installed': 'The app is installed on this device.',
  'account.install.manual':
    'On iPhone or iPad, open this page in Safari, tap Share, then Add to Home Screen. On Android, open the browser menu and choose Install app or Add to Home screen.',
  'account.install.started': 'Installation started.',
  'account.install.failed': 'Could not open the install prompt. Use your browser menu instead.',
  'account.install.opening': 'Opening install prompt…',
  'account.install.button': 'Install the app',

  // Account page
  'account.page.step': 'ACCOUNT',
  'account.page.title': 'Account',
  'account.page.email': 'Email',
  'account.page.notVerified': 'Not verified yet.',
  'account.page.app': 'App',
  'account.page.language': 'Language',
  'account.page.languageSave': 'Save language',
  'account.page.languageSaved': 'Saved.',
  'account.page.currency': 'Currency',
  'account.page.currencySave': 'Save currency',
  'account.page.currencySaved': 'Saved. New strategies use it; running ones keep theirs.',
  'account.page.reloadFailed': 'Could not reload the account.',
  'account.page.emails': 'Emails',
  'account.page.admin': 'Admin',
  'account.page.adminLine': 'Settings, database, prices and feedback review.',
  'account.page.adminLink': 'Admin settings →',
  'account.page.changePassword': 'Change password',
  'account.page.dataPrivacy': 'Data and privacy',
  'account.aiSharing': 'Share my AI rounds, to learn which AIs score stocks best. Turning it off removes what I shared.',
  'account.page.currentPassword': 'Current password',
  'account.page.passwordChanged': 'Password changed. Your other devices are logged out.',
  'account.page.logOut': 'Log out',
  'account.page.loggingOut': 'Logging out…',
} as const
