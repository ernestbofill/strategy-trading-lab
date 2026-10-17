import { Link, useLocation } from 'react-router-dom'
import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Tagline } from '../../components/Tagline'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { homePath, reviewPath } from './paths'
import styles from './PickTemplate.module.css'
import { useTemplates } from './useTemplate'

/** 1 · Pick a strategy template (wireframe `Main`). */
export function PickTemplate() {
  const { templateKey, switchTemplate } = useNewStrategyDraft()
  const templates = useTemplates()
  // Back only when there is somewhere to go back to (the confirm screen's Edit template), not on a fresh start.
  const hasHistory = useLocation().key !== 'default'

  return (
    <Screen
      top={<TopBar title="STL // NEW STRATEGY" back={hasHistory ? homePath : undefined} step="1 / 5" />}
      action={<PrimaryAction to={reviewPath(templateKey)}>Continue</PrimaryAction>}
      note="Pick one to start. You can add more strategies later, from any template."
    >
      <PageHeader
        eyebrow="Welcome to Strategy Trading Lab"
        title="Pick a strategy template"
        lead="Each template is a few simple rules with tested defaults. See how it did in the past, tune it, then run it as a simulation or live."
      />

      {templates.status === 'loading' && <ScreenMessage>Loading templates…</ScreenMessage>}
      {(templates.status === 'error' || templates.status === 'missing') && (
        <ScreenMessage>Could not load the templates. Try again later.</ScreenMessage>
      )}

      {templates.status === 'ready' && (
        <ChoiceList label="Strategy templates">
          {templates.data.map((t) => (
            <ChoiceCard
              key={t.key}
              name="template"
              value={t.key}
              checked={t.key === templateKey}
              onSelect={() => switchTemplate(t.key)}
              title={t.name}
              subtitle={<Tagline text={t.tagline} />}
              footer={
                <Link
                  to={reviewPath(t.key)}
                  onClick={() => switchTemplate(t.key)}
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
