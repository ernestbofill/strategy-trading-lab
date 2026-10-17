export const pickPath = '/'
export const reviewPath = (key: string) => `/new/templates/${key}`
export const howTestedPath = (key: string) => `/new/templates/${key}/how-tested`
export const advancedPath = (key: string) => `/new/templates/${key}/advanced`
export const modePath = '/new/mode'
export const budgetPath = '/new/budget'
export const confirmPath = '/new/confirm'
/** Link target for an Edit link on the confirm screen: Back and Continue there return to it. */
export const editFromConfirm = (path: string) => `${path}?from=confirm`
export const dashboardPath = '/dashboard'
