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
    <LoadableScreen state={useTemplate(key)} top={<TopBar up={key ? reviewPath(key) : '/'} />} what={t('newStrategy.what.test')}>
      {(template) => <HowTestedContent template={template} />}
    </LoadableScreen>
  )
}

function HowTestedContent({ template }: { template: TemplateDetail }) {
  const t = useT()
  const top = <TopBar up={reviewPath(template.key)} step={template.name} />
  const b = template.backtest
  if (!b) {
    return (
      <Screen top={top}>
        <ScreenMessage>{t('newStrategy.how.noBacktest')}</ScreenMessage>
      </Screen>
    )
  }

  // The backtest's costs have no fixed part: "0.2%"; a fixed part shows as "0.2% + €1".
  const feeText = [b.feePercent ? `${b.feePercent}%` : null, b.feePerTrade ? eur(b.feePerTrade) : null].filter(Boolean).join(' + ')

  return (
    <Screen top={top}>
      <PageHeader
        title={t('newStrategy.how.title')}
        lead={t('newStrategy.how.lead')}
      />
      <Columns
        main={
          <>
            <Section title={t('newStrategy.how.byPeriod')}>
              <PeriodTable periods={b.periods} benchmarks={b.benchmarks} />
              <p className="caption">{t('newStrategy.how.independentPeriods')}</p>
            </Section>
            <Section title={t('newStrategy.how.run')}>
              <BulletList
                items={[
                  t('newStrategy.how.run.checked'),
                  t('newStrategy.how.run.bought'),
                  b.testedDefaults,
                  t('newStrategy.how.run.euro'),
                  t(template.watches.kind === 'fund' ? 'newStrategy.how.run.indexFund' : 'newStrategy.how.run.index'),
                  t('newStrategy.how.run.irr'),
                  t('newStrategy.how.run.fees', { fee: feeText }),
                ]}
              />
            </Section>
          </>
        }
        aside={
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
            <Callout tone="warning" title={t('newStrategy.how.cantShow')}>
              {/* Survivorship is about the stocks drawn; an index fund has its whole history. */}
              {template.watches.kind !== 'fund' && <p>{t('newStrategy.how.survivors')}</p>}
              <p>{t('newStrategy.how.realPrices')}</p>
            </Callout>
          </>
        }
      />
    </Screen>
  )
}
