# QA sessions

An AI agent tests the live app the way an expert manual tester would. Ernest starts each session by hand. One session explores a few areas in depth, writes everything it did and found in a session log, and leaves a handoff note so the next session carries on where it stopped.

Start a session with `/qa-session <account email>`, for example `/qa-session you+qaagent@example.com`. Without an email, the agent asks which account to use. The command exists for Claude Code (`.claude/commands/qa-session.md`) and Antigravity (`.agents/skills/qa-session/SKILL.md`); keep the two in step. Any other tool starts with: "Read `qa/README.md` and run one QA session as <account email>."

The method is the same whichever AI runs it. Where these files name Claude's tools, use your own equivalent.

### From Gemini in Chrome

Gemini in Chrome's auto browse can test the app in your own Chrome, but it can't write to the repo. It follows the same guide in **browser-only mode** (GUIDE, *Browser-only mode*): a shorter session, and the log handed back in the chat. Before the first time, check that *Settings → AI innovations → Gemini in Chrome* has "Let Gemini browse for you", that the account's password is in Google Password Manager, and that this Chrome is signed in to GitHub (so Gemini can open the guide). Then open `https://strategytradinglab.com`, click *Ask Gemini* and paste this, with the account's email filled in:

```text
Run one QA session of the web app Strategy Trading Lab, at https://strategytradinglab.com, as the account <ACCOUNT EMAIL>.

Read these first, in this order, and follow them for the whole session. They are the rules and the method:
1. https://github.com/ernestbofill/strategy-trading-lab/blob/main/qa/README.md (the safety rules come before anything else)
2. https://github.com/ernestbofill/strategy-trading-lab/blob/main/qa/GUIDE.md (follow its "Browser-only mode")
3. https://github.com/ernestbofill/strategy-trading-lab/blob/main/qa/STATE.md (what to test next)
The app's spec, to check screens against: https://github.com/ernestbofill/strategy-trading-lab/blob/main/docs/strategy-trading-lab-app-spec.md

Sign in to the app through Google Password Manager. Never type, show or ask for the password, and never write the account's email in your notes: call the account by the part after "+" in its address. If the sign-in gets stuck, pause and ask me to take over; I'll sign in and give the task back, and you carry on with the session.

Run 2 charters of about 50 actions each. Don't stop when you find a bug: follow it as the guide says, and stop only when the budget is spent or the stop checklist is done. You can't write files, so at the end put the whole session log, and the changes to STATE.md and FINDINGS.md, in this chat as one Markdown block, as the guide's "Browser-only mode" says.
```

When it's done, copy its Markdown block and give it to Claude or Antigravity ("record this QA session"): the agent writes the files from it and opens the session PR, as in *After a session*.

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
- **An account Ernest owns, named when the session starts** (the command's argument; the agent asks when it's missing). Normally the vip QA account. The address isn't kept in the repo. Its password is **saved in the browser** the agent drives: for Claude, the desktop app's built-in browser or Chrome with Claude in Chrome; for Antigravity, the Chrome profile its browser agent opens (which may not be your everyday profile, so save the password there too). The agent never types, reads, logs or asks for the password. When the app shows Log-in, it picks the saved login for that address from the browser's autofill. If it is signed in as a different account, it logs out first. If there is no saved login for it, or the sign-in gets stuck, the agent pauses and asks Ernest to take over and sign in. That isn't the end of the session: once he hands it back, the agent checks it is signed in as the right account and carries on.
- **The account's label.** Logs, `STATE.md` and findings name the account by its label, never by its address: the part after `+` (`qaagent` for `…+qaagent@…`), or else a label Ernest gives. `STATE.md`'s account baseline and fixtures belong to one account: with another account, the agent adds a section for its label and doesn't touch the others' fixtures.
- The account's email is confirmed, so Live mode and feedback work. The vip role has the same rights as a plain user for now. `/admin` is out of scope: the agent may open it once to check it is refused, and nothing more.
- A cloud session without a link to Ernest's computer has no such browser, so it can't run a session. Run it locally on the Mac or the PC (the Claude desktop app, or Antigravity).

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
