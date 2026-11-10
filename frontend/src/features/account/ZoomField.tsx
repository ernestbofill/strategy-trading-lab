import { useState } from 'react'
import { SegmentedField } from '../../components/Field'
import { useT } from '../../i18n/I18n'
import { currentUiZoom, setUiZoom, ZOOMS, type UiZoom } from '../../lib/uiZoom'
import styles from './Account.module.css'

/** The zoom choice (Sign-up, Settings). It applies at once and stays on this device, so it needs no save button. */
export function ZoomField({ hideLabel }: { hideLabel?: boolean }) {
  const t = useT()
  // The zoom the page has now, which is the stored one unless storing it failed.
  const [zoom, setZoom] = useState(currentUiZoom)
  const change = (value: string) => {
    setZoom(value as UiZoom)
    setUiZoom(value as UiZoom)
  }

  return (
    <>
      <SegmentedField label={t('account.zoom')} hideLabel={hideLabel} value={zoom} onChange={change} options={[...ZOOMS]} />
      <p className={styles.line}>{t('account.zoom.hint')}</p>
    </>
  )
}
