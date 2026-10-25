import { useId, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import type { Setting, TemplateDetail, TemplateKey } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { DISCLAIMER, PAST_RESULTS_CAPTION } from '../../lib/copy'
import styles from './Advanced.module.css'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { reviewPath } from './paths'
import { PeriodTable } from './review/Results'
import {
  changedSettings,
  formatSetting,
  settingBounds,
  settingValues,
  testedValues,
  type SettingValues,
} from './settings'
import { useTemplate } from './useTemplate'

/** 2b · Advanced settings (wireframe `Advanced`). Opened from Review and from Confirm's Settings row. */
export function Advanced() {
  const { key } = useParams<{ key: TemplateKey }>()
  const back = key ? reviewPath(key) : '/'
  return (
    <LoadableScreen state={useTemplate(key)} top={<TopBar back={back} />} what="the template">
      {(template) => <AdvancedContent key={template.key} template={template} />}
    </LoadableScreen>
  )
}

function AdvancedContent({ template }: { template: TemplateDetail }) {
  const draft = useNewStrategyDraft()
  const navigate = useNavigate()
  const hasHistory = useLocation().key !== 'default'
  // The draft's changes belong to its template; another template's screen starts from its own tested values.
  const ownDraft = draft.templateKey === template.key
  const [values, setValues] = useState<SettingValues>(() => settingValues(template, ownDraft ? draft.settings : {}))
  const top = <TopBar back={reviewPath(template.key)} step={template.name} />

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

  const save = () => {
    if (!ownDraft) draft.switchTemplate(template.key)
    draft.update({ settings: changed })
    if (hasHistory) navigate(-1)
    else navigate(reviewPath(template.key))
  }

  return (
    <Screen top={top} action={<PrimaryAction onClick={save}>Save settings</PrimaryAction>} note={DISCLAIMER}>
      <PageHeader title="Advanced settings" lead="Every rule as an exact number, next to the tested default." />
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
        <button type="button" className={`ui-strong ${styles.reset}`} onClick={() => setValues(tested)} disabled={!anyChanged}>
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

export interface SettingRowProps {
  setting: Setting
  value: number
  tested: number
  bounds: { min: number; max: number }
  onChange: (value: number) => void
}

/** The setting as a sentence with its value highlighted, a − / + stepper and the tested value. */
export function SettingRow({ setting, value, tested, bounds, onChange }: SettingRowProps) {
  const id = useId()
  const [before, after] = setting.sentence.split('{value}')
  const isChanged = value !== tested
  return (
    <div className={styles.row} role="group" aria-labelledby={id} data-changed={isChanged || undefined}>
      <div className={styles.text}>
        <div id={id} className={styles.sentence}>
          {before}
          <span className={styles.value} aria-live="polite">
            {formatSetting(setting, value)}
          </span>
          {after}
        </div>
        <div className={styles.tested}>
          <span>Tested: {formatSetting(setting, tested)}</span>
          {isChanged && <span className={styles.changed}>CHANGED</span>}
        </div>
      </div>
      <div className={styles.stepper}>
        <button
          type="button"
          aria-label="Decrease"
          disabled={value - setting.step < bounds.min}
          onClick={() => onChange(value - setting.step)}
        >
          −
        </button>
        <button
          type="button"
          aria-label="Increase"
          disabled={value + setting.step > bounds.max}
          onClick={() => onChange(value + setting.step)}
        >
          +
        </button>
      </div>
    </div>
  )
}
