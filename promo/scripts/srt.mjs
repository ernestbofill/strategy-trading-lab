// Writes the caption tracks (out/promo-en.srt, out/promo-ca.srt) from the strings and the timeline.
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const FPS = 30;
const json = async (path) => JSON.parse(await readFile(new URL(path, import.meta.url), 'utf8'));
const stamp = (frame) => {
  const ms = Math.round((frame / FPS) * 1000);
  const pad = (n, size = 2) => String(n).padStart(size, '0');
  return `${pad(Math.floor(ms / 3600000))}:${pad(Math.floor(ms / 60000) % 60)}:${pad(Math.floor(ms / 1000) % 60)},${pad(ms % 1000, 3)}`;
};
const sentence = (lines) => {
  const text = lines.join(' ').toLowerCase();
  return text.charAt(0).toUpperCase() + text.slice(1).replace(/\bai\b/g, 'AI').replace(/\bia\b/g, 'IA');
};

const timeline = await json('../src/timeline.json');
await mkdir('out', { recursive: true });
for (const lang of ['en', 'ca']) {
  const strings = await json(`../src/strings/${lang}.json`);
  const cues = Object.entries(timeline).sort((a, b) => a[1][0] - b[1][0]);
  const srt = cues.map(([key, [from]], i) => {
    // A cue ends when the next one starts, so two lines are never on screen at once.
    const to = Math.min(timeline[key][1], cues[i + 1]?.[1][0] ?? Infinity);
    return `${i + 1}\n${stamp(from)} --> ${stamp(to)}\n${sentence(strings[key])}\n`;
  });
  await writeFile(`out/promo-${lang}.srt`, srt.join('\n'));
  console.log(`out/promo-${lang}.srt`);
}
