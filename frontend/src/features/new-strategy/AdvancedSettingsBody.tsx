import type { Dispatch, SetStateAction } from 'react'
import type { TemplateDetail } from '../../api'
import { Section } from '../../components/Section'
import { PAST_RESULTS_CAPTION } from '../../lib/copy'
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

/** The setting rows with steppers, a reset to tested button, and the backtest comparison section. */
export function AdvancedSettingsBody({ template, values, onChange }: AdvancedSettingsBodyProps) {
  const tested = testedValues(template)
  const changed = changedSettings(template, values)
  const anyChanged = Object.keys(changed).length > 0

  return (
    <>
      <div className={styles.rows}>
        {template.settings.map((setting) => (
          <SettingRow
            key={setting.key}
            setting={setting}
            value={values[setting.key]}
            tested={tested[setting.key]}
            bounds={settingBounds(setting, template.settings, values)}
            onChange={(value) => onChange((v) => ({ ...v, [setting.key]: value }))}
          />
        ))}
        <button
          type="button"
          className={`ui-strong ${styles.reset}`}
          onClick={() => onChange(tested)}
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
    </>
  )
}
