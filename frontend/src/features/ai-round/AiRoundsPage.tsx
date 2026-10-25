import { useParams } from 'react-router-dom'
import type { AiRoundItem } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER, PAST_RESULTS_CAPTION } from '../../lib/copy'
import { pctTone, shortDate, signedPct, signedPctOrDash } from '../../lib/format'
import { aiRoundPath, strategyPath } from '../dashboard/paths'
import styles from './AiRound.module.css'
import { useAiRounds } from './useAiRound'
import { ScoreList } from './ScoreList'

const STATUS: Record<AiRoundItem['status'], string> = { due: 'DUE', done: 'DONE', skipped: 'SKIPPED' }

/** AI rounds (not drawn): every round of the strategy, newest first, and how its picks did against the rest. */
export function AiRoundsPage() {
  const { id = '' } = useParams()
  const history = useAiRounds(id)
  const top = <TopBar back={strategyPath(id)} aside={<span>AI ROUNDS</span>} />
  return (
    <LoadableScreen state={history} top={top} what="the AI rounds">
      {(h) => (
        <Screen top={top} note={`${DISCLAIMER} ${PAST_RESULTS_CAPTION}`}>
          <PageHeader title="AI rounds" />
          <p className={styles.plain}>{h.strategyName}</p>
          {h.picksVsRest && (
            <Section title="Picks vs the rest">
              <p className={styles.plain}>
                Since each round, the stocks that got a step moved{' '}
                <b className={styles.mono} data-tone={pctTone(h.picksVsRest.picks)}>
                  {signedPct(h.picksVsRest.picks, 1)}
                </b>{' '}
                on average, the others <b className={styles.mono}>{signedPct(h.picksVsRest.rest, 1)}</b> ({h.picksVsRest.rounds}{' '}
                {h.picksVsRest.rounds === 1 ? 'round' : 'rounds'}).
              </p>
              <p className="caption">Price changes in each stock's own currency, without fees. It shows whether the scores picked well, not your results.</p>
            </Section>
          )}
          <ul className={styles.rounds}>
            {h.rounds.map((round) => (
              <li key={round.id} className={styles.round}>
                <div className={styles.roundHead}>
                  <span>{shortDate(round.dueOn)}</span>
                  <span className="ui-strong">{STATUS[round.status]}</span>
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
  if (round.status === 'due')
    return round.dueOn <= today ? (
      <TextAction to={aiRoundPath(strategyId)}>Run it now</TextAction>
    ) : (
      <p className={styles.plain}>The next round.</p>
    )
  if (round.status === 'skipped') return <p className={styles.plain}>Not run within its week: no signal that round.</p>
  return (
    <>
      <p className={styles.plain}>
        {round.aiModel} · run {round.doneAt ? shortDate(round.doneAt.slice(0, 10)) : ''}
        {round.promptEdited ? ' · your prompt' : ''}
      </p>
      {(round.picks !== null || round.rest !== null) && (
        <p className={styles.plain}>
          Since then: picks <span className={styles.mono}>{signedPctOrDash(round.picks, 1)}</span> · the rest{' '}
          <span className={styles.mono}>{signedPctOrDash(round.rest, 1)}</span>
        </p>
      )}
      <ScoreList scores={round.scores} maxSteps={null} />
      {round.prompt && (
        <details className={styles.details}>
          <summary className="ui-strong">The prompt</summary>
          <pre className={`well ${styles.answer}`}>{round.prompt}</pre>
        </details>
      )}
      {round.answer && (
        <details className={styles.details}>
          <summary className="ui-strong">Your AI's answer</summary>
          <pre className={`well ${styles.answer}`}>{round.answer}</pre>
        </details>
      )}
    </>
  )
}
