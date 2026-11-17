export type FeedbackKind = 'bug' | 'idea' | 'note' | 'feature'

/** Open, then closed once handled, then validated once the owner has checked the fix. */
export type FeedbackStatus = 'open' | 'closed' | 'validated'

/** A feedback comment, optionally pinned to selected text on a screen: backend Feedback/FeedbackEndpoints.cs (FeedbackDto). */
export interface FeedbackItem {
  id: string
  userEmail: string
  kind: FeedbackKind
  status: FeedbackStatus
  text: string
  route: string | null
  element: string | null
  selectedText: string | null
  createdAt: string
  updatedAt: string
  closedAt: string | null
}

export interface NewFeedback {
  kind: FeedbackKind
  text: string
  route?: string | null
  element?: string | null
  selectedText?: string | null
}

export interface FeedbackPatch {
  kind?: FeedbackKind
  status?: FeedbackStatus
  text?: string
}
