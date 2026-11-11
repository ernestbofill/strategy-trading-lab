import React from 'react';
import { AbsoluteFill, random, useCurrentFrame } from 'remotion';
import { ci, sp } from '../anim';
import { EndCard } from '../components/EndCard';
import { Glitch, Grade, Pulse as Rings, Shake } from '../components/Fx';
import { Lines } from '../components/Lines';
import { C, FONT, STRINGS, W } from '../theme';
import { Draw } from './Draw';
import { Cam, P2, ecg, flare, glowLine, noise1, project } from './engine';
import t from './strings.json';
import timeline from './timeline.json';

// The second video, "Pulse" (docs/promo/storyboard.md): a heartbeat line that turns into rules. No app screens and
// no footage: every picture is drawn here. English only.

export const PULSE_DURATION = 660;
const CUE = timeline as unknown as Record<keyof typeof timeline, [number, number]>;
const CODE = t.code as [string, string][];
// Scene starts, in frames. One bar is 60 frames (120 BPM).
const T = { hook: 0, rules: 120, scan: 240, signal: 360, end: 480 };

// --- The heartbeat line -------------------------------------------------------------------------------------------

const STEP = 0.25; // frames between two points of the line
const SPEED = 14; // world units the line travels per frame

// The hook's heart speeds up: a beat every 22 frames at first, every 9 at the end. PHI is the beat count so far.
const PHI: number[] = [];
for (let i = 0, beats = 0; i <= 130 / STEP; i++) {
  PHI.push(beats);
  beats += STEP / (22 - 13 * Math.min(1, (i * STEP) / 120));
}
const hookPhase = (time: number) => (time <= 0 ? time / 22 : PHI[Math.min(PHI.length - 1, Math.round(time / STEP))]);
/** How far the hook has turned from a heartbeat into a falling price, 0..1. */
const panic = (time: number) => ci(time, [62, 112], [0, 1]);
const hookY = (time: number) => {
  const w = panic(time);
  return 150 * (1 + w * 0.8) * ecg(hookPhase(time)) + w * (-520 * w + (noise1(time / 2.2, 'crash') - 0.5) * 260);
};
const calmY = (time: number) => 80 * ecg((time - T.rules) / 60 + 0.45);

/** The line as a monitor draws it: its head stays put, older points travel away to the left. */
const trace = (ctx: CanvasRenderingContext2D, f: number, cam: Cam, y: (time: number) => number, since: number, color: string) => {
  const points: P2[] = [];
  for (let k = 0; k < 720; k++) {
    const time = f - k * STEP;
    if (time < since) break;
    points.push(project([-k * STEP * SPEED, y(time), 0], cam));
  }
  glowLine(ctx, points, color, 9);
  flare(ctx, points[0] ?? null, color, 80);
};

/** The monitor's grid, in the line's plane, travelling with it. */
const grid = (ctx: CanvasRenderingContext2D, f: number, cam: Cam, color: string, alpha: number) => {
  ctx.globalCompositeOperation = 'lighter';
  ctx.strokeStyle = color;
  ctx.globalAlpha = alpha;
  ctx.lineWidth = 2;
  const line = (a: P2, b: P2) => {
    if (!a || !b) return;
    ctx.beginPath();
    ctx.moveTo(a[0], a[1]);
    ctx.lineTo(b[0], b[1]);
    ctx.stroke();
  };
  const shift = (f * SPEED) % 120;
  for (let k = -3; k < 26; k++) line(project([-shift - k * 120, -480, 0], cam), project([-shift - k * 120, 480, 0], cam));
  for (let y = -480; y <= 480; y += 120) line(project([-3000, y, 0], cam), project([360, y, 0], cam));
  ctx.globalAlpha = 1;
};

const drawHook = (ctx: CanvasRenderingContext2D, f: number) => {
  const fall = panic(f);
  const cam: Cam = { pos: [250, 230 - 390 * fall * fall, -700], yaw: -0.6, pitch: 0, focal: 1100 };
  grid(ctx, f, cam, C.danger, 0.16);
  trace(ctx, f, cam, hookY, -200, C.danger);
};

