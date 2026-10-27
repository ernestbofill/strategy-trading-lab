import type { Dispatch, SetStateAction } from 'react'
import type { Setting, TemplateDetail } from '../../api'
import { RichText } from '../../components/RichText'
import { Section } from '../../components/Section'
import { useT } from '../../i18n/I18n'
import { SettingRow } from './Advanced'
import styles from './Advanced.module.css'
import { PeriodTable } from './review/Results'
import {
  changedSettings,
  settingBounds,
  testedValues,
  type SettingValues,
} from './settings'

interface AdvancedSettingsBodyProps {
  template: TemplateDetail
  values: SettingValues
  onChange: Dispatch<SetStateAction<SettingValues>>
}

/**
 * The setting rows with steppers, in the template's groups when it has them (each with what that part of the rules
 * decides), a reset to tested button, and the backtest comparison section.
 */
export function AdvancedSettingsBody({ template, values, onChange }: AdvancedSettingsBodyProps) {
  const t = useT()
  const tested = testedValues(template)
  const changed = changedSettings(template, values)
  const anyChanged = Object.keys(changed).length > 0

  const rows = (settings: Setting[]) =>
    settings.map((setting) => (
      <SettingRow
        key={setting.key}
        setting={setting}
        value={values[setting.key]}
        tested={tested[setting.key]}
        bounds={settingBounds(setting, template.settings, values)}
        onChange={(value) => onChange((v) => ({ ...v, [setting.key]: value }))}
      />
    ))

  return (
    <>
      <div className={styles.rows}>
        {rows(template.settings.filter((setting) => !setting.group))}
        {template.settingGroups.map((group) => (
          <section key={group.key} className={styles.group} aria-labelledby={`settings-${group.key}`}>
            <h2 id={`settings-${group.key}`} className={styles.groupTitle}>
              {group.title}
            </h2>
            <p className={styles.groupLead}>
              <RichText text={group.lead} />
            </p>
            {rows(template.settings.filter((setting) => setting.group === group.key))}
          </section>
        ))}
        <button
          type="button"
          className={`ui-strong ${styles.reset}`}
          onClick={() => onChange(tested)}
          disabled={!anyChanged}
        >
          {t('newStrategy.advanced.reset')}
        </button>
      </div>
      {template.backtest && (
        <Section title={t('newStrategy.advanced.backtest')}>
          {anyChanged ? (
            <>
              <PeriodTable periods={template.backtest.periods} />
              <p className="caption">
                {t('newStrategy.advanced.defaultsResults')}{' '}
                {t('common.pastResults')}
              </p>
            </>
          ) : (
            <p className={`well ${styles.message}`}>{t('newStrategy.advanced.onDefaults')}</p>
          )}
        </Section>
      )}
    </>
  )
}
