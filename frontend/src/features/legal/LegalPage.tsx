import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import type { MessageKey } from '../../i18n/messages/en'
import { LegalNav } from './LegalNav'
import { LegalNoticeContent } from './LegalNoticeContent'
import { TermsOfUseContent } from './TermsOfUseContent'
import { PrivacyPolicyContent } from './PrivacyPolicyContent'
import { DisclaimerContent } from './DisclaimerContent'
import { AboutContent } from './AboutContent'

export type Section = 'notice' | 'terms' | 'privacy' | 'disclaimer' | 'about'

const sections: Record<Section, { title: MessageKey; lead: MessageKey }> = {
  notice: { title: 'legal.notice.title', lead: 'legal.notice.lead' },
  terms: { title: 'legal.terms.title', lead: 'legal.terms.lead' },
  privacy: { title: 'legal.privacy.title', lead: 'legal.privacy.lead' },
  disclaimer: { title: 'legal.disclaimer.title', lead: 'common.disclaimerFull' },
  about: { title: 'legal.about.title', lead: 'legal.about.lead' },
}

export function LegalPage({ section }: { section: Section }) {
  const t = useT()
  const { title, lead } = sections[section]

  return (
    <Screen top={<TopBar title={t('legal.topBar')} back="/" />}>
      <PageHeader title={t(title)} lead={t(lead)} />
      <LegalNav />
      {section === 'notice' && <LegalNoticeContent />}
      {section === 'terms' && <TermsOfUseContent />}
      {section === 'privacy' && <PrivacyPolicyContent />}
      {section === 'disclaimer' && <DisclaimerContent />}
      {section === 'about' && <AboutContent />}
    </Screen>
  )
}
