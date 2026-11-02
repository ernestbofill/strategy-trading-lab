import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, InvalidFieldsError, type Security, type StrategyDetail } from '../../api'
import { CheckList } from '../../components/CheckList'
import { LoadableScreen } from '../../components/LoadableScreen'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'
import { invalidateAll } from '../../lib/useLoadable'
import { strategyPath } from '../dashboard/paths'
import { MAX_STOCKS } from '../new-strategy/draftState'
import { AssetPicker } from '../new-strategy/review/AssetPicker'
import { followedStocks } from './stockLine'
import { useStrategy } from './useStrategy'

/** Edit list (/strategies/:id/list): the review's stock list, for a running strategy. */
export function EditList() {
  const t = useT()
  const { id = '' } = useParams<{ id: string }>()
  return (
    <LoadableScreen state={useStrategy(id)} top={<TopBar back={strategyPath(id)} />} what={t('strategy.what')}>
      {(s) => <EditListContent s={s} />}
    </LoadableScreen>
  )
}

interface Listed {
  id: string
  name: string
  detail: string | null
}

function EditListContent({ s }: { s: StrategyDetail }) {
  const t = useT()
  const navigate = useNavigate()
  // Its stocks, and those unticked earlier that still have trades; then the ones added here.
  const [stocks, setStocks] = useState<Listed[]>(() => s.stocks.map((row) => ({ id: row.assetId, name: row.name, detail: row.what })))
  const [ticked, setTicked] = useState<string[]>(() => followedStocks(s).map((row) => row.assetId))
  const [adding, setAdding] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const top = <TopBar back={strategyPath(s.id)} step={s.name} />
  const full = ticked.length >= MAX_STOCKS

  const toggle = (id: string) => setTicked((list) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]))

  // As on the review: a picked stock goes to the end, ticked; one already listed is only ticked.
  const add = (picked: Security, pricesIn: boolean) => {
    if (!stocks.some((stock) => stock.id === picked.id))
      setStocks((list) => [...list, { id: picked.id, name: picked.name, detail: pricesIn ? picked.what : t('newStrategy.review.noHistory') }])
    setTicked((list) => (list.includes(picked.id) ? list : [...list, picked.id]))
    setAdding(false)
  }

  const save = async () => {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      await api.updateStrategyAssets(s.id, ticked)
      invalidateAll()
      navigate(strategyPath(s.id))
    } catch (err) {
      setError(err instanceof InvalidFieldsError ? Object.values(err.fields)[0] || t('strategy.list.saveFailed') : t('strategy.list.saveFailed'))
      setSaving(false)
    }
  }

  return (
    <Screen
      top={top}
      action={
        <PrimaryAction onClick={() => void save()} disabled={saving || ticked.length === 0}>
          {saving ? t('strategy.settings.saving') : t('strategy.list.save')}
        </PrimaryAction>
      }
      note={ticked.length === 0 ? t('newStrategy.review.tickOne') : undefined}
    >
      <PageHeader title={t('strategy.list.title')} lead={t('strategy.list.lead')} />
      {error && (
        <ScreenMessage>
          <span role="alert">{error}</span>
        </ScreenMessage>
      )}
      <Section
        title={t('newStrategy.review.chooseStocks')}
        aside={<span className="caption">{t('newStrategy.review.tickedOf', { ticked: ticked.length, total: stocks.length })}</span>}
      >
        <CheckList
          label={t('newStrategy.review.stocksLabel')}
          items={stocks.map((stock) => ({ id: stock.id, title: stock.name, detail: stock.detail ?? undefined }))}
          isChecked={(id) => ticked.includes(id)}
          onToggle={toggle}
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
        {adding && !full && <AssetPicker kind="stock" onPick={add} />}
      </Section>
    </Screen>
  )
}
