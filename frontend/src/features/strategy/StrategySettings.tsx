import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, type StrategyDetail, type TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { strategyPath } from '../dashboard/paths'
import { AdvancedSettingsBody } from '../new-strategy/AdvancedSettingsBody'
import { changedSettings, settingValues, type SettingValues } from '../new-strategy/settings'
import { useTemplate } from '../new-strategy/useTemplate'
import { DISCLAIMER } from '../../lib/copy'
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

  const save = async () => {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      await api.updateStrategySettings(strategy.id, changedSettings(template, values))
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
      <AdvancedSettingsBody template={template} values={values} onChange={setValues} />
    </Screen>
  )
}
