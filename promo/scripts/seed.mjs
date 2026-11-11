// Makes the promo's demo account on the DEV database, through the app's own API, and keeps its session
// in .auth/state.json (gitignored) for capture.mjs. The password is random and kept nowhere.
//   node scripts/seed.mjs signup       -> the account (then confirm its email in the dev database)
//   node scripts/seed.mjs strategies   -> its three strategies
// The signal, trades and AI answer are rows written by hand afterwards: see promo/README.md.
import { request } from 'playwright';
import { randomBytes } from 'node:crypto';
import { existsSync } from 'node:fs';
import { mkdir } from 'node:fs/promises';

const BASE = process.env.STL_URL ?? 'http://localhost:5173';
const STATE = '.auth/state.json';
const step = process.argv[2];

const version = await (await fetch(`${BASE}/api/version`)).json();
if (version.database !== 'dev') throw new Error(`Not the dev database: ${version.database}`);

const api = await request.newContext({
  baseURL: BASE,
  extraHTTPHeaders: { 'X-Requested-With': 'stl' },
  storageState: existsSync(STATE) ? STATE : undefined,
});
const check = async (response) => {
  if (!response.ok()) throw new Error(`${response.url()} ${response.status()} ${await response.text()}`);
  return response;
};

if (step === 'signup') {
  const day = new Date().toISOString().slice(0, 10).replaceAll('-', '');
  const email = `claude-promo-${day}@stl.invalid`;
  await check(
    await api.post('/api/account/sign-up', {
      data: {
        email,
        password: randomBytes(18).toString('base64url') + 'aA1!',
        locale: 'en',
        timeZone: 'Europe/Madrid',
        currency: 'EUR',
        firstName: 'Alex',
        lastName: 'Demo',
        disclaimerAccepted: true,
      },
    }),
  );
  await mkdir('.auth', { recursive: true });
  await api.storageState({ path: STATE });
  console.log('signed up', email);
} else if (step === 'strategies') {
  const wanted = [
    { key: 'dip', mode: 'simulation' },
    { key: 'comeback', mode: 'live' },
    { key: 'ai', mode: 'live', ai: { news: true, mood: true, figures: true, instructions: null } },
  ];
  for (const { key, mode, ai } of wanted) {
    const template = await (await check(await api.get(`/api/templates/${key}`))).json();
    const list = template.watches.starterList ?? [];
    const created = await check(
      await api.post('/api/strategies', {
        data: {
          templateKey: key,
          name: template.name,
          mode,
          budget: template.defaults.budget,
          assetIds: list.map((asset) => asset.id),
          ai,
        },
      }),
    );
    console.log(key, (await created.json()).id);
  }
} else {
  throw new Error('Say which step: signup or strategies');
}
await api.dispose();
