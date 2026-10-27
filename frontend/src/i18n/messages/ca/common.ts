import type { common as en } from '../en/common'

export const common: Record<keyof typeof en, string> = {
  'common.disclaimer': 'No és assessorament financer. Els senyals segueixen les teves regles.',
  'common.disclaimerFull': 'No és assessorament financer. Els senyals segueixen les teves regles; tu decideixes i operes al teu broker.',
  'common.pastResults': 'Els resultats passats no garanteixen els futurs.',
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
}
