import { useSearchParams } from 'react-router-dom'

/**
 * The `token` of an emailed link (`?token=…`). It stays in the address so a reload still works; the app loads nothing
 * from other sites that could see it (the browser sends other sites only the origin).
 */
export const useLinkToken = (): string | null => useSearchParams()[0].get('token')
