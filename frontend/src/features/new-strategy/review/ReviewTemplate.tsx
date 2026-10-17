import { useEffect, useState, type ReactNode } from 'react'
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
import { DISCLAIMER } from '../../../lib/copy'
import { useNewStrategyDraft, type AiSource } from '../NewStrategyDraft'
import { useFlowTarget } from '../useFlowTarget'
import { advancedPath, howTestedPath, modePath, pickPath } from '../paths'
import { useTemplate } from '../useTemplate'
import { AiPrompt } from './AiPrompt'
import { AI_SOURCES } from './aiSources'
import styles from './Review.module.css'
import { TestedBox } from './TestedBox'

/** 2 · Review a template (wireframes `Review`, `ReviewComeback`, `ReviewDrop`, `ReviewTrend`, `ReviewAI`). */
export function ReviewTemplate() {
  const { key } = useParams<{ key: TemplateKey }>()
  const backTo = useFlowTarget(pickPath)
  const top = <TopBar back={backTo} step="2 / 5" />
  return (
    <LoadableScreen state={useTemplate(key)} top={top} what="the template">
      {(template) => <ReviewContent key={template.key} template={template} top={top} />}
    </LoadableScreen>
  )
}

function ReviewContent({ template, top }: { template: TemplateDetail; top: ReactNode }) {
  const draft = useNewStrategyDraft()
  const next = useFlowTarget(modePath)
  // Opened by URL for another template than the draft holds (a link, a reload): make it the chosen one.
  const adopted = draft.templateKey === template.key
  const { switchTemplate } = draft
  useEffect(() => {
    if (!adopted) switchTemplate(template.key)
  }, [adopted, switchTemplate, template.key])
  if (!adopted) return null

  const { aiRound, watches } = template
  const stocks = watches.kind === 'stocks' ? watches.starterList : []
  const { unticked } = draft
  const tickedCount = stocks.length - unticked.length
  const canContinue = watches.kind === 'fund' || tickedCount > 0

  const toggleStock = (symbol: string) =>
    draft.update({ unticked: unticked.includes(symbol) ? unticked.filter((s) => s !== symbol) : [...unticked, symbol] })

  const stockList = (
    <>
      <CheckList
        label="Stocks to watch"
        items={stocks.map((s) => ({ id: s.symbol, title: s.name, detail: s.what }))}
        isChecked={(symbol) => !unticked.includes(symbol)}
        onToggle={toggleStock}
        footer={<TextAction>+ Add a stock</TextAction>}
      />
      <p className={styles.info}>In our tests, longer lists gave steadier results.</p>
    </>
  )

  const main = (
    <>
      <Section title="The idea">
        <p className={styles.plain}>{template.rationale}</p>
      </Section>
      <Section title="It signals when">
        <RuleList rules={template.rules} />
      </Section>
      {aiRound ? (
        <AiSettings />
      ) : watches.kind === 'stocks' ? (
        <Section
          title="Choose stocks to watch"
          aside={
            <span className={styles.counter}>
              {tickedCount} of {stocks.length} ticked
            </span>
          }
        >
          {stockList}
        </Section>
      ) : (
        <Section title="The fund it watches">
          <div className={styles.fund}>
            <ItemRow title={watches.fund.name} detail={watches.fund.what} trailing={<TextAction>Change</TextAction>} />
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
            <LabelledLine label="What you need:">
              an AI service that can search the web (most paid plans can). The app doesn't connect to it and never sees
              your account.
            </LabelledLine>
          </Callout>
        </>
      )}
      {aiRound ? (
        <AiRows aiRound={aiRound} stockList={stockList} tickedCount={tickedCount} advancedTo={advancedPath(template.key)} />
      ) : (
        <NavRow label="Advanced settings" to={advancedPath(template.key)} />
      )}
    </>
  )

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction
          to={next}
          disabled={!canContinue}
        >
          Use this template
        </PrimaryAction>
      }
      note={canContinue ? DISCLAIMER : 'Tick at least one stock to watch.'}
    >
      <PageHeader
        eyebrow={
          <>
            Strategy template
            {aiRound && <Tag tone="ai">AI</Tag>}
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
function AiSettings() {
  const { aiSources, update } = useNewStrategyDraft()
  const toggle = (id: AiSource) => update({ aiSources: { ...aiSources, [id]: !aiSources[id] } })

  return (
    <>
      <Section title="What the AI reads">
        <ToggleTiles tiles={AI_SOURCES} isOn={(id) => aiSources[id]} onToggle={toggle} keepOne />
        <p className="caption">At least one stays on. Each one adds a section to the prompt.</p>
      </Section>
      <Section title="The prompt" aside={<TextAction>Edit prompt</TextAction>}>
        <AiPrompt sources={aiSources} />
        <p className="caption">It asks for scores against stated criteria, never which stock to buy.</p>
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
  const [showStocks, setShowStocks] = useState(false)
  return (
    <div className={styles.rows}>
      <NavRow
        label="Choose stocks to watch"
        meta={`${tickedCount} ticked`}
        onClick={() => setShowStocks((v) => !v)}
        expanded={showStocks}
      />
      {showStocks && stockList}
      <NavRow
        label="Advanced settings"
        meta={`every ${aiRound.everyWeeks} weeks · top ${aiRound.topN}`}
        to={advancedTo}
      />
    </div>
  )
}
