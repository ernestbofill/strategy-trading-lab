// Captures the real app's screens for the promo, in English and Catalan, at phone size and 3x.
// Run the app locally first (frontend on :5173, API on the dev database), then: npm run capture
// With the demo account's session in .auth/state.json (scripts/seed.mjs), the dashboard, the waiting
// signal and the AI round are the signed-in screens; without it, guest pages stand in for them.
import { chromium } from 'playwright';
import { existsSync } from 'node:fs';
import { mkdir } from 'node:fs/promises';

const BASE = process.env.STL_URL ?? 'http://localhost:5173';
const STATE = '.auth/state.json';
const signedIn = existsSync(STATE);

// The promo's screen slot -> the app's path.
const SCREENS = {
  dashboard: '/welcome',
  templates: '/new',
  tested: '/new/templates/dip/how-tested',
  signal: '/new/templates/dip',
  ai: '/new/templates/ai',
};

const browser = await chromium.launch();
for (const lang of ['en', 'ca']) {
  const context = await browser.newContext({
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 3,
    isMobile: true,
    hasTouch: true,
    reducedMotion: 'reduce',
    storageState: signedIn ? STATE : undefined,
  });
  const page = await context.newPage();
  const screens = { ...SCREENS };
  if (signedIn) {
    // The account's language is the app's language once signed in.
    const headers = { 'X-Requested-With': 'stl' };
    const saved = await context.request.post(`${BASE}/api/account/locale`, { headers, data: { locale: lang } });
    if (!saved.ok()) throw new Error(`Setting the language failed: ${saved.status()} ${await saved.text()}`);
    const dashboard = await (await context.request.get(`${BASE}/api/dashboard`)).json();
    const signal = dashboard.waiting.find((item) => item.kind === 'buySignal');
    const round = dashboard.waiting.find((item) => item.kind === 'aiRound');
    screens.dashboard = '/dashboard';
    if (signal) screens.signal = `/strategies/${signal.strategyId}/signals/${signal.id}`;
    if (round) screens.ai = `/strategies/${round.strategyId}/ai-round`;
  }
  // A link that starts with the language sets it on this device; every page after it loads in that language.
  await page.goto(`${BASE}/${lang}/welcome`, { waitUntil: 'networkidle' });
  await mkdir(`public/screens/${lang}`, { recursive: true });
  for (const [name, path] of Object.entries(screens)) {
    await page.goto(`${BASE}${path}`, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(600);
    await page.screenshot({ path: `public/screens/${lang}/${name}.png`, fullPage: true });
    console.log(lang, name, page.url());
  }
  await context.close();
}
await browser.close();
