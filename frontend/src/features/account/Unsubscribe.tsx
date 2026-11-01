import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, ExpiredLinkError } from '../../api'
import { FormMessage } from '../../components/Field'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, TopBar } from '../../components/Screen'
import { useT } from '../../i18n/I18n'
import { homePath } from '../new-strategy/paths'
import { accountNotificationsPath } from './paths'

type State = 'ask' | 'busy' | 'done' | 'invalid' | 'failed'

/**
 * The link in the emails ("Turn off the daily overview"). Opening it changes nothing, because mail scanners open every link:
 * the one button does it. It needs no log-in; the token in the address is the whole credential, and it can only turn emails off.
 */
export function Unsubscribe() {
  const t = useT()
  const { token = '', kind = '' } = useParams()
  const [state, setState] = useState<State>('ask')
  const known = kind === 'overview' || kind === 'alerts'
  const result: State = known ? state : 'invalid'

  const turnOff = async () => {
    if (state === 'busy') return
    setState('busy')
    try {
      await api.unsubscribe(token, kind as 'overview' | 'alerts')
      setState('done')
    } catch (err) {
      setState(err instanceof ExpiredLinkError ? 'invalid' : 'failed')
    }
  }

  const action =
    result === 'ask' || result === 'busy' || result === 'failed' ? (
      <PrimaryAction onClick={() => void turnOff()} disabled={result === 'busy'}>
        {t('account.unsubscribe.button')}
      </PrimaryAction>
    ) : (
      <PrimaryAction to={result === 'done' ? accountNotificationsPath : homePath}>
        {result === 'done' ? t('account.unsubscribe.goAccount') : t('account.unsubscribe.goApp')}
      </PrimaryAction>
    )

  return (
    <Screen top={<TopBar title={t('account.unsubscribe.topBar')} />} action={action}>
      <PageHeader title={result === 'done' ? t('account.unsubscribe.titleDone') : t('account.unsubscribe.title')} />
      {(result === 'ask' || result === 'busy' || result === 'failed') && (
        <p>{kind === 'alerts' ? t('account.unsubscribe.askAlerts') : t('account.unsubscribe.askOverview')}</p>
      )}
      {result === 'done' && <p>{kind === 'alerts' ? t('account.unsubscribe.doneAlerts') : t('account.unsubscribe.doneOverview')}</p>}
      {result === 'invalid' && <FormMessage tone="error">{t('account.unsubscribe.invalid')}</FormMessage>}
      {result === 'failed' && <FormMessage tone="error">{t('account.unsubscribe.failed')}</FormMessage>}
    </Screen>
  )
}
