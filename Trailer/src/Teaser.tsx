import React from 'react';
import {
  AbsoluteFill, Audio, Easing, Img, OffthreadVideo, Sequence, interpolate, staticFile, useCurrentFrame,
} from 'remotion';

// ---- Design tokens (SPEC.md): bronze / parchment / blood red on black, the game's own fonts ----
const C = { black: '#07060a', bronze: '#c9a15a', parchment: '#ead9b4', blood: '#8e1b1b' };
const GRADE = 'sepia(0.28) saturate(0.92) contrast(1.12) brightness(0.92)';
const BAR = 138; // 2.39:1 letterbox; also hides the game HUD (hearts, clock, inventory)
const EASE_IN = Easing.bezier(0.4, 0, 0.6, 1); // "premium" preset: slow, no bounce
const FPS = 30;
const s = (sec: number) => Math.round(sec * FPS);

// ---- Single source of truth for timing; SFX and captions derive from it ----
export const SHOTS = {
  intro: { from: 0, duration: 105 },
  island: { from: 105, duration: 120 },
  gather: { from: 225, duration: 105 },
  omen: { from: 330, duration: 45 },
  boss1: { from: 375, duration: 60 },
  boss2: { from: 435, duration: 60 },
  title: { from: 495, duration: 105 },
} as const;
export const DURATION = 600;

const fade = (f: number, inStart: number, inLen: number, outStart: number, outLen: number) =>
  interpolate(f, [inStart, inStart + inLen, outStart, outStart + outLen], [0, 1, 1, 0], {
    extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: EASE_IN,
  });

// Greek key (meander) band that draws itself from the centre outwards.
const Meander: React.FC<{ progress: number; width?: number; y?: number }> = ({ progress, width = 760, y = 0 }) => {
  const unit = 40;
  const n = Math.floor(width / unit);
  let d = '';
  for (let i = 0; i < n; i++) {
    const x = i * unit;
    d += `M${x},32 L${x},0 L${x + 32},0 L${x + 32},24 L${x + 8},24 L${x + 8},8 L${x + 24},8 L${x + 24},16 L${x + 16},16 `;
  }
  return (
    <svg width={n * unit} height={44} viewBox={`-2 -6 ${n * unit + 4} 44`}
      style={{ position: 'absolute', left: '50%', top: `calc(50% + ${y}px)`, transform: 'translate(-50%,-50%)' }}>
      <path d={d} fill="none" stroke={C.bronze} strokeWidth={3} pathLength={1}
        strokeDasharray={1} strokeDashoffset={1 - progress} opacity={0.9} />
      <line x1={0} x2={n * unit} y1={36} y2={36} stroke={C.bronze} strokeWidth={2} pathLength={1}
        strokeDasharray={1} strokeDashoffset={1 - progress} opacity={0.6} />
    </svg>
  );
};

const Caption: React.FC<{ text: string; from: number; to: number; italic?: boolean; size?: number; y?: number }> =
  ({ text, from, to, italic, size = 46, y = 300 }) => {
    const f = useCurrentFrame();
    const o = fade(f, from, 15, to - 12, 12);
    const rise = interpolate(f, [from, from + 20], [14, 0], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: EASE_IN });
    return (
      <div style={{
        position: 'absolute', left: 0, right: 0, top: `calc(50% + ${y}px)`, transform: `translateY(calc(-50% + ${rise}px))`,
        textAlign: 'center', opacity: o, color: C.parchment, fontSize: size,
        fontFamily: italic ? 'OptiItalic' : 'Opti', letterSpacing: italic ? '0.04em' : '0.32em',
        textTransform: italic ? 'none' : 'uppercase', textShadow: '0 2px 18px rgba(0,0,0,0.9)',
      }}>{text}</div>
    );
  };

