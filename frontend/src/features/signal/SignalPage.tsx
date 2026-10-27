import { useState, type FormEvent, type ReactNode } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError, InvalidValueError, type SignalDetail, type SkipReason } from '../../api'
import { Callout } from '../../components/Callout'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { Tag } from '../../components/Tag'
import { TextAction } from '../../components/TextAction'
import { exact, formatShares, money, shortDate, symbolOf } from '../../lib/format'
import { invalidate, useLoadable } from '../../lib/useLoadable'
import { useT } from '../../i18n/I18n'
import { strategyPath } from '../dashboard/paths'
import { dashboardCacheKey } from '../dashboard/useDashboard'
import { strategyCacheKey } from '../strategy/useStrategy'
import styles from './SignalPage.module.css'
import { ambiguousFields, initialForm, nextStep, rateOn, SKIP_REASON_KEYS, skipReasons, tradeRequest, type TradeForm } from './signalForm'

const signalCacheKey = (id: string) => `signal:${id}`
const FORM_ID = 'signal-answer'

/** Confirm a signal (wireframe `Signal`), live strategies only. */
export function SignalPage() {
  const t = useT()
  const { id = '', signalId = '' } = useParams()
  const signal = useLoadable(signalCacheKey(signalId), () => api.getSignal(signalId))
  const top = (s?: SignalDetail) => (
    <TopBar back={strategyPath(s?.strategyId ?? id)} aside={s && <span>{t('signal.title', { date: shortDate(s.signalDate).toUpperCase() })}</span>} />
  )

  return (
    <LoadableScreen state={signal} top={top()} what={t('signal.what')}>
      {(s) => <SignalScreen s={s} top={top(s)} />}
    </LoadableScreen>
  )
}

function SignalScreen({ s, top }: { s: SignalDetail; top: ReactNode }) {
  const t = useT()
  const sell = s.side === 'sell'
  const header = (
    <div className={styles.header}>
      <div className={styles.eyebrow}>
        <Tag tone="live">{t('signal.live')}</Tag>
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
        {sell
          ? t('signal.rulesSell', { strategy: s.strategyName, reason: s.reason })
          : t('signal.rules', { strategy: s.strategyName, stock: s.stockName, reason: s.reason })}
        {!sell && s.stepAmount !== null && (
          <>
            {' '}
            {s.stepNo !== null && s.maxSteps !== null
              ? t('signal.stepAmount', {
                  step: t('signal.stepOf', { n: s.stepNo, max: s.maxSteps }),
                  amount: money(s.stepAmount, s.strategyCurrency),
                })
              : t('signal.amountOnly', { amount: money(s.stepAmount, s.strategyCurrency) })}
          </>
        )}
        {!sell && s.stepAmount === null && s.stepNo !== null && s.maxSteps !== null && (
          <>
            {' '}
            {t('signal.step', { step: t('signal.stepOf', { n: s.stepNo, max: s.maxSteps }) })}
          </>
        )}
      </p>
      <p className={styles.close}>
        {t('signal.close', { date: shortDate(s.signalDate), price: exact(s.closePrice, s.assetCurrency) })}
      </p>
    </div>
  )

  if (s.answer) {
    return (
      <Screen top={top} note={t('common.disclaimer')} action={<PrimaryAction to={strategyPath(s.strategyId)}>{t('signal.back')}</PrimaryAction>}>
        {header}
        {box}
        <Callout tone="info" title={t('signal.answered')}>
          <AnswerLine s={s} />
        </Callout>
      </Screen>
    )
  }
  return <AnswerForm s={s} top={top} header={header} box={box} />
}

/** "You did it: 0.52 shares at $190.00 on Oct 23 (€98.80, fee €1.20)." or "You skipped it: no cash." */
function AnswerLine({ s }: { s: SignalDetail }) {
  const t = useT()
  const a = s.answer!
  if (a.status === 'skipped') {
    const reasonKey = a.skipReason ? SKIP_REASON_KEYS[a.skipReason] : null
    const reason = reasonKey ? t(reasonKey) : null
    return <p className={styles.plain}>{reason ? t('signal.skippedWhy', { reason: reason.toLowerCase() }) : t('signal.skipped')}</p>
  }
  // The trade may have been deleted since (manual trades can be): the answer stands without its numbers.
  if (a.shares === null) return <p className={styles.plain}>{t('signal.did')}</p>
  return (
    <p className={styles.plain}>
      {t('signal.didFull', {
        shares: formatShares(a.shares),
        price: exact(a.price ?? 0, s.assetCurrency),
        date: a.date ? shortDate(a.date) : '–',
        amount: exact(a.amount ?? 0, s.strategyCurrency),
        fee: exact(a.fee ?? 0, s.strategyCurrency),
      })}
    </p>
  )
}

