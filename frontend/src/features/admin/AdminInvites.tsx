import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  api,
  InvalidFieldsError,
  InvalidValueError,
  TooManyTriesError,
  UnauthorizedError,
  type AdminInvite,
  type Locale,
} from '../../api'
import { FormMessage, SegmentedField, TextField, type FormMessageState } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { Screen, ScreenMessage, TopBar } from '../../components/Screen'
import styles from './AdminInvites.module.css'

/** The admin setting that gates sign-up (backend Admin/SettingDefinitions.cs). */
const SIGN_UPS = 'SIGN_UPS'
type SignUps = 'open' | 'invite_only'

const LANGUAGES = [
  { value: 'en', label: 'English' },
  { value: 'ca', label: 'Català' },
]

type State = { status: 'loading' } | { status: 'error' } | { status: 'ready'; invites: AdminInvite[] }

/**
 * Invites (/admin/invites, owner only; spec: Admin, Invites): who can sign up, a new invite for one person in their
 * language (emailed, or a message to copy into WhatsApp or anywhere), and the invites made so far.
 */
export function AdminInvites() {
  const [state, setState] = useState<State>({ status: 'loading' })
  const [created, setCreated] = useState<{ invite: AdminInvite; sent: boolean } | null>(null)

  const load = useCallback(async () => setState(await fetchInvites()), [])

  useEffect(() => {
    let live = true
    void fetchInvites().then((next) => live && setState(next))
    return () => {
      live = false
    }
  }, [])

  const onCreated = async (invite: AdminInvite, sent: boolean) => {
    setCreated({ invite, sent })
    await load()
  }

  return (
    <Screen top={<TopBar title="STL // INVITES" titleLink="/admin" back="/admin" />}>
      <PageHeader title="Invites" lead="Each invite is for one person and gives the vip role. Only you can invite for now." />
      <SignUpGate />
      <section className={styles.group} aria-labelledby="new-invite">
        <h2 id="new-invite" className={styles.groupTitle}>
          New invite
        </h2>
        {created ? (
          <ShareCard invite={created.invite} sent={created.sent} onAnother={() => setCreated(null)} />
        ) : (
          <NewInviteForm onCreated={onCreated} />
        )}
      </section>
      <section className={styles.group} aria-labelledby="invites-made">
        <h2 id="invites-made" className={styles.groupTitle}>
          Sent so far
        </h2>
        {state.status === 'loading' && <ScreenMessage>Loading the invites…</ScreenMessage>}
        {state.status === 'error' && <ScreenMessage>Could not load the invites. Try again later.</ScreenMessage>}
        {state.status === 'ready' && state.invites.length === 0 && <p className={styles.muted}>No invites yet.</p>}
        {state.status === 'ready' && state.invites.length > 0 && (
          <ul className={styles.list}>
            {state.invites.map((invite) => (
              <InviteRow key={`${invite.id}:${invite.sentAt}:${invite.revokedAt}:${invite.uses}`} invite={invite} onChanged={load} />
            ))}
          </ul>
        )}
      </section>
    </Screen>
  )
}

async function fetchInvites(): Promise<State> {
  try {
    return { status: 'ready', invites: await api.getAdminInvites() }
  } catch {
    return { status: 'error' }
  }
}

/** Who can sign up: anyone, or only people with an invite. */
function SignUpGate() {
  const [saved, setSaved] = useState<SignUps | null>(null)
  const [choice, setChoice] = useState<SignUps>('open')
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let live = true
    void api
      .getAdminSettings()
      .then((settings) => {
        const value = settings.find((s) => s.key === SIGN_UPS)?.value === 'invite_only' ? 'invite_only' : 'open'
        if (!live) return
        setSaved(value)
        setChoice(value)
      })
      .catch(() => live && setMessage({ tone: 'error', text: 'Could not load the setting.' }))
    return () => {
      live = false
    }
  }, [])

  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setMessage(null)
    try {
      await api.setAdminSetting(SIGN_UPS, choice)
      setSaved(choice)
      setMessage({ tone: 'done', text: choice === 'invite_only' ? 'Saved: only people with an invite can sign up.' : 'Saved: anyone can sign up.' })
    } catch (err) {
      setMessage({ tone: 'error', text: err instanceof InvalidValueError ? err.message : "Couldn't save it. Try again." })
    }
    setBusy(false)
  }

  return (
    <section className={styles.group} aria-labelledby="sign-ups">
      <h2 id="sign-ups" className={styles.groupTitle}>
        Who can sign up
      </h2>
      <form className={styles.form} onSubmit={save} noValidate>
        <SegmentedField
          label="Who can sign up"
          hideLabel
          value={choice}
          onChange={(v) => setChoice(v as SignUps)}
          options={[
            { value: 'open', label: 'Anyone' },
            { value: 'invite_only', label: 'Invite only' },
          ]}
        />
        <p className={styles.muted}>
          {choice === 'invite_only'
            ? 'Sign-up asks for an invite code. Invite links fill it in.'
            : 'Anyone can sign up. An invite code still gives the vip role.'}
        </p>
        {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
        <button type="submit" className={`ui-strong ${styles.button}`} disabled={busy || saved === null || choice === saved}>
          {busy ? 'Saving…' : 'Save'}
        </button>
      </form>
    </section>
  )
}

