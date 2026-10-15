import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, type StrategyTemplate, type TemplateKey } from '../../api'
import { Screen, TopBar } from '../../components/Screen'

/** 2 · Review a template — not built yet; stands in so "Continue" and "Learn more" have a target. */
export function ReviewPlaceholder() {
  const { key } = useParams<{ key: TemplateKey }>()
  const [template, setTemplate] = useState<StrategyTemplate | null | undefined>(undefined)

  useEffect(() => {
    if (key) api.getTemplate(key).then((t) => setTemplate(t ?? null))
  }, [key])

  return (
    <Screen top={<TopBar back="/" step="2 / 5" />}>
      {template === undefined && <p>Loading…</p>}
      {template === null && <p>Template not found.</p>}
      {template && (
        <>
          <h1 style={{ margin: 0, font: '600 28px/36px var(--font-mono)', color: 'var(--primary)' }}>{template.name}</h1>
          <p style={{ margin: 0, color: 'var(--ink-muted)' }}>The review screen comes in the next step.</p>
        </>
      )}
    </Screen>
  )
}
