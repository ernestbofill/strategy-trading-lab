import { useState, type FormEvent, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError, InvalidValueError, type SignalDetail, type SkipReason } from '../../api'
import { Callout } from '../../components/Callout'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { DISCLAIMER } from '../../lib/copy'
import { exact, money, shortDate, symbolOf } from '../../lib/format'
import { invalidate, useLoadable } from '../../lib/useLoadable'
import { strategyPath } from '../dashboard/paths'
import { dashboardCacheKey } from '../dashboard/useDashboard'
import { strategyCacheKey } from '../strategy/useStrategy'
import styles from './SignalPage.module.css'
import { initialForm, nextStep, rateOn, SKIP_REASONS, tradeRequest, type TradeForm } from './signalForm'

const signalCacheKey = (id: string) => `signal:${id}`
const FORM_ID = 'signal-answer'

/** "SIGNAL · OCT 22". */
const title = (s: SignalDetail) => `SIGNAL · ${shortDate(s.signalDate).toUpperCase()}`

/** Confirm a signal (wireframe `Signal`), live strategies only. */
export function SignalPage() {
  const { id = '', signalId = '' } = useParams()
  const signal = useLoadable(signalCacheKey(signalId), () => api.getSignal(signalId))
  const top = (s?: SignalDetail) => <TopBar back={strategyPath(s?.strategyId ?? id)} aside={s && <span>{title(s)}</span>} />

  return (
    <LoadableScreen state={signal} top={top()} what="this signal">
      {(s) => <SignalScreen s={s} top={top(s)} />}
    </LoadableScreen>
  )
}

function SignalScreen({ s, top }: { s: SignalDetail; top: ReactNode }) {
  const header = (
    <div className={styles.header}>
      <div className={styles.eyebrow}>
        <Tag tone="live">LIVE</Tag>
        <span>
          {s.strategyName} · <em>{s.templateName}</em>
        </span>
      </div>
      <h1 className={styles.title}>{s.stockName}</h1>
    </div>
  )
  const box = (
    <div className={styles.box}>
      <p className={styles.text}>
        Your rules for <b>{s.strategyName}</b> are met for {s.stockName}: {s.reason}.
        {s.stepNo !== null && s.maxSteps !== null && (
          <>
            {' '}
            Under your settings that's step{' '}
            <span className={styles.mono}>
              {s.stepNo} of {s.maxSteps}
            </span>
            {s.stepAmount !== null && (
              <>
                {' '}
                (<span className={styles.mono}>{money(s.stepAmount, s.strategyCurrency)}</span>)
              </>
            )}
            .
          </>
        )}
      </p>
      <p className={styles.close}>
        Close on {shortDate(s.signalDate)}: <span className={styles.mono}>{exact(s.closePrice, s.assetCurrency)}</span>
      </p>
    </div>
  )

  if (s.answer) {
    return (
      <Screen top={top} note={DISCLAIMER} action={<PrimaryAction to={strategyPath(s.strategyId)}>Back to the strategy</PrimaryAction>}>
        {header}
        {box}
        <Callout tone="info" title="Answered">
          <AnswerLine s={s} />
        </Callout>
      </Screen>
    )
  }
  return <AnswerForm s={s} top={top} header={header} box={box} />
}

/** "You did it: 0.52 shares at $190.00 on Oct 23 (€98.80, fee €1.20)." or "You skipped it: no cash." */
function AnswerLine({ s }: { s: SignalDetail }) {
  const a = s.answer!
  if (a.status === 'skipped') {
    const reason = SKIP_REASONS.find((r) => r.id === a.skipReason)?.label
    return <p className={styles.plain}>You skipped it{reason ? `: ${reason.toLowerCase()}` : ''}.</p>
  }
  return (
    <p className={styles.plain}>
      You did it: <span className={styles.mono}>{a.shares}</span> shares at{' '}
      <span className={styles.mono}>{exact(a.price ?? 0, s.assetCurrency)}</span> on {a.date ? shortDate(a.date) : '–'} (
      <span className={styles.mono}>{exact(a.amount ?? 0, s.strategyCurrency)}</span>, fee{' '}
      <span className={styles.mono}>{exact(a.fee ?? 0, s.strategyCurrency)}</span>).
    </p>
  )
}

