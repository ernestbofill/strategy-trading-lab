import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, type StrategyTemplate } from '../../api'
import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
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
        <ChoiceList label="Strategy templates">
          {templates.map((t) => (
            <ChoiceCard
              key={t.key}
              name="template"
              value={t.key}
              checked={t.key === templateKey}
              onSelect={() => setTemplateKey(t.key)}
              title={t.name}
              subtitle={`"${t.tagline}"`}
              footer={
                <Link
                  to={reviewPath(t.key)}
                  onClick={() => setTemplateKey(t.key)}
                  className={styles.learnMore}
                  aria-label={`Learn more about ${t.name}`}
                >
                  Learn more
                </Link>
              }
            >
              {t.idea}
            </ChoiceCard>
          ))}
        </ChoiceList>
      )}
    </Screen>
  )
}
