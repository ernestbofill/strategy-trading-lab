import { TopBar } from '../../components/Screen'
import { TextAction } from '../../components/TextAction'
import { useT } from '../../i18n/I18n'

/** The admin pages' top bar: DASHBOARD on the left, ADMIN (the /admin page) on the right. */
export function AdminTopBar() {
  const t = useT()
  return <TopBar home aside={<TextAction to="/admin">{t('common.admin')}</TextAction>} />
}
