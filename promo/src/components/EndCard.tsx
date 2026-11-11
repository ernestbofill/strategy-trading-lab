import React from 'react';
import { AbsoluteFill, interpolate, useCurrentFrame } from 'remotion';
import { ci, sp } from '../anim';
import { C, FONT, Strings, W } from '../theme';
import { Backdrop } from './Fx';
import { Cue, Lines } from './Lines';

/** The last shot of every video: logo, tagline, address, the alpha line and the disclaimer. */
export const EndCard: React.FC<{ t: Strings; at: number; tagline: Cue; alpha: Cue }> = ({ t, at, tagline, alpha }) => {
  const f = useCurrentFrame();
  if (f < at) return null;
  const logo = sp(f, at, 10, 150);
  const url = sp(f, at + 60, 11, 170);
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
          letterSpacing: ci(f, [at + 8, at + 30], [30, 7]),
          color: C.lilac,
          opacity: ci(f, [at + 8, at + 22], [0, 1]),
        }}
      >
        STRATEGY TRADING LAB
      </div>
      <Lines lines={t.tagline} cue={tagline} top={890} maxSize={92} color={C.ink} center />
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
      <Lines lines={t.alpha} cue={alpha} top={1345} maxSize={48} color={C.lilac} center />
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
          opacity: ci(f, [at + 90, at + 105], [0, 0.85]),
        }}
      >
        {t.disclaimer}
      </div>
    </AbsoluteFill>
  );
};
