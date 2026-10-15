import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { NewStrategyDraftProvider } from './features/new-strategy/NewStrategyDraft'
import { PickTemplate } from './features/new-strategy/PickTemplate'
import { ReviewPlaceholder } from './features/new-strategy/ReviewPlaceholder'

export default function App() {
  return (
    <BrowserRouter>
      <NewStrategyDraftProvider>
        <Routes>
          <Route path="/" element={<PickTemplate />} />
          <Route path="/new/templates/:key" element={<ReviewPlaceholder />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </NewStrategyDraftProvider>
    </BrowserRouter>
  )
}
