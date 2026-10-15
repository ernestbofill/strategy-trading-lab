import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type StrategyTemplate } from '../../api'
import { Screen, TopBar } from '../../components/Screen'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { reviewPath } from './paths'
import styles from './PickTemplate.module.css'

/** 1 · Pick a strategy template (wireframe `Main`). */
export function PickTemplate() {
  const { templateKey, setTemplateKey } = useNewStrategyDraft()
  const [templates, setTemplates] = useState<StrategyTemplate[] | null>(null)
  const [error, setError] = useState(false)

  useEffect(() => {
    api.getTemplates().then(setTemplates, () => setError(true))
  }, [])

  return (
    <Screen
      top={<TopBar title="STL // NEW STRATEGY" step="1 / 5" />}
      footer={
        <>
          <Link to={reviewPath(templateKey)} className="btn-primary">
            Continue
          </Link>
          <p className={styles.hint}>Pick one to start. You can add more strategies later, from any template.</p>
        </>
      }
    >
      <div className={styles.intro}>
        <div className={styles.eyebrow}>Welcome to Strategy Trading Lab</div>
        <h1 className={styles.title}>Pick a strategy template</h1>
        <p className={styles.lead}>
          Each template is a few simple rules with tested defaults. See how it did in the past, tune it, then run it
          as a simulation or live.
        </p>
      </div>

      {error && <p className={styles.status}>Could not load the templates. Try again later.</p>}
      {!templates && !error && <p className={styles.status}>Loading templates…</p>}

      {templates && (
        <div role="radiogroup" aria-label="Strategy templates" className={styles.list}>
          {templates.map((t) => {
            const selected = t.key === templateKey
            return (
              <div key={t.key} className={styles.card} data-selected={selected || undefined}>
                <label className={styles.cardMain}>
                  <input
                    type="radio"
                    name="template"
                    value={t.key}
                    checked={selected}
                    onChange={() => setTemplateKey(t.key)}
                    className="visually-hidden"
                  />
                  <span className={styles.cardHead}>
                    <span className={styles.cardText}>
                      <span className={styles.name}>{t.name}</span>
                      <span className={styles.tagline}>"{t.tagline}"</span>
                    </span>
                    <span className={styles.check} aria-hidden="true">
                      {selected && (
                        <svg width="12" height="12" viewBox="0 0 12 12">
                          <path d="M2 6.5l2.6 2.5L10 3.5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="square" />
                        </svg>
                      )}
                    </span>
                  </span>
                  <span className={styles.idea}>{t.idea}</span>
                </label>
                <Link
                  to={reviewPath(t.key)}
                  onClick={() => setTemplateKey(t.key)}
                  className={styles.learnMore}
                  aria-label={`Learn more about ${t.name}`}
                >
                  Learn more
                </Link>
              </div>
            )
          })}
        </div>
      )}
    </Screen>
  )
}
