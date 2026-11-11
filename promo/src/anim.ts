import { interpolate, spring } from 'remotion';
import { FPS } from './theme';

const clamp = { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' } as const;

/** interpolate, clamped at both ends. */
export const ci = (f: number, input: number[], output: number[]) => interpolate(f, input, output, clamp);

/** A spring that starts at a frame. */
export const sp = (f: number, at: number, damping = 13, stiffness = 140) =>
  spring({ frame: f - at, fps: FPS, config: { damping, stiffness, mass: 0.7 } });