const drawCalm = (ctx: CanvasRenderingContext2D, f: number) => {
  const cam: Cam = { pos: [200, 330, -800], yaw: -0.35, pitch: 0, focal: 1100 };
  grid(ctx, f, cam, C.primary, 0.07);
  trace(ctx, f, cam, calmY, T.rules, C.primary);
};

// --- The field of stocks ------------------------------------------------------------------------------------------

const RIBBONS = 30;
const GAP = 115;
const ribbonX = (i: number) => (i - (RIBBONS - 1) / 2) * GAP;
const ribbonY = (i: number, z: number) => (noise1(z / 420, `a${i}`) - 0.5) * 170 + (noise1(z / 110, `b${i}`) - 0.5) * 44;
/** Where a stock meets its rules: at most one place per stretch of 700 units, on some stocks only. */
const hitZ = (i: number, stretch: number) => (random(`hit${i}-${stretch}`) < 0.17 ? stretch * 700 + random(`at${i}-${stretch}`) * 600 : null);

/** Each stock is a price line running away from the viewer. `scanned` is how far the daily check has got. */
const field = (ctx: CanvasRenderingContext2D, cam: Cam, scanZ: number | null, scanned: number) => {
  const near = Math.ceil((cam.pos[2] + 80) / 55) * 55;
  for (let i = 0; i < RIBBONS; i++) {
    const x = ribbonX(i);
    for (let chunk = 0; chunk < 4; chunk++) {
      const points: P2[] = [];
      for (let z = near + chunk * 1100; z <= near + (chunk + 1) * 1100; z += 55) points.push(project([x, ribbonY(i, z), z], cam));
      glowLine(ctx, points, C.lilac, 3, [0.75, 0.5, 0.28, 0.12][chunk], 10, false);
    }
    for (let stretch = Math.floor(near / 700); stretch <= Math.floor((near + 4400) / 700); stretch++) {
      const z = hitZ(i, stretch);
      if (z === null || z < near || z > scanned) continue;
      const at = project([x, ribbonY(i, z), z], cam);
      if (!at) continue;
      const fresh = scanZ === null ? 0 : Math.max(0, 1 - (scanZ - z) / 500);
      glowLine(ctx, [at, project([x, ribbonY(i, z) + 110, z], cam)], C.primary, 3, 0.7, 16);
      flare(ctx, at, C.primary, Math.min(46, 30 * at[2] * (1 + fresh * 2.2)));
    }
  }
  if (scanZ !== null) {
    const sweep: P2[] = [];
    for (let i = 0; i < RIBBONS; i++) sweep.push(project([ribbonX(i), ribbonY(i, scanZ), scanZ], cam));
    glowLine(ctx, sweep, C.primary, 5, 0.9, 30);
  }
};

const scanCam = (f: number): Cam => ({
  pos: [Math.sin(f / 40) * 120, 430, -600 + (f - T.scan) * 24],
  yaw: Math.sin(f / 55) * 0.06,
  pitch: -0.27,
  focal: 1100,
});

const drawScan = (ctx: CanvasRenderingContext2D, f: number) => {
  const cam = scanCam(f);
  // The check sweeps away from the viewer, once every 50 frames.
  const sweep = Math.floor((f - T.scan) / 50);
  const scanZ = cam.pos[2] + 300 + (((f - T.scan) % 50) / 50) * 4000;
  const before = sweep > 0 ? scanCam(T.scan + sweep * 50).pos[2] + 4300 : -Infinity;
  field(ctx, cam, scanZ, Math.max(scanZ, before));
};

const drawAfter = (ctx: CanvasRenderingContext2D, f: number) => {
  const cam = scanCam(T.signal);
  cam.pos[2] += (f - T.signal) * 3;
  field(ctx, cam, null, Infinity);
};

// --- Scenes -------------------------------------------------------------------------------------------------------

