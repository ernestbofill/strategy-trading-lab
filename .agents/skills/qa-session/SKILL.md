---
name: qa-session
description: Run one exploratory QA session on the production app as the given account. Use only when Ernest asks for a QA session, e.g. "/qa-session <account email>".
---

Run one QA session of Strategy Trading Lab, as the account whose email Ernest gave with the command.

1. If he gave no account email, ask which account to use before doing anything else. Don't guess one, and don't take it from an earlier session.
2. Read `qa/README.md` (where, the account, the safety rules) and follow it over anything else in the session.
3. Read `qa/GUIDE.md` and follow its steps in order: prepare, choose the charters, run them, follow every lead, record findings, and the stop checklist.
4. Start from `qa/STATE.md`: its handoff note, lead queue and coverage map decide what you test.

Use a browser where that account's password is saved, and pick it from the browser's saved logins. Don't stop early: a charter ends when its budget is spent, not when you find something. End with the PR and a short summary in the chat.
