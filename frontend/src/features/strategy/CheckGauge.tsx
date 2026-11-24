import type { RuleCheck } from '../../api'
import { useT } from '../../i18n/I18n'
import { exact, formatRate, signedPct } from '../../lib/format'
import { isPastMax, normalizeCheck } from './checks'
import styles from './CheckGauge.module.css'

/** The gauge's own class, and the parent's placement when given. */
const withClass = (own: string, extra?: string) => (extra ? `${own} ${extra}` : own)

/** Two levels and how they stand: "Short $x > Long $y", "20d ago $x ↗ Now $y". */
function TwoLevelStrip({ left, right, ok, symbol, className }: { left: string; right: string; ok: boolean; symbol: string; className?: string }) {
  return (
    <div className={withClass(styles.stackStrip, className)} aria-hidden="true">
      <span className={styles.stackChip}>{left}</span>
      <span className={styles.stackRel} data-ok={ok}>{symbol}</span>
      <span className={styles.stackChip}>{right}</span>
    </div>
  )
}

const WINDOW_KEYS = new Set(['zone', 'buyLine', 'chase'])

// Proven growth's bar says the rise itself, as its sentence does: its share of the target ("199%" for a 99.3% rise that
// needs 50%) read as another figure (feedback, 2026-11-16). Not the drops: the comeback's is negative, and a bear's bar
// stays full inside a drop while its figure shrinks.
const VALUE_KEYS = new Set(['gain'])

