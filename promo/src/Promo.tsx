import React from 'react';
import { AbsoluteFill, OffthreadVideo, Sequence, interpolate, random, spring, staticFile, useCurrentFrame } from 'remotion';
import { Backdrop, Glitch, Grade, Pulse, Shake } from './components/Fx';
import { Lines } from './components/Lines';
import { Phone } from './components/Phone';
import { BEAT, C, CINE, FONT, FPS, H, Lang, STRINGS, Strings, W } from './theme';

// Scene starts, in frames (see docs/promo/storyboard.md). One bar is 60 frames.
const T = { hook: 0, reveal: 60, templates: 120, proof: 240, night: 360, signal: 420, ai: 540, end: 630 };
const CUTS = [T.reveal, T.templates, T.proof, T.signal, T.ai, T.end];

const clamp = { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' } as const;
const ci = (f: number, input: number[], output: number[]) => interpolate(f, input, output, clamp);
const sp = (f: number, at: number, damping = 13, stiffness = 140) =>
  spring({ frame: f - at, fps: FPS, config: { damping, stiffness, mass: 0.7 } });

type SceneProps = { lang: Lang; t: Strings };

const Cine: React.FC<{ file: string; from: number; length: number; tint: string }> = ({ file, from, length, tint }) => (
  <Sequence from={from} durationInFrames={length}>
    <OffthreadVideo muted src={staticFile(`cine/${file}.mp4`)} style={{ width: W, height: H, objectFit: 'cover' }} />
    <AbsoluteFill style={{ background: tint, mixBlendMode: 'soft-light' }} />
  </Sequence>
);

/** 1. The "emotion" world: a red chart falling in the dark. */
const Hook: React.FC<SceneProps> = ({ t }) => {
  const f = useCurrentFrame();
  if (f >= T.reveal) return null;
  let price = 520;
  const candles = Array.from({ length: 40 }).map((_, i) => {
    const open = price;
    price += (random(`c${i}`) - 0.34) * 95;
    return { open, close: price, wick: 20 + random(`w${i}`) * 60 };
  });
  const flicker = 0.75 + random(`f${f}`) * 0.25;
  return (
    <AbsoluteFill style={{ background: '#07030a' }}>
      {CINE.a ? (
        <Cine file="a" from={0} length={60} tint={`${C.danger}55`} />
      ) : (
        <AbsoluteFill
          style={{
            opacity: flicker,
            transform: `scale(${1.5 + f * 0.006}) translate(${-f * 3.2 + (random(`hx${f}`) - 0.5) * 8}px, ${-120 + (random(`hy${f}`) - 0.5) * 8}px)`,
            filter: 'blur(0.6px) brightness(1.5)',
          }}
        >
          <AbsoluteFill style={{ background: `radial-gradient(ellipse at 60% 60%, ${C.danger}55, transparent 65%)` }} />
          {candles.map((c, i) => {
            if (i > 8 + f * 0.62) return null;
            const down = c.close > c.open;
            const top = Math.min(c.open, c.close);
            const colour = down ? C.danger : C.pink;
            return (
              <React.Fragment key={i}>
                <div style={{ position: 'absolute', left: i * 34 + 13, top: top - c.wick, width: 4, height: Math.abs(c.close - c.open) + c.wick * 2, background: colour }} />
                <div
                  style={{
                    position: 'absolute',
                    left: i * 34,
                    top,
                    width: 28,
                    height: Math.max(8, Math.abs(c.close - c.open)),
                    background: down ? colour : 'transparent',
                    border: `3px solid ${colour}`,
                    boxShadow: `0 0 26px ${colour}`,
                  }}
                />
              </React.Fragment>
            );
          })}
        </AbsoluteFill>
      )}
      <AbsoluteFill style={{ background: 'linear-gradient(#07030abb 0%, #07030a44 38%, transparent 60%)' }} />
      <Lines lines={t.hook} cue="hook" top={330} />
    </AbsoluteFill>
  );
};

/** 2. The cut to the "rules" world: the phone slams in. */
const Reveal: React.FC<SceneProps> = ({ lang, t }) => {
  const f = useCurrentFrame();
  if (f < T.reveal || f >= T.templates) return null;
  const s = sp(f, T.reveal + 2, 11, 110);
  return (
    <AbsoluteFill>
      <Backdrop />
      <Phone
        screen={`screens/${lang}/dashboard.png`}
        x={540}
        y={interpolate(s, [0, 1], [1700, 1150])}
        scale={interpolate(s, [0, 1], [2.4, 0.86])}
        rotateY={interpolate(s, [0, 1], [-75, -14]) + Math.sin(f / 14) * 3}
        rotateX={interpolate(s, [0, 1], [24, 6])}
        rotateZ={interpolate(s, [0, 1], [-12, 0])}
        sweep={ci(f, [T.reveal + 12, T.reveal + 40], [0, 1])}
        scroll={ci(f, [T.reveal + 30, T.templates], [0, 140])}
      />
      <Lines lines={t.reveal} cue="reveal" top={300} color={C.primary} />
    </AbsoluteFill>
  );
};

/** 3. Five template cards fly out of the phone, one per beat. */
const Templates: React.FC<SceneProps> = ({ lang, t }) => {
  const f = useCurrentFrame();
  if (f < T.templates || f >= T.proof) return null;
  const back = ci(f, [T.proof - 14, T.proof - 2], [0, 1]);
  return (
    <AbsoluteFill>
      <Backdrop tint={C.info} />
      <Phone
        screen={`screens/${lang}/templates.png`}
        x={540}
        y={1100}
        scale={0.62 + back * 0.1}
        rotateY={Math.sin((f - T.templates) / 18) * 16}
        rotateX={8}
        scroll={ci(f, [T.templates, T.proof], [0, 420])}
      />
      {t.templateNames.map((name, i) => {
        const at = T.templates + i * BEAT;
        if (f < at) return null;
        const s = sp(f, at, 12, 170) * (1 - back);
        const left = i % 2 === 0;
        const lit = ci(f, [at, at + 4, at + BEAT], [1, 1, 0]);
        const size = Math.min(40, Math.floor(500 / (name.length * 0.6)));
        return (
          <div
            key={i}
            style={{
              position: 'absolute',
              left: interpolate(s, [0, 1], [540, left ? 310 : 700]) - 290,
              top: interpolate(s, [0, 1], [1100, 660 + i * 172]) - 65,
              width: 580,
              height: 130,
              boxSizing: 'border-box',
              padding: '0 30px',
              display: 'flex',
              alignItems: 'center',
              gap: 22,
              fontFamily: FONT,
              background: '#170f2eee',
              border: `3px solid ${lit > 0.1 ? C.pink : C.primary}`,
              boxShadow: `0 0 ${30 + lit * 70}px ${lit > 0.1 ? C.pink : C.primary}aa, 0 30px 60px #000a`,
              transform: `scale(${s * (1 + lit * 0.12)}) rotate(${(left ? -5 : 5) * s}deg)`,
              opacity: Math.min(1, s * 2),
            }}
          >
            <span style={{ color: C.lilac, fontSize: 30, fontWeight: 700 }}>0{i + 1}</span>
            <span style={{ color: C.ink, fontSize: size, fontWeight: 700, whiteSpace: 'nowrap' }}>{name}</span>
          </div>
        );
      })}
      <Lines lines={t.templates1} cue="templates1" top={300} />
      <Lines lines={t.templates2} cue="templates2" top={410} color={C.primary} />
    </AbsoluteFill>
  );
};

/** 4. A price line draws itself over a skyline of bars; steps light up on the dips. */
const Proof: React.FC<SceneProps> = ({ lang, t }) => {
  const f = useCurrentFrame();
  if (f < T.proof || f >= T.night) return null;
  const n = 30;
  let v = 0.25;
  const series = Array.from({ length: n }).map((_, i) => {
    v += 0.028 + (random(`p${i}`) - 0.5) * 0.14;
    if (i === 9 || i === 10 || i === 19) v -= 0.12;
    return Math.max(0.08, Math.min(1, v));
  });
  const x = (i: number) => 40 + (i * (W - 80)) / (n - 1);
  const y = (val: number) => 1500 - val * 720;
  const draw = ci(f, [T.proof + 18, T.proof + 78], [0, 1]);
  const dim = ci(f, [T.proof + 62, T.proof + 80], [1, 0.22]);
  const phoneIn = sp(f, T.proof + 62, 14, 110);
  const dips = [5, 11, 20];
  return (
    <AbsoluteFill>
      <Backdrop grid={0.5} />
      {CINE.b && <Cine file="b" from={T.proof} length={45} tint={`${C.violet}44`} />}
      <AbsoluteFill style={{ opacity: dim }}>
        {series.map((val, i) => {
          const rise = sp(f, T.proof + i * 1.2, 15, 150);
          const h = (1500 - y(val)) * rise;
          return (
            <div
              key={i}
              style={{
                position: 'absolute',
                left: x(i) - 13,
                top: 1500 - h,
                width: 26,
                height: h,
                background: `linear-gradient(${C.violet}, ${C.violet}11)`,
                borderTop: `4px solid ${C.lilac}`,
              }}
            />
          );
        })}
        <svg width={W} height={H} style={{ position: 'absolute', filter: `drop-shadow(0 0 16px ${C.primary})` }}>
          <path
            d={series.map((val, i) => `${i ? 'L' : 'M'}${x(i)} ${y(val) - 4}`).join(' ')}
            fill="none"
            stroke={C.primary}
            strokeWidth={9}
            strokeLinejoin="round"
            pathLength={1}
            strokeDasharray={1}
            strokeDashoffset={1 - draw}
          />
          {dips.map((d) => {
            const hit = sp(f, T.proof + 18 + (d / (n - 1)) * 60, 9, 200);
            return hit > 0.01 ? (
              <circle key={d} cx={x(d)} cy={y(series[d]) - 4} r={22 * hit} fill={C.deep} stroke={C.pink} strokeWidth={8} />
            ) : null;
          })}
        </svg>
      </AbsoluteFill>
      <Phone
        screen={`screens/${lang}/tested.png`}
        x={540}
        y={interpolate(phoneIn, [0, 1], [2700, 1230])}
        scale={0.8}
        rotateY={interpolate(phoneIn, [0, 1], [40, 12])}
        rotateX={6}
        scroll={ci(f, [T.proof + 70, T.night], [0, 170])}
        sweep={ci(f, [T.proof + 78, T.proof + 104], [0, 1])}
      />
      <Lines lines={t.proof} cue="proof" top={290} maxSize={118} />
    </AbsoluteFill>
  );
};

/** 5. Silence, a dark room at 3 a.m., then the signal drops in. */
const Signal: React.FC<SceneProps> = ({ lang, t }) => {
  const f = useCurrentFrame();
  if (f < T.night || f >= T.ai) return null;
  const night = f < T.signal;
  const breath = Math.max(0, Math.sin(((f - T.night - 18) / 36) * Math.PI));
  const drop = sp(f, T.signal, 10, 150);
  const away = ci(f, [T.signal + 40, T.signal + 52], [0, 1]);
  const phoneIn = sp(f, T.signal + 30, 14, 110);
  const size = Math.min(58, Math.floor(760 / (t.signalCard.text.length * 0.6)));
  return (
    <AbsoluteFill style={{ background: '#030208' }}>
      {night ? (
        CINE.c ? (
          <Cine file="c" from={T.night} length={60} tint={`${C.primary}22`} />
        ) : (
          <>
            <AbsoluteFill style={{ background: `radial-gradient(circle at 50% 62%, ${C.primary}${Math.round(breath * 90).toString(16).padStart(2, '0')}, transparent 45%)` }} />
            <div style={{ position: 'absolute', top: 1090, width: W, textAlign: 'center', fontFamily: FONT, fontSize: 150, fontWeight: 700, color: C.primary, opacity: 0.12 + breath * 0.5, textShadow: `0 0 60px ${C.primary}` }}>
              03:07
            </div>
          </>
        )
      ) : (
        <>
          <Backdrop tint={C.primary} grid={0.7} />
          <Pulse at={T.signal} x={540} y={720} />
          <Phone
            screen={`screens/${lang}/signal.png`}
            x={540}
            y={interpolate(phoneIn, [0, 1], [2700, 1210])}
            scale={0.82}
            rotateY={interpolate(phoneIn, [0, 1], [-40, -10]) + Math.sin(f / 16) * 3}
            rotateX={6}
            sweep={ci(f, [T.signal + 44, T.signal + 70], [0, 1])}
          />
          <div
            style={{
              position: 'absolute',
              left: 70,
              top: interpolate(drop, [0, 1], [-320, 620]) + away * 480,
              width: 860,
              boxSizing: 'border-box',
              padding: '34px 40px',
              fontFamily: FONT,
              background: '#002212f2',
              border: `3px solid ${C.primary}`,
              boxShadow: `0 0 70px ${C.primary}aa, 0 30px 80px #000c`,
              transform: `scale(${1 - away * 0.7})`,
              opacity: 1 - away,
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 16, color: C.primary, fontSize: 30, fontWeight: 700, letterSpacing: 6 }}>
              <span style={{ width: 20, height: 20, borderRadius: 10, background: C.primary, opacity: f % 10 < 5 ? 1 : 0.2, boxShadow: `0 0 20px ${C.primary}` }} />
              {t.signalCard.label}
            </div>
            <div style={{ marginTop: 14, color: C.ink, fontSize: size, fontWeight: 700, whiteSpace: 'nowrap' }}>{t.signalCard.text}</div>
          </div>
        </>
      )}
      <Lines lines={t.signal1} cue="signal1" top={330} color={C.lilac} />
      <Lines lines={t.signal2} cue="signal2" top={300} color={C.primary} />
    </AbsoluteFill>
  );
};

/** 6. The AI round: scores tick up and the top one lights. */
const Ai: React.FC<SceneProps> = ({ lang, t }) => {
  const f = useCurrentFrame();
  if (f < T.ai || f >= T.end) return null;
  const s = sp(f, T.ai, 12, 130);
  const scores = [62, 71, 88, 54, 79];
  return (
    <AbsoluteFill>
      <Backdrop tint={C.pink} />
      <Phone
        screen={`screens/${lang}/ai.png`}
        x={interpolate(s, [0, 1], [-300, 360])}
        y={1210}
        scale={0.78}
        rotateY={interpolate(s, [0, 1], [60, 20]) + Math.sin(f / 15) * 3}
        rotateX={6}
        glow={C.pink}
        scroll={ci(f, [T.ai, T.end], [0, 260])}
      />
      {scores.map((score, i) => {
        const at = T.ai + 8 + i * 5;
        const inn = sp(f, at, 12, 180);
        const shown = Math.round(ci(f, [at, at + 28], [0, score]));
        const top = score === 88 && f > at + 28;
        const colour = top ? C.primary : C.lilac;
        return (
          <div
            key={i}
            style={{
              position: 'absolute',
              left: interpolate(inn, [0, 1], [W + 100, 560]),
              top: 780 + i * 118,
              width: 360,
              height: 92,
              boxSizing: 'border-box',
              padding: '0 22px',
              display: 'flex',
              alignItems: 'center',
              gap: 18,
              fontFamily: FONT,
              background: '#170f2eee',
              border: `3px solid ${colour}`,
              boxShadow: top ? `0 0 70px ${C.primary}` : `0 0 18px ${C.violet}`,
              transform: `scale(${top ? 1.12 : 1})`,
            }}
          >
            <div style={{ flex: 1, height: 14, background: '#ffffff22' }}>
              <div style={{ width: `${shown}%`, height: '100%', background: colour, boxShadow: `0 0 14px ${colour}` }} />
            </div>
            <span style={{ width: 70, textAlign: 'right', color: colour, fontSize: 46, fontWeight: 700 }}>{shown}</span>
          </div>
        );
      })}
      <Lines lines={t.ai} cue="ai" top={300} />
    </AbsoluteFill>
  );
};

/** 7. Logo, the app's tagline, the URL and the small print. */
const End: React.FC<SceneProps> = ({ t }) => {
  const f = useCurrentFrame();
  if (f < T.end) return null;
  const logo = sp(f, T.end, 10, 150);
  const url = sp(f, T.end + 60, 11, 170);
  return (
    <AbsoluteFill>
      <Backdrop />
      <div
        style={{
          position: 'absolute',
          left: 540 - 140,
          top: 430,
          width: 280,
          height: 280,
          boxSizing: 'border-box',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontFamily: FONT,
          fontSize: 98,
          fontWeight: 700,
          color: C.primary,
          background: C.editor,
          border: `12px solid ${C.primary}`,
          boxShadow: `0 0 90px ${C.primary}aa, inset 0 0 40px ${C.primary}44`,
          textShadow: `0 0 30px ${C.primary}`,
          transform: `scale(${interpolate(logo, [0, 1], [3, 1])}) rotate(${interpolate(logo, [0, 1], [-20, 0])}deg)`,
          opacity: Math.min(1, logo * 2),
        }}
      >
        STL
      </div>
      <div
        style={{
          position: 'absolute',
          top: 750,
          width: W,
          textAlign: 'center',
          fontFamily: FONT,
          fontSize: 40,
          fontWeight: 700,
          letterSpacing: ci(f, [T.end + 8, T.end + 30], [30, 7]),
          color: C.lilac,
          opacity: ci(f, [T.end + 8, T.end + 22], [0, 1]),
        }}
      >
        STRATEGY TRADING LAB
      </div>
      <Lines lines={t.tagline} cue="tagline" top={890} maxSize={92} color={C.ink} center />
      <div
        style={{
          position: 'absolute',
          left: 150,
          width: 780,
          top: 1180,
          height: 110,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontFamily: FONT,
          fontSize: 50,
          fontWeight: 700,
          color: '#00140b',
          background: C.primary,
          boxShadow: `0 0 ${50 + Math.sin(f / 5) * 25}px ${C.primary}`,
          transform: `scale(${url})`,
          opacity: Math.min(1, url * 2),
        }}
      >
        {t.url}
      </div>
      <Lines lines={t.alpha} cue="alpha" top={1345} maxSize={48} color={C.lilac} center />
      <div
        style={{
          position: 'absolute',
          left: 150,
          width: 780,
          top: 1475,
          textAlign: 'center',
          fontFamily: FONT,
          fontSize: 26,
          color: C.ink,
          opacity: ci(f, [T.end + 90, T.end + 105], [0, 0.85]),
        }}
      >
        {t.disclaimer}
      </div>
    </AbsoluteFill>
  );
};

export const Promo: React.FC<{ lang: Lang }> = ({ lang }) => {
  const f = useCurrentFrame();
  const t = STRINGS[lang];
  // A small push on every beat while the music plays (not in the silent bar).
  const onBeat = f >= T.reveal && !(f >= T.night && f < T.signal) ? 1 - ((f % BEAT) / BEAT) : 0;
  const props = { lang, t };
  return (
    <AbsoluteFill style={{ background: '#000' }}>
      <Shake cuts={CUTS}>
        <AbsoluteFill style={{ transform: `scale(${1 + onBeat * onBeat * 0.018})` }}>
          <Hook {...props} />
          <Reveal {...props} />
          <Templates {...props} />
          <Proof {...props} />
          <Signal {...props} />
          <Ai {...props} />
          <End {...props} />
        </AbsoluteFill>
      </Shake>
      {CUTS.map((cut) => (
        <Glitch key={cut} at={cut} />
      ))}
      <Grade />
    </AbsoluteFill>
  );
};
