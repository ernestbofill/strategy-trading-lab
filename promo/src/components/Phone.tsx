import React from 'react';
import { Img, staticFile } from 'remotion';
import { C } from '../theme';

export const PHONE_W = 560;
export const PHONE_H = 1212; // the app's 390 x 844 frame

/** A phone drawn with CSS 3D, carrying a real capture of the app. */
export const Phone: React.FC<{
  screen: string; // path under public/, e.g. screens/en/dashboard.png
  x: number;
  y: number;
  scale?: number;
  rotateX?: number;
  rotateY?: number;
  rotateZ?: number;
  glow?: string;
  scroll?: number; // px of the capture scrolled away, at phone scale
  sweep?: number; // 0..1 position of the light sweep, or -1 for none
  opacity?: number;
}> = ({
  screen,
  x,
  y,
  scale = 1,
  rotateX = 0,
  rotateY = 0,
  rotateZ = 0,
  glow = C.primary,
  scroll = 0,
  sweep = -1,
  opacity = 1,
}) => (
  <div
    style={{
      position: 'absolute',
      left: x - PHONE_W / 2,
      top: y - PHONE_H / 2,
      width: PHONE_W,
      height: PHONE_H,
      perspective: 2200,
      opacity,
    }}
  >
    <div
      style={{
        width: '100%',
        height: '100%',
        boxSizing: 'border-box',
        padding: 16,
        borderRadius: 72,
        background: '#05040c',
        transform: `scale(${scale}) rotateX(${rotateX}deg) rotateY(${rotateY}deg) rotateZ(${rotateZ}deg)`,
        boxShadow: [
          `0 0 0 3px ${glow}`,
          `${-rotateY * 0.5}px ${rotateX * 0.5}px 0 3px #2a2150`,
          `0 0 50px ${glow}aa`,
          `0 0 200px ${glow}55`,
          '0 90px 140px #000c',
        ].join(', '),
      }}
    >
      <div
        style={{
          position: 'relative',
          width: '100%',
          height: '100%',
          overflow: 'hidden',
          borderRadius: 56,
          background: C.editor,
        }}
      >
        <Img
          src={staticFile(screen)}
          style={{ position: 'absolute', left: 0, top: -scroll, width: '100%' }}
        />
        {sweep >= 0 && (
          <div
            style={{
              position: 'absolute',
              inset: 0,
              background: `linear-gradient(115deg, transparent ${sweep * 140 - 40}%, #ffffff55 ${sweep * 140 - 20}%, transparent ${sweep * 140}%)`,
            }}
          />
        )}
        <div
          style={{
            position: 'absolute',
            inset: 0,
            borderRadius: 56,
            boxShadow: 'inset 0 0 60px #0008',
          }}
        />
      </div>
    </div>
  </div>
);