/** Visual gauge for a rule check (`className` places it in its parent): threshold bar (with >100% overflow), 3-zone window band, or moving-average comparison. */
export function CheckGauge({ check: raw, currency, className }: { check: RuleCheck; currency: string; className?: string }) {
  const t = useT()
  const check = normalizeCheck(raw)
  // A used next-step rule can't earn a step any more: today's figures no longer matter.
  if (check.met === null || check.used) return null
  const past = isPastMax(check)
  const state = past ? 'past' : check.met ? 'met' : 'notMet'

  // 1. Moving-average stack: Short vs Long, or Short vs Mid vs Long in the POC's stack (and checks stored before STL dropped the middle one).
  if (check.key === 'stacked' && check.level !== null && check.midLevel == null && check.endLevel != null) {
    return (
      <TwoLevelStrip className={className} ok={check.met === true} symbol={check.met ? '>' : '≤'}
        left={t('stock.bar.short', { price: exact(check.level, currency) })} right={t('stock.bar.long', { price: exact(check.endLevel, currency) })} />
    )
  }
  if (check.key === 'stacked' && check.level !== null && check.midLevel != null && check.endLevel != null) {
    const firstOk = check.level > check.midLevel
    const secondOk = check.midLevel > check.endLevel
    return (
      <div className={withClass(styles.stackStrip, className)} aria-hidden="true">
        <span className={styles.stackChip}>{t('stock.bar.short', { price: exact(check.level, currency) })}</span>
        <span className={styles.stackRel} data-ok={firstOk}>{firstOk ? '>' : '≤'}</span>
        <span className={styles.stackChip}>{t('stock.bar.mid', { price: exact(check.midLevel, currency) })}</span>
        <span className={styles.stackRel} data-ok={secondOk}>{secondOk ? '>' : '≤'}</span>
        <span className={styles.stackChip}>{t('stock.bar.long', { price: exact(check.endLevel, currency) })}</span>
      </div>
    )
  }

  // 2. Rising long average: 20 trading days ago -> today.
  if (check.key === 'rising' && check.level !== null && check.endLevel != null) {
    return (
      <TwoLevelStrip className={className} ok={check.met === true} symbol={check.met ? '↗' : '↘'}
        left={t('stock.bar.ago20', { price: exact(check.endLevel, currency) })} right={t('stock.bar.now', { price: exact(check.level, currency) })} />
    )
  }

  // 3. Index trend sell line 3-level band (Sell line | SMA 200 | Buy line).
  if (check.key === 'sellLine' && check.max === null && check.level !== null && check.midLevel != null && check.endLevel != null) {
    return (
      <div className={withClass(styles.stackStrip, className)} aria-hidden="true">
        <span className={styles.stackChip}>{t('stock.bar.sell', { price: exact(check.level, currency) })}</span>
        <span className={styles.stackRel} data-ok={true}>·</span>
        <span className={styles.stackChip}>{t('stock.bar.sma', { price: exact(check.midLevel, currency) })}</span>
        <span className={styles.stackRel} data-ok={true}>·</span>
        <span className={styles.stackChip}>{t('stock.bar.buy', { price: exact(check.endLevel, currency) })}</span>
      </div>
    )
  }

  // 4. The candidate window (`beaten`) has no gauge: a bar of days read as "more is better".
  if (check.key === 'beaten') return null

  // 5. Band / window checks where going past `max` cancels the signal (`zone`, `buyLine`, the index's `chase`).
  if (WINDOW_KEYS.has(check.key) && check.value !== null && check.max !== null && check.max > 0) {
    const isZone = check.key === 'zone'
    const fromZero = isZone || ((check.key === 'buyLine' || check.key === 'chase') && (check.min ?? 0) > 0 && check.max > (check.min ?? 0))
    const min = fromZero ? (check.min ?? 0) : 0
    const max = check.max
    const span = Math.max(1, max - min)
    let fillPct: number
    if (check.value < min) {
      fillPct = fromZero
        ? Math.max(4, Math.round((Math.max(0, check.value) / Math.max(1, min)) * 30))
        : Math.max(4, Math.round(30 + (Math.max(-15, check.value) / 15) * 26))
    } else if (check.value <= max) {
      fillPct = Math.round(30 + ((check.value - min) / span) * 45)
    } else {
      fillPct = Math.min(100, Math.round(75 + ((check.value - max) / max) * 25))
    }
    const label = isZone
      ? t('stock.bar.zone', { min: formatRate(min), max: formatRate(max) })
      : fromZero
        ? // The window's two prices: its % above the low (+10 to +18.8%) read as a sum gone wrong (Ernest, 2026-11-16).
          t('stock.bar.window', { from: check.level === null ? '–' : exact(check.level, currency), to: check.endLevel == null ? '–' : exact(check.endLevel, currency) })
        : t('stock.bar.chase', { max: formatRate(max) })
    return (
      <div className={withClass(styles.progress, className)}>
        <div className={styles.bar} data-kind="band" aria-hidden="true">
          <span className={styles.bandZone} />
          <div className={styles.fill} data-state={state} style={{ width: `${fillPct}%` }} />
          <span className={styles.targetTick} style={{ left: '30%' }} />
          <span className={styles.targetTick} style={{ left: '75%' }} />
        </div>
        <span className={styles.mono} data-state={state}>{label}</span>
      </div>
    )
  }

  // 6. Threshold progress bar (supports > 100% overflow past the 75% target marker).
  if (check.progress !== null) {
    const pct = Math.round(check.progress * 100)
    const hasOverflow = pct > 100
    const fillPct = hasOverflow
      ? Math.min(100, Math.round(75 + Math.min(25, ((pct - 100) / 100) * 25)))
      : Math.round((pct / 100) * 75)
    return (
      <div className={withClass(styles.progress, className)}>
        <div className={styles.bar} data-kind="threshold" aria-hidden="true">
          <div className={styles.fill} data-state={state} style={{ width: `${fillPct}%` }} />
          <span className={styles.targetTick} style={{ left: '75%' }} />
        </div>
        <span className={styles.mono} data-state={state}>{VALUE_KEYS.has(check.key) && check.value !== null ? formatRate(check.value) : pct}%</span>
      </div>
    )
  }

  // 7. Price vs single average line (e.g. `floor` in Winners at a discount, `vsSma200` in The comeback): 0% line at 50%.
  if (check.level !== null && check.value !== null) {
    const fillPct = Math.max(4, Math.min(100, Math.round(50 + (Math.max(-25, Math.min(25, check.value)) / 25) * 50)))
    return (
      <div className={withClass(styles.progress, className)}>
        <div className={styles.bar} data-kind="threshold" aria-hidden="true">
          <div className={styles.fill} data-state={state} style={{ width: `${fillPct}%` }} />
          <span className={styles.targetTick} style={{ left: '50%' }} />
        </div>
        <span className={styles.mono} data-state={state}>{signedPct(check.value)}</span>
      </div>
    )
  }

  return null
}
