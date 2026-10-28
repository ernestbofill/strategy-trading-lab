import { useEffect, useState } from 'react'
import { api, TooManyTriesError, InvalidValueError, type AssetMatch, type Security } from '../../../api'
import { TextField } from '../../../components/Field'
import { useT } from '../../../i18n/I18n'
import styles from './FundPicker.module.css'

/** Searches start this long after the last key, so typing a name asks once. */
const DEBOUNCE_MS = 400

/** What to say when a search or an add fails: the server's message when it has one for the user. */
function failure(e: unknown, t: ReturnType<typeof useT>): string {
  if (e instanceof TooManyTriesError) return t('newStrategy.fund.tooMany')
  if (e instanceof InvalidValueError) return e.message
  return t('newStrategy.fund.failed')
}

interface FundPickerProps {
  /** Called with the picked fund once it is stored; `pricesIn` is false while its history loads. */
  onPick: (fund: Security, pricesIn: boolean) => void
}

/** "Change" on the fund block: search funds by name or symbol, and add one that isn't stored yet. */
export function FundPicker({ onPick }: FundPickerProps) {
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
        const found = await api.searchAssets(trimmed, 'fund')
        if (current) setResults(found)
      } catch (e) {
        if (current) {
          setResults(null)
          setError(failure(e, t))
        }
      } finally {
        if (current) setBusy(false)
      }
    }, DEBOUNCE_MS)
    return () => {
      current = false
      clearTimeout(timer)
    }
  }, [trimmed, t])

  const pick = async (match: AssetMatch) => {
    if (match.id) {
      onPick({ id: match.id, symbol: match.symbol, name: match.name, what: match.what }, true)
      return
    }
    setBusy(true)
    setError(null)
    try {
      const added = await api.addAsset(match.symbol, 'fund')
      onPick(added.asset, added.pricesIn)
    } catch (e) {
      setError(failure(e, t))
      setBusy(false)
    }
  }

  const shown = trimmed.length < 2 ? null : results
  return (
    <div className={styles.picker}>
      <TextField
        label={t('newStrategy.fund.search')}
        placeholder={t('newStrategy.fund.placeholder')}
        value={query}
        onChange={setQuery}
        error={error}
        hint={busy ? t('newStrategy.fund.searching') : undefined}
      />
      {shown && shown.length === 0 && !busy && <p className="caption">{t('newStrategy.fund.none')}</p>}
      {shown && shown.length > 0 && (
        <ul className={styles.results} aria-label={t('newStrategy.fund.results')}>
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
      <p className="caption">{t('newStrategy.fund.exchanges')}</p>
    </div>
  )
}
