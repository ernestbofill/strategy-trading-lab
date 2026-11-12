import { loadFont } from '@remotion/google-fonts/FiraCode';
import en from './strings/en.json';
import ca from './strings/ca.json';
import timeline from './timeline.json';

// Colours from design-system/cyberpunk/tokens.json (the Cyberpunk theme).
export const C = {
  deep: '#100d23',
  editor: '#261d45',
  sidebar: '#372963',
  primary: '#00ff9c',
  accent: '#00ffc8',
  violet: '#721bf2',
  lilac: '#b08bff',
  pink: '#ff4081',
  danger: '#ff004c',
  info: '#00c3ff',
  ink: '#ffffff',
  muted: '#7877b3',
};

export const W = 1080;
export const H = 1920;
export const FPS = 30;
export const BEAT = 15; // 120 BPM
export const DURATION = 780;

// Reels and Shorts cover the top, the bottom and the right edge with their own UI.
export const SAFE = { top: 250, bottom: 400, left: 70, right: 150 };

export const { fontFamily: FONT } = loadFont('normal', {
  weights: ['400', '700'],
  subsets: ['latin', 'latin-ext'],
});

export type Lang = 'en' | 'ca';
export type Strings = typeof en;
export const STRINGS: Record<Lang, Strings> = { en, ca };

export type CueKey = keyof typeof timeline;
export const CUES = timeline as unknown as Record<CueKey, [number, number]>;

// Cinematic clips in public/cine/ (not in git): the take each shot uses, and the frame of the clip it starts on.
// A shot set to null plays its coded fallback.
export type Clip = { file: string; start: number };
export const CINE: Record<'a' | 'b' | 'c', Clip | null> = {
  a: { file: 'a', start: 15 }, // 0.5 s: the face holds still, the reflections flicker
  b: { file: 'b2', start: 90 }, // 3.0 s: the camera climbs past a tower onto the skyline
  c: { file: 'c1', start: 45 }, // 1.5 s: the phone goes from dark to a green flash
};
