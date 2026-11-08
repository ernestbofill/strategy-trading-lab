import { useEffect, useId, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api, InvalidFieldsError, type AdminTemplate, type AdminTemplateAsset, type AdminTemplateVersion } from '../../api'
import { SelectField, TextField } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Tabs } from '../../components/Tabs'
import { tabPanel } from '../../components/tabPanel'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { formatRate, isoDate } from '../../lib/format'
import { invalidate } from '../../lib/useLoadable'
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

/** "v3 · 2026-11-08 · active · 4 strategies": what the version picker lists. */
function versionLabel(v: AdminTemplateVersion): string {
  const parts = [`v${v.version}`, v.version === 1 ? 'tested' : isoDate(new Date(v.createdAt))]
  if (v.active) parts.push('active')
  if (v.strategies > 0) parts.push(`${v.strategies} ${v.strategies === 1 ? 'strategy' : 'strategies'}`)
  return parts.join(' · ')
}

interface VersionFormProps {
  template: AdminTemplate
  version: AdminTemplateVersion
  /** Called with the template as the server now has it, and the version to show. */
  onChanged: (next: AdminTemplate, show: number) => void
}

/** One version's values, editable: Save makes them a new version, and an older one can be made active again. */
function VersionForm({ template, version, onChanged }: VersionFormProps) {
  const fund = template.investsIn !== 'stockList'
  const tested = template.versions.find((v) => v.version === 1)
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(template.settings.map((s) => [s.key, String(version.defaults[s.key] ?? '')])),
  )
  const [close, setClose] = useState(String(version.closePct))
  const [assets, setAssets] = useState<AdminTemplateAsset[]>(version.assets)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)

  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setFailed(false)
    setErrors({})
    try {
      await action()
    } catch (e) {
      if (e instanceof InvalidFieldsError) setErrors(e.fields)
      else setFailed(true)
    } finally {
      // Also after a success that keeps this form (making a version active, a save that changed nothing).
      setBusy(false)
    }
  }

  const save = (e: FormEvent) => {
    e.preventDefault()
    const defaults = Object.fromEntries(template.settings.map((s) => [s.key, numberOf(values[s.key] ?? '')]))
    const closePct = template.hasClosePct ? numberOf(close) : null
    // A blank or a word would reach the API as null: stop it here instead.
    const notNumbers = Object.fromEntries([
      ...Object.entries(defaults).filter(([, n]) => Number.isNaN(n)).map(([key]) => [`settings.${key}`, 'Type a number.']),
      ...(closePct !== null && Number.isNaN(closePct) ? [['closePct', 'Type a number.']] : []),
    ])
    if (Object.keys(notNumbers).length > 0) {
      setErrors(notNumbers)
      return
    }
    void run(async () => {
      const next = await api.saveAdminTemplateVersion(template.key, { defaults, assetIds: assets.map((a) => a.id), closePct })
      onChanged(next, next.versions.find((v) => v.active)?.version ?? version.version)
    })
  }

  const activate = () =>
    void run(async () => {
      onChanged(await api.activateAdminTemplateVersion(template.key, version.version), version.version)
    })

  const add = (picked: { id: string; symbol: string; name: string }) => {
    const asset = { id: picked.id, symbol: picked.symbol, name: picked.name, active: true }
    setAssets((list) => (fund ? [asset] : list.some((a) => a.id === asset.id) ? list : [...list, asset]))
  }

  const v1 = (n: number | undefined) => (tested && n !== undefined ? `v1 ${formatRate(n)} · ` : '')

  return (
    <form className={styles.form} onSubmit={save}>
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
              hint={`${v1(tested?.defaults[s.key])}${formatRate(s.min)} to ${formatRate(s.max)}, steps of ${formatRate(s.step)}`}
              error={errors[`settings.${s.key}`]}
            />
          ))}
          {errors.settings && <p className={styles.error} role="alert">{errors.settings}</p>}
        </fieldset>
      )}

      {template.hasClosePct && (
        <TextField
          type="decimal"
          label="Getting close: within _% of the buy rule"
          value={close}
          onChange={setClose}
          hint={`${v1(tested?.closePct)}the active version's applies to every strategy of this template, running ones too: the dashboard card (never for Bear markets) and the summary`}
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
      <div className={styles.actions}>
        <button type="submit" className={`ui-strong ${styles.primary}`} disabled={busy}>
          Save as new version
        </button>
        {!version.active && (
          <button type="button" className={`ui-strong ${styles.button}`} disabled={busy} onClick={activate}>
            Make v{version.version} active
          </button>
        )}
      </div>
    </form>
  )
}

