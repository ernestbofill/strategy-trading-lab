import { useState, type ReactNode } from 'react'
import { useParams } from 'react-router-dom'
import type { AiRound, TemplateDetail, TemplateKey } from '../../../api'
import { Callout, LabelledLine } from '../../../components/Callout'
import { CheckList } from '../../../components/CheckList'
import { Columns } from '../../../components/Columns'
import { ItemRow } from '../../../components/ItemRow'
import { LoadableScreen } from '../../../components/LoadableScreen'
import { NavRow } from '../../../components/NavRow'
import { PageHeader } from '../../../components/PageHeader'
import { RuleList } from '../../../components/RuleList'
import { PrimaryAction, Screen, TopBar } from '../../../components/Screen'
import { Section } from '../../../components/Section'
import { Tag } from '../../../components/Tag'
import { TextAction } from '../../../components/TextAction'
import { ToggleTiles } from '../../../components/ToggleTiles'
import { useT } from '../../../i18n/I18n'
import { useNewStrategyDraft, type AiSource } from '../NewStrategyDraft'
import { advancedPath, howTestedPath, modePath, pickPath, promptPath } from '../paths'
import { rulesWithSettings, settingsSummary } from '../settings'
import { useTemplate } from '../useTemplate'
import { PromptText } from '../../ai-round/PromptText'
import { useAiPromptPreview } from '../../ai-round/useAiPromptPreview'
import { aiPromptScope, aiSources as aiSourcesOf } from './aiSources'
import styles from './Review.module.css'
import { TestedBox } from './TestedBox'

const top = <TopBar back={pickPath} step="2 / 5" />

/** 2 · Review a template (wireframes `Review`, `ReviewComeback`, `ReviewDrop`, `ReviewTrend`, `ReviewAI`). */
export function ReviewTemplate() {
  const t = useT()
  const { key } = useParams<{ key: TemplateKey }>()
  return (
    <LoadableScreen state={useTemplate(key)} top={top} what={t('newStrategy.what.template')}>
      {(template) => <ReviewContent key={template.key} template={template} />}
    </LoadableScreen>
  )
}

function ReviewContent({ template }: { template: TemplateDetail }) {
  const t = useT()
  const draft = useNewStrategyDraft()
  const { aiRound, watches } = template
  const stocks = watches.kind === 'stocks' ? watches.starterList : []
  const { unticked } = draft
  const tickedCount = stocks.length - unticked.length
  const canContinue = watches.kind === 'fund' || tickedCount > 0

  const toggleStock = (symbol: string) =>
    draft.update({
      unticked: unticked.includes(symbol) ? unticked.filter((s) => s !== symbol) : [...unticked, symbol],
    })

  const stockList = (
    <>
      <CheckList
        label={t('newStrategy.review.stocksLabel')}
        items={stocks.map((s) => ({ id: s.symbol, title: s.name, detail: s.what }))}
        isChecked={(symbol) => !unticked.includes(symbol)}
        onToggle={toggleStock}
      />
      <p className={styles.info}>{t('newStrategy.review.longerLists')}</p>
    </>
  )

  const main = (
    <>
      <Section title={t('newStrategy.review.idea')}>
        <p className={styles.plain}>{template.rationale}</p>
      </Section>
      <Section title={t('newStrategy.review.signalsWhen')}>
        <RuleList rules={rulesWithSettings(template, draft.templateKey === template.key ? draft.settings : {})} />
      </Section>
      {aiRound ? (
        <AiSettings template={template} />
      ) : watches.kind === 'stocks' ? (
        <Section
          title={t('newStrategy.review.chooseStocks')}
          aside={
            <span className={styles.counter}>
              {t('newStrategy.review.tickedOf', { ticked: tickedCount, total: stocks.length })}
            </span>
          }
        >
          {stockList}
        </Section>
      ) : (
        <Section title={t('newStrategy.review.fund')}>
          <div className={styles.fund}>
            <ItemRow title={watches.fund.name} detail={watches.fund.what} />
          </div>
        </Section>
      )}
    </>
  )

  const aside = (
    <>
      {template.backtest ? (
        <TestedBox backtest={template.backtest} howTestedTo={howTestedPath(template.key)} />
      ) : (
        <>
          <Callout tone="info">
            <LabelledLine label={t('newStrategy.review.needLabel')}>
              {t('newStrategy.review.needText')}
            </LabelledLine>
          </Callout>
        </>
      )}
      {aiRound ? (
        <AiRows aiRound={aiRound} stockList={stockList} tickedCount={tickedCount} advancedTo={advancedPath(template.key)} />
      ) : (
        <NavRow
          label={t('newStrategy.review.advanced')}
          meta={
            template.settings.length > 0
              ? // Another template's draft changes belong to that one.
                settingsSummary(draft.templateKey === template.key ? draft.settings : {}).toLowerCase()
              : undefined
          }
          to={advancedPath(template.key)}
        />
      )}
    </>
  )

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction
          to={modePath}
          disabled={!canContinue}
        >
          {t('newStrategy.review.useTemplate')}
        </PrimaryAction>
      }
      note={canContinue ? t('common.disclaimer') : t('newStrategy.review.tickOne')}
    >
      <PageHeader
        eyebrow={
          <>
            {t('newStrategy.review.eyebrow')}
            {aiRound && <Tag tone="ai">{t('newStrategy.review.aiTag')}</Tag>}
          </>
        }
        title={template.name}
        tagline={template.tagline}
      />
      <Columns main={main} aside={aside} />
    </Screen>
  )
}

