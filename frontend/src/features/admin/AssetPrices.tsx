import { useState } from 'react'
import { api, InvalidFieldsError, InvalidValueError, UnauthorizedError, type AdminAsset } from '../../api'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { Section } from '../../components/Section'
import styles from './AdminAssets.module.css'

const DEFAULT_YEARS = '2'
/** The API's limit (AdminEndpoints.MaxDownloadYears). */
const MAX_YEARS = 30

function yearsProblem(years: number): string | null {
  return Number.isInteger(years) && years >= 1 && years <= MAX_YEARS ? null : `Choose 1 to ${MAX_YEARS} years.`
}

/** Why a call failed, in one line; null when the session ended (the app sends the user to log in). */
function reason(e: unknown): string | null {
  if (e instanceof UnauthorizedError) return null
  if (e instanceof InvalidFieldsError) return Object.values(e.fields)[0] ?? 'Check the years.'
  if (e instanceof InvalidValueError) return e.message
  return "Couldn't reach the server."
}

/** The failures, the first few in full: an asset's reason also shows in its row. */
function failedText(failed: string[]): string {
  const shown = 5
  const more = failed.length > shown ? ` …and ${failed.length - shown} more, shown in their rows.` : ''
  return `Failed: ${failed.slice(0, shown).join(' ')}${more}`
}

function YearsField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return <TextField label="Years to download" type="decimal" value={value} onChange={onChange} hint="Daily prices, back from today." />
}

/** "Sure?" first: a second click does it. */
function DeleteButton({ label, busy, onDelete }: { label: string; busy: boolean; onDelete: () => Promise<void> }) {
  const [confirming, setConfirming] = useState(false)
  return confirming ? (
    <button
      type="button"
      className={`ui-strong ${styles.actionButton} ${styles.danger}`}
      disabled={busy}
      onClick={() => {
        setConfirming(false)
        void onDelete()
      }}
    >
      Sure? {label}
    </button>
  ) : (
    <button type="button" className={`ui-strong ${styles.actionButton}`} disabled={busy} onClick={() => setConfirming(true)}>
      {label}
    </button>
  )
}

/**
 * "Prices" for every asset: download the last years of daily prices (one asset at a time, then the exchange rates), or
 * delete them all. The daily refresh fetches two years for a watched asset with none stored.
 */
export function AllPricesSection({ assets, onChanged }: { assets: AdminAsset[]; onChanged: () => Promise<void> }) {
  const [years, setYears] = useState(DEFAULT_YEARS)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<FormMessageState>(null)

  const downloadAll = async () => {
    const count = Number(years)
    const problem = yearsProblem(count)
    if (problem) {
      setMessage({ tone: 'error', text: problem })
      return
    }
    setBusy(true)
    const failed: string[] = []
    try {
      // Refused while it runs, which would fail every asset alike: say it once.
      if ((await api.getAdminPrices()).running) {
        setMessage({ tone: 'error', text: "The daily price refresh is running: try again when it's done." })
        return
      }
      for (const [i, asset] of assets.entries()) {
        setMessage({ tone: 'done', text: `Downloading ${i + 1} of ${assets.length}: ${asset.symbol}…` })
        try {
          await api.downloadAdminAssetPrices(asset.id, count)
        } catch (e) {
          const why = reason(e)
          if (why === null) return
          failed.push(`${asset.symbol}: ${why}`)
        }
      }
      setMessage({ tone: 'done', text: 'Downloading the exchange rates…' })
      const rates = await api.downloadAdminRates(count)
      const done = `Downloaded ${assets.length - failed.length} of ${assets.length} assets and the exchange rates.`
      // First: a currency has no row of its own to show it.
      failed.unshift(...rates.problems.map((p) => `${p.name}: ${p.problem ?? 'failed'}`))
      setMessage(failed.length === 0 ? { tone: 'done', text: done } : { tone: 'error', text: `${done} ${failedText(failed)}` })
    } catch (e) {
      const why = reason(e)
      if (why !== null) setMessage({ tone: 'error', text: why })
    } finally {
      setBusy(false)
      await onChanged()
    }
  }

  const deleteAll = async () => {
    setBusy(true)
    setMessage(null)
    try {
      const { prices, rates } = await api.flushAdminPrices()
      setMessage({ tone: 'done', text: `Deleted ${prices} prices and ${rates} exchange rates.` })
      await onChanged()
    } catch (e) {
      const why = reason(e)
      if (why !== null) setMessage({ tone: 'error', text: why })
    } finally {
      setBusy(false)
    }
  }

  return (
    <Section title="Prices">
      <div className={styles.form}>
        <YearsField value={years} onChange={setYears} />
        <div className={styles.buttons}>
          <button type="button" className={`ui-strong ${styles.actionButton}`} disabled={busy || assets.length === 0} onClick={() => void downloadAll()}>
            Download all
          </button>
          <DeleteButton label="Delete all" busy={busy} onDelete={deleteAll} />
        </div>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </div>
    </Section>
  )
}

/** One asset's prices, in its Edit panel: download the last years again, or delete them. */
export function AssetPricesActions({ asset, onChanged }: { asset: AdminAsset; onChanged: () => Promise<void> }) {
  const [years, setYears] = useState(DEFAULT_YEARS)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<FormMessageState>(null)

  const run = async (action: () => Promise<string>) => {
    setBusy(true)
    setMessage(null)
    try {
      setMessage({ tone: 'done', text: await action() })
      await onChanged()
    } catch (e) {
      const why = reason(e)
      if (why !== null) setMessage({ tone: 'error', text: why })
    } finally {
      setBusy(false)
    }
  }

  const download = () =>
    run(async () => {
      const problem = yearsProblem(Number(years))
      if (problem) throw new InvalidValueError(problem)
      const row = await api.downloadAdminAssetPrices(asset.id, Number(years))
      return `Downloaded ${asset.symbol}, prices to ${row.latestPriceDate ?? '–'}.`
    })

  const remove = () =>
    run(async () => {
      await api.flushAdminAssetPrices(asset.id)
      return `Deleted the prices of ${asset.symbol}.`
    })

  return (
    <div className={styles.form}>
      <YearsField value={years} onChange={setYears} />
      <div className={styles.buttons}>
        <button type="button" className={`ui-strong ${styles.actionButton}`} disabled={busy} onClick={() => void download()}>
          {busy ? 'Working…' : 'Download prices'}
        </button>
        <DeleteButton label="Delete prices" busy={busy} onDelete={remove} />
      </div>
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </div>
  )
}
