import type { account as en } from '../en/account'

export const account: Record<keyof typeof en, string> = {
  // Shared by the screens
  'account.field.email': 'Correu electrònic',
  'account.field.password': 'Contrasenya',
  'account.field.firstName': 'Nom',
  'account.field.lastName': 'Cognoms',
  'account.field.newPassword': 'Contrasenya nova',
  'account.passwordHint': 'Almenys 10 caràcters.',
  'account.saving': 'Desant…',
  'account.expiredLink': "Aquest enllaç ha caducat. Demana'n un de nou.",
  'account.error.tooManyTries': 'Massa intents. Espera uns minuts i torna-ho a provar.',
  'account.error.generic': 'Alguna cosa ha fallat. Torna-ho a provar.',
  'account.overviewEmail': 'Envia’m un resum diari per correu, després del tancament del mercat dels EUA.',
  'account.currencyName.EUR': 'Euro',
  'account.currencyName.USD': 'Dòlar dels EUA',
  'account.currencyName.GBP': 'Lliura esterlina',

  // Sign-up
  'account.signUp.topBar': 'STL // CREA UN COMPTE',
  'account.signUp.title': 'Crea el teu compte',
  'account.signUp.lead':
    'Tria una plantilla d’estratègia, ajusta les seves regles i segueix-la en simulació o en directe. L’aplicació mai no posa ordres ni es connecta a cap broker.',
  'account.signUp.skipLookAround': 'Salta-ho de moment, fes un cop d’ull',
  'account.signUp.language': 'Llengua',
  'account.signUp.currency': 'Moneda',
  'account.signUp.timeZone': 'Zona horària',
  'account.signUp.accept':
    '{disclaimer} L’aplicació serveix per aprendre, provar i seguir estratègies; mai no opera per tu.',
  'account.signUp.checkEmail': 'Mira el correu.',
  'account.signUp.submit': 'Crea el meu compte',
  'account.signUp.submitting': 'Creant-lo…',
  'account.signUp.note': 'Marca la casella de dalt per continuar.',
  'account.signUp.haveAccount': 'Ja tinc un compte',
  'account.signUp.inviteCode': "Codi d'invitació",
  'account.signUp.inviteHint': 'El trobaràs a la invitació, com ara ABCD-EFGH.',
  'account.signUp.haveInvite': "Tinc un codi d'invitació",
  'account.signUp.inviteOnly': 'De moment, el registre és amb invitació. Escriu el codi de la teva invitació.',
  'account.invite.eyebrow': 'Tens una invitació',
  'account.invite.title': 'Benvingut/da, {name}',
  'account.invite.from': "{name} t'ha convidat a Strategy Trading Lab.",
  'account.invite.anonymous': 'Tens una invitació a Strategy Trading Lab.',
  'account.invite.lead': 'Prova estratègies amb les teves pròpies regles, primer en simulació i sense diners reals. L’aplicació mai no opera per tu ni es connecta al teu bròquer.',
  'account.invite.code': 'El teu codi',
  'account.invite.notValid': "Aquesta invitació ja no es pot fer servir. Demana'n una de nova o escriu un altre codi a sota.",

  // Log-in
  'account.logIn.topBar': 'STL // INICIA LA SESSIÓ',
  'account.logIn.title': 'Inicia la sessió',
  'account.logIn.submit': 'Inicia la sessió',
  'account.logIn.submitting': 'Iniciant la sessió…',
  'account.logIn.mismatch': 'Aquest correu i aquesta contrasenya no coincideixen.',
  'account.logIn.tooManyTries': 'Massa intents. Espera uns minuts, o restableix la contrasenya.',
  'account.logIn.forgot': 'Has oblidat la contrasenya?',
  'account.logIn.create': 'Crea un compte',

  // Forgot password
  'account.forgot.topBar': 'STL // CONTRASENYA',
  'account.forgot.title': 'Has oblidat la contrasenya?',
  'account.forgot.lead': 'T’enviarem per correu un enllaç per triar-ne una de nova.',
  'account.forgot.sent': 'Si aquest correu té un compte, hi hem enviat un enllaç. Funciona durant 1 hora.',
  'account.forgot.submit': 'Envia l’enllaç',
  'account.forgot.submitting': 'Enviant…',
  'account.forgot.logIn': 'Inicia la sessió',

  // Reset password
  'account.reset.topBar': 'STL // CONTRASENYA',
  'account.reset.title': 'Tria una contrasenya nova',
  'account.reset.lead': 'Es tancarà la sessió a tots els altres dispositius.',
  'account.reset.submit': 'Desa la contrasenya',
  'account.reset.forgot': 'Has oblidat la contrasenya?',

  // Verify email (the link in the email)
  'account.verify.topBar': 'STL // CORREU',
  'account.verify.titleConfirmed': 'El teu correu està confirmat',
  'account.verify.title': 'Confirma el teu correu',
  'account.verify.checking': 'Comprovant l’enllaç…',
  'account.verify.expiredLoggedIn': 'La pàgina del teu compte te’n torna a enviar un.',
  'account.verify.expiredLoggedOut': 'Inicia la sessió perquè te’n tornem a enviar un.',
  'account.verify.failed': 'No s’ha pogut comprovar l’enllaç. Recarrega la pàgina per tornar-ho a provar.',
  'account.verify.logIn': 'Inicia la sessió',
  'account.verify.goAccount': 'Ves al teu compte',
  'account.verify.goDashboard': 'Ves al tauler',

  // Check-your-email notice and the resend link
  'account.notice.title': 'Mira el correu',
  'account.notice.body': 'Hem enviat un enllaç a {email} per confirmar-lo.',
  'account.notice.resend': 'Torna-l’hi a enviar',
  'account.resend.label': 'Torna a enviar l’enllaç',
  'account.resend.sent': 'Enviat. Mira el correu.',

  // Sign-in gate
  'account.gate.loading': 'Carregant…',
  'account.gate.unreachable': 'No s’ha pogut contactar amb Strategy Trading Lab.',
  'account.gate.retry': 'Torna-ho a provar',

  // Install the app
  'account.install.installed': 'L’aplicació està instal·lada en aquest dispositiu.',
  'account.install.manual':
    'A l’iPhone o l’iPad, obre aquesta pàgina amb el Safari, toca Comparteix i després Afegeix a la pantalla d’inici. A Android, obre el menú del navegador i tria Instal·la l’aplicació o Afegeix a la pantalla d’inici.',
  'account.install.started': 'S’ha iniciat la instal·lació.',
  'account.install.failed': 'No s’ha pogut obrir el diàleg d’instal·lació. Fes servir el menú del navegador.',
  'account.install.opening': 'Obrint el diàleg d’instal·lació…',
  'account.install.button': 'Instal·la l’aplicació',

  // Account page
  'account.page.step': 'COMPTE',
  'account.page.title': 'Compte',
  'account.page.email': 'Correu electrònic',
  'account.page.name': 'Nom',
  'account.page.nameSave': 'Desa el nom',
  'account.page.nameSaved': 'Desat.',
  'account.page.notVerified': 'Encara no està verificat.',
  'account.page.app': 'Aplicació',
  'account.page.language': 'Llengua',
  'account.page.languageSave': 'Desa la llengua',
  'account.page.languageSaved': 'Desat.',
  'account.page.currency': 'Moneda',
  'account.page.currencySave': 'Desa la moneda',
  'account.page.currencySaved': 'Desat. Les estratègies noves la faran servir; les que ja funcionen mantenen la seva.',
  'account.page.reloadFailed': 'No s’ha pogut tornar a carregar el compte.',
  'account.page.emails': 'Correus',
  'account.page.admin': 'Administració',
  'account.page.adminLine': 'Configuració, base de dades, preus i revisió de comentaris.',
  'account.page.adminLink': 'Configuració d\'administració →',
  'account.page.changePassword': 'Canvia la contrasenya',
  'account.page.dataPrivacy': 'Dades i privacitat',
  'account.aiSharing': 'Comparteix les meves rondes d’IA, per saber quines IA puntuen millor les accions. Si ho desactivo, s’esborra el que he compartit.',
  'account.page.currentPassword': 'Contrasenya actual',
  'account.page.passwordChanged': 'Contrasenya canviada. S’ha tancat la sessió als teus altres dispositius.',
  'account.page.logOut': 'Tanca la sessió',
  'account.page.loggingOut': 'Tancant la sessió…',
}
