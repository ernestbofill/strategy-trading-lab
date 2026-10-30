import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { api, InvalidFieldsError, type Currency, type Locale, type SignUpOptions } from '../../api'
import { Checkbox, FormMessage, PasswordField, SegmentedField, SelectField, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useI18n, useT } from '../../i18n/I18n'
import { LOCALES } from '../../i18n/locale'
import { currencyOptionsWith } from './currencies'
import { dashboardPath, pickPath } from '../new-strategy/paths'
import styles from './Account.module.css'
import { useAccount } from './AccountContext'
import { errorText } from './copy'
import { InviteWelcome } from './InviteWelcome'
import { INVITE_PARAM, logInPath } from './paths'
import { guessTimeZone, timeZones } from './timeZones'

/**
 * Sign-up (spec: Sign-up). A new account is logged in at once and goes to Pick a template. Opened from an invite link
 * (`?invite=CODE`) it greets the invitee and fills in what the invite knows; while sign-ups are invite only it asks for a code.
 */
export function SignUp() {
  const t = useT()
  const { locale: currentAppLocale, setLocale: setAppLocale } = useI18n()
  const { state, refresh } = useAccount()
  const [params] = useSearchParams()
  const linkCode = params.get(INVITE_PARAM)?.trim() ?? ''
  const [options, setOptions] = useState<SignUpOptions | null>(null)
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [inviteCode, setInviteCode] = useState(linkCode)
  const [showCode, setShowCode] = useState(linkCode !== '')
  const [locale, setLocale] = useState<Locale>(currentAppLocale)
  const [currency, setCurrency] = useState<Currency>('EUR')
  const [timeZone, setTimeZone] = useState(guessTimeZone)
  const [overviewEmail, setOverviewEmail] = useState(true)
  const [accepted, setAccepted] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)
  const [signedUp, setSignedUp] = useState(false)
  // Built once: the list holds the guessed zone, and any zone picked comes from it.
  const zones = useMemo(() => timeZones(guessTimeZone()).map((z) => ({ value: z, label: z.replaceAll('_', ' ') })), [])
  const currOptions = useMemo(() => currencyOptionsWith(t), [t])

  // Asked once: whether a code is needed, and who the link's invite is for. Without an answer the form still works.
  useEffect(() => {
    let live = true
    api
      .getSignUpOptions(linkCode || undefined)
      .then((loaded) => {
        if (!live) return
        setOptions(loaded)
        const invite = loaded.invite
        // A dead code from the link would block a sign-up that needs none: clear it.
        if (!invite) return setInviteCode((code) => (code === linkCode ? '' : code))
        setFirstName((typed) => typed || invite.firstName) // Never over what was typed while it loaded.
        setInviteCode(invite.code)
        setLocale(invite.locale)
        setAppLocale(invite.locale)
      })
      .catch(() => live && setShowCode(true))
    return () => {
      live = false
    }
  }, [linkCode, setAppLocale])

  if (state.status === 'ready' && state.account) return <Navigate to={signedUp ? pickPath : dashboardPath} replace />

  const invite = options?.invite ?? null
  const linkFailed = linkCode !== '' && options !== null && invite === null
  const codeNeeded = options?.inviteOnly === true && invite === null

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy || !accepted) return // Enter while the button is off.
    setBusy(true)
    setErrors({})
    setMessage(null)
    try {
      await api.signUp({
        firstName, lastName, email, password, locale, timeZone, currency, overviewEmail, disclaimerAccepted: accepted,
        inviteCode: inviteCode.trim() || undefined,
      })
      setSignedUp(true)
      // A new account is logged in now; an email that already had one isn't, and gets the same answer.
      const account = await refresh()
      if (account === null) setMessage({ tone: 'done', text: t('account.signUp.checkEmail') })
      else if (account === undefined) setMessage({ tone: 'error', text: errorText(null) })
    } catch (err) {
      if (err instanceof InvalidFieldsError) {
        setErrors(err.fields)
        // The link's invite was used or revoked since the page loaded: drop the welcome and let them type another code.
        if (err.fields.inviteCode) {
          setShowCode(true)
          setOptions((o) => o && { ...o, invite: null })
        }
      } else setMessage({ tone: 'error', text: errorText(err) })
    }
    setBusy(false)
  }

  const onLocaleChange = (v: string) => {
    const next = v as Locale
    setLocale(next)
    setAppLocale(next)
  }

  return (
    <Screen
      top={<TopBar title={t('account.signUp.topBar')} />}
      action={
        <PrimaryAction form="sign-up" disabled={busy || !accepted}>
          {busy ? t('account.signUp.submitting') : t('account.signUp.submit')}
        </PrimaryAction>
      }
      note={accepted ? undefined : t('account.signUp.note')}
    >
      {invite ? <InviteWelcome invite={invite} /> : <PageHeader title={t('account.signUp.title')} lead={t('account.signUp.lead')} />}
      {linkFailed && <FormMessage tone="error">{t('account.invite.notValid')}</FormMessage>}
      {codeNeeded && !linkFailed && <p className={styles.line}>{t('account.signUp.inviteOnly')}</p>}
      <form id="sign-up" className={styles.form} onSubmit={submit} noValidate>
        {!invite && (showCode || codeNeeded) && (
          <TextField
            label={t('account.signUp.inviteCode')}
            autoComplete="off"
            autoCapitalize="characters"
            placeholder="ABCD-EFGH"
            value={inviteCode}
            onChange={setInviteCode}
            hint={t('account.signUp.inviteHint')}
            error={errors.inviteCode}
          />
        )}
        <div className={styles.names}>
          <TextField
            label={t('account.field.firstName')}
            autoComplete="given-name"
            autoCapitalize="words"
            value={firstName}
            onChange={setFirstName}
            error={errors.firstName}
          />
          <TextField
            label={t('account.field.lastName')}
            autoComplete="family-name"
            autoCapitalize="words"
            value={lastName}
            onChange={setLastName}
            error={errors.lastName}
          />
        </div>
        <TextField label={t('account.field.email')} type="email" autoComplete="username" value={email} onChange={setEmail} error={errors.email} />
        <PasswordField
          label={t('account.field.password')}
          autoComplete="new-password"
          value={password}
          onChange={setPassword}
          hint={t('account.passwordHint')}
          error={errors.password}
        />
        <SegmentedField
          label={t('account.signUp.language')}
          value={locale}
          onChange={onLocaleChange}
          options={LOCALES}
          error={errors.locale}
        />
        <SegmentedField
          label={t('account.signUp.currency')}
          value={currency}
          onChange={(v) => setCurrency(v as Currency)}
          options={currOptions}
          error={errors.currency}
        />
        <SelectField label={t('account.signUp.timeZone')} value={timeZone} onChange={setTimeZone} options={zones} error={errors.timeZone} />
        <Checkbox checked={overviewEmail} onChange={setOverviewEmail}>
          {t('account.overviewEmail')}
        </Checkbox>
        <Checkbox checked={accepted} onChange={setAccepted} error={errors.disclaimerAccepted}>
          {t('account.signUp.accept', { disclaimer: t('common.disclaimerFull') })}
        </Checkbox>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      </form>
      <div className={styles.links}>
        {!invite && !showCode && !codeNeeded && (
          <TextAction onClick={() => setShowCode(true)}>{t('account.signUp.haveInvite')}</TextAction>
        )}
        <TextAction to={logInPath}>{t('account.signUp.haveAccount')}</TextAction>
      </div>
    </Screen>
  )
}
