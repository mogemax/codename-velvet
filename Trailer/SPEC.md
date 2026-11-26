# Velvet teaser: design spec

20 s game teaser, set in the world of the Odyssey. 1920×1080, 30 fps. Built with Remotion following the video-shotcraft pipeline in its free-creation mode.

## Brief
- **Goal:** a "coming soon" teaser. Shipwrecked hero → survival on the islands → the Cyclops → logo.
- **Footage:** real gameplay recorded with Unity Recorder and an autopilot (`Recordings/clip_181503.mp4` for island 1, `clip_181840.mp4` for the boss).
- **Audio:** the game's own score ("The Clash of Empires") plus the boss SFX (roar, footsteps, stomp). Three cinematic SFX come from the skill's library.

## Visual tokens (taken from the game)
- **Fonts:** OPTITimes Roman / Italic (the game's UI font) for captions; gelio.retsina (the game's Greek display font) for "PRÓXIMAMENTE".
- **Logo:** `Assets/UI/MainMenu/VelvetTitle.png`.
- **Colours:** black `#07060a`, bronze `#c9a15a`, parchment `#ead9b4`, blood red `#8e1b1b` (the logo's red).
- **Look:** 2.39:1 letterbox (it also hides the HUD), warm sepia grade, vignette, a Greek-key meander band.
- **Motion:** the "premium" preset, `bezier(0.4,0,0.6,1)` with no bounce. Only the logo uses `bezier(0.16,1,0.3,1)`, because it lands.

## Storyboard
| # | Time | Shot | Key motion |
|---|------|------|------------|
| 1 | 0–3.5 s | Invocation: "Musa, cántame del héroe..." | Meander draws itself, text rises in |
| 2 | 3.5–7.5 s | Island 1, boars and trees · "Náufrago en islas olvidadas" | Slow push-in |
| 3 | 7.5–11 s | Chopping near the big tree · "Forja tu regreso" | Slow pull-out |
| 4 | 11–12.5 s | Black · "Pero algo despierta en la cueva..." | Footsteps + riser |
| 5 | 12.5–14.5 s | Cyclops reveal | White flash + roar (slam 1) |
| 6 | 14.5–16.5 s | Cyclops being hit · "Enfrenta al Cíclope" | Tight push-in + stomp (slam 2) |
| 7 | 16.5–20 s | VELVET logo + "PRÓXIMAMENTE" | Logo lands (slam 3), held for 2.5 s, fade to black |

No shot cards are used. Every shot is a simple custom one: graded footage, a push-in and captions.
