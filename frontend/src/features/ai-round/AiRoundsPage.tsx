import { useParams } from 'react-router-dom'
import type { AiRoundItem } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useI18n, useT } from '../../i18n/I18n'
import { isoDate, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { aiRoundPath, strategyPath } from '../dashboard/paths'
import styles from './AiRound.module.css'
import { ScoreList } from './ScoreList'
import { useAiRounds } from './useAiRound'

/** AI rounds (not drawn): every round of the strategy, newest first, and how its picks did against the rest. */
export function AiRoundsPage() {
  const { id = '' } = useParams()
  const t = useT()
  const { locale } = useI18n()
  const history = useAiRounds(id)
  const top = <TopBar back={strategyPath(id)} aside={<span>{t('aiRound.history.topBar')}</span>} />
  return (
    <LoadableScreen state={history} top={top} what={t('aiRound.historyWhat')}>
      {(h) => (
        <Screen top={top} note={`${t('common.disclaimer')} ${t('common.pastResults')}`}>
          <PageHeader title={t('aiRound.history.title')} />
          <p className={styles.eyebrow}>
            <Tag tone={h.mode === 'live' ? 'live' : 'sim'}>{h.mode === 'live' ? 'LIVE' : 'SIM'}</Tag>
            <span>{h.strategyName}</span>
          </p>
          {h.picksVsRest && (
            <Section title={t('aiRound.history.picksVsRest')}>
              <p className={styles.plain}>
                {t('aiRound.history.picksLine', {
                  picks: signedPct(h.picksVsRest.picks, 1),
                  rest: signedPct(h.picksVsRest.rest, 1),
                  n: h.picksVsRest.rounds,
                })}
              </p>
              <p className="caption">{t('aiRound.history.picksNote')}</p>
            </Section>
          )}
          <ul className={styles.rounds}>
            {h.rounds.map((round) => (
              <li key={round.id} className={styles.round}>
                <div className={styles.roundHead}>
                  <span>{shortDate(round.dueOn, locale)}</span>
                  <span className="ui-strong">{t(`aiRound.history.status.${round.status}`)}</span>
                </div>
                <RoundBody strategyId={h.strategyId} round={round} today={h.today} />
              </li>
            ))}
          </ul>
        </Screen>
      )}
    </LoadableScreen>
  )
}

function RoundBody({ strategyId, round, today }: { strategyId: string; round: AiRoundItem; today: string }) {
  const t = useT()
  const { locale } = useI18n()
  if (round.status === 'due')
    return round.dueOn <= today ? (
      <TextAction to={aiRoundPath(strategyId)}>{t('aiRound.history.runNow')}</TextAction>
    ) : (
      <p className={styles.plain}>{t('aiRound.history.next')}</p>
    )
  if (round.status === 'skipped') return <p className={styles.plain}>{t('aiRound.history.skipped')}</p>
  return (
    <>
      <p className={styles.plain}>
        {[
          round.aiModel,
          // The day it was run where the user is, not in UTC.
          round.doneAt && t('aiRound.history.run', { date: shortDate(isoDate(new Date(round.doneAt)), locale) }),
          round.promptEdited && t('aiRound.history.yourPrompt'),
        ]
          .filter(Boolean)
          .join(' · ')}
      </p>
      {(round.picks !== null || round.rest !== null) && (
        <p className={styles.plain}>
          {t('aiRound.history.since', { picks: signedPctOrDash(round.picks, 1), rest: signedPctOrDash(round.rest, 1) })}
        </p>
      )}
      <ScoreList scores={round.scores} maxSteps={null} />
      {round.prompt && (
        <details className={styles.details}>
          <summary className="ui-strong">{t('aiRound.history.prompt')}</summary>
          <pre className={`well ${styles.answer}`}>{round.prompt}</pre>
        </details>
      )}
      {round.answer && (
        <details className={styles.details}>
          <summary className="ui-strong">{t('aiRound.history.answer')}</summary>
          <pre className={`well ${styles.answer}`}>{round.answer}</pre>
        </details>
      )}
    </>
  )
}
