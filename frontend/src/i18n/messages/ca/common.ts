import type { common as en } from '../en/common'

export const common: Record<keyof typeof en, string> = {
  'common.disclaimer': 'No és assessorament financer. Els senyals segueixen les teves regles.',
  'common.disclaimerFull': 'No és assessorament financer. Els senyals segueixen les teves regles; tu decideixes i operes al teu broker.',
  'common.pastResults': 'Els resultats passats no garanteixen els futurs.',
  'common.indexTip.label': 'Què és el fons S&P 500?',
  'common.indexTip.text': 'Comprar i mantenir: els mateixos diners en un fons de l’S&P 500 (SPY) els mateixos dies, sense vendre mai.',
  'common.back': '\u2190 ENRERE',
  'common.devDatabase': "L'API fa servir la base de dades de desenvolupament",
  'common.show': 'Mostra',
  'common.hide': 'Amaga',
  'common.passwordSuffix': ' la contrasenya',
  'common.loading': 'Carregant {what}…',
  'common.loadError': "No s'ha pogut carregar {what}. Torna-ho a provar més tard.",
  'common.notFound': 'No hem trobat {what}.',
  'common.decrease': 'Redueix {label}',
  'common.increase': 'Augmenta {label}',

  // Legal footer and pages
  'legal.nav': 'Legal',
  'legal.notice.title': 'Avís legal',
  'legal.notice.lead': 'Informació sobre el responsable i dades de contacte.',
  'legal.terms.title': 'Condicions d’ús',
  'legal.terms.lead': 'Normes per fer servir Strategy Trading Lab.',
  'legal.privacy.title': 'Política de privadesa',
  'legal.privacy.lead': 'Com s’emmagatzemen i es tracten les dades.',
  'legal.disclaimer.title': 'Avís de responsabilitat',
  'legal.disclaimer.content':
    'Els resultats passats no garanteixen els futurs. Strategy Trading Lab és un laboratori d’estratègies i una aplicació de seguiment per aprendre i seguir estratègies amb les teves pròpies regles. L’aplicació mai no posa ordres ni es connecta a cap broker.',
  'legal.about.title': 'Quant a',
  'legal.about.lead': 'Quant a Strategy Trading Lab.',
  'legal.about.content':
    'Un laboratori d’estratègies i aplicació d’acompanyament. Tria una plantilla d’estratègia, ajusta les seves regles i segueix-la en simulació o en directe. L’aplicació mai no posa ordres ni es connecta a cap broker.',
  'legal.topBar': 'STL // LEGAL',
  'legal.genericTitle': 'Legal',
  'legal.genericLead': 'Informació legal.',
  'legal.laterStep': 'Aquesta pàgina vindrà en un pas posterior.',
}
