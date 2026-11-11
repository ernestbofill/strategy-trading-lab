import { random } from 'remotion';
import { H, W } from '../theme';

// A small 3D line renderer on a 2D canvas: points are projected by hand and drawn as glowing, additive strokes.
// The world has +x right, +y up and +z away from the viewer.

export type V3 = [number, number, number];
/** Where the camera stands, where it looks (radians) and its focal length in pixels. */
export type Cam = { pos: V3; yaw: number; pitch: number; focal: number };
/** A point on screen with its scale (pixels per world unit), or null behind the camera. */
export type P2 = [number, number, number] | null;

export const project = (p: V3, cam: Cam): P2 => {
  let x = p[0] - cam.pos[0];
  let y = p[1] - cam.pos[1];
  let z = p[2] - cam.pos[2];
  const cy = Math.cos(cam.yaw);
  const sy = Math.sin(cam.yaw);
  [x, z] = [x * cy - z * sy, x * sy + z * cy];
  const cp = Math.cos(cam.pitch);
  const sp = Math.sin(cam.pitch);
  [y, z] = [y * cp - z * sp, y * sp + z * cp];
  if (z < 20) return null;
  const s = cam.focal / z;
  return [W / 2 + x * s, H / 2 - y * s, s];
};

/** Smooth noise in 0..1 along x, the same on every render. */
export const noise1 = (x: number, seed: string) => {
  const i = Math.floor(x);
  const t = x - i;
  const a = random(`${seed}${i}`);
  const b = random(`${seed}${i + 1}`);
  return a + (b - a) * t * t * (3 - 2 * t);
};

/** One heartbeat on a monitor, for a phase of 0..1: the small P wave, the QRS spike and the T wave. */
export const ecg = (phase: number) => {
  const p = phase - Math.floor(phase);
  const bump = (centre: number, width: number, height: number) => height * Math.exp(-(((p - centre) / width) ** 2));
  return bump(0.15, 0.03, 0.12) + bump(0.3, 0.012, -0.18) + bump(0.335, 0.014, 1) + bump(0.37, 0.014, -0.3) + bump(0.6, 0.05, 0.28);
};

/** A neon line through the points: a wide soft stroke, then a thin hot core. A null point breaks the line. */
export const glowLine = (ctx: CanvasRenderingContext2D, points: P2[], color: string, width: number, alpha = 1, glow = 28, core = true) => {
  const path = () => {
    ctx.beginPath();
    let pen = false;
    for (const p of points) {
      if (!p) {
        pen = false;
        continue;
      }
      if (pen) ctx.lineTo(p[0], p[1]);
      else ctx.moveTo(p[0], p[1]);
      pen = true;
    }
  };
  ctx.globalCompositeOperation = 'lighter';
  ctx.lineJoin = 'round';
  ctx.lineCap = 'round';
  ctx.globalAlpha = alpha;
  ctx.shadowColor = color;
  ctx.shadowBlur = glow;
  ctx.strokeStyle = color;
  ctx.lineWidth = width;
  path();
  ctx.stroke();
  ctx.shadowBlur = 0;
  if (core) {
    ctx.globalAlpha = alpha * 0.75;
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = Math.max(1, width * 0.3);
    path();
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
};

/** A soft glowing dot. */
export const flare = (ctx: CanvasRenderingContext2D, p: P2, color: string, radius: number, alpha = 1) => {
  if (!p) return;
  ctx.globalCompositeOperation = 'lighter';
  const fill = ctx.createRadialGradient(p[0], p[1], 0, p[0], p[1], radius);
  fill.addColorStop(0, '#ffffff');
  fill.addColorStop(0.15, color);
  fill.addColorStop(1, `${color}00`);
  ctx.globalAlpha = alpha;
  ctx.fillStyle = fill;
  ctx.beginPath();
  ctx.arc(p[0], p[1], radius, 0, Math.PI * 2);
  ctx.fill();
  ctx.globalAlpha = 1;
};
