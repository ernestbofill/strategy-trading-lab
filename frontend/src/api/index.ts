import { httpApi } from './httpApi'
import { mockApi } from './mock/mockApi'
import type { Api } from './types'

// The .NET API, or the in-browser mock with VITE_API=mock (to work without the backend running).
export const api: Api = import.meta.env.VITE_API === 'mock' ? mockApi : httpApi

export {
  ExpiredLinkError,
  InvalidFieldsError,
  InvalidValueError,
  NameTakenError,
  ForbiddenError,
  TooManyTriesError,
  UnauthorizedError,
} from './errors'
export { onSessionEnded } from './session'
export type * from './types'
