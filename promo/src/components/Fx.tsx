import React from 'react';
import { AbsoluteFill, interpolate, random, useCurrentFrame } from 'remotion';
import { C, H, W } from '../theme';

/** The "rules" world: deep violet, a neon grid floor running towards the viewer, drifting sparks. */
export const Backdrop: React.FC<{ tint?: string; grid?: number }> = ({ tint = C.violet, grid = 1 }) => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ background: C.deep }}>
      <AbsoluteFill
        style={{
          background: `radial-gradient(ellipse 90% 50% at 50% 42%, ${tint}66 0%, ${C.editor}55 45%, transparent 75%)`,
        }}
      />
      <div style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: 900, perspective: 700, opacity: grid }}>
        <div
          style={{
            position: 'absolute',
            left: -W,
            right: -W,
            top: 0,
            bottom: -600,
            transform: 'rotateX(74deg)',
            transformOrigin: 'top center',
            backgroundImage: `linear-gradient(${C.violet} 3px, transparent 3px), linear-gradient(90deg, ${C.violet} 3px, transparent 3px)`,
            backgroundSize: '120px 120px',
            backgroundPosition: `0 ${(frame * 8) % 120}px`,
            maskImage: 'linear-gradient(transparent 0%, #000 35%)',
            WebkitMaskImage: 'linear-gradient(transparent 0%, #000 35%)',
          }}
        />
      </div>
      {Array.from({ length: 46 }).map((_, i) => {
        const speed = 2 + random(`s${i}`) * 5;
        const y = H - ((frame * speed + random(`y${i}`) * H) % H);
        const size = 2 + random(`z${i}`) * 5;
        return (
          <div
            key={i}
            style={{
              position: 'absolute',
              left: random(`x${i}`) * W,
              top: y,
              width: size,
              height: size * (2 + speed),
              background: i % 5 === 0 ? C.pink : C.primary,
              opacity: 0.15 + random(`o${i}`) * 0.5,
              boxShadow: `0 0 12px ${C.primary}`,
            }}
          />
        );
      })}
    </AbsoluteFill>
  );
};

/** Scanlines, grain and a vignette over everything, so code and footage read as one film. */
export const Grade: React.FC<{ grain?: number }> = ({ grain = 0.1 }) => {
  const frame = useCurrentFrame();
  return (
    <>
      <AbsoluteFill
        style={{
          backgroundImage: 'repeating-linear-gradient(0deg, #0000 0px, #0000 3px, #00000038 4px)',
          mixBlendMode: 'multiply',
        }}
      />
      <AbsoluteFill style={{ opacity: grain, mixBlendMode: 'overlay' }}>
        <svg width={W} height={H}>
          <filter id="grain">
            <feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="2" seed={frame % 12} />
            <feColorMatrix type="saturate" values="0" />
          </filter>
          <rect width={W} height={H} filter="url(#grain)" />
        </svg>
      </AbsoluteFill>
      <AbsoluteFill style={{ background: 'radial-gradient(ellipse at center, transparent 55%, #000000b0 100%)' }} />
    </>
  );
};

/** A glitch on a cut: a white flash, then torn colour bars for a few frames. */
export const Glitch: React.FC<{ at: number; length?: number }> = ({ at, length = 7 }) => {
  const frame = useCurrentFrame();
  const t = frame - at;
  if (t < -2 || t >= length) return null;
  const fade = interpolate(t, [0, length], [1, 0], { extrapolateLeft: 'clamp' });
  const colours = [C.pink, C.info, C.primary, '#fff'];
  return (
    <AbsoluteFill>
      {t >= 0 && t < 2 && <AbsoluteFill style={{ background: '#fff', opacity: 0.85 - t * 0.4 }} />}
      {Array.from({ length: 14 }).map((_, i) => {
        const k = `${at}-${frame}-${i}`;
        return (
          <div
            key={i}
            style={{
              position: 'absolute',
              left: (random(`gx${k}`) - 0.3) * W,
              top: random(`gy${k}`) * H,
              width: W * (0.3 + random(`gw${k}`)),
              height: 6 + random(`gh${k}`) * 90,
              background: colours[i % colours.length],
              opacity: fade * (0.25 + random(`go${k}`) * 0.5),
              mixBlendMode: 'screen',
            }}
          />
        );
      })}
    </AbsoluteFill>
  );
};

/** Shakes its children for a few frames after each cut. */
export const Shake: React.FC<{ cuts: number[]; children: React.ReactNode }> = ({ cuts, children }) => {
  const frame = useCurrentFrame();
  let dx = 0;
  let dy = 0;
  for (const cut of cuts) {
    const t = frame - cut;
    if (t >= 0 && t < 8) {
      const power = (1 - t / 8) * 26;
      dx += (random(`sx${frame}`) - 0.5) * power;
      dy += (random(`sy${frame}`) - 0.5) * power;
    }
  }
  return <AbsoluteFill style={{ transform: `translate(${dx}px, ${dy}px)` }}>{children}</AbsoluteFill>;
};

/** Rings spreading from a point: the signal's pulse. */
export const Pulse: React.FC<{ at: number; x: number; y: number; color?: string; rings?: number }> = ({
  at,
  x,
  y,
  color = C.primary,
  rings = 3,
}) => {
  const frame = useCurrentFrame();
  return (
    <>
      {Array.from({ length: rings }).map((_, i) => {
        const t = frame - at - i * 7;
        if (t < 0 || t > 40) return null;
        const size = interpolate(t, [0, 40], [40, 1500]);
        return (
          <div
            key={i}
            style={{
              position: 'absolute',
              left: x - size / 2,
              top: y - size / 2,
              width: size,
              height: size,
              borderRadius: '50%',
              border: `5px solid ${color}`,
              boxShadow: `0 0 40px ${color}, inset 0 0 40px ${color}66`,
              opacity: interpolate(t, [0, 40], [0.9, 0]),
            }}
          />
        );
      })}
    </>
  );
};
