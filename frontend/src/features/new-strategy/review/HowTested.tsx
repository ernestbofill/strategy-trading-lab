import { useParams } from 'react-router-dom'
import type { TemplateDetail, TemplateKey } from '../../../api'
import { Callout } from '../../../components/Callout'
import { Columns } from '../../../components/Columns'
import { BulletList, KeyValueList } from '../../../components/Lists'
import { LoadableScreen } from '../../../components/LoadableScreen'
import { PageHeader } from '../../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../../components/Screen'
import { Section } from '../../../components/Section'
import { useT } from '../../../i18n/I18n'
import { eur } from '../../../lib/format'
import { reviewPath } from '../paths'
import { useTemplate } from '../useTemplate'
import { PeriodTable } from './Results'

/** 2c · How we tested (wireframe `HowTested`). Not offered for templates without a backtest. */
export function HowTested() {
  const t = useT()
  const { key } = useParams<{ key: TemplateKey }>()
  return (
    <LoadableScreen state={useTemplate(key)} top={<TopBar back={key ? reviewPath(key) : '/'} />} what={t('newStrategy.what.test')}>
      {(template) => <HowTestedContent template={template} />}
    </LoadableScreen>
  )
}

function HowTestedContent({ template }: { template: TemplateDetail }) {
  const t = useT()
  const top = <TopBar back={reviewPath(template.key)} step={template.name} />
  const b = template.backtest
  if (!b) {
    return (
      <Screen top={top}>
        <ScreenMessage>{t('newStrategy.how.noBacktest')}</ScreenMessage>
      </Screen>
    )
  }

  const feeText = `${b.feePercent ? `${b.feePercent}% + ` : ''}${eur(b.feePerTrade)}`

  return (
    <Screen top={top} note={t('common.disclaimer')}>
      <PageHeader
        title={t('newStrategy.how.title')}
        lead={t('newStrategy.how.lead')}
      />
      <Columns
        main={
          <>
            <Section title={t('newStrategy.how.data')}>
              <KeyValueList
                rows={[
                  [t('newStrategy.how.prices'), t('newStrategy.how.pricesValue', { provider: b.provider })],
                  [t('newStrategy.how.period'), b.years],
                  [template.watches.kind === 'fund' ? t('newStrategy.how.fund') : t('newStrategy.how.stocks'), b.universe],
                  [t('newStrategy.how.adjusted'), t('newStrategy.how.adjustedValue')],
                ]}
              />
            </Section>
            <Section title={t('newStrategy.how.run')}>
              <BulletList
                items={[
                  t('newStrategy.how.run.checked'),
                  t('newStrategy.how.run.bought'),
                  b.testedDefaults,
                  t('newStrategy.how.run.euro'),
                  t('newStrategy.how.run.index'),
                  t('newStrategy.how.run.fees', { fee: feeText }),
                ]}
              />
            </Section>
          </>
        }
        aside={
          <>
            <Section title={t('newStrategy.how.byPeriod')}>
              <PeriodTable periods={b.periods} />
            </Section>
            <Callout tone="warning" title={t('newStrategy.how.cantShow')}>
              <p>{t('newStrategy.how.survivors')}</p>
              <p>{t('newStrategy.how.realPrices')}</p>
              <p>{t('common.pastResults')}</p>
            </Callout>
          </>
        }
      />
    </Screen>
  )
}