function NewInviteForm({ onCreated }: { onCreated: (invite: AdminInvite, sent: boolean) => Promise<void> }) {
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [locale, setLocale] = useState<Locale>('en')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState<'send' | 'copy' | null>(null)

  const create = async (send: boolean) => {
    if (busy) return
    setBusy(send ? 'send' : 'copy')
    setErrors({})
    setMessage(null)
    try {
      const invite = await api.createAdminInvite({ firstName, lastName, email, locale, send })
      await onCreated(invite, send)
    } catch (err) {
      if (err instanceof InvalidFieldsError) setErrors(err.fields)
      else if (err instanceof TooManyTriesError)
        setMessage({ tone: 'error', text: 'Too many invite emails this hour. Make it without sending and copy the message.' })
      else if (!(err instanceof UnauthorizedError)) setMessage({ tone: 'error', text: "Couldn't make the invite. Try again." })
      setBusy(null)
    }
  }

  return (
    <form
      className={styles.form}
      onSubmit={(e) => {
        e.preventDefault()
        void create(email.trim() !== '')
      }}
      noValidate
    >
      <div className={styles.names}>
        <TextField label="First name" autoCapitalize="words" autoComplete="off" value={firstName} onChange={setFirstName} error={errors.firstName} />
        <TextField label="Last name (optional)" autoCapitalize="words" autoComplete="off" value={lastName} onChange={setLastName} error={errors.lastName} />
      </div>
      <TextField
        label="Email (optional)"
        type="email"
        autoComplete="off"
        value={email}
        onChange={setEmail}
        hint="To send it by email. Without one, copy the message into WhatsApp or anywhere."
        error={errors.email}
      />
      <SegmentedField label="Language of the invite" value={locale} onChange={(v) => setLocale(v as Locale)} options={LANGUAGES} error={errors.locale} />
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
      <div className={styles.actions}>
        <button type="submit" className={`ui-strong ${styles.primary}`} disabled={busy !== null || email.trim() === ''}>
          {busy === 'send' ? 'Sending…' : '✉ Send by email'}
        </button>
        <button type="button" className={`ui-strong ${styles.button}`} disabled={busy !== null} onClick={() => void create(false)}>
          {busy === 'copy' ? 'Making it…' : 'Make it, copy the text'}
        </button>
      </div>
    </form>
  )
}

/** The invite just made: its code, link and message, with ways to pass it on. */
function ShareCard({ invite, sent, onAnother }: { invite: AdminInvite; sent: boolean; onAnother: () => void }) {
  const canShare = typeof navigator !== 'undefined' && typeof navigator.share === 'function'
  return (
    <div className={styles.share}>
      <p className={`ui-strong ${styles.shareEyebrow}`}>{sent ? `✉ Sent to ${invite.email}` : '✦ Ready to send'}</p>
      <h3 className={styles.shareTitle}>Invite for {fullName(invite)}</h3>
      <dl className={styles.facts}>
        <div>
          <dt>Code</dt>
          <dd className={styles.code}>{invite.code}</dd>
        </div>
        <div>
          <dt>Link</dt>
          <dd className={styles.link}>{invite.link}</dd>
        </div>
      </dl>
      <p className={styles.previewLabel}>Message ({languageName(invite.locale)})</p>
      <pre className={styles.preview}>{invite.message}</pre>
      <div className={styles.actions}>
        <CopyButton text={invite.message} label="Copy message" primary />
        <a className={`ui-strong ${styles.button} ${styles.linkButton}`} href={whatsAppUrl(invite.message)} target="_blank" rel="noreferrer">
          WhatsApp
        </a>
        {canShare && (
          <button
            type="button"
            className={`ui-strong ${styles.button}`}
            onClick={() => void navigator.share({ text: invite.message }).catch(() => undefined)}
          >
            Share…
          </button>
        )}
        <CopyButton text={invite.link} label="Copy link" />
      </div>
      <button type="button" className={`ui-strong ${styles.textButton}`} onClick={onAnother}>
        + Another invite
      </button>
    </div>
  )
}

