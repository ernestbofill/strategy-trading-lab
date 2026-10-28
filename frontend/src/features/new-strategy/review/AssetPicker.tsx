import { useEffect, useState } from 'react'
import { api, ForbiddenError, TooManyTriesError, InvalidValueError, type AssetMatch, type Security } from '../../../api'
import { TextField } from '../../../components/Field'
import { useT } from '../../../i18n/I18n'
import styles from './AssetPicker.module.css'

/** What the picker looks for: stocks for a stock template's list, funds for a fund template. */
export type PickerKind = 'stock' | 'fund'

/** Searches start this long after the last key, so typing a name asks once. */
const DEBOUNCE_MS = 400

/** What to say when a search or an add fails: the server's message when it has one for the user. */
function failure(e: unknown, kind: PickerKind, t: ReturnType<typeof useT>): string {
  if (e instanceof ForbiddenError) return t(`newStrategy.picker.${kind}.confirmEmail`)
  if (e instanceof TooManyTriesError) return t('newStrategy.picker.tooMany')
  // The server's reason: not a stock or fund it can follow, the user's limit of new ones, the price source down.
  if (e instanceof InvalidValueError) return e.message
  return t('newStrategy.picker.failed')
}

interface AssetPickerProps {
  kind: PickerKind
  /** Called with the picked stock or fund once it is stored; `pricesIn` is false while its history loads. */
  onPick: (asset: Security, pricesIn: boolean) => void
}

/**
 * "+ Add a stock" on the stock list and "Change" on the fund block: search by name or symbol, pick a stored one at once,
 * and add one that isn't stored yet (a confirmed email, and the user's limit of new ones).
 */
export function AssetPicker({ kind, onPick }: AssetPickerProps) {
  const t = useT()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<AssetMatch[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const trimmed = query.trim()
  useEffect(() => {
    if (trimmed.length < 2) return
    let current = true
    const timer = setTimeout(async () => {
      setBusy(true)
      setError(null)
      try {
        const found = await api.searchAssets(trimmed, kind)
        if (current) setResults(found)
      } catch (e) {
        if (current) {
          setResults(null)
          setError(failure(e, kind, t))
        }
      } finally {
        if (current) setBusy(false)
      }
    }, DEBOUNCE_MS)
    return () => {
      current = false
      clearTimeout(timer)
      // A search dropped mid-way doesn't leave "Searching…" behind.
      setBusy(false)
    }
  }, [trimmed, kind, t])

  const pick = async (match: AssetMatch) => {
    if (match.id) {
      onPick({ id: match.id, symbol: match.symbol, name: match.name, what: match.what }, true)
      return
    }
    setBusy(true)
    setError(null)
    try {
      const added = await api.addAsset(match.symbol, kind)
      onPick(added.asset, added.pricesIn)
    } catch (e) {
      setError(failure(e, kind, t))
      setBusy(false)
    }
  }

  const shown = trimmed.length < 2 ? null : results
  return (
    <div className={styles.picker}>
      <TextField
        label={t(`newStrategy.picker.${kind}.search`)}
        placeholder={t('newStrategy.picker.placeholder')}
        value={query}
        onChange={setQuery}
        error={error}
        hint={busy ? t('newStrategy.picker.searching') : undefined}
      />
      {shown && shown.length === 0 && !busy && <p className="caption">{t(`newStrategy.picker.${kind}.none`)}</p>}
      {shown && shown.length > 0 && (
        <ul className={styles.results} aria-label={t(`newStrategy.picker.${kind}.results`)}>
          {shown.map((match) => (
            <li key={match.symbol}>
              <button type="button" className={styles.result} disabled={busy} onClick={() => pick(match)}>
                <span className={styles.name}>{match.name}</span>
                <span className={styles.detail}>
                  <span className={styles.mono}>{match.symbol}</span> · {match.exchange}
                  {match.what !== match.name && ` · ${match.what}`}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <p className="caption">{t(`newStrategy.picker.${kind}.exchanges`)}</p>
    </div>
  )
}
