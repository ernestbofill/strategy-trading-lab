import { mockApi } from './mock/mockApi'
import type { Api } from './types'

// Only the mock backend exists for now. An HTTP client for the .NET API will be selected here later.
export const api: Api = mockApi

export type * from './types'
