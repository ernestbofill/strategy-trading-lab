import { Navigate, Outlet } from 'react-router-dom'
import { useCurrentAccount } from './AccountContext'

/** A layout route inside `RequireAccount` for the owner's screens (/admin): anyone else goes to the start page, as if it weren't there. */
export function RequireOwner() {
  return useCurrentAccount().role === 'owner' ? <Outlet /> : <Navigate to="/" replace />
}