// Gameplay shot: graded, slow push-in, vignette.
const Footage: React.FC<{ src: string; startSec: number; duration: number; zoom: [number, number]; volume?: number; flashIn?: boolean; origin?: string }> =
  ({ src, startSec, duration, zoom, volume = 0.3, flashIn, origin = '50% 50%' }) => {
    const f = useCurrentFrame();
    const scale = interpolate(f, [0, duration], zoom, { easing: Easing.out(Easing.quad) });
    const flash = flashIn ? interpolate(f, [0, 8], [0.85, 0], { extrapolateRight: 'clamp' }) : 0;
    return (
      <AbsoluteFill style={{ backgroundColor: C.black }}>
        <AbsoluteFill style={{ transform: `scale(${scale})`, transformOrigin: origin }}>
          <OffthreadVideo src={staticFile(src)} startFrom={s(startSec)} volume={volume} style={{ width: '100%', height: '100%', filter: GRADE }} />
        </AbsoluteFill>
        <AbsoluteFill style={{ background: 'radial-gradient(ellipse at center, rgba(0,0,0,0) 45%, rgba(10,6,2,0.75) 100%)' }} />
        <AbsoluteFill style={{ backgroundColor: '#fff4dc', opacity: flash }} />
      </AbsoluteFill>
    );
  };

