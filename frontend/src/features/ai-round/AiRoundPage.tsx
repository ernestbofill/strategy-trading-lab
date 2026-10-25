import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError, InvalidValueError, type AiReading, type AiRoundDetail } from '../../api'
import { Callout } from '../../components/Callout'
import { Checkbox, FormMessage, SelectField, TextAreaField, TextField, type FormMessageState } from '../../components/Field'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER_FULL } from '../../lib/copy'
import { money, shortDate } from '../../lib/format'
import { invalidate } from '../../lib/useLoadable'
import { accountPath, aiPromptPath, aiRoundsPath, signalPath, strategyPath } from '../dashboard/paths'
import { dashboardCacheKey } from '../dashboard/useDashboard'
import { dashboardPath } from '../new-strategy/paths'
import { strategyCacheKey } from '../strategy/useStrategy'
import { AI_MODELS, AI_SERVICES, OTHER_MODEL } from './aiModels'
import styles from './AiRound.module.css'
import { AiSharingDetails } from './AiSharingDetails'
import { PromptText } from './PromptText'
import { ScoreList } from './ScoreList'
import { aiRoundCacheKey, aiRoundsCacheKey, useAiRound } from './useAiRound'

/** AI round (wireframe `AIRound`): copy the prompt, paste the answer, check the scores, use them. */
export function AiRoundPage() {
  const { id = '' } = useParams()
  const round = useAiRound(id)
  const top = (r?: AiRoundDetail) => (
    <TopBar back={strategyPath(id)} aside={r && <span>AI ROUND · {shortDate(r.open ? r.today : r.dueOn).toUpperCase()}</span>} />
  )
  return (
    <LoadableScreen state={round} top={top()} what="this AI round">
      {(r) => <RoundScreen key={r.roundId} r={r} top={top(r)} />}
    </LoadableScreen>
  )
}

function Header({ r }: { r: AiRoundDetail }) {
  return (
    <div className={styles.header}>
      <div className={styles.eyebrow}>
        <Tag tone={r.mode === 'live' ? 'live' : 'sim'}>{r.mode === 'live' ? 'LIVE' : 'SIM'}</Tag>
        <span>
          {r.strategyName} · <em>{r.templateName}</em>
        </span>
      </div>
      <h1 className={styles.title}>{r.open ? 'Time for the AI round' : 'Next AI round'}</h1>
    </div>
  )
}

/** The model the last round used, as the select and the "Other" field hold it. */
function initialModel(last: string | null): { choice: string; other: string } {
  if (!last) return { choice: AI_MODELS[0], other: '' }
  return AI_MODELS.includes(last) ? { choice: last, other: '' } : { choice: OTHER_MODEL, other: last }
}

