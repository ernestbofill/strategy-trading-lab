import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, type StrategyDetail, type TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { strategyPath } from '../dashboard/paths'
import { SettingRow } from '../new-strategy/Advanced'
import styles from '../new-strategy/Advanced.module.css'
import { PeriodTable } from '../new-strategy/review/Results'
import {
  changedSettings,
  settingBounds,
  settingValues,
  testedValues,
  type SettingValues,
} from '../new-strategy/settings'
import { useTemplate } from '../new-strategy/useTemplate'
import { DISCLAIMER, PAST_RESULTS_CAPTION } from '../../lib/copy'
import { invalidateAll } from '../../lib/useLoadable'
import { useStrategy } from './useStrategy'

/** Tune the settings for a running strategy (/strategies/:id/settings). */
export function StrategySettings() {
  const { id = '' } = useParams<{ id: string }>()
  const strategy = useStrategy(id)
  return (
    <LoadableScreen state={strategy} top={<TopBar back={strategyPath(id)} />} what="this strategy">
      {(loadedStrategy) => <StrategySettingsWithTemplate strategy={loadedStrategy} />}
    </LoadableScreen>
  )
}

function StrategySettingsWithTemplate({ strategy }: { strategy: StrategyDetail }) {
  const template = useTemplate(strategy.templateKey)
  const top = <TopBar back={strategyPath(strategy.id)} step={strategy.name} />
  return (
    <LoadableScreen state={template} top={top} what="the template">
      {(loadedTemplate) => <StrategySettingsContent strategy={strategy} template={loadedTemplate} />}
    </LoadableScreen>
  )
}

function StrategySettingsContent({ strategy, template }: { strategy: StrategyDetail; template: TemplateDetail }) {
  const navigate = useNavigate()
  const [values, setValues] = useState<SettingValues>(() => settingValues(template, strategy.settings ?? {}))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const top = <TopBar back={strategyPath(strategy.id)} step={strategy.name} />

  if (template.settings.length === 0) {
    return (
      <Screen top={top}>
        <PageHeader title="Advanced settings" />
        <ScreenMessage>This template's settings come in a later step.</ScreenMessage>
      </Screen>
    )
  }

  const tested = testedValues(template)
  const changed = changedSettings(template, values)
  const anyChanged = Object.keys(changed).length > 0

  const save = async () => {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      await api.updateStrategySettings(strategy.id, changed)
      invalidateAll()
      navigate(strategyPath(strategy.id))
    } catch {
      setError('Could not save settings. Please try again.')
      setSaving(false)
    }
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void save()} disabled={saving}>
          {saving ? 'Saving…' : 'Save settings'}
        </PrimaryAction>
      }
      note={DISCLAIMER}
    >
      <PageHeader title="Advanced settings" lead="Every rule as an exact number, next to the tested default." />
      {error && <ScreenMessage>{error}</ScreenMessage>}
      <div className={styles.rows}>
        {template.settings.map((setting) => (
          <SettingRow
            key={setting.key}
            setting={setting}
            value={values[setting.key]}
            tested={tested[setting.key]}
            bounds={settingBounds(setting, template.settings, values)}
            onChange={(value) => setValues((v) => ({ ...v, [setting.key]: value }))}
          />
        ))}
        <button
          type="button"
          className={`ui-strong ${styles.reset}`}
          onClick={() => setValues(tested)}
          disabled={!anyChanged}
        >
          ⟳ Reset to tested
        </button>
      </div>
      {template.backtest && (
        <Section title="Backtest">
          {anyChanged ? (
            <>
              <PeriodTable periods={template.backtest.periods} />
              <p className="caption">
                These are the tested defaults' results. Re-running the test with your settings comes later.{' '}
                {PAST_RESULTS_CAPTION}
              </p>
            </>
          ) : (
            <p className={`well ${styles.message}`}>You're on the tested defaults. Change a setting to compare.</p>
          )}
        </Section>
      )}
    </Screen>
  )
}
