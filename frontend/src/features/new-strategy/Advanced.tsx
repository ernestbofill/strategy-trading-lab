import { useId, useState } from 'react'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import type { Setting, TemplateDetail, TemplateKey } from '../../api'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { useT } from '../../i18n/I18n'
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
  const t = useT()
  const { key } = useParams<{ key: TemplateKey }>()
  const back = key ? reviewPath(key) : '/'
  return (
    <LoadableScreen state={useTemplate(key)} top={<TopBar back={back} />} what={t('newStrategy.what.template')}>
      {(template) => <AdvancedContent key={template.key} template={template} />}
    </LoadableScreen>
  )
}

function AdvancedContent({ template }: { template: TemplateDetail }) {
  const t = useT()
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
        <PageHeader title={t('newStrategy.advanced.title')} />
        <ScreenMessage>{t('newStrategy.advanced.later')}</ScreenMessage>
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
    <Screen top={top} action={<PrimaryAction onClick={save}>{t('newStrategy.advanced.save')}</PrimaryAction>} note={t('common.disclaimer')}>
      <PageHeader title={t('newStrategy.advanced.title')} lead={t('newStrategy.advanced.lead')} />
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
    </Screen>
  )
}

interface SettingRowProps {
  setting: Setting
  value: number
  tested: number
  bounds: { min: number; max: number }
  onChange: (value: number) => void
}

/** The setting as a sentence with its value highlighted, a − / + stepper and the tested value. */
function SettingRow({ setting, value, tested, bounds, onChange }: SettingRowProps) {
  const t = useT()
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
          <span>{t('newStrategy.advanced.tested', { value: formatSetting(setting, tested) })}</span>
          {isChanged && <span className={styles.changed}>{t('newStrategy.advanced.changed')}</span>}
        </div>
      </div>
      <div className={styles.stepper}>
        <button
          type="button"
          aria-label={t('newStrategy.advanced.decrease')}
          disabled={value - setting.step < bounds.min}
          onClick={() => onChange(value - setting.step)}
        >
          −
        </button>
        <button
          type="button"
          aria-label={t('newStrategy.advanced.increase')}
          disabled={value + setting.step > bounds.max}
          onClick={() => onChange(value + setting.step)}
        >
          +
        </button>
      </div>
    </div>
  )
}
