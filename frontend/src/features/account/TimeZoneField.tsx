import { useMemo } from 'react'
import { SelectField } from '../../components/Field'
import { timeZoneOptions } from './timeZones'

interface TimeZoneFieldProps {
  label: string
  /** The zone saved (or guessed), which the list always offers, whatever row is picked since. */
  saved: string
  value: string
  onChange: (zone: string) => void
  error?: string | null
  hideLabel?: boolean
}

/** The one time zone list ("GMT+1 · Barcelona, Paris, Copenhagen"), on Sign-up, Settings and the admin page. */
export function TimeZoneField({ saved, ...field }: TimeZoneFieldProps) {
  const options = useMemo(() => timeZoneOptions(saved), [saved])
  return <SelectField {...field} options={options} />
}
