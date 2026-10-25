import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'

/** Stand-in for screens of the flow that aren't built yet, so every link has a target. */
export function NotBuiltYet({ title, back, step }: { title: string; back: string; step?: string }) {
  const t = useT()
  return (
    <Screen top={<TopBar back={back} step={step} />}>
      <PageHeader title={title} />
      <ScreenMessage>{t('newStrategy.notBuiltYet')}</ScreenMessage>
    </Screen>
  )
}
