import { useEffect, useState, type FormEvent } from 'react'
import { api, InvalidFieldsError, type AdminTemplate, type AdminTemplateAsset } from '../../api'
import { TextField } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { formatRate } from '../../lib/format'
import { AssetPicker } from '../new-strategy/review/AssetPicker'
import styles from './AdminTemplates.module.css'

type State = { status: 'loading' } | { status: 'error' } | { status: 'ready'; templates: AdminTemplate[] }

async function fetchTemplates(): Promise<State> {
  try {
    return { status: 'ready', templates: await api.getAdminTemplates() }
  } catch {
    return { status: 'error' }
  }
}

/** The setting's sentence with a blank for its value: "Rose at least _% from its low". */
const labelOf = (sentence: string, value: string) => sentence.replace('{value}', value.replace('{n}', '_'))

/** The typed text as a number, or NaN: the API says what is wrong with it. */
const numberOf = (text: string) => (text.trim() === '' ? Number.NaN : Number(text.replace(',', '.')))

function TemplateCard({ template, onSaved }: { template: AdminTemplate; onSaved: (next: AdminTemplate | null) => void }) {
  const fund = template.investsIn !== 'stockList'
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(template.settings.map((s) => [s.key, formatRate(s.current)])),
  )
  const [close, setClose] = useState(template.closePct === null ? '' : formatRate(template.closePct))
  const [assets, setAssets] = useState<AdminTemplateAsset[]>(template.assets)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)
  const [saved, setSaved] = useState(false)

  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setFailed(false)
    setSaved(false)
    setErrors({})
    try {
      await action()
    } catch (e) {
      if (e instanceof InvalidFieldsError) setErrors(e.fields)
      else setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  const save = (e: FormEvent) => {
    e.preventDefault()
    void run(async () => {
      const next = await api.setAdminTemplate(template.key, {
        defaults: Object.fromEntries(template.settings.map((s) => [s.key, numberOf(values[s.key] ?? '')])),
        assetIds: assets.map((a) => a.id),
        closePct: template.closePct === null ? null : numberOf(close),
      })
      onSaved(next)
      setSaved(true)
    })
  }

  const reset = () =>
    void run(async () => {
      await api.resetAdminTemplate(template.key)
      onSaved(null)
    })

  const add = (picked: { id: string; symbol: string; name: string }) => {
    const asset = { id: picked.id, symbol: picked.symbol, name: picked.name, active: true }
    setAssets((list) => (fund ? [asset] : list.some((a) => a.id === asset.id) ? list : [...list, asset]))
  }

  return (
    <form className={`well ${styles.card}`} onSubmit={save} aria-labelledby={`template-${template.key}`}>
      <div className={styles.head}>
        <h2 id={`template-${template.key}`} className={styles.title}>
          {template.name}
        </h2>
        <span className={`ui-strong ${styles.state}`} data-changed={template.changed}>
          {template.changed ? 'Changed' : 'As tested'}
        </span>
      </div>

      {template.settings.length > 0 && (
        <fieldset className={styles.group}>
          <legend className="ui-strong">Defaults</legend>
          {template.settings.map((s) => (
            <TextField
              key={s.key}
              type="decimal"
              label={labelOf(s.sentence, s.value)}
              value={values[s.key] ?? ''}
              onChange={(text) => setValues((v) => ({ ...v, [s.key]: text }))}
              hint={`Tested ${formatRate(s.tested)} · ${formatRate(s.min)} to ${formatRate(s.max)}, steps of ${formatRate(s.step)}`}
              error={errors[`settings.${s.key}`]}
            />
          ))}
          {errors.settings && <p className={styles.error} role="alert">{errors.settings}</p>}
        </fieldset>
      )}

      {template.closePct !== null && (
        <TextField
          type="decimal"
          label="Getting close: within _% of the buy rule"
          value={close}
          onChange={setClose}
          hint={`Tested ${formatRate(template.testedClosePct)} · on the dashboard card and in the summary`}
          error={errors.closePct}
        />
      )}

      <fieldset className={styles.group}>
        <legend className="ui-strong">{fund ? 'Fund' : `Starter list · ${assets.length}`}</legend>
        <ul className={styles.assets}>
          {assets.map((a) => (
            <li key={a.id} className={styles.asset}>
              <span>
                <span className={styles.symbol}>{a.symbol}</span> {a.name}
                {!a.active && <span className={styles.hidden}> · hidden</span>}
              </span>
              {!fund && (
                <button
                  type="button"
                  className={styles.remove}
                  aria-label={`Remove ${a.name}`}
                  onClick={() => setAssets((list) => list.filter((x) => x.id !== a.id))}
                >
                  ×
                </button>
              )}
            </li>
          ))}
        </ul>
        <AssetPicker kind={fund ? 'fund' : 'stock'} onPick={add} />
        {errors.assetIds && <p className={styles.error} role="alert">{errors.assetIds}</p>}
      </fieldset>

      {failed && <p className={styles.error} role="alert">Could not save. Try again.</p>}
      {saved && <p className={styles.saved} role="status">Saved. New strategies start with these.</p>}
      <div className={styles.actions}>
        <button type="submit" className={`ui-strong ${styles.primary}`} disabled={busy}>
          Save
        </button>
        {template.changed && (
          <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={reset}>
            Back to tested
          </button>
        )}
      </div>
    </form>
  )
}

/**
 * Admin templates (/admin/templates): what new strategies of each template start with — the defaults of Advanced settings,
 * the starter list or fund, and "getting close". Running strategies keep what they started with; past results stay the tested ones.
 */
export function AdminTemplates() {
  const [state, setState] = useState<State>({ status: 'loading' })
  // A reset reloads the template from the server, so its card starts over from the tested values.
  const [generation, setGeneration] = useState(0)

  const load = () => fetchTemplates().then(setState)

  useEffect(() => {
    let current = true
    void fetchTemplates().then((next) => current && setState(next))
    return () => {
      current = false
    }
  }, [])

  const saved = (key: string, next: AdminTemplate | null) => {
    if (next === null) {
      void load().then(() => setGeneration((g) => g + 1))
      return
    }
    setState((s) => (s.status === 'ready' ? { ...s, templates: s.templates.map((t) => (t.key === key ? next : t)) } : s))
  }

  return (
    <Screen top={<TopBar back="/admin" step="ADMIN" />}>
      <PageHeader
        title="Templates"
        lead="What new strategies start with. Running strategies keep their own values, and past results stay the tested ones."
      />
      {state.status === 'loading' && <ScreenMessage>Loading the templates…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the templates. Try again later.</ScreenMessage>}
      {state.status === 'ready' && (
        <div className={styles.list}>
          {state.templates.map((t) => (
            <TemplateCard key={`${t.key}-${generation}`} template={t} onSaved={(next) => saved(t.key, next)} />
          ))}
        </div>
      )}
    </Screen>
  )
}
