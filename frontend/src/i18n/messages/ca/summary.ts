import type { summary as en } from '../en/summary'

export const summary: Record<keyof typeof en, string> = {
  'summary.what': 'el resum diari',
  'summary.title': 'Resum diari',
  'summary.heading': 'El teu resum, {date}',
  'summary.lead': 'Darrer dia de negociació, després del tancament del mercat nord-americà.',
  'summary.quiet.title': 'Cap canvi',
  'summary.quiet.body': 'Les teves estratègies continuen actives sense nous senyals ni canvis en valors.',
  'summary.waiting.title': "T'espera a tu",
  'summary.waiting.signal': '{stock}: es compleixen les teves regles per a {strategy}',
  'summary.waiting.step': 'Fase {step} de {max}',
  'summary.waiting.aiRound': "És l'hora de la ronda d'IA de {strategy}.",
  'summary.whatChanged.title': 'Què ha canviat',
  'summary.stockChanges.title': 'Canvis en valors',
  'summary.strategies.title': 'Les teves estratègies',
  'summary.strategies.since': 'des del {date}',
  'summary.strategies.signals': '{n, plural, one {# senyal} other {# senyals}}',
  'summary.noStrategies': 'Encara no tens cap estratègia activa.',
  'summary.startStrategy': 'Tria una plantilla per començar',
}