/** 1. A racing heartbeat on a monitor, which turns into a falling price. */
const Hook: React.FC = () => {
  const f = useCurrentFrame();
  const sinceBeat = (hookPhase(f) - 0.335 + 10) % 1;
  const flash = Math.exp(-sinceBeat * 7);
  const kick = flash * (6 + panic(f) * 22);
  return (
    <AbsoluteFill style={{ background: '#07030b' }}>
      <AbsoluteFill
        style={{
          background: `radial-gradient(ellipse 80% 45% at 60% 66%, ${C.danger} 0%, transparent 70%)`,
          opacity: 0.12 + flash * 0.4,
        }}
      />
      <AbsoluteFill style={{ transform: `translate(${(random(`kx${f}`) - 0.5) * kick}px, ${(random(`ky${f}`) - 0.5) * kick}px)` }}>
        <Draw draw={drawHook} />
      </AbsoluteFill>
      <Lines lines={t.hook} cue={CUE.hook} top={300} />
      <Lines lines={t.not} cue={CUE.not} top={300} />
      <Lines lines={t.pulse} cue={CUE.pulse} top={610} maxSize={112} color={C.danger} />
    </AbsoluteFill>
  );
};

/** 2. Calm: the same line, slow and green, and the rules typed out as code. */
const Rules: React.FC = () => {
  const f = useCurrentFrame();
  const keyWidth = Math.max(...CODE.map(([key]) => key.length)) + 2;
  const longest = Math.max(...CODE.map(([, text]) => keyWidth + text.length)) + 4;
  const size = Math.min(56, Math.floor(780 / (longest * 0.6)));
  const panel = sp(f, T.rules + 10, 16, 160);
  const last = CODE.length - 1;
  return (
    <AbsoluteFill style={{ background: C.deep }}>
      <AbsoluteFill style={{ background: `radial-gradient(ellipse 90% 50% at 50% 60%, ${C.violet}55 0%, transparent 75%)` }} />
      <Draw draw={drawCalm} />
      <Lines lines={t.rules} cue={CUE.rules} top={300} color={C.primary} />
      <div
        style={{
          position: 'absolute',
          left: 70,
          top: 740,
          width: 860,
          boxSizing: 'border-box',
          padding: '44px 40px',
          background: '#0b0918e6',
          border: `2px solid ${C.violet}`,
          boxShadow: `0 0 60px ${C.violet}88, inset 0 0 40px ${C.violet}22`,
          fontFamily: FONT,
          fontSize: size,
          lineHeight: 1.7,
          whiteSpace: 'pre',
          opacity: Math.min(1, panel * 2) * ci(f, [T.scan - 8, T.scan], [1, 0]),
          transform: `scale(${ci(panel, [0, 1], [0.92, 1])})`,
        }}
      >
        {CODE.map(([key, text], i) => {
          const start = T.rules + 22 + i * 26;
          const typed = Math.max(0, Math.floor((f - start) * 1.6));
          const line = key.padEnd(keyWidth) + text;
          // The cursor sits on the line being typed, and stays on the last one.
          const active = f >= start && (i === last || f < start + 26);
          return (
            <div key={i} style={{ opacity: f < start ? 0.25 : 1 }}>
              <span style={{ color: C.muted }}>{`0${i + 1}  `}</span>
              <span style={{ color: i === last ? C.primary : C.pink, fontWeight: 700 }}>{line.slice(0, Math.min(typed, keyWidth))}</span>
              <span style={{ color: C.ink }}>{line.slice(keyWidth, Math.max(keyWidth, typed))}</span>
              {active && <span style={{ color: C.primary, opacity: f % 10 < 6 ? 1 : 0 }}>█</span>}
            </div>
          );
        })}
      </div>
    </AbsoluteFill>
  );
};

/** 3. A flight over every stock on the list, with the daily check sweeping across them. */
const Scan: React.FC = () => {
  const f = useCurrentFrame();
  const day = 1 + Math.floor((f - T.scan) / 2);
  return (
    <AbsoluteFill style={{ background: `linear-gradient(${C.deep} 0%, #1b1240 34%, #0a0716 36%, #05030c 100%)` }}>
      <AbsoluteFill style={{ background: `radial-gradient(ellipse 80% 14% at 50% 35%, ${C.violet}aa 0%, transparent 100%)` }} />
      <Draw draw={drawScan} />
      <Lines lines={t.scan1} cue={CUE.scan1} top={300} />
      <Lines lines={t.scan2} cue={CUE.scan2} top={300} color={C.primary} maxSize={150} />
      <div
        style={{
          position: 'absolute',
          left: 70,
          top: 1420,
          fontFamily: FONT,
          fontSize: 60,
          fontWeight: 700,
          color: C.primary,
          textShadow: `0 0 24px ${C.primary}`,
          opacity: ci(f, [T.scan + 6, T.scan + 14], [0, 1]),
        }}
      >
        {`${t.day} ${String(day).padStart(3, '0')}`}
      </div>
    </AbsoluteFill>
  );
};

