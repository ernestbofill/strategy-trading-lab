import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError, type StrategyDetail, type TemplateDetail } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { strategyPath } from '../dashboard/paths'
import { AdvancedSettingsBody } from '../new-strategy/AdvancedSettingsBody'
import { changedSettings, settingValues, type SettingValues } from '../new-strategy/settings'
import { useTemplate } from '../new-strategy/useTemplate'
import { useT } from '../../i18n/I18n'
import { invalidateAll } from '../../lib/useLoadable'
import { useStrategy } from './useStrategy'

/** Tune the settings for a running strategy (/strategies/:id/settings). */
export function StrategySettings() {
  const t = useT()
  const { id = '' } = useParams<{ id: string }>()
  const strategy = useStrategy(id)
  return (
    <LoadableScreen state={strategy} top={<TopBar back={strategyPath(id)} />} what={t('strategy.what')}>
      {(loadedStrategy) => <StrategySettingsWithTemplate strategy={loadedStrategy} />}
    </LoadableScreen>
  )
}

function StrategySettingsWithTemplate({ strategy }: { strategy: StrategyDetail }) {
  const t = useT()
  const template = useTemplate(strategy.templateKey)
  const top = <TopBar back={strategyPath(strategy.id)} step={strategy.name} />
  return (
    <LoadableScreen state={template} top={top} what={t('newStrategy.what.template')}>
      {(loadedTemplate) => <StrategySettingsContent strategy={strategy} template={loadedTemplate} />}
    </LoadableScreen>
  )
}

function StrategySettingsContent({ strategy, template }: { strategy: StrategyDetail; template: TemplateDetail }) {
  const t = useT()
  const navigate = useNavigate()
  const [values, setValues] = useState<SettingValues>(() => settingValues(template, strategy.settings ?? {}))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const top = <TopBar back={strategyPath(strategy.id)} step={strategy.name} />

  if (template.settings.length === 0) {
    return (
      <Screen top={top} note={t('common.disclaimer')}>
        <PageHeader title={t('newStrategy.advanced.title')} />
        <ScreenMessage>{t('newStrategy.advanced.later')}</ScreenMessage>
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
    } catch (err) {
      if (err instanceof InvalidFieldsError) {
        setError(Object.values(err.fields)[0] || t('strategy.settings.checkSettings'))
      } else {
        setError(t('strategy.settings.saveFailed'))
      }
      setSaving(false)
    }
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void save()} disabled={saving}>
          {saving ? t('strategy.settings.saving') : t('newStrategy.advanced.save')}
        </PrimaryAction>
      }
      note={t('common.disclaimer')}
    >
      <PageHeader title={t('newStrategy.advanced.title')} lead={t('newStrategy.advanced.lead')} />
      {error && (
        <ScreenMessage>
          <span role="alert">{error}</span>
        </ScreenMessage>
      )}
      <AdvancedSettingsBody template={template} values={values} onChange={setValues} />
    </Screen>
  )
}
