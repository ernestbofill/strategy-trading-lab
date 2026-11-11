// Captures the real app's screens for the promo, in English and Catalan, at phone size and 3x.
// Run the app locally first (frontend on :5173, API on the dev database), then: npm run capture
import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';

const BASE = process.env.STL_URL ?? 'http://localhost:5173';

// The promo's screen slot -> the app's path. Guest pages only, for now: the signed-in
// screens (dashboard, a waiting signal, an AI round) need a seeded account on dev.
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
  });
  const page = await context.newPage();
  // A link that starts with the language sets it on this device; every page after it loads in that language.
  await page.goto(`${BASE}/${lang}/welcome`, { waitUntil: 'networkidle' });
  await mkdir(`public/screens/${lang}`, { recursive: true });
  for (const [name, path] of Object.entries(SCREENS)) {
    await page.goto(`${BASE}${path}`, { waitUntil: 'networkidle' });
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(600);
    await page.screenshot({ path: `public/screens/${lang}/${name}.png`, fullPage: true });
    console.log(lang, name, page.url());
  }
  await context.close();
}
await browser.close();
