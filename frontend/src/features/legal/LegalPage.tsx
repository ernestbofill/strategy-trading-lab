import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import styles from './Legal.module.css'

type Section = 'notice' | 'terms' | 'privacy' | 'disclaimer' | 'about'

// Message keys per section; `content` is absent while the page still comes in a later step.
const sections: Record<Section, { title: MessageKey; lead: MessageKey; content?: MessageKey }> = {
  notice: { title: 'legal.notice.title', lead: 'legal.notice.lead' },
  terms: { title: 'legal.terms.title', lead: 'legal.terms.lead' },
  privacy: { title: 'legal.privacy.title', lead: 'legal.privacy.lead' },
  disclaimer: { title: 'legal.disclaimer.title', lead: 'common.disclaimerFull', content: 'legal.disclaimer.content' },
  about: { title: 'legal.about.title', lead: 'legal.about.lead', content: 'legal.about.content' },
}

export function LegalPage({ section }: { section: Section }) {
  const t = useT()
  const { title, lead, content } = sections[section]

  return (
    <Screen top={<TopBar title={t('legal.topBar')} back="/" />}>
      <PageHeader title={t(title)} lead={t(lead)} />
      <div className={styles.content}>
        {content ? (
          <p className={styles.text}>
            {section === 'disclaimer' && `${t('common.pastResults')} `}
            {t(content)}
          </p>
        ) : <ScreenMessage>{t('legal.laterStep')}</ScreenMessage>}
      </div>
    </Screen>
  )
}
