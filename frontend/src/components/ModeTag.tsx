import { Tag } from './Tag'

/** A strategy's mode tag: SIM or MANUAL (the code's `live` mode). The same word in every language. */
export function ModeTag({ live }: { live: boolean }) {
  return <Tag tone={live ? 'live' : 'sim'}>{live ? 'MANUAL' : 'SIM'}</Tag>
}
