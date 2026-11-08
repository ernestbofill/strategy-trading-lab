import { TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'

/** "ADMIN", the link to /admin on the right of the admin pages' and the owner's account top bar. */
export function AdminLink() {
  const t = useT()
  return <TextAction to="/admin">{t('common.admin')}</TextAction>
}

/** The admin pages' top bar: DASHBOARD on the left, ADMIN (the /admin page) on the right. */
export function AdminTopBar() {
  return <TopBar home aside={<AdminLink />} />
}