/** "What the AI reads" and "The prompt", for templates driven by AI rounds. */
function AiSettings({ template }: { template: TemplateDetail }) {
  const t = useT()
  const { aiSources, aiInstructions, settings, unticked, update } = useNewStrategyDraft()
  const toggle = (id: AiSource) => update({ aiSources: { ...aiSources, [id]: !aiSources[id] } })
  const { preview, failed } = useAiPromptPreview({ ...aiSources, instructions: aiInstructions, ...aiPromptScope(template, settings, unticked) })

  return (
    <>
      <Section title={t('newStrategy.review.aiReads')}>
        <ToggleTiles tiles={aiSourcesOf(t)} isOn={(id) => aiSources[id]} onToggle={toggle} keepOne />
        <p className="caption">{t('newStrategy.review.aiAtLeastOne')}</p>
      </Section>
      <Section title={t('newStrategy.review.prompt')} aside={<TextAction to={promptPath(template.key)}>{t('newStrategy.review.editPrompt')}</TextAction>}>
        {preview ? (
          <PromptText text={preview.text} version={preview.version} edited={aiInstructions !== null} collapsed />
        ) : (
          <p className={styles.plain}>{t(failed ? 'aiRound.prompt.loadError' : 'aiRound.prompt.loading')}</p>
        )}
        <p className="caption">{t('newStrategy.review.promptNote')}</p>
      </Section>
    </>
  )
}

interface AiRowsProps {
  aiRound: AiRound
  stockList: ReactNode
  tickedCount: number
  advancedTo: string
}

/** The stock list folds into a row here; "Advanced settings" shows the round settings. */
function AiRows({ aiRound, stockList, tickedCount, advancedTo }: AiRowsProps) {
  const t = useT()
  const [showStocks, setShowStocks] = useState(false)
  return (
    <div className={styles.rows}>
      <NavRow
        label={t('newStrategy.review.chooseStocks')}
        meta={t('newStrategy.review.ticked', { n: tickedCount })}
        onClick={() => setShowStocks((v) => !v)}
        expanded={showStocks}
      />
      {showStocks && stockList}
      <NavRow
        label={t('newStrategy.review.advanced')}
        meta={t('newStrategy.review.aiMeta', { days: aiRound.everyDays, top: aiRound.topN })}
        to={advancedTo}
      />
    </div>
  )
}