/** 4. One of them meets the rules: one message, and the choice stays with the viewer. */
const Signal: React.FC = () => {
  const f = useCurrentFrame();
  const card = sp(f, T.signal + 10, 11, 190);
  const chips = sp(f, CUE.decide[0], 12, 200);
  return (
    <AbsoluteFill style={{ background: '#05030c' }}>
      <Draw draw={drawAfter} opacity={0.35} />
      <Rings at={T.signal + 6} x={540} y={1100} />
      <Rings at={CUE.decide[0] - 2} x={540} y={1330} color={C.pink} rings={2} />
      <Lines lines={t.one} cue={CUE.one} top={330} />
      <Lines lines={t.decide} cue={CUE.decide} top={330} color={C.primary} />
      <div
        style={{
          position: 'absolute',
          left: 90,
          top: 1000,
          width: 840,
          boxSizing: 'border-box',
          padding: 36,
          display: 'flex',
          gap: 32,
          alignItems: 'center',
          background: C.editor,
          border: `3px solid ${C.primary}`,
          borderRadius: 28,
          boxShadow: `0 0 70px ${C.primary}99, 0 40px 80px #000a`,
          fontFamily: FONT,
          opacity: Math.min(1, card * 2),
          transform: `scale(${ci(card, [0, 1], [1.5, 1])})`,
        }}
      >
        <div
          style={{
            flex: 'none',
            width: 110,
            height: 110,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 38,
            fontWeight: 700,
            color: C.primary,
            background: C.deep,
            border: `5px solid ${C.primary}`,
            textShadow: `0 0 16px ${C.primary}`,
          }}
        >
          STL
        </div>
        <div>
          <div style={{ fontSize: 28, letterSpacing: 5, color: C.lilac }}>{`${t.card.label} · ${t.card.when}`}</div>
          <div style={{ fontSize: 50, fontWeight: 700, color: C.ink, marginTop: 8 }}>{t.card.text}</div>
        </div>
      </div>
      {t.chips.map((chip, i) => (
        <div
          key={i}
          style={{
            position: 'absolute',
            left: 90 + i * 430,
            top: 1270,
            width: 410,
            height: 110,
            boxSizing: 'border-box',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontFamily: FONT,
            fontSize: 36,
            fontWeight: 700,
            color: i === 0 ? '#00140b' : C.ink,
            background: i === 0 ? C.primary : 'transparent',
            border: `3px solid ${i === 0 ? C.primary : C.lilac}`,
            boxShadow: i === 0 ? `0 0 40px ${C.primary}aa` : 'none',
            opacity: f < CUE.decide[0] + i * 5 ? 0 : Math.min(1, chips * 2),
            transform: `translateY(${ci(chips, [0, 1], [60, 0])}px)`,
          }}
        >
          {`${i === 0 ? '✓' : '×'} ${chip}`}
        </div>
      ))}
    </AbsoluteFill>
  );
};

export const PulseVideo: React.FC = () => {
  const f = useCurrentFrame();
  const cuts = [T.rules, T.scan, T.signal, T.end];
  return (
    <AbsoluteFill style={{ background: '#05030c', width: W }}>
      <Shake cuts={cuts}>
        {f < T.rules && <Hook />}
        {f >= T.rules && f < T.scan && <Rules />}
        {f >= T.scan && f < T.signal && <Scan />}
        {f >= T.signal && f < T.end && <Signal />}
        <EndCard t={STRINGS.en} at={T.end} tagline={CUE.tagline} alpha={CUE.alpha} />
      </Shake>
      {cuts.map((cut) => (
        <Glitch key={cut} at={cut} />
      ))}
      <Grade />
    </AbsoluteFill>
  );
};