function InviteRow({ invite, onChanged }: { invite: AdminInvite; onChanged: () => Promise<void> }) {
  const [confirming, setConfirming] = useState(false)
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)
  const status = statusOf(invite)
  const usable = status === 'sent' || status === 'waiting'

  const run = async (action: () => Promise<unknown>, done?: string) => {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      if (done) setMessage({ tone: 'done', text: done })
      await onChanged()
    } catch (err) {
      if (err instanceof UnauthorizedError) return
      setMessage({
        tone: 'error',
        text: err instanceof TooManyTriesError ? 'Too many invite emails this hour. Copy the message instead.'
          : err instanceof InvalidValueError ? err.message
            : "Couldn't do it. Try again.",
      })
    }
    setBusy(false)
  }

  return (
    <li className={styles.item}>
      <div className={styles.itemHead}>
        <span className={styles.itemName}>{fullName(invite)}</span>
        <span className={`ui-strong ${styles.status}`} data-status={status}>
          {statusText[status]}
        </span>
      </div>
      <p className={styles.meta}>
        <span className={styles.mono}>{invite.code}</span> · {languageName(invite.locale)} · {invite.createdAt.slice(0, 10)}
        {invite.email && <> · {invite.email}</>}
      </p>
      {invite.joined.map((u) => (
        <p key={u.email} className={styles.meta}>
          Joined {u.joinedAt.slice(0, 10)} as <span className={styles.mono}>{u.email}</span>
        </p>
      ))}
      {usable && (
        <div className={styles.rowActions}>
          <CopyButton text={invite.message} label="Copy message" />
          {invite.email && (
            <button type="button" className={`ui-strong ${styles.textButton}`} disabled={busy}
              onClick={() => void run(() => api.sendAdminInvite(invite.id))}>
              {invite.sentAt ? 'Email again' : 'Email it'}
            </button>
          )}
          {confirming ? (
            <button type="button" className={`ui-strong ${styles.textButton} ${styles.danger}`} disabled={busy}
              onClick={() => void run(() => api.revokeAdminInvite(invite.id))}>
              Sure? Revoke it
            </button>
          ) : (
            <button type="button" className={`ui-strong ${styles.textButton}`} onClick={() => setConfirming(true)}>
              Revoke
            </button>
          )}
        </div>
      )}
      {message && <FormMessage tone={message.tone}>{message.text}</FormMessage>}
    </li>
  )
}

/** Copies the text and says so for a moment. */
function CopyButton({ text, label, primary }: { text: string; label: string; primary?: boolean }) {
  const [copied, setCopied] = useState<boolean | null>(null)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
    } catch {
      setCopied(false)
    }
    setTimeout(() => setCopied(null), 2000)
  }
  return (
    <button type="button" className={`ui-strong ${primary ? styles.primary : styles.button}`} onClick={() => void copy()}>
      <span aria-live="polite">{copied === true ? '✓ Copied' : copied === false ? 'Copy failed' : label}</span>
    </button>
  )
}

type Status = 'joined' | 'revoked' | 'sent' | 'waiting'

const statusText: Record<Status, string> = { joined: 'Joined', revoked: 'Revoked', sent: 'Emailed', waiting: 'Not sent' }

function statusOf(invite: AdminInvite): Status {
  if (invite.uses >= invite.maxUses) return 'joined'
  if (invite.revokedAt) return 'revoked'
  return invite.sentAt ? 'sent' : 'waiting'
}

const languageName = (locale: Locale) => LANGUAGES.find((l) => l.value === locale)?.label

const fullName = (invite: AdminInvite) => [invite.firstName, invite.lastName].filter(Boolean).join(' ')

/** WhatsApp's share link: it opens the app (or WhatsApp Web) with the message, to pick who gets it. */
const whatsAppUrl = (text: string) => `https://wa.me/?text=${encodeURIComponent(text)}`
