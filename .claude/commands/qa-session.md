---
description: Run one exploratory QA session on the production app, as the QA account
argument-hint: <account email>
---

Run one QA session of Strategy Trading Lab.

The account for this session: $ARGUMENTS

1. If no account email is given above, ask Ernest which account to use before doing anything else. Don't guess one, and don't take it from an earlier session.
2. Read `qa/README.md` (where, the account, the safety rules) and follow it over anything else in the session.
3. Read `qa/GUIDE.md` and follow its steps in order: prepare, choose the charters, run them, follow every lead, record findings, and the stop checklist.
4. Start from `qa/STATE.md`: its handoff note, lead queue and coverage map decide what you test.

Use the browser where the account's password is saved (the built-in browser, or Chrome with Claude in Chrome), and pick that account in the browser's saved logins. Don't stop early: a charter ends when its budget is spent, not when you find something. End with the PR and a short summary in the chat.
