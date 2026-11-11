import React from 'react';
import { interpolate, spring, useCurrentFrame } from 'remotion';
import { C, CUES, CueKey, FONT, FPS, SAFE, W } from '../theme';

const USABLE = W - SAFE.left - SAFE.right;

/** Kinetic type: each word slams in with a colour split, and the line glitches out. */
export const Lines: React.FC<{
  lines: string[];
  cue: CueKey;
  top: number;
  color?: string;
  maxSize?: number;
  stagger?: number;
  center?: boolean; // centred in the frame, clear of both edges
}> = ({ lines, cue, top, color = C.ink, maxSize = 132, stagger = 4, center = false }) => {
  const frame = useCurrentFrame();
  const [from, to] = CUES[cue];
  if (frame < from || frame >= to) return null;

  const longest = Math.max(...lines.map((l) => l.length));
  const left = center ? SAFE.right : SAFE.left;
  const width = center ? W - SAFE.right * 2 : USABLE;
  const size = Math.min(maxSize, Math.floor(width / (longest * 0.6)));
  const out = interpolate(frame, [to - 5, to], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });

  let wordIndex = 0;
  return (
    <div
      style={{
        position: 'absolute',
        left,
        top,
        width,
        textAlign: center ? 'center' : 'left',
        fontFamily: FONT,
        fontWeight: 700,
        fontSize: size,
        lineHeight: 1.08,
        letterSpacing: -size * 0.02,
        color,
        opacity: 1 - out,
        transform: `translateX(${out * (frame % 2 ? 40 : -40)}px) skewX(${out * 20}deg)`,
      }}
    >
      {lines.map((line, li) => (
        <div key={li} style={{ whiteSpace: 'nowrap' }}>
          {line.split(' ').map((word, wi) => {
            const start = from + wordIndex++ * stagger;
            const s = spring({ frame: frame - start, fps: FPS, config: { damping: 13, stiffness: 240, mass: 0.5 } });
            const split = (1 - Math.min(1, s)) * 18;
            return (
              <span
                key={wi}
                style={{
                  display: 'inline-block',
                  marginRight: wi < line.split(' ').length - 1 ? size * 0.6 : 0,
                  opacity: frame < start ? 0 : Math.min(1, s * 2),
                  transform: `scale(${interpolate(s, [0, 1], [1.7, 1])})`,
                  transformOrigin: center ? 'center' : 'left center',
                  filter: `blur(${(1 - Math.min(1, s)) * 10}px)`,
                  textShadow: `${split}px 0 ${C.pink}, ${-split}px 0 ${C.info}, 0 0 28px ${color}88`,
                }}
              >
                {word}
              </span>
            );
          })}
        </div>
      ))}
    </div>
  );
};
