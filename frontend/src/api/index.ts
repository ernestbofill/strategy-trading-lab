import { httpApi } from './httpApi'
import type { Api } from './types'

export const api: Api = httpApi

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