function AnswerForm({ s, top, header, box }: { s: SignalDetail; top: ReactNode; header: ReactNode; box: ReactNode }) {
  const navigate = useNavigate()
  const [answer, setAnswer] = useState<'did' | 'skip'>('did')
  const [form, setForm] = useState<TradeForm>(() => initialForm(s))
  const [reason, setReason] = useState<SkipReason | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [saving, setSaving] = useState(false)
  const [snoozedUntil, setSnoozedUntil] = useState(s.snoozedUntil)
  const set = (patch: Partial<TradeForm>) => setForm((f) => ({ ...f, ...patch }))

  const otherCurrency = s.assetCurrency !== s.strategyCurrency
  const reference = rateOn(s.rates, form.date)
  const rateUnit = `${symbolOf(s.assetCurrency)} per ${symbolOf(s.strategyCurrency)}1`

  const done = () => {
    for (const key of [dashboardCacheKey, strategyCacheKey(s.strategyId), signalCacheKey(s.id)]) invalidate(key)
    navigate(strategyPath(s.strategyId))
  }

  async function save(e: FormEvent) {
    e.preventDefault()
    if (saving) return
    setSaving(true)
    setErrors({})
    setMessage(null)
    try {
      if (answer === 'did') await api.confirmSignal(s.id, tradeRequest(form))
      else await api.skipSignal(s.id, reason)
      done()
    } catch (error) {
      if (error instanceof InvalidFieldsError) setErrors(error.fields)
      else setMessage({ tone: 'error', text: error instanceof InvalidValueError ? error.message : 'Could not save it. Try again.' })
      setSaving(false)
    }
  }

  async function snooze() {
    setMessage(null)
    try {
      setSnoozedUntil(await api.snoozeSignal(s.id))
      invalidate(signalCacheKey(s.id))
    } catch (error) {
      setMessage({ tone: 'error', text: error instanceof InvalidValueError ? error.message : 'Could not snooze it. Try again.' })
    }
  }

  const following = nextStep(s)
  const priceHint =
    s.prefill.price !== null
      ? `Prefilled with ${shortDate(s.prefill.date)}'s opening price. Change anything that differs from your broker.`
      : `The next opening price isn't in yet. The close on ${shortDate(s.signalDate)} was ${exact(s.closePrice, s.assetCurrency)}.`

  return (
    <Screen
      top={top}
      note={DISCLAIMER}
      action={
        <PrimaryAction form={FORM_ID} disabled={saving}>
          {answer === 'did' ? 'Save the trade' : 'Save the skip'}
        </PrimaryAction>
      }
    >
      {header}
      {box}
      {s.status === 'missed' && s.endedOn && (
        <Callout tone="warning" title="Missed">
          <p className={styles.plain}>
            This signal ended on {shortDate(s.endedOn)} without an answer. You can still record what you did.
          </p>
        </Callout>
      )}
      <form id={FORM_ID} className={styles.form} onSubmit={save} noValidate>
        <fieldset className={styles.choices}>
          <legend className={styles.legend}>Did you act on it?</legend>
          {(
            [
              ['did', '✓ I did it'],
              ['skip', '× I skipped it'],
            ] as const
          ).map(([id, label]) => (
            <label key={id} className={styles.choice} data-on={answer === id || undefined}>
              <input
                type="radio"
                name="answer"
                value={id}
                checked={answer === id}
                onChange={() => setAnswer(id)}
                className="visually-hidden"
              />
              {label}
            </label>
          ))}
        </fieldset>

        {answer === 'did' ? (
          <div className={styles.fields}>
            <TextField label="Date" type="date" value={form.date} onChange={(date) => set({ date })} error={errors.date} />
            <TextField
              label={`Price per share (${s.assetCurrency})`}
              type="decimal"
              value={form.price}
              onChange={(price) => set({ price })}
              hint={priceHint}
              error={errors.price}
            />
            {form.enteredAs === 'amount' ? (
              <TextField
                label={`Amount (${s.strategyCurrency})`}
                type="decimal"
                value={form.amount}
                onChange={(amount) => set({ amount })}
                hint={s.stepAmount !== null ? `What bought shares. Your step of ${money(s.stepAmount, s.strategyCurrency)} includes the fee.` : undefined}
                error={errors.amount}
                aside={<TextAction onClick={() => set({ enteredAs: 'shares' })}>Shares instead</TextAction>}
              />
            ) : (
              <TextField
                label="Shares"
                type="decimal"
                value={form.shares}
                onChange={(shares) => set({ shares })}
                error={errors.shares}
                aside={<TextAction onClick={() => set({ enteredAs: 'amount' })}>Amount instead</TextAction>}
              />
            )}
            <TextField
              label={`Fee (${s.strategyCurrency}, optional)`}
              type="decimal"
              value={form.fee}
              placeholder="0.00"
              onChange={(fee) => set({ fee })}
              error={errors.fee}
            />
            {otherCurrency &&
              (form.ownRate === null ? (
                <div className={styles.rate}>
                  <p className={styles.plain}>
                    {reference ? (
                      <>
                        At{' '}
                        <span className={styles.mono}>
                          {symbolOf(s.assetCurrency)}
                          {reference.rate.toFixed(4)}
                        </span>{' '}
                        per {symbolOf(s.strategyCurrency)}1, the rate of{' '}
                        {shortDate(reference.date)}.
                      </>
                    ) : (
                      "No reference rate for that day yet: enter your broker's."
                    )}
                  </p>
                  <TextAction onClick={() => set({ ownRate: reference ? reference.rate.toFixed(4) : '' })}>Change</TextAction>
                  {errors.fxRate && <FormMessage tone="error">{errors.fxRate}</FormMessage>}
                </div>
              ) : (
                <TextField
                  label={`Your broker's rate (${rateUnit})`}
                  type="decimal"
                  value={form.ownRate}
                  onChange={(ownRate) => set({ ownRate })}
                  error={errors.fxRate}
                  aside={reference && <TextAction onClick={() => set({ ownRate: null })}>Reference</TextAction>}
                />
              ))}
          </div>
        ) : (
          <div className={styles.fields}>
            <fieldset className={styles.reasons}>
              <legend className={styles.legend}>Why? (optional)</legend>
              {SKIP_REASONS.map((r) => (
                <button
                  key={r.id}
                  type="button"
                  aria-pressed={reason === r.id}
                  className={styles.chip}
                  onClick={() => setReason(reason === r.id ? null : r.id)}
                >
                  {r.label}
                </button>
              ))}
            </fieldset>
            <p className={styles.plain}>
              A skip uses no budget. The rules carry on as if the step was bought
              {following ? `, so the next signal for ${s.stockName} is ${following}.` : '.'}
            </p>
          </div>
        )}
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>

      {s.status === 'waiting' && (
        <div className={styles.snooze}>
          {snoozedUntil && snoozedUntil > s.today ? (
            <p role="status" className={styles.plain}>
              We'll remind you on {shortDate(snoozedUntil)}.
            </p>
          ) : (
            <TextAction onClick={snooze}>Remind me tomorrow</TextAction>
          )}
        </div>
      )}
    </Screen>
  )
}