function RoundScreen({ r, top }: { r: AiRoundDetail; top: ReactNode }) {
  const navigate = useNavigate()
  const [copied, setCopied] = useState(false)
  const [answer, setAnswer] = useState('')
  const [reading, setReading] = useState<AiReading | null>(null)
  const [model, setModel] = useState(() => initialModel(r.lastModel))
  const [share, setShare] = useState(false)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<FormMessageState>(null)
  const [modelError, setModelError] = useState<string | null>(null)
  const readCount = useRef(0)

  // Read the answer a moment after the user stops typing or pastes; only the latest reading counts.
  useEffect(() => {
    if (!answer.trim()) return
    const count = ++readCount.current
    const timer = setTimeout(() => {
      api.readAiAnswer(r.strategyId, answer).then(
        (result) => count === readCount.current && setReading(result),
        () => count === readCount.current && setMessage({ tone: 'error', text: "We couldn't read it. Try again." }),
      )
    }, 400)
    return () => clearTimeout(timer)
  }, [answer, r.strategyId])

  if (!r.open) {
    return (
      <Screen top={top} action={<PrimaryAction to={strategyPath(r.strategyId)}>Back to the strategy</PrimaryAction>}>
        <Header r={r} />
        <p className={styles.plain}>
          Your next AI round is due on <span className={styles.mono}>{shortDate(r.dueOn)}</span>. You can run it until{' '}
          <span className={styles.mono}>{shortDate(r.lastDay)}</span>; after that it's skipped, with no signal that round.
        </p>
        <TextAction to={aiRoundsPath(r.strategyId)}>See the past rounds</TextAction>
      </Screen>
    )
  }

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(r.prompt)
      setCopied(true)
    } catch {
      setMessage({ tone: 'error', text: "Your browser didn't let us copy it. Select the prompt and copy it by hand." })
    }
  }

  const modelName = model.choice === OTHER_MODEL ? model.other.trim() : model.choice
  const canUse = !!reading?.ok && answer.trim() !== '' && modelName !== '' && !saving

  const use = async () => {
    if (!canUse) return
    setSaving(true)
    setMessage(null)
    setModelError(null)
    try {
      const result = await api.useAiScores(r.strategyId, answer, modelName, r.sharingAsked ? null : share)
      for (const key of [dashboardCacheKey, strategyCacheKey(r.strategyId), aiRoundCacheKey(r.strategyId), aiRoundsCacheKey(r.strategyId)])
        invalidate(key)
      const first = result.signalIds[0]
      navigate(result.mode === 'live' && first ? signalPath(r.strategyId, first) : dashboardPath)
    } catch (error) {
      setSaving(false)
      if (error instanceof InvalidFieldsError) {
        setModelError(error.fields.aiModel ?? null)
        if (error.fields.answer) setMessage({ tone: 'error', text: error.fields.answer })
      } else
        setMessage({ tone: 'error', text: error instanceof InvalidValueError ? error.message : "We couldn't save it. Try again." })
    }
  }

  const shown = answer.trim() ? reading : null

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void use()} disabled={!canUse}>
          {saving ? 'Saving…' : 'Use these scores'}
        </PrimaryAction>
      }
      note={DISCLAIMER_FULL}
    >
      <Header r={r} />
      <p className={styles.plain}>It takes about two minutes. The app doesn't connect to your AI.</p>

      <section className={styles.step}>
        <h2 className={`ui-strong ${styles.stepTitle}`}>
          <span className={styles.stepNumber}>01</span> Copy the prompt
        </h2>
        <PromptText text={r.prompt} version={r.promptVersion} edited={r.promptEdited} collapsed />
        <div className={styles.row}>
          <button type="button" className={`ui-strong ${styles.copy}`} data-copied={copied || undefined} onClick={() => void copy()}>
            {copied ? '✓ Copied' : 'Copy prompt'}
          </button>
          <TextAction to={aiPromptPath(r.strategyId)}>Edit prompt</TextAction>
        </div>
        <p className={styles.services}>
          Open in{' '}
          {AI_SERVICES.map((service, i) => (
            <span key={service.name}>
              {i > 0 && ' · '}
              <a href={service.url} target="_blank" rel="noopener noreferrer">
                {service.name}
              </a>
            </span>
          ))}
        </p>
      </section>

      <section className={styles.step}>
        <h2 className={`ui-strong ${styles.stepTitle}`}>
          <span className={styles.stepNumber}>02</span> Paste the answer
        </h2>
        <TextAreaField
          label="Your AI's whole answer"
          value={answer}
          onChange={(value) => {
            setAnswer(value)
            if (!value.trim()) setReading(null)
          }}
          rows={8}
          maxLength={50_000}
          placeholder="| Stock | … | Overall | Reason |"
        />
        <SelectField
          label="AI model you used"
          value={model.choice}
          onChange={(choice) => setModel((m) => ({ ...m, choice }))}
          options={[...AI_MODELS, OTHER_MODEL].map((m) => ({ value: m, label: m }))}
          error={model.choice === OTHER_MODEL ? null : modelError}
        />
        {model.choice === OTHER_MODEL && (
          <TextField label="Which AI model?" value={model.other} onChange={(other) => setModel((m) => ({ ...m, other }))} error={modelError} />
        )}
      </section>

      {shown && (
        <section className={styles.step}>
          <h2 className={`ui-strong ${styles.stepTitle}`}>
            <span className={styles.stepNumber}>03</span> Your AI's scores
          </h2>
          {shown.problems.length > 0 && (
            <Callout tone="warning" title="Ask your AI again">
              <ul className={styles.problems}>
                {shown.problems.map((p) => (
                  <li key={p}>{p}</li>
                ))}
              </ul>
            </Callout>
          )}
          {shown.scores.length > 0 && <ScoreList scores={shown.scores} maxSteps={shown.maxSteps} />}
          {shown.ok && (
            <>
              <p className={styles.check}>✓ Every stock scored, all scores 0-100.</p>
              <p className={styles.plain}>{stepLine(shown, r)}</p>
            </>
          )}
        </section>
      )}

      {r.sharingAsked ? (
        <p className={styles.plain}>
          {r.sharing ? 'Sharing anonymously' : 'Not sharing your AI rounds'} · <Link to={accountPath}>Change</Link>
        </p>
      ) : (
        <div className={styles.consent}>
          <h2 className="ui-strong">Help us learn which AIs score stocks best</h2>
          <p>
            Share this round anonymously: the AI model you used and its answer. We never keep your name, email, budget or trades
            with it. You can stop at any time in Account → Data and privacy.
          </p>
          <Checkbox checked={share} onChange={setShare}>
            Share my AI rounds anonymously
          </Checkbox>
          <AiSharingDetails />
        </div>
      )}

      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </Screen>
  )
}

/** What the scores give: "Zscaler gets step 2 of 5 (€100)." or why no stock gets one. */
function stepLine(reading: AiReading, r: AiRoundDetail): string {
  const picks = reading.scores.filter((s) => s.step !== null)
  if (picks.length === 0)
    return reading.short === 'budget'
      ? 'No budget left: this round gives no step.'
      : 'Every scored stock has all its steps: this round gives no step.'
  const step = reading.stepAmount === null ? '' : ` (${money(reading.stepAmount, r.currency)})`
  const of = reading.maxSteps === null ? '' : ` of ${reading.maxSteps}`
  const gets = picks.map((p) => `${p.name} gets step ${p.step}${of}`).join(', ')
  const fewer = picks.length < r.topN ? (reading.short === 'budget' ? ' The budget has no room for more.' : ' The others have all their steps.') : ''
  return `${gets}${step}.${fewer}`
}
