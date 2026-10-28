# ARHeroes — Living Spellbook Demo

A hackathon demo: one interactive spellbook page, built end to end with Meta's
models. Open the book, press Play, and the spell animation loops on the page.

> Status: placeholder build. The scene, UI and loop player work; the test
> frames in `Assets/Spellbook/Frames/` are temporary solid-color placeholders
> and will be replaced by real generated frames (see Provenance).

## The idea: problem → Meta work → new result

- **Problem.** An indie dev with no art team needs a living, animated spellbook
  for a VR/MR strategy game — art that moves, not static pictures.
- **Work with Meta.** Spell art is generated with Meta's models through the
  Meta Model API, then processed with a reproducible pipeline (below) and
  placed into a working Unity demo.
- **New result.** A playable book page: generated art, transparent loop
  animation, and buttons that actually control it — not a video of an effect.

## Built with Meta

- **Model:** Muse Image via the Meta Model API — TODO: confirm exact model id
  after the first generation run.
- **What Meta made:** the spell artwork/frames. Assembly, background removal,
  loop preparation and Unity integration are this project's own code, listed
  below, and are described as such — never as model output.

## Pipeline (reproducible)

1. Generate the spell art on a solid magenta `#FF00FF` background.
2. `processing/process_spell.py` — magenta keying into transparency:
   magenta-ness = `min(R, B) - G`, smoothstep 30→110 (low threshold on
   purpose: higher values eat dark ink edges), despill on edge pixels,
   canvas size never changed so frames stay aligned.
3. Transparent PNG frames go to `Assets/Spellbook/Frames/`.
4. `Assets/Spellbook/Scripts/LoopPlayer.cs` cycles the frames
   (12 fps, seamless loop) on the right page of the book.

## How to run

- **In the Editor:** open the project in Unity 6000.4.9f1, open
  `Assets/Spellbook/Scenes/SpellbookScene.unity`, press Play.
- **Standalone:** run the Windows build — TODO: release link.
- **Controls:** `Проиграть цикл` plays the loop from frame 0,
  `Повтор` restarts it, `Перелистнуть` is disabled until pages 2–3 exist.

## Project structure

```
Assets/Spellbook/
  Scenes/  SpellbookScene.unity (only start scene)
  Scripts/ LoopPlayer.cs (frame loop player)
           SpellbookSelfTest.cs (runs only with -spellbookSelfTest)
  Editor/  SpellbookSceneBuilder.cs (Spellbook menu: rebuild scene/player)
  Frames/  spell animation frames (transparent PNG, Sprite)
  Art/     page/cover materials (placeholder colors for now)
processing/  process_spell.py (magenta keying -> transparent frames)
Builds/    Windows player (git-ignored, distributed via release)
```

## Provenance: what is new here

Everything in this repo except the Unity URP template files was created
during the Meta Global AI Developer Hackathon window
(Oct 26 – Nov 4, 2026). No artwork, code or scenes from the existing ARHeroes
game were reused. The placeholder frames and placeholder materials are
temporary test assets, clearly named `PLACEHOLDER_*`, and are replaced by
real hackathon-generated frames in a separate commit.

## Submission

- Demo video (3 min, hackathon features only) — TODO: link.
- Cerebral Valley submission — TODO: link.
- Author: Remelt Games (Arxideon), solo, Indie track, "Build for Your World".
