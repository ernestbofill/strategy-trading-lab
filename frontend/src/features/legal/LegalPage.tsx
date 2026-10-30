import { useParams } from 'react-router-dom'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import styles from './Legal.module.css'

export function LegalPage({ section }: { section?: string }) {
  const t = useT()
  const params = useParams<{ section: string }>()
  const key = section ?? params.section ?? 'notice'

  let title = t('legal.genericTitle')
  let lead = t('legal.genericLead')
  let content: string | undefined

  if (key === 'notice') {
    title = t('legal.notice.title')
    lead = t('legal.notice.lead')
  } else if (key === 'terms') {
    title = t('legal.terms.title')
    lead = t('legal.terms.lead')
  } else if (key === 'privacy') {
    title = t('legal.privacy.title')
    lead = t('legal.privacy.lead')
  } else if (key === 'disclaimer') {
    title = t('legal.disclaimer.title')
    lead = t('common.disclaimerFull')
    content = t('legal.disclaimer.content')
  } else if (key === 'about') {
    title = t('legal.about.title')
    lead = t('legal.about.lead')
    content = t('legal.about.content')
  }

  return (
    <Screen top={<TopBar title={t('legal.topBar')} back="/" />}>
      <PageHeader title={title} lead={lead} />
      <div className={styles.content}>
        {content ? (
          <p className={styles.text}>{content}</p>
        ) : (
          <ScreenMessage>{t('legal.laterStep')}</ScreenMessage>
        )}
      </div>
    </Screen>
  )
}
