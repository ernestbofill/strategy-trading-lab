import { useParams } from 'react-router-dom'
import type { TemplateDetail, TemplateKey } from '../../../api'
import { Callout } from '../../../components/Callout'
import { Columns } from '../../../components/Columns'
import { BulletList, KeyValueList } from '../../../components/Lists'
import { LoadableScreen } from '../../../components/LoadableScreen'
import { PageHeader } from '../../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../../components/Screen'
import { Section } from '../../../components/Section'
import { DISCLAIMER } from '../../../lib/copy'
import { eur } from '../../../lib/format'
import { useReviewSubPageBack } from '../useEditing'
import { useTemplate } from '../useTemplate'
import { PeriodTable } from './Results'

/** 2c · How we tested (wireframe `HowTested`). Not offered for templates without a backtest. */
export function HowTested() {
  const { key } = useParams<{ key: TemplateKey }>()
  const back = useReviewSubPageBack(key)
  return (
    <LoadableScreen state={useTemplate(key)} top={<TopBar back={back} />} what="the test">
      {(template) => <HowTestedContent template={template} back={back} />}
    </LoadableScreen>
  )
}

function HowTestedContent({ template, back }: { template: TemplateDetail; back: string }) {
  const top = <TopBar back={back} step={template.name} />
  const b = template.backtest
  if (!b) {
    return (
      <Screen top={top}>
        <ScreenMessage>There is no backtest for this template.</ScreenMessage>
      </Screen>
    )
  }

  return (
    <Screen top={top} note={DISCLAIMER}>
      <PageHeader
        title="How we tested"
        lead="We ran the template's rules on past prices, day by day, as if it had been running then. Here's exactly how."
      />
      <Columns
        main={
          <>
            <Section title="The data">
              <KeyValueList
                rows={[
                  ['Prices', `Daily, from ${b.provider}`],
                  ['Period', b.years],
                  [template.watches.kind === 'fund' ? 'Fund' : 'Stocks', b.universe],
                  ['Adjusted for', 'Splits and dividends'],
                ]}
              />
            </Section>
            <Section title="How a test runs">
              <BulletList
                items={[
                  'The rules are checked once a day, after the close, as in the app.',
                  "Each signal is bought at the next day's opening price.",
                  b.testedDefaults,
                  "Results in euro, at each day's exchange rate.",
                  'The index fund gets the same money on the same days.',
                  `Fees: ${eur(b.feePerTrade)} per trade. Taxes not included.`,
                ]}
              />
            </Section>
          </>
        }
        aside={
          <>
            <Section title="By period">
              <PeriodTable periods={b.periods} />
            </Section>
            <Callout tone="warning" title="What a backtest can't show">
              <p>
                The starter list holds companies that are around today. Stocks that failed along the way aren't in it,
                which flatters past results.
              </p>
              <p>Real prices can differ from the opening price, especially on busy days.</p>
              <p>Past results don't promise future ones.</p>
            </Callout>
          </>
        }
      />
    </Screen>
  )
}
