import { useNavigate } from 'react-router-dom'
import { Callout } from '../../components/Callout'
import { ChoiceCard, ChoiceList } from '../../components/ChoiceCard'
import { PageHeader } from '../../components/PageHeader'
import { PrimaryAction, Screen, ScreenMessage, TopBar } from '../../components/Screen'
import { Tag, type TagTone } from '../../components/Tag'
import styles from './ChooseMode.module.css'
import { useNewStrategyDraft, type Mode } from './NewStrategyDraft'
import { budgetPath, reviewPath } from './paths'
import { useTemplate } from './useTemplate'

const MODES: { id: Mode; tag: TagTone; name: string; line: string; facts: [label: string, text: string][] }[] = [
  {
    id: 'simulation',
    tag: 'sim',
    name: 'Simulation',
    line: 'Test it on paper. No real money.',
    facts: [
      ['Who trades:', 'the app, on paper, every time the rules say so.'],
      ['The app:', "records each trade at the next day's opening price."],
      ['You:', 'nothing. Read the daily overview.'],
    ],
  },
  {
    id: 'live',
    tag: 'live',
    name: 'Live',
    line: 'Follow it with real money at your own broker.',
    facts: [
      ['Who trades:', 'you, at your broker, if and when you choose.'],
      ['The app:', 'alerts you to each signal and records what you confirm.'],
      ['You:', 'confirm each signal: I did it, or I skipped it.'],
    ],
  },
]

/** Live signals arrive by web push; email is the fallback if this is refused or unsupported. */
async function askForNotifications() {
  try {
    if ('Notification' in window && Notification.permission === 'default') await Notification.requestPermission()
  } catch {
    // Some browsers (iPhone outside the home screen) don't support it: email stays on.
  }
}

/** 3 · Choose the mode (wireframe `Mode`). */
export function ChooseMode() {
  const { mode, templateKey, update } = useNewStrategyDraft()
  const template = useTemplate(templateKey)
  const navigate = useNavigate()
  const isLive = mode === 'live'

  const onContinue = async () => {
    if (isLive) await askForNotifications()
    navigate(budgetPath)
  }

  return (
    <Screen
      top={<TopBar back={reviewPath(templateKey)} step="3 / 5" />}
      action={<PrimaryAction onClick={onContinue}>Continue</PrimaryAction>}
      note={
        isLive && (
          <span className={styles.inkNote}>
            The app never trades for you or connects to your broker. It records only what you confirm.
          </span>
        )
      }
      noteAbove
    >
      <PageHeader title="How do you want to run it?" lead="You can start a live copy of a simulation later." />

      <ChoiceList label="Mode">
        {MODES.map((m) => (
          <ChoiceCard
            key={m.id}
            name="mode"
            value={m.id}
            checked={mode === m.id}
            onSelect={() => update({ mode: m.id })}
            badge={<Tag tone={m.tag}>{m.tag.toUpperCase()}</Tag>}
            title={m.name}
          >
            <span className={styles.line}>{m.line}</span>
            <span className={styles.facts}>
              {m.facts.map(([label, text]) => (
                <span key={label}>
                  <span className={styles.label}>{label}</span> {text}
                </span>
              ))}
            </span>
          </ChoiceCard>
        ))}
      </ChoiceList>

      {template.status === 'ready' && !template.data.backtest && (
        <ScreenMessage>
          {template.data.name} has no backtest, in either mode: results start on the day the strategy starts.
        </ScreenMessage>
      )}

      {isLive && (
        <Callout tone="info" title="Alerts">
          <p>
            Your browser will ask to allow notifications, so signals reach you right after the market close. Email
            alerts are on too, as a fallback. On iPhone, add the app to your home screen first.
          </p>
        </Callout>
      )}
    </Screen>
  )
}
