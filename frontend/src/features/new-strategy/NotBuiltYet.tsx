import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'

/** Stand-in for screens of the flow that aren't built yet, so every link has a target. */
export function NotBuiltYet({ title, back, step }: { title: string; back: string; step?: string }) {
  return (
    <Screen top={<TopBar back={back} step={step} />}>
      <PageHeader title={title} />
      <ScreenMessage>This screen comes in a later step.</ScreenMessage>
    </Screen>
  )
}
