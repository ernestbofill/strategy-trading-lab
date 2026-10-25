import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError, InvalidValueError, type AiReading, type AiRoundDetail } from '../../api'
import { Callout } from '../../components/Callout'
import { Checkbox, FormMessage, SelectField, TextAreaField, TextField, type FormMessageState } from '../../components/Field'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { useI18n, useT, type Translate } from '../../i18n/I18n'
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
import { stepOf } from './stepOf'
import { aiRoundCacheKey, aiRoundsCacheKey, useAiRound } from './useAiRound'

/** AI round (wireframe `AIRound`): copy the prompt, paste the answer, check the scores, use them. */
export function AiRoundPage() {
  const { id = '' } = useParams()
  const round = useAiRound(id)
  const t = useT()
  const { locale } = useI18n()
  const top = (r?: AiRoundDetail) => (
    <TopBar
      back={strategyPath(id)}
      aside={r && <span>{t('aiRound.topBar', { date: shortDate(r.open ? r.today : r.dueOn, locale).toUpperCase() })}</span>}
    />
  )
  return (
    <LoadableScreen state={round} top={top()} what={t('aiRound.what')}>
      {(r) => <RoundScreen key={r.roundId} r={r} top={top(r)} />}
    </LoadableScreen>
  )
}

function Header({ r }: { r: AiRoundDetail }) {
  const t = useT()
  return (
    <div className={styles.header}>
      <div className={styles.eyebrow}>
        <Tag tone={r.mode === 'live' ? 'live' : 'sim'}>{r.mode === 'live' ? 'LIVE' : 'SIM'}</Tag>
        <span>
          {r.strategyName} · <em>{r.templateName}</em>
        </span>
      </div>
      <h1 className={styles.title}>{t(r.open ? 'aiRound.title' : 'aiRound.nextTitle')}</h1>
    </div>
  )
}

function StepTitle({ n, children }: { n: string; children: ReactNode }) {
  return (
    <h2 className={`ui-strong ${styles.stepTitle}`}>
      <span className={styles.stepNumber}>{n}</span> {children}
    </h2>
  )
}

/** The model the last round used, as the select and the "Other" field hold it. */
function initialModel(last: string | null): { choice: string; other: string } {
  if (!last) return { choice: AI_MODELS[0], other: '' }
  return AI_MODELS.includes(last) ? { choice: last, other: '' } : { choice: OTHER_MODEL, other: last }
}

function RoundScreen({ r, top }: { r: AiRoundDetail; top: ReactNode }) {
  const t = useT()
  const { locale } = useI18n()
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
        () => count === readCount.current && setMessage({ tone: 'error', text: t('aiRound.readFailed') }),
      )
    }, 400)
    return () => clearTimeout(timer)
  }, [answer, r.strategyId, t])

  if (!r.open) {
    return (
      <Screen top={top} action={<PrimaryAction to={strategyPath(r.strategyId)}>{t('aiRound.backToStrategy')}</PrimaryAction>}>
        <Header r={r} />
        <p className={styles.plain}>{t('aiRound.notDue', { due: shortDate(r.dueOn, locale), last: shortDate(r.lastDay, locale) })}</p>
        <TextAction to={aiRoundsPath(r.strategyId)}>{t('aiRound.pastRounds')}</TextAction>
      </Screen>
    )
  }

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(r.prompt)
      setCopied(true)
    } catch {
      setMessage({ tone: 'error', text: t('aiRound.copyFailed') })
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
        setMessage({ tone: 'error', text: error instanceof InvalidValueError ? error.message : t('aiRound.saveFailed') })
    }
  }

  const shown = answer.trim() ? reading : null

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void use()} disabled={!canUse}>
          {t(saving ? 'aiRound.saving' : 'aiRound.use')}
        </PrimaryAction>
      }
      note={t('common.disclaimerFull')}
    >
      <Header r={r} />
      <p className={styles.plain}>{t('aiRound.intro')}</p>

      <section className={styles.step}>
        <StepTitle n="01">{t('aiRound.step.copy')}</StepTitle>
        <PromptText text={r.prompt} version={r.promptVersion} edited={r.promptEdited} collapsed />
        <div className={styles.row}>
          <button type="button" className={`ui-strong ${styles.copy}`} data-copied={copied || undefined} onClick={() => void copy()}>
            {t(copied ? 'aiRound.copied' : 'aiRound.copy')}
          </button>
          <TextAction to={aiPromptPath(r.strategyId)}>{t('aiRound.editPrompt')}</TextAction>
        </div>
        <p className={styles.services}>
          {t('aiRound.openIn')}{' '}
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
        <StepTitle n="02">{t('aiRound.step.paste')}</StepTitle>
        <TextAreaField
          label={t('aiRound.answer')}
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
          label={t('aiRound.model')}
          value={model.choice}
          onChange={(choice) => setModel((m) => ({ ...m, choice }))}
          options={[...AI_MODELS.map((m) => ({ value: m, label: m })), { value: OTHER_MODEL, label: t('aiRound.modelOther') }]}
          error={model.choice === OTHER_MODEL ? null : modelError}
        />
        {model.choice === OTHER_MODEL && (
          <TextField label={t('aiRound.modelName')} value={model.other} onChange={(other) => setModel((m) => ({ ...m, other }))} error={modelError} />
        )}
      </section>

      {shown && (
        <section className={styles.step}>
          <StepTitle n="03">{t('aiRound.step.scores')}</StepTitle>
          {shown.problems.length > 0 && (
            <Callout tone="warning" title={t('aiRound.askAgain')}>
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
              <p className={styles.check}>{t('aiRound.allScored')}</p>
              <p className={styles.plain}>{stepLine(shown, r, t)}</p>
            </>
          )}
        </section>
      )}

      {r.sharingAsked ? (
        <p className={styles.plain}>
          {t(r.sharing ? 'aiRound.sharing' : 'aiRound.notSharing')} · <Link to={accountPath}>{t('aiRound.change')}</Link>
        </p>
      ) : (
        <div className={styles.consent}>
          <h2 className="ui-strong">{t('aiRound.consent.title')}</h2>
          <p>{t('aiRound.consent.text')}</p>
          <Checkbox checked={share} onChange={setShare}>
            {t('aiRound.consent.check')}
          </Checkbox>
          <AiSharingDetails />
        </div>
      )}

      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </Screen>
  )
}

/** What the scores give: "Zscaler gets step 2 of 5 (€100)." or why no stock gets one. */
function stepLine(reading: AiReading, r: AiRoundDetail, t: Translate): string {
  const picks = reading.scores.filter((s) => s.step !== null)
  if (picks.length === 0) return t(reading.short === 'budget' ? 'aiRound.noStepBudget' : 'aiRound.noStepAllSteps')
  const gets = picks
    .map((p) => t('aiRound.gets', { stock: p.name, step: stepOf(t, p.step ?? 0, reading.maxSteps) }))
    .join(', ')
  const line =
    reading.stepAmount === null
      ? t('aiRound.getsNoAmount', { picks: gets })
      : t('aiRound.getsAmount', { picks: gets, amount: money(reading.stepAmount, r.currency) })
  if (picks.length >= r.topN) return line
  return `${line} ${t(reading.short === 'budget' ? 'aiRound.fewerBudget' : 'aiRound.fewerAllSteps')}`
}
