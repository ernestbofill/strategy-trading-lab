import React from 'react';
import { Composition } from 'remotion';
import { Promo } from './Promo';
import { PULSE_DURATION, PulseVideo } from './pulse/Pulse';
import { DURATION, FPS, H, W } from './theme';

export const Root: React.FC = () => (
  <>
    {/* The first video. One composition per language: the same edit, with that language's words and app screens. */}
    <Composition id="promo-en" component={Promo} durationInFrames={DURATION} fps={FPS} width={W} height={H} defaultProps={{ lang: 'en' as const }} />
    <Composition id="promo-ca" component={Promo} durationInFrames={DURATION} fps={FPS} width={W} height={H} defaultProps={{ lang: 'ca' as const }} />
    {/* The second video: a different angle, drawn in code only, in English. */}
    <Composition id="pulse-en" component={PulseVideo} durationInFrames={PULSE_DURATION} fps={FPS} width={W} height={H} />
  </>
);
