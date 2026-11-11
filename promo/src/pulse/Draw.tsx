import React, { useLayoutEffect, useRef } from 'react';
import { useCurrentFrame } from 'remotion';
import { H, W } from '../theme';

/** A full-frame canvas, redrawn from scratch on every frame. */
export const Draw: React.FC<{ draw: (ctx: CanvasRenderingContext2D, frame: number) => void; opacity?: number }> = ({ draw, opacity = 1 }) => {
  const frame = useCurrentFrame();
  const ref = useRef<HTMLCanvasElement>(null);
  useLayoutEffect(() => {
    const ctx = ref.current?.getContext('2d');
    if (!ctx) return;
    ctx.save();
    ctx.clearRect(0, 0, W, H);
    draw(ctx, frame);
    ctx.restore();
  }, [draw, frame]);
  return <canvas ref={ref} width={W} height={H} style={{ position: 'absolute', left: 0, top: 0, opacity }} />;
};
