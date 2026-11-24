import type { Account, BackendVersion, Currency, Locale, PushSubscriptionJson, SignUp, SignUpOptions } from './account'
import type { AdminAddedAsset, AdminAlert, AdminAsset, AdminDatabases, AdminInvite, AdminNotifications, AdminPrices, AdminPush, AdminSetting, AdminTemplate, AdminTemplateChange, AdminUsers, DatabaseName, NewInvite, PricesFlushed, RatesDownloaded, TestPush, YahooCheck } from './admin'
import type { AiPromptPreview, AiPromptRequest, AiReading, AiRoundDetail, AiRoundHistory, AiRoundResult, AiSetup } from './ai'
import type { AddedAsset, AssetMatch } from './assets'
import type { FeedbackItem, FeedbackPatch, FeedbackStatus, NewFeedback } from './feedback'
import type { ManualTradeRequest, SignalDetail, SignalTrade, SkipReason, StrategyTradeItem, UpdateTradeRequest } from './signals'
import type { Dashboard, NewStrategy, StockChart, StrategyDetail, StrategySummary } from './strategies'
import type { DailySummary } from './summary'
import type { StrategyTemplate, TemplateDetail, TemplateKey } from './templates'

export interface Api {
  /** The logged-in user, or null without a session: the app's start-up check. */
  getAccount(): Promise<Account | null>
  /** Creates an account and logs it in; the same answer when the email already has one. Rejects with `InvalidFieldsError`, `TooManyTriesError`. */
  signUp(signUp: SignUp): Promise<void>
  /** Whether an invite is needed, and who `invite` (a code) is for. Rejects with `TooManyTriesError` when rate-limited. */
  getSignUpOptions(invite?: string): Promise<SignUpOptions>
  /** Rejects with `InvalidFieldsError` (`firstName`, `lastName`). */
  setName(firstName: string, lastName: string): Promise<void>
  /** Rejects with `UnauthorizedError` for a wrong email or password, `TooManyTriesError` while the email is locked. */
  logIn(email: string, password: string): Promise<void>
  logOut(): Promise<void>
  /** Confirms the email from its link; logs no one in. Rejects with `ExpiredLinkError`. */
  verifyEmail(token: string): Promise<void>
  resendVerification(): Promise<void>
  /** Always resolves the same, whether or not the email has an account. */
  forgotPassword(email: string): Promise<void>
  /** Sets the password from the reset link and logs this device in. Rejects with `ExpiredLinkError`, `InvalidFieldsError`. */
  resetPassword(token: string, password: string): Promise<void>
  /** Rejects with `InvalidFieldsError` (`currentPassword`, `newPassword`). */
  changePassword(currentPassword: string, newPassword: string): Promise<void>
  /** Deletes the account and everything about it, ending every session. Rejects with `InvalidFieldsError` (`password`). */
  deleteAccount(password: string): Promise<void>
  /** Sets the currency of the user's new strategies. Rejects with `InvalidFieldsError`. */
  setCurrency(currency: Currency): Promise<void>
  /** Sets the language of the user's account and emails. Rejects with `InvalidFieldsError`. */
  setLocale(locale: Locale): Promise<void>
  /** Sets the user's time zone (an IANA name). Rejects with `InvalidFieldsError`. */
  setTimeZone(timeZone: string): Promise<void>
  /** Sets when the emails and reminders come: an hour, or null for as soon as they're ready. Pushes don't wait for it. */
  setNotifyHour(hour: number | null): Promise<void>
  /** Turns the summary email on or off. */
  setOverviewEmail(enabled: boolean): Promise<void>
  /** Turns signal alerts and reminders by email on or off. */
  setAlertEmails(enabled: boolean): Promise<void>
  /** Turns web push on or off: signals, stock changes and reminders. */
  setAlertPush(enabled: boolean): Promise<void>
  /** The link in an email: turns that kind off for the user whose private token it holds. Rejects with `ExpiredLinkError` for an unknown token. */
  unsubscribe(token: string, kind: 'overview' | 'alerts'): Promise<void>
  getTemplates(): Promise<StrategyTemplate[]>
  /** The template as its active version, or as `version` (a strategy's own, for its settings screen). */
  getTemplate(key: TemplateKey, version?: number): Promise<TemplateDetail | undefined>
  /** Rejects with `UnauthorizedError` when the session has ended (also for `startStrategy`). */
  getDashboard(): Promise<Dashboard>
  /** The daily summary for the dedicated daily summary page. */
  getSummary(): Promise<DailySummary>
  /** The strategy page; undefined when the user has no such strategy. */
  getStrategy(id: string): Promise<StrategyDetail | undefined>
  /** A stock's last 90 days with each day's rule checks, oldest first; undefined when it isn't in the user's strategy. */
  getStockChart(strategyId: string, assetId: string): Promise<StockChart | undefined>
  /** Updates the settings (rules) of a running strategy. */
  updateStrategySettings(id: string, settings: Record<string, number>): Promise<void>
  /** Edit list: the stocks a running strategy follows from now on. Rejects with `InvalidFieldsError` (`assetIds`). */
  updateStrategyAssets(id: string, assetIds: string[]): Promise<void>
  /** A live signal for Confirm a signal; undefined when the user has no such signal. */
  getSignal(id: string): Promise<SignalDetail | undefined>
  /** Rejects with `InvalidFieldsError` (by field) or `InvalidValueError` (already answered). */
  confirmSignal(id: string, trade: SignalTrade): Promise<void>
  /** Log a trade by hand on one of the strategy's stocks (marked Manual). Rejects with `InvalidFieldsError`, `InvalidValueError`. */
  logManualTrade(strategyId: string, trade: ManualTradeRequest): Promise<StrategyTradeItem>
  /** Edit a trade's date, price, amount/shares, fee, or rate. Rejects with `InvalidFieldsError`, `InvalidValueError`. */
  updateTrade(tradeId: string, trade: UpdateTradeRequest): Promise<void>
  /** Soft-deletes a live trade. */
  deleteTrade(tradeId: string): Promise<void>
  skipSignal(id: string, reason: SkipReason | null): Promise<void>
  /** Resolves with the day the user is reminded. */
  snoozeSignal(id: string): Promise<string>
  /** Stored and new stocks or funds by name or symbol. Rejects with `InvalidValueError` (its message is for the user), `TooManyTriesError`. */
  searchAssets(query: string, kind: 'stock' | 'fund'): Promise<AssetMatch[]>
  /** Adds a search result that has no id yet and loads its prices. Rejects as `searchAssets` does, with `ForbiddenError` before the email is confirmed, and with `InvalidValueError` (the server's words) at the user's limit of new ones. */
  addAsset(symbol: string, kind: 'stock' | 'fund'): Promise<AddedAsset>
  /** Starts a strategy. Rejects with `NameTakenError` when the user already runs one with that name. */
  startStrategy(strategy: NewStrategy): Promise<StrategySummary>
  /** Deletes an archived strategy for good, with its signals, trades, results and AI rounds. One already gone counts as deleted. */
  deleteStrategy(id: string): Promise<void>
  /** Rejects with `NameTakenError` when another running strategy has the name, `InvalidFieldsError` for a wrong length. */
  renameStrategy(id: string, name: string): Promise<void>
  /** The VAPID key to subscribe with; null when the server doesn't send push. */
  getPushKey(): Promise<string | null>
  /** Saves this device's push subscription for the logged-in user. */
  savePushSubscription(subscription: PushSubscriptionJson): Promise<void>
  /** Forgets this device's push subscription. */
  removePushSubscription(endpoint: string): Promise<void>
  /** Archives a running strategy: the daily check stops, its waiting signals become missed and its name is free. Rejects with `InvalidValueError` when it is archived already. */
  archiveStrategy(id: string): Promise<void>
  /** Admin page, for the owner. Each rejects with `UnauthorizedError` when not logged in, `ForbiddenError` for any other role, `TooManyTriesError` when rate-limited. */
  getAdminSettings(): Promise<AdminSetting[]>
  getAdminInvites(): Promise<AdminInvite[]>
  /** Rejects with `InvalidFieldsError` (`firstName`, `lastName`, `email`, `locale`). */
  createAdminInvite(invite: NewInvite): Promise<AdminInvite>
  /** Emails an unused invite again. Rejects with `InvalidValueError` saying why not. */
  sendAdminInvite(id: string): Promise<AdminInvite>
  revokeAdminInvite(id: string): Promise<void>
  /**
   * Rejects with `InvalidValueError` (its message is for the user) when the value is refused. A secret needs `shown`:
   * the values the page shows, so the API can refuse it if its mail server changed since (409).
   */
  setAdminSetting(key: string, value: string, shown?: Record<string, string | null>): Promise<void>
  /** Back to the server's value or the default; `clear` leaves no value at all (secrets). */
  resetAdminSetting(key: string, clear?: boolean): Promise<void>
  /** Sends a test email to the mail account itself and resolves with its address; rejects with `InvalidValueError` saying why not. */
  sendAdminTestEmail(): Promise<string>
  getAdminPrices(): Promise<AdminPrices>
  /** Starts a refresh in the background, whatever the schedule says. Rejects with `InvalidValueError` when one is running. */
  refreshAdminPrices(): Promise<void>
  /** Asks Yahoo for a fund and an exchange rate, and stores nothing. Rejects with `TooManyTriesError` after 30 an hour. */
  testAdminYahoo(): Promise<YahooCheck[]>
  /** Public: the API's build and the database it uses. */
  getVersion(): Promise<BackendVersion>
  getAdminDatabases(): Promise<AdminDatabases>
  /** Rejects with `InvalidValueError` saying why when refused (no URL on the server, a database that doesn't answer). */
  switchAdminDatabase(name: DatabaseName): Promise<void>
/** All assets, for the admin assets page. */
  getAdminAssets(): Promise<AdminAsset[]>
  getAdminUsers(): Promise<AdminUsers>
  getAdminNotifications(): Promise<AdminNotifications>
  /** Pushes to every device of the user and resolves with the push as recorded (sent or failed). Rejects with `InvalidFieldsError` (`title`, `body`), `InvalidValueError` saying why not (no push set up, no device), `TooManyTriesError` after 30 an hour. */
  sendAdminTestPush(push: TestPush): Promise<AdminPush>
  /** The open and dismissed alerts, and those cleared in the last 7 days, last seen first. */
  getAdminAlerts(): Promise<AdminAlert[]>
  /** Dismisses an open alert: it stays quiet while its problem lasts. */
  dismissAdminAlert(id: string): Promise<void>
  /** Every template: what new strategies start with, next to the tested values. */
  getAdminTemplates(): Promise<AdminTemplate[]>
  /** Saves a new version and makes it active. Rejects with `InvalidFieldsError` (`settings.<key>`, `assetIds`, `closePct`). */
  saveAdminTemplateVersion(key: string, change: AdminTemplateChange): Promise<AdminTemplate>
  /** Makes a version active: new strategies of the template start with it. */
  activateAdminTemplateVersion(key: string, version: number): Promise<AdminTemplate>
  /** Hides or restores an asset. */
  setAdminAssetActive(id: string, active: boolean): Promise<void>
  /** Adds an asset by its Yahoo symbol and loads its prices. Rejects with `InvalidFieldsError` (`symbol`), or `InvalidValueError` when Yahoo fails. */
  addAdminAsset(symbol: string): Promise<AdminAddedAsset>
  /** Changes an asset's name and English description. Rejects with `InvalidFieldsError` (`name`, `what`). */
  updateAdminAsset(id: string, edit: { name: string; what: string }): Promise<void>
  /** Deletes every stored price and exchange rate. */
  flushAdminPrices(): Promise<PricesFlushed>
  /** Deletes one asset's stored prices. */
  flushAdminAssetPrices(id: string): Promise<void>
  /**
   * Replaces one asset's prices with the daily prices of the last `years` years and returns its row. Rejects with
   * `InvalidFieldsError` (`years`), or `InvalidValueError` when Yahoo fails or the daily refresh is running.
   */
  downloadAdminAssetPrices(id: string, years: number): Promise<AdminAsset>
  /** Replaces the exchange rates of every currency in use with the last `years` years. */
  downloadAdminRates(years: number): Promise<RatesDownloaded>
/** Submits a feedback comment (requires a confirmed email). Rejects with `ForbiddenError`, `InvalidValueError` or `TooManyTriesError`. */
  submitFeedback(feedback: NewFeedback): Promise<FeedbackItem>
  /** Owner only: lists, edits, closes/reopens or deletes feedback comments. */
  getFeedback(status?: FeedbackStatus): Promise<FeedbackItem[]>
  updateFeedback(id: string, patch: FeedbackPatch): Promise<FeedbackItem>
  deleteFeedback(id: string): Promise<void>
  /** Public: the AI template's prompt for these sources, instructions and stocks. Rejects with `InvalidFieldsError`. */
  previewAiPrompt(request: AiPromptRequest): Promise<AiPromptPreview>
  /** The strategy's AI round that is due (or the next one); undefined for anything else. */
  getAiRound(strategyId: string): Promise<AiRoundDetail | undefined>
  /** Reads a pasted answer; saves nothing. */
  readAiAnswer(strategyId: string, answer: string): Promise<AiReading>
  /**
   * "Use these scores", with the prompt as the screen showed it. Rejects with `InvalidFieldsError` (`answer`, `aiModel`) or
   * `InvalidValueError` (not due, done already, or the prompt changed since).
   */
  useAiScores(strategyId: string, answer: string, aiModel: string, share: boolean | null, prompt: string, extra: boolean): Promise<AiRoundResult>
  /** Skips the round that is due. Rejects with `InvalidValueError` when it isn't due or changed meanwhile. */
  skipAiRound(strategyId: string, roundId: string): Promise<void>
  getAiRounds(strategyId: string): Promise<AiRoundHistory | undefined>
  /** A running AI strategy's prompt. Rejects with `InvalidFieldsError` (`sources`, `instructions`). */
  updateAiPrompt(strategyId: string, setup: AiSetup): Promise<void>
  /** Turns sharing AI rounds anonymously on or off. */
  setAiSharing(enabled: boolean): Promise<void>

}