/** A template's card: the version picker, and the picked version's values. */
function TemplateCard({ template, onChanged }: { template: AdminTemplate; onChanged: (next: AdminTemplate) => void }) {
  const active = template.versions.find((v) => v.active) ?? template.versions[0]
  const [shown, setShown] = useState(active?.version ?? 1)
  const [note, setNote] = useState<string | null>(null)
  const version = template.versions.find((v) => v.version === shown) ?? active
  if (!version) return null

  const changed = (next: AdminTemplate, show: number) => {
    onChanged(next)
    setShown(show)
    setNote(`v${show} is active. New strategies start with it.`)
  }

  return (
    <section className={`well ${styles.card}`} aria-labelledby={`template-${template.key}`}>
      <div className={styles.head}>
        <h2 id={`template-${template.key}`} className={styles.title}>
          {template.name}
        </h2>
        <span className={`ui-strong ${styles.state}`} data-retuned={active?.version !== 1}>
          v{active?.version} active
        </span>
      </div>
      <SelectField
        label="Version"
        value={String(version.version)}
        onChange={(v) => {
          setShown(Number(v))
          setNote(null)
        }}
        options={template.versions.map((v) => ({ value: String(v.version), label: versionLabel(v) }))}
      />
      {note && (
        <p className={styles.saved} role="status">
          {note}
        </p>
      )}
      {/* Keyed by version, so picking another one loads its values. */}
      <VersionForm key={`${version.version}-${template.versions.length}`} template={template} version={version} onChanged={changed} />
    </section>
  )
}

/**
 * Admin templates (/admin/templates): versions of what new strategies of each template start with: the defaults of Advanced
 * settings, the starter list or fund, and "getting close". Saving makes a new version; any version can be made active again.
 * A strategy keeps the version it started from.
 */
export function AdminTemplates() {
  const [state, setState] = useState<State>({ status: 'loading' })
  const tabsId = useId()
  // The template shown is in the address (?template=dip), so a reload or a shared link opens it; the first one otherwise.
  const [params, setParams] = useSearchParams()
  const shown = (templates: AdminTemplate[]) =>
    templates.find((t) => t.key === params.get('template'))?.key ?? templates[0].key

  useEffect(() => {
    let current = true
    void fetchTemplates().then((next) => current && setState(next))
    return () => {
      current = false
    }
  }, [])

  const changed = (next: AdminTemplate) => {
    // This tab's new-strategy screens keep templates until a deploy: load them again with the new active version.
    invalidate('templates')
    invalidate(`template:${next.key}`)
    setState((s) => (s.status === 'ready' ? { ...s, templates: s.templates.map((t) => (t.key === next.key ? next : t)) } : s))
  }

  return (
    <Screen top={<TopBar back="/admin" step="ADMIN" />}>
      <PageHeader
        title="Templates"
        lead="What new strategies start with, by version. Saving makes a new version; any version can be made active again. Strategies keep the version they started from."
      />
      {state.status === 'loading' && <ScreenMessage>Loading the templates…</ScreenMessage>}
      {state.status === 'error' && <ScreenMessage>Could not load the templates. Try again later.</ScreenMessage>}
      {state.status === 'ready' && state.templates.length > 0 && (
        <div className={styles.list}>
          <Tabs
            id={tabsId}
            label="Template"
            items={state.templates.map((t) => ({ key: t.key, label: t.name }))}
            selected={shown(state.templates)}
            onSelect={(key) => setParams({ template: key }, { replace: true })}
          />
          {/* Every card stays mounted, the others hidden, so switching templates keeps unsaved edits. */}
          {state.templates.map((t) => (
            <div key={t.key} {...tabPanel(tabsId, t.key)} hidden={t.key !== shown(state.templates)}>
              <TemplateCard template={t} onChanged={changed} />
            </div>
          ))}
        </div>
      )}
    </Screen>
  )
}
