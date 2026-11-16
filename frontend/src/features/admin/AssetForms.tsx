import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api, type AdminAddedAsset, type AdminAsset } from '../../api'
import { FormMessage, TextField, type FormMessageState } from '../../components/Field'
import { Section } from '../../components/Section'
import { useSubmit } from '../../components/useSubmit'
import styles from './AdminAssets.module.css'
import { AssetPricesActions } from './AssetPrices'
import { Button } from '../../components/Button'

/** What adding did, in one line. */
function addedText({ asset, created, problem }: AdminAddedAsset): FormMessageState {
  const label = `${asset.name} (${asset.symbol})`
  if (problem) return { tone: 'error', text: `${created ? `Added ${label}` : `${label} is already here`}, but its prices didn't load: ${problem} Add it again to retry.` }
  if (!created) return { tone: 'done', text: asset.active ? `${label} is already here.` : `${label} is already here, hidden: Show it to use it.` }
  return { tone: 'done', text: `Added ${label}, prices to ${asset.latestPriceDate ?? '–'}.` }
}

const FAILED = "Couldn't reach the server. Try again."

/** "Add an asset": a Yahoo symbol, added with its name, kind, currency and price history. */
export function AddAssetForm({ onAdded }: { onAdded: () => Promise<void> }) {
  const [symbol, setSymbol] = useState('')
  const { busy, errors, message, setMessage, run, reset } = useSubmit(FAILED)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    let added: AdminAddedAsset | null = null
    if (
      !(await run(async () => {
        added = await api.addAdminAsset(symbol)
        await onAdded()
      }))
    )
      return
    reset()
    setSymbol('')
    if (added) setMessage(addedText(added))
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
        <Button type="submit" variant="outline" disabled={busy}>
          {busy ? 'Adding…' : 'Add'}
        </Button>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
    </Section>
  )
}

/** Edits an asset's name and its English "what it does", and downloads or deletes its prices. */
export function EditAssetForm({ asset, onDone, onPricesChanged }: {
  asset: AdminAsset
  onDone: (saved: boolean) => Promise<void>
  onPricesChanged: () => Promise<void>
}) {
  const [name, setName] = useState(asset.name)
  const [what, setWhat] = useState(asset.what)
  const { busy, errors, message, run } = useSubmit(FAILED)
  const form = useRef<HTMLFormElement>(null)

  // Opened from a row further down: bring it into view (jsdom has no scrollIntoView).
  // In braces: a browser where scrollIntoView returns a promise would have React call it as the effect's clean-up.
  useEffect(() => {
    form.current?.scrollIntoView?.({ block: 'center' })
  }, [])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (await run(() => api.updateAdminAsset(asset.id, { name, what }))) await onDone(true)
  }

  return (
    <Section title={`Edit ${asset.symbol}`}>
      <form ref={form} className={styles.form} onSubmit={(e) => void submit(e)}>
        <TextField label="Name" value={name} onChange={setName} error={errors.name} />
        <TextField label="What it does" value={what} onChange={setWhat} error={errors.what} />
        <div className={styles.buttons}>
          <Button type="submit" variant="outline" disabled={busy}>
            Save
          </Button>
          <Button variant="outline" disabled={busy} onClick={() => void onDone(false)}>
            Cancel
          </Button>
        </div>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
      <AssetPricesActions asset={asset} onChanged={onPricesChanged} />
    </Section>
  )
}
