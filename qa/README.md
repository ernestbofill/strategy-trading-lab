# QA sessions

An AI agent tests the live app the way an expert manual tester would. Ernest starts each session by hand. One session explores a few areas in depth, writes everything it did and found in a session log, and leaves a handoff note so the next session carries on where it stopped.

Start a session with `/qa-session` in Claude Code. Another tool starts with: "Read `qa/README.md` and run one QA session."

## Files

| File | What it is | Who writes it |
| --- | --- | --- |
| [README.md](README.md) | How to start, the account, the safety rules | Ernest and Claude, when the setup changes |
| [GUIDE.md](GUIDE.md) | The method: charters, techniques, oracles, following leads, when to stop | Same |
| [STATE.md](STATE.md) | **Read first.** Coverage map, lead queue, fixtures, account baseline, handoff | The agent, at the end of every session |
| [FINDINGS.md](FINDINGS.md) | Every finding, with an ID, severity, steps and status | The agent; Ernest moves real ones to GitHub issues |
| [sessions/](sessions/) | One log per session (`YYYY-MM-DD-N.md`), from [_TEMPLATE.md](sessions/_TEMPLATE.md) | The agent, during the session; never edited afterwards |

## Where and as whom

- **Production**, at `https://strategytradinglab.com`. It's what users see. The agent only does what a user can do.
- **A vip account** that Ernest owns. Its password is **saved in the browser** the agent drives: the built-in browser of the Claude desktop app, or Chrome with Claude in Chrome. The agent never types, reads, logs or asks for the password. When the app shows Log-in, it picks the saved login from the browser's autofill. If there is no saved login, or autofill doesn't fill it, the agent stops and asks Ernest to sign in.
- The account's email is confirmed, so Live mode and feedback work. The vip role has the same rights as a plain user for now. `/admin` is out of scope: the agent may open it once to check it is refused, and nothing more.
- A cloud session without a link to Ernest's computer has no such browser, so it can't run a session. Run it from the desktop app on the Mac or the PC.

## Safety rules

This is production, with real emails and a real database. These rules come before any charter.

**Never:**

- change the password or the email, log out other sessions, or delete the account;
- sign up a new account, or submit Sign-up (fill and check the form only);
- try wrong passwords on the QA account (failed log-ins lock it). Test a wrong log-in only with `qa-nobody@stl.invalid`, at most twice a session;
- send invites, or ask for password-reset emails for any address but `qa-nobody@stl.invalid`;
- touch strategies, trades or signals whose name doesn't start with `QA ` (read them, don't change them);
- load test, fuzz, script requests at the API, or try to get round access control by anything but typing an address in the browser;
- paste personal data, the account email, cookies or tokens into a log, finding or PR;
- leave the account's settings changed (see *Account baseline* in `STATE.md`: restore them before the session ends).

**Allowed, with care:**

- Create strategies named `QA …`, in both modes. Running strategies are capped at 10. Keep the fixtures (see `STATE.md`), and archive and delete throwaway ones before the session ends.
- Answer live signals, log, edit and delete trades, and run AI rounds on `QA` strategies. The app never reaches a broker, so nothing is traded.
- For an AI round, the agent writes the AI's answer itself (including broken ones on purpose). **Decline AI data sharing**, so made-up answers never reach the shared AI data.
- Send in-app feedback at most once a session, starting the text with `QA test, ignore:`. It emails Ernest.
- Switch the language, the currency or the notification boxes, as long as they are restored afterwards. "Send the link again" sends a real email to Ernest: at most once a session.
- Allow notifications on the device. Push alerts reach Ernest's devices, so keep it to `QA` strategies.

## After a session

The agent works on a branch `qa/<yyyy-mm-dd>-<n>`, commits the session log with `STATE.md` and `FINDINGS.md`, pushes it and opens a docs-only PR titled `QA session <yyyy-mm-dd>-<n>`. CI skips docs-only PRs. Ernest reads the log, moves the findings worth fixing to GitHub issues (or asks Claude to fix them), and merges it. The next session starts from `main`, so **merge one session's PR before starting the next**.
