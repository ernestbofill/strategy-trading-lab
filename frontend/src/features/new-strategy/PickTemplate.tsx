import { Link, useLocation } from 'react-router-dom'
import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
import { LanguageSwitch } from '../../components/LanguageSwitch'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Tagline } from '../../components/Tagline'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { useOptionalAccount } from '../account/AccountContext'
import { logInPathFor } from '../account/paths'
import { VerifyNotice } from '../account/VerifyNotice'
import { useNewStrategyDraft } from './NewStrategyDraft'
import { pickPath, reviewPath } from './paths'
import styles from './PickTemplate.module.css'
import { useTemplates } from './useTemplate'

/** 1 · Pick a strategy template (wireframe `Main`). */
export function PickTemplate() {
  const t = useT()
  const { templateKey, switchTemplate } = useNewStrategyDraft()
  const templates = useTemplates()
  const account = useOptionalAccount()
  const isGuest = !account
  // A fresh start (no screen before this one) shows the welcome eyebrow.
  const hasHistory = useLocation().key !== 'default'

  return (
    <Screen
      top={
        <TopBar
          home
          step={isGuest ? undefined : '1 / 5'}
          aside={
            isGuest ? (
              <>
                <LanguageSwitch />
                <TextAction to={logInPathFor(pickPath)}>{t('common.logIn')}</TextAction>
              </>
            ) : undefined
          }
        />
      }
      action={<PrimaryAction to={reviewPath(templateKey)}>{t('newStrategy.pick.continue')}</PrimaryAction>}
      note={t('newStrategy.pick.note')}
    >
      <VerifyNotice />
      <PageHeader
        eyebrow={isGuest || !hasHistory ? t('newStrategy.pick.eyebrow') : undefined}
        title={t('newStrategy.pick.title')}
        lead={t('newStrategy.pick.lead')}
      />

      {templates.status === 'loading' && <ScreenMessage>{t('newStrategy.pick.loading')}</ScreenMessage>}
      {(templates.status === 'error' || templates.status === 'missing') && (
        <ScreenMessage>{t('newStrategy.pick.loadError')}</ScreenMessage>
      )}

      {templates.status === 'ready' && (
        <ChoiceList label={t('newStrategy.pick.listLabel')}>
          {templates.data.map((item) => (
            <ChoiceCard
              key={item.key}
              name="template"
              value={item.key}
              checked={item.key === templateKey}
              onSelect={() => switchTemplate(item.key)}
              title={item.name}
              subtitle={<Tagline text={item.tagline} />}
              footer={
                <Link
                  to={reviewPath(item.key)}
                  onClick={() => switchTemplate(item.key)}
                  className={styles.learnMore}
                  aria-label={t('newStrategy.pick.learnMoreAria', { name: item.name })}
                >
                  {t('newStrategy.pick.learnMore')}
                </Link>
              }
            >
              {item.idea}
            </ChoiceCard>
          ))}
        </ChoiceList>
      )}
    </Screen>
  )
}
