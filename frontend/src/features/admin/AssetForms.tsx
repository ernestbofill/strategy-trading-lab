import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api, InvalidFieldsError, InvalidValueError, UnauthorizedError, type AdminAddedAsset, type AdminAsset } from '../../api'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { Section } from '../../components/Section'
import styles from './AdminAssets.module.css'

/** What adding did, in one line. */
function addedText({ asset, created, problem }: AdminAddedAsset): FormMessageState {
  const label = `${asset.name} (${asset.symbol})`
  if (problem) return { tone: 'error', text: `${created ? `Added ${label}` : `${label} is already here`}, but its prices didn't load: ${problem} Add it again to retry.` }
  if (!created) return { tone: 'done', text: `${label} is already here.` }
  return { tone: 'done', text: `Added ${label}, prices to ${asset.latestPriceDate ?? '–'}.` }
}

function failure(e: unknown): Record<string, string> | null {
  if (e instanceof UnauthorizedError) return null
  if (e instanceof InvalidFieldsError) return e.fields
  if (e instanceof InvalidValueError) return { form: e.message }
  return { form: "Couldn't reach the server. Try again." }
}

/** "Add an asset": a Yahoo symbol, added with its name, kind, currency and price history. */
export function AddAssetForm({ onAdded }: { onAdded: () => Promise<void> }) {
  const [symbol, setSymbol] = useState('')
  const [busy, setBusy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setErrors({})
    setMessage(null)
    try {
      const added = await api.addAdminAsset(symbol)
      setMessage(addedText(added))
      setSymbol('')
      await onAdded()
    } catch (error) {
      setErrors(failure(error) ?? {})
    } finally {
      setBusy(false)
    }
  }

  return (
    <Section title="Add an asset">
      <form className={styles.form} onSubmit={(e) => void submit(e)}>
        <TextField
          label="Yahoo symbol"
          value={symbol}
          onChange={setSymbol}
          placeholder="AAPL"
          hint="As Yahoo writes it: AAPL, SAP.DE, ^GDAXI."
          error={errors.symbol}
        />
        <button type="submit" className={`ui-strong ${styles.actionButton}`} disabled={busy}>
          {busy ? 'Adding…' : 'Add'}
        </button>
        {errors.form && <FormMessage tone="error">{errors.form}</FormMessage>}
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
    </Section>
  )
}

/** Edits an asset's name and its English "what it does". */
export function EditAssetForm({ asset, onDone }: { asset: AdminAsset; onDone: (saved: boolean) => Promise<void> }) {
  const [name, setName] = useState(asset.name)
  const [what, setWhat] = useState(asset.what)
  const [busy, setBusy] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const form = useRef<HTMLFormElement>(null)

  // Opened from a row further down: bring it into view (jsdom has no scrollIntoView).
  useEffect(() => form.current?.scrollIntoView?.({ block: 'center' }), [])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setErrors({})
    try {
      await api.updateAdminAsset(asset.id, { name, what })
      await onDone(true)
    } catch (error) {
      setErrors(failure(error) ?? {})
      setBusy(false)
    }
  }

  return (
    <Section title={`Edit ${asset.symbol}`}>
      <form ref={form} className={styles.form} onSubmit={(e) => void submit(e)}>
        <TextField label="Name" value={name} onChange={setName} error={errors.name} />
        <TextField label="What it does" value={what} onChange={setWhat} error={errors.what} />
        <div className={styles.buttons}>
          <button type="submit" className={`ui-strong ${styles.actionButton}`} disabled={busy}>
            Save
          </button>
          <button type="button" className={`ui-strong ${styles.actionButton}`} disabled={busy} onClick={() => void onDone(false)}>
            Cancel
          </button>
        </div>
        {errors.form && <FormMessage tone="error">{errors.form}</FormMessage>}
      </form>
    </Section>
  )
}
