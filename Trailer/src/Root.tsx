import React from 'react';
import { Composition, continueRender, delayRender, staticFile } from 'remotion';
import { DURATION, Teaser } from './Teaser';

// The game's own fonts, loaded before the first frame renders.
const FONTS: [string, string][] = [
  ['Opti', 'fonts/OPTITimes-Roman.otf'],
  ['OptiItalic', 'fonts/OPTITimesRoman-Italic.otf'],
  ['Retsina', 'fonts/gelio.retsina.ttf'],
];
const handle = delayRender('fonts', { timeoutInMilliseconds: 120000 });
Promise.all(FONTS.map(([name, file]) => new FontFace(name, `url(${staticFile(file)})`).load().then((f) => document.fonts.add(f))))
  .catch((e) => console.error('font load failed', e)) // render with fallback fonts rather than hang
  .then(() => continueRender(handle));

export const Root: React.FC = () => (
  <Composition id="VelvetTeaser" component={Teaser} durationInFrames={DURATION} fps={30} width={1920} height={1080}
    defaultProps={{ bgm: true }} />
);
