import { BrowserRouter, Navigate, Route, Routes, useParams } from 'react-router-dom'
import { SetBudget } from './features/new-strategy/SetBudget'
import { ChooseMode } from './features/new-strategy/ChooseMode'
import { NewStrategyDraftProvider } from './features/new-strategy/NewStrategyDraft'
import { NotBuiltYet } from './features/new-strategy/NotBuiltYet'
import { Confirm } from './features/new-strategy/Confirm'
import { budgetPath, confirmPath, dashboardPath, reviewPath } from './features/new-strategy/paths'
import { useFlowTarget } from './features/new-strategy/useEditing'
import { PickTemplate } from './features/new-strategy/PickTemplate'
import { HowTested } from './features/new-strategy/review/HowTested'
import { ReviewTemplate } from './features/new-strategy/review/ReviewTemplate'

function AdvancedStub() {
  const { key = '' } = useParams()
  return <NotBuiltYet title="Advanced settings" back={useFlowTarget(reviewPath(key))} />
}

export default function App() {
  return (
    <BrowserRouter>
      <NewStrategyDraftProvider>
        <Routes>
          <Route path="/" element={<PickTemplate />} />
          <Route path="/new/templates/:key" element={<ReviewTemplate />} />
          <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
          <Route path="/new/templates/:key/advanced" element={<AdvancedStub />} />
          <Route path="/new/mode" element={<ChooseMode />} />
          <Route path={budgetPath} element={<SetBudget />} />
          <Route path={confirmPath} element={<Confirm />} />
          <Route path={dashboardPath} element={<NotBuiltYet title="Dashboard" back={confirmPath} />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </NewStrategyDraftProvider>
    </BrowserRouter>
  )
}