function AnswerForm({ s, top, header, box }: { s: SignalDetail; top: ReactNode; header: ReactNode; box: ReactNode }) {
  const t = useT()
  const navigate = useNavigate()
  const [answer, setAnswer] = useState<'did' | 'skip'>('did')
  const [form, setForm] = useState<TradeForm>(() => initialForm(s))
  const [reason, setReason] = useState<SkipReason | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [saving, setSaving] = useState(false)
  const [snoozedUntil, setSnoozedUntil] = useState(s.snoozedUntil)
  const set = (patch: Partial<TradeForm>) => setForm((f) => ({ ...f, ...patch }))
  const sell = s.side === 'sell'

  const otherCurrency = s.assetCurrency !== s.strategyCurrency
  const reference = rateOn(s.rates, form.date)
  const rateUnit = t('signal.rate.unit', { asset: symbolOf(s.assetCurrency), strategy: symbolOf(s.strategyCurrency) })

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
    const unclear = answer === 'did' ? ambiguousFields(form, t('signal.error.ambiguous')) : {}
    if (Object.keys(unclear).length > 0) {
      setErrors(unclear)
      setSaving(false)
      return
    }
    try {
      if (answer === 'did') await api.confirmSignal(s.id, tradeRequest(form))
      else await api.skipSignal(s.id, reason)
      done()
    } catch (error) {
      if (error instanceof InvalidFieldsError) setErrors(error.fields)
      else setMessage({ tone: 'error', text: error instanceof InvalidValueError ? error.message : t('signal.error.save') })
      setSaving(false)
    }
  }

  async function snooze() {
    setMessage(null)
    try {
      setSnoozedUntil(await api.snoozeSignal(s.id))
      invalidate(signalCacheKey(s.id))
    } catch (error) {
      setMessage({ tone: 'error', text: error instanceof InvalidValueError ? error.message : t('signal.error.snooze') })
    }
  }

  const following = nextStep(s, t)
  const priceHint =
    s.prefill.price !== null
      ? t('signal.hint.prefilled', { date: shortDate(s.prefill.date) })
      : t('signal.hint.noOpen', { date: shortDate(s.signalDate), price: exact(s.closePrice, s.assetCurrency) })

  const reasons = skipReasons(t)

  return (
    <Screen
      top={top}
      note={t('common.disclaimer')}
      action={
        <PrimaryAction form={FORM_ID} disabled={saving}>
          {answer === 'did' ? t('signal.save.did') : t('signal.save.skip')}
        </PrimaryAction>
      }
    >
      {header}
      {box}
      {s.status === 'missed' && s.endedOn && (
        <Callout tone="warning" title={t('signal.missed.title')}>
          <p className={styles.plain}>
            {t('signal.missed.text', { date: shortDate(s.endedOn) })}
          </p>
        </Callout>
      )}
      <form id={FORM_ID} className={styles.form} onSubmit={save} noValidate>
        <fieldset className={styles.choices}>
          <legend className={styles.legend}>{t('signal.question')}</legend>
          {(
            [
              ['did', t('signal.choice.did')],
              ['skip', t('signal.choice.skip')],
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
            <TextField
              label={t('signal.field.date')}
              type="date"
              value={form.date}
              min={s.signalDate}
              max={s.today}
              onChange={(date) => set({ date })}
              error={errors.date}
            />
            <TextField
              label={t('signal.field.price', { currency: s.assetCurrency })}
              type="decimal"
              value={form.price}
              onChange={(price) => set({ price })}
              hint={priceHint}
              error={errors.price}
            />
            {form.enteredAs === 'amount' ? (
              <TextField
                label={t('signal.field.amount', { currency: s.strategyCurrency })}
                type="decimal"
                value={form.amount}
                onChange={(amount) => set({ amount })}
                hint={
                  !sell && s.stepAmount !== null
                    ? t(s.stepNo !== null ? 'signal.hint.amount' : 'signal.hint.amountBudget', {
                        step: money(s.stepAmount, s.strategyCurrency),
                      })
                    : undefined
                }
                error={errors.amount}
                aside={<TextAction onClick={() => set({ enteredAs: 'shares' })}>{t('signal.field.sharesInstead')}</TextAction>}
              />
            ) : (
              <TextField
                label={t('signal.field.shares')}
                type="decimal"
                value={form.shares}
                onChange={(shares) => set({ shares })}
                error={errors.shares}
                aside={<TextAction onClick={() => set({ enteredAs: 'amount' })}>{t('signal.field.amountInstead')}</TextAction>}
              />
            )}
            <TextField
              label={t('signal.field.fee', { currency: s.strategyCurrency })}
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
                      t('signal.rate.at', {
                        rate: `${symbolOf(s.assetCurrency)}${reference.rate.toFixed(4)}`,
                        unit: `${symbolOf(s.strategyCurrency)}1`,
                        date: shortDate(reference.date),
                      })
                    ) : (
                      t('signal.rate.none')
                    )}
                  </p>
                  <TextAction onClick={() => set({ ownRate: reference ? String(reference.rate) : '' })}>{t('signal.rate.change')}</TextAction>
                  {errors.fxRate && <FormMessage tone="error">{errors.fxRate}</FormMessage>}
                </div>
              ) : (
                <TextField
                  label={t('signal.rate.own', { unit: rateUnit })}
                  type="decimal"
                  value={form.ownRate}
                  onChange={(ownRate) => set({ ownRate })}
                  error={errors.fxRate}
                  aside={reference && <TextAction onClick={() => set({ ownRate: null })}>{t('signal.rate.reference')}</TextAction>}
                />
              ))}
          </div>
        ) : (
          <div className={styles.fields}>
            <fieldset className={styles.reasons}>
              <legend className={styles.legend}>{t('signal.skip.why')}</legend>
              {reasons.map((r) => (
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
              {t(sell ? (following ? 'signal.skip.noteSaleNext' : 'signal.skip.noteSale') : following ? 'signal.skip.noteNext' : 'signal.skip.note', {
                stock: s.stockName,
                next: following ?? '',
              })}
            </p>
          </div>
        )}
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>

      {s.status === 'waiting' && (
        <div className={styles.snooze}>
          {snoozedUntil && snoozedUntil > s.today ? (
            <p role="status" className={styles.plain}>
              {t('signal.snooze.until', { date: shortDate(snoozedUntil) })}
            </p>
          ) : (
            <TextAction onClick={snooze}>{t('signal.snooze.action')}</TextAction>
          )}
        </div>
      )}
    </Screen>
  )
}
