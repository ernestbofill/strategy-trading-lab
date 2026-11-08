import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import { homePath } from '../new-strategy/paths'

/** An address the app doesn't have (spec: Every screen): says so, with a way home, instead of quietly opening the home page. */
export function NotFoundPage() {
  const t = useT()
  return (
    <Screen top={<TopBar home />} action={<PrimaryAction to={homePath}>{t('errors.goHome')}</PrimaryAction>}>
      <PageHeader title={t('errors.notFound.title')} />
      <ScreenMessage>{t('errors.notFound.line')}</ScreenMessage>
    </Screen>
  )
}