const Title: React.FC = () => {
  const f = useCurrentFrame(); // local to the title shot
  const land = interpolate(f, [5, 20], [1.35, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: Easing.bezier(0.16, 1, 0.3, 1) });
  const o = interpolate(f, [5, 14], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });
  const glow = interpolate(f, [5, 40], [0.55, 0.18], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' });
  return (
    <AbsoluteFill style={{ background: `radial-gradient(ellipse at center, rgba(142,27,27,${glow}) 0%, ${C.black} 62%)` }}>
      <Img src={staticFile('img/VelvetTitle.png')} style={{
        position: 'absolute', left: '50%', top: '44%', width: 1000, opacity: o,
        transform: `translate(-50%,-50%) scale(${land})`, filter: 'drop-shadow(0 10px 40px rgba(0,0,0,0.8))',
      }} />
      <Meander progress={interpolate(f, [30, 60], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp', easing: EASE_IN })} width={560} y={175} />
      <div style={{
        position: 'absolute', left: 0, right: 0, top: '50%', transform: 'translateY(240px)', textAlign: 'center',
        fontFamily: 'Retsina', fontSize: 52, letterSpacing: '0.35em', color: C.bronze,
        opacity: interpolate(f, [40, 58], [0, 1], { extrapolateLeft: 'clamp', extrapolateRight: 'clamp' }),
      }}>PRÓXIMAMENTE</div>
    </AbsoluteFill>
  );
};

// ---- Sound: relative pins only (SHOTS.x.from + offset) ----
const SFX: { from: number; src: string; volume: number; startSec?: number; duration?: number; note: string }[] = [
  { from: SHOTS.intro.from, src: 'audio/air-woosh-deep.mp3', volume: 0.35, note: 'sea wind under the invocation' },
  { from: SHOTS.island.from - 8, src: 'audio/whoosh-big.mp3', volume: 0.3, note: 'cut into the island' },
  { from: SHOTS.omen.from, src: 'audio/PasosBoss.mp3', volume: 1, duration: SHOTS.omen.duration, note: 'cyclops footsteps in the dark' },
  { from: SHOTS.omen.from, src: 'audio/riser-cine.mp3', volume: 0.55, startSec: 4.8 - SHOTS.omen.duration / FPS, duration: SHOTS.omen.duration, note: 'riser peaks on the boss cut' },
  { from: SHOTS.boss1.from, src: 'audio/RugidoBoss.mp3', volume: 1, duration: s(3), note: 'roar on reveal' },
  { from: SHOTS.boss1.from, src: 'audio/impact-epic-trailer.mp3', volume: 0.55, duration: s(2), note: 'slam 1/3' },
  { from: SHOTS.boss2.from, src: 'audio/PisotonBoss.wav', volume: 0.9, note: 'stomp on second boss cut, slam 2/3' },
  { from: SHOTS.title.from + 5, src: 'audio/impact-epic-trailer.mp3', volume: 0.85, note: 'logo lands, slam 3/3 (peak)' },
];

export const Teaser: React.FC<{ bgm: boolean }> = ({ bgm }) => {
  const f = useCurrentFrame();
  const { intro, island, gather, omen, boss1, boss2, title } = SHOTS;
  const end = DURATION;
  return (
    <AbsoluteFill style={{ backgroundColor: C.black }}>
      {/* 1. Invocation */}
      <Sequence from={intro.from} durationInFrames={intro.duration}>
        <AbsoluteFill>
          <Meander progress={interpolate(f, [0, 40], [0, 1], { extrapolateRight: 'clamp', easing: EASE_IN })} y={-90} />
          <Caption text="Musa, cántame del héroe..." from={15} to={98} italic size={58} y={0} />
          <Caption text="que se perdió entre islas y monstruos" from={45} to={98} italic size={40} y={70} />
        </AbsoluteFill>
      </Sequence>

      {/* 2. Island exploration */}
      <Sequence from={island.from} durationInFrames={island.duration}>
        <Footage src="clips/island.mp4" startSec={0.5} duration={island.duration} zoom={[1.2, 1.3]} />
        <Caption text="Náufrago en islas olvidadas" from={20} to={island.duration} y={290} />
      </Sequence>

      {/* 3. Gathering / fighting */}
      <Sequence from={gather.from} durationInFrames={gather.duration}>
        <Footage src="clips/island.mp4" startSec={12} duration={gather.duration} zoom={[1.32, 1.22]} />
        <Caption text="Forja tu regreso" from={15} to={gather.duration} y={290} />
      </Sequence>

      {/* 4. Omen */}
      <Sequence from={omen.from} durationInFrames={omen.duration}>
        <Caption text="Pero algo despierta en la cueva..." from={4} to={omen.duration} italic size={50} y={0} />
      </Sequence>

      {/* 5. Cyclops */}
      <Sequence from={boss1.from} durationInFrames={boss1.duration}>
        <Footage src="clips/boss.mp4" startSec={1} duration={boss1.duration} zoom={[1.05, 1.15]} origin="50% 30%" volume={0.4} flashIn />
      </Sequence>
      <Sequence from={boss2.from} durationInFrames={boss2.duration}>
        <Footage src="clips/boss.mp4" startSec={11.5} duration={boss2.duration} zoom={[1.3, 1.45]} origin="50% 22%" volume={0.4} flashIn />
        <Caption text="Enfrenta al Cíclope" from={8} to={boss2.duration} y={290} />
      </Sequence>

      {/* 6. Title card */}
      <Sequence from={title.from} durationInFrames={title.duration}>
        <Title />
      </Sequence>

      {/* Letterbox + final fade */}
      <AbsoluteFill style={{ pointerEvents: 'none' }}>
        <div style={{ position: 'absolute', top: 0, left: 0, right: 0, height: BAR, background: '#000' }} />
        <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, height: BAR, background: '#000' }} />
      </AbsoluteFill>
      <AbsoluteFill style={{ backgroundColor: '#000', opacity: interpolate(f, [end - 12, end], [0, 1], { extrapolateLeft: 'clamp' }) }} />

      {/* Music: quiet opening of the game's own score, then its loud finale for the boss + title */}
      {bgm && (
        <>
          <Sequence from={0} durationInFrames={omen.from}>
            <Audio src={staticFile('audio/music.mp3')} volume={(t) => 0.6 * fade(t, 0, 20, omen.from - 15, 15)} />
          </Sequence>
          <Sequence from={boss1.from} durationInFrames={end - boss1.from}>
            <Audio src={staticFile('audio/music.mp3')} startFrom={s(154)} volume={(t) => 0.55 * fade(t, 0, 4, end - boss1.from - 25, 25)} />
          </Sequence>
        </>
      )}
      {SFX.map((x, i) => (
        <Sequence key={i} from={x.from} durationInFrames={x.duration}>
          <Audio src={staticFile(x.src)} volume={x.volume} startFrom={x.startSec ? s(x.startSec) : 0} />
        </Sequence>
      ))}
    </AbsoluteFill>
  );
};
