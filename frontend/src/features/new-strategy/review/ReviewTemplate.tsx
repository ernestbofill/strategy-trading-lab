import { useState, type ReactNode } from 'react'
import { useParams } from 'react-router-dom'
import type { AiRound, Security, TemplateDetail, TemplateKey } from '../../../api'
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
import { fundOf, MAX_STOCKS, stocksOf, tickedStocks, untickedOf } from '../draftState'
import { AssetPicker } from './AssetPicker'
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
  const [adding, setAdding] = useState(false)
  const stocks = stocksOf(draft, template)
  const unticked = untickedOf(draft, template)
  const added = draft.templateKey === template.key ? draft.added : []
  const tickedCount = tickedStocks(draft, template).length
  const canContinue = watches.kind === 'fund' || tickedCount > 0
  const full = stocks.length >= MAX_STOCKS

  // A change on another template's draft starts this template's over (as Advanced settings and the fund picker do).
  const own = () => {
    if (draft.templateKey !== template.key) draft.switchTemplate(template.key)
  }

  const toggleStock = (symbol: string) => {
    own()
    draft.update({
      unticked: unticked.includes(symbol) ? unticked.filter((s) => s !== symbol) : [...unticked, symbol],
    })
  }

  // A picked stock goes to the end of the list, ticked; one already listed is only ticked.
  const addStock = (picked: Security, pricesIn: boolean) => {
    own()
    draft.update({
      added: stocks.some((s) => s.id === picked.id) ? added : [...added, { ...picked, pricesIn }],
      unticked: unticked.filter((s) => s !== picked.symbol),
    })
    setAdding(false)
  }

  const stockList = (
    <>
      <CheckList
        label={t('newStrategy.review.stocksLabel')}
        items={stocks.map((s) => ({
          id: s.symbol,
          title: s.name,
          detail: 'pricesIn' in s && !s.pricesIn ? t('newStrategy.review.noHistory') : s.what,
        }))}
        isChecked={(symbol) => !unticked.includes(symbol)}
        onToggle={toggleStock}
        footer={
          full ? (
            <span className="caption">{t('newStrategy.review.stocksFull', { max: MAX_STOCKS })}</span>
          ) : (
            <TextAction onClick={() => setAdding((v) => !v)}>
              {t(adding ? 'newStrategy.review.closePicker' : 'newStrategy.review.addStock')}
            </TextAction>
          )
        }
      />
      {adding && !full && <AssetPicker kind="stock" onPick={addStock} />}
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
        <FundSection template={template} />
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

/** "The fund it watches": the template's, or one the user picks with "Change". */
function FundSection({ template }: { template: TemplateDetail }) {
  const t = useT()
  const draft = useNewStrategyDraft()
  const [picking, setPicking] = useState(false)
  // Funds added this visit whose price history isn't in yet.
  const [waiting, setWaiting] = useState<string[]>([])
  const fund = fundOf(draft, template)!
  const tested = template.watches.kind === 'fund' ? template.watches.fund : fund
  const changed = fund.id !== tested.id

  const choose = (picked: Security | null) => {
    if (draft.templateKey !== template.key) draft.switchTemplate(template.key)
    draft.update({ fund: picked && picked.id !== tested.id ? picked : null })
    setPicking(false)
  }

  return (
    <Section
      title={t('newStrategy.review.fund')}
      aside={
        <TextAction onClick={() => setPicking((v) => !v)}>
          {t(picking ? 'newStrategy.review.closePicker' : 'newStrategy.review.change')}
        </TextAction>
      }
    >
      <div className={styles.fund}>
        <ItemRow title={fund.name} detail={waiting.includes(fund.id) ? t('newStrategy.review.noHistory') : fund.what} />
      </div>
      {changed && (
        <div className={styles.tested}>
          <p className="caption">{t('newStrategy.review.testedFund', { name: tested.name })}</p>
          <TextAction onClick={() => choose(null)}>{t('newStrategy.review.useTestedFund')}</TextAction>
        </div>
      )}
      {picking && (
        <AssetPicker
          kind="fund"
          onPick={(picked, pricesIn) => {
            if (!pricesIn) setWaiting((w) => [...w, picked.id])
            choose(picked)
          }}
        />
      )}
    </Section>
  )
}

/** "What the AI reads" and "The prompt", for templates driven by AI rounds. */
function AiSettings({ template }: { template: TemplateDetail }) {
  const t = useT()
  const draft = useNewStrategyDraft()
  const { aiSources, aiInstructions, settings, update } = draft
  const toggle = (id: AiSource) => update({ aiSources: { ...aiSources, [id]: !aiSources[id] } })
  const { preview, failed } = useAiPromptPreview({
    ...aiSources,
    instructions: aiInstructions,
    ...aiPromptScope(template, settings, tickedStocks(draft, template)),
  })

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
