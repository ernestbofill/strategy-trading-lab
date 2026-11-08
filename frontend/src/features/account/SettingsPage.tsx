import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { api, InvalidFieldsError, type Currency, type Locale } from '../../api'
import { FormMessage, SegmentedField, SelectField, type FormMessageState } from '../../components/Field'
import { NavRow } from '../../components/NavRow'
import { PageHeader } from '../../components/PageHeader'
import { Screen, TopBar } from '../../components/Screen'
import { Section } from '../../components/Section'
import { AdminLink } from '../admin/AdminTopBar'
import { useI18n, useT } from '../../i18n/I18n'
import { LOCALES } from '../../i18n/locale'
import styles from './Account.module.css'
import { useAccount, useCurrentAccount } from './AccountContext'
import { currencyOptionsWith } from './currencies'
import { errorText } from './copy'
import { APP_HASH, NOTIFICATIONS_HASH, accountPath } from './paths'
import { InstallApp } from './InstallApp'
import { NotificationSettings } from './NotificationSettings'
import { timeZoneOptions } from './timeZones'
import { SendFeedback } from '../feedback/SendFeedback'

/** Settings (spec: Settings): how the app behaves for the user. Who they are and their data are on Account, its first row. */
export function SettingsPage() {
  const t = useT()
  const account = useCurrentAccount()
  const { refresh } = useAccount()
  const { hash } = useLocation()
  const name = [account.firstName, account.lastName].filter(Boolean).join(' ')

  // Deep-linking to a section (e.g. #notifications or #app) scrolls it into view.
  useEffect(() => {
    if (hash) document.getElementById(hash.slice(1))?.scrollIntoView({ block: 'start' })
  }, [hash])

  return (
    <Screen top={<TopBar home aside={account.role === 'owner' ? <AdminLink /> : null} />}>
      <PageHeader title={t('settings.page.title')} />

      <NavRow
        to={accountPath}
        label={
          <>
            {name || account.email}
            <span className={`${styles.line} ${styles.block}`}>{t('settings.page.accountLine')}</span>
          </>
        }
      />

      <Section id={APP_HASH.slice(1)} title={t('account.page.app')}>
        <InstallApp />
      </Section>

      <Section title={t('account.page.language')}>
        <LanguageForm current={account.locale} onSaved={refresh} />
      </Section>

      <Section title={t('account.page.currency')}>
        <CurrencyForm current={account.currency} onSaved={refresh} />
      </Section>

      <Section title={t('account.page.timeZone')}>
        <TimeZoneForm current={account.timeZone} onSaved={refresh} />
      </Section>

      <Section id={NOTIFICATIONS_HASH.slice(1)} title={t('account.page.notifications')}>
        <NotificationSettings account={account} onSaved={refresh} />
      </Section>

      <Section title={t('feedback.send.title')}>
        <SendFeedback />
      </Section>
    </Screen>
  )
}

type Saved = () => Promise<unknown>

/** Saves one setting: a select or segmented field, a "Saved." line and a save button, enabled once it changed. */
function useSave<T>(current: T, save: (value: T) => Promise<void>, onSaved: Saved, field: string, savedText: string) {
  const [value, setValue] = useState(current)
  const [message, setMessage] = useState<FormMessageState>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setMessage(null)
    try {
      await save(value)
      if ((await onSaved()) === undefined) throw new Error('Could not reload the account.')
      setMessage({ tone: 'done', text: savedText })
    } catch (err) {
      setMessage({ tone: 'error', text: (err instanceof InvalidFieldsError && err.fields[field]) || errorText(err) })
    }
    setBusy(false)
  }

  return { value, setValue, message, busy, submit, changed: value !== current }
}

/** The shell of one setting's form: its field(s) as children, then the "Saved." line and the save button. */
function SettingForm({ form, saveLabel, children }: { form: Omit<ReturnType<typeof useSave>, 'value' | 'setValue'>; saveLabel: string; children: ReactNode }) {
  const t = useT()
  return (
    <form className={styles.form} onSubmit={form.submit} noValidate>
      {children}
      {form.message && <FormMessage tone={form.message.tone}>{form.message.text}</FormMessage>}
      <button type="submit" className={`ui-strong ${styles.button}`} disabled={form.busy || !form.changed}>
        {form.busy ? t('account.saving') : saveLabel}
      </button>
    </form>
  )
}

function LanguageForm({ current, onSaved }: { current: Locale; onSaved: Saved }) {
  const t = useT()
  const { setLocale } = useI18n()
  const save = async (locale: Locale) => {
    await api.setLocale(locale)
    setLocale(locale)
  }
  const form = useSave(current, save, onSaved, 'locale', t('account.page.languageSaved'))

  return (
    <SettingForm form={form} saveLabel={t('account.page.languageSave')}>
      <SegmentedField label={t('account.page.language')} hideLabel value={form.value} onChange={(v) => form.setValue(v as Locale)} options={LOCALES} />
    </SettingForm>
  )
}

function CurrencyForm({ current, onSaved }: { current: Currency; onSaved: Saved }) {
  const t = useT()
  const form = useSave(current, api.setCurrency, onSaved, 'currency', t('account.page.currencySaved'))

  return (
    <SettingForm form={form} saveLabel={t('account.page.currencySave')}>
      <SegmentedField label={t('account.page.currency')} hideLabel value={form.value} onChange={(v) => form.setValue(v as Currency)} options={currencyOptionsWith(t)} />
    </SettingForm>
  )
}

function TimeZoneForm({ current, onSaved }: { current: string; onSaved: Saved }) {
  const t = useT()
  const options = useMemo(() => timeZoneOptions(current), [current])
  const form = useSave(current, api.setTimeZone, onSaved, 'timeZone', t('account.page.timeZoneSaved'))

  return (
    <SettingForm form={form} saveLabel={t('account.page.timeZoneSave')}>
      <SelectField label={t('account.page.timeZone')} hideLabel value={form.value} onChange={form.setValue} options={options} />
      <p className={styles.line}>{t('account.page.timeZoneHint')}</p>
    </SettingForm>
  )
}
