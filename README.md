# ARHeroes — Living Spellbook Demo

An open grimoire on a wooden table, made for the Meta Global AI Developer Hackathon. The left page shows four
Ice Magic spells as medallions; the right page plays the selected spell's animation in an endless loop, with its
title, description and stat row. The page is alive at once: Ice Storm starts by itself, and a click on a medallion
stops the previous spell and starts the selected one from its first frame.

- **Windows build:** [v0.1-demo release](https://github.com/arxideon-dotcom/ARHeroes-Spellbook-Demo/releases/tag/v0.1-demo)
  (`ARHeroes-Spellbook-Demo-v0.1-demo-Windows-x64.zip`)
- **Demo video:** https://www.youtube.com/watch?v=AgeCmKbe4a0

## The idea: problem → work with Meta → result

- **Problem.** A solo indie developer with no art team needs a living, animated spellbook for a fantasy strategy
  game — art that moves, not static pictures.
- **Work with Meta.** The book, its decoration and the spell artwork were made with Meta's Muse, and the spell
  animation sequences were created with Muse and Meta AI chat; everything was then cleaned, looped and laid out by this project's
  own pipeline (below) and placed into a working Unity application.
- **Result.** A playable book: four looping spell animations drawn on the curved page surface, selected by
  clicking the medallions — not a video of an effect.

## Run the Windows build

1. Download `ARHeroes-Spellbook-Demo-v0.1-demo-Windows-x64.zip` from the release and unpack it anywhere.
2. Run `ARHeroes-Spellbook-Demo/ARHeroes-Spellbook-Demo.exe` (64-bit Windows). Keep the folder together: the EXE
   needs `ARHeroes-Spellbook-Demo_Data/`, `MonoBleedingEdge/`, `D3D12/` and the DLLs next to it.
3. The demo opens in borderless full screen; close it with Alt+F4. Optional window:
   `ARHeroes-Spellbook-Demo.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0`.

Tested on Windows 10 Pro x64 (Direct3D 12, NVIDIA GeForce RTX 5060 Ti), at 1920×1080.

## Controls

- **Left mouse click on a medallion** (its round frame or its name ribbon) — switch to that spell. The previous
  animation stops at once, the new one starts from frame 0, the title and description change, and the selected
  medallion's rim lights up.
- Nothing else is needed: Ice Storm plays on start and every spell repeats endlessly. There are no Play, Pause,
  Replay or page buttons; clicks outside the medallions do nothing.

## The four spells

All animations play in Unity from transparent PNG frames with their own frame timing (`LoopPlayer`).

| Spell | Frames in the build | Timing | Loop | Description on the page |
|---|---|---|---|---|
| Ice Storm | 36 | 9 fps (1/9 s per frame) | 4.00 s | Summons a vortex of ice shards that slows enemies in an area. |
| Frost Breath | 40 | 0.11 s per frame, one frame 0.22 s | 4.51 s | An icy dragon exhales a torrent of freezing breath. |
| Ice Comet | 36 | 0.11 s per frame | 3.96 s | A frozen comet streaks through the air, trailing ice shards. |
| Absolute Zero | 45 | 0.12 s per frame, two frames 0.36 s | 5.64 s | Extreme cold freezes a knight until the figure shatters. |

Frame counts and timings are those of the v0.1-demo build (measured by the self-test). **Ice Storm** is the
existing 36-frame sequence (created in Meta AI chat, see below), with the green spill removed and a 3-frame
crossfade at the loop seam. **Frost Breath, Ice Comet and Absolute Zero** were imported from the provided APNG
files and processed here (`processing/ice_spells/fix_v1/`): their sources have 36 frames at 0.11 s, 36 frames at
0.11 s and 40 frames at 0.12 s; the extra frames and longer holds in the build are the fade-out / pause / fade-in
loop seams added in this project. The stat row (Mana, Power,
Duration, Range) reads **Not set**: the demo has no gameplay numbers.

## Built with Meta — and what was done in this project

- **Image generation:** `muse-image-1.0` through the Meta Model API (`/v1/images/generations` and
  `/v1/images/edits`): the book spread (an edits chain v1–v8), the wooden table and every visible page decoration
  (frames, ribbons, tabs, glyphs, stat icons, ornaments, medallion underlays, page drawings, glows). Prompts and
  raw results are kept in `processing/`; for the decoration pass there is a JSON per request (model, endpoint,
  inputs, prompt, time).
- **Animation sequences:** the 36-frame Ice Storm sequence was created in Meta AI chat and delivered on Oct 27
  2026, 23:33 PT (confirmed by the author; not an API generation). The APNG sequences of Frost Breath, Ice Comet
  and Absolute Zero were created with Muse (confirmed by the creator). `muse-image-1.0` itself returns still images; how Muse assembled the
  sequences is not documented here and is not claimed. The medallion thumbnails of these three spells are frames
  of their own APNGs, not separate generations.
- **Done by the developer in this project:** creative direction and selection; keying and cleanup; APNG
  compositing with the files' own delays; edge fades, removal of a dark ghost haze and a green key fringe; seamless
  loops; sizing each clip by its main figure; mapping every layer onto the curvature of the painted pages
  (`PageSurface` + `PageWarp`); typography, layout, selection logic, the self-test and all Unity integration. The
  book's contact shadow, the invisible page masks and the technical surface grid are drawn by code.

The per-asset table is in [`processing/ASSET_PROVENANCE.md`](processing/ASSET_PROVENANCE.md).

## Open the Unity project

- Unity **6000.4.9f1** with **Windows Build Support (Mono)**. Universal Render Pipeline 17.4.0, Input System
  1.19.0 and uGUI are restored from `Packages/manifest.json`.
- Open `Assets/Spellbook/Scenes/SpellbookScene.unity` and press Play.
- Menu **Spellbook ▸ Build Scene** regenerates the scene from `SpellbookSceneBuilder.cs`. **Spellbook ▸ Build
  Release Player (without Trailer/Private)** builds the public Windows player into `Builds/Release/v0.1-demo/`
  (`Builds/` is git-ignored).

## Pipeline (reproducible)

1. Generate on a flat magenta `#FF00FF` background (glows on black): `processing/muse_book_v1/` (spread),
   `muse_ui_v1/` + `ui_prompts.json` (first UI pass), `grimoire_v2/` (decoration pass: `prompts.json`,
   `muse_gen.py`, `raw/`). The API key is read from the `META_MODEL_API_KEY` environment variable and is never
   stored in the repository.
2. `processing/process_spell.py` keys the background into transparency; `grimoire_v2/make_grimoire_v2.py` and
   `make_name_ribbons.py` recolour the ink to one sepia scale and export sprites.
3. Muse APNGs: `processing/ice_spells/extract_apng.py` composites the frames (blend/dispose, own delays);
   `fix_apng_v1.py` fixes edges, haze, fringe and loop seams; `layout_frames_v2.py` sizes each clip and writes
   `Assets/Spellbook/Art/SpellClips.json`.
4. `Assets/Spellbook/Editor/SpellbookSceneBuilder.cs` builds the whole scene: every page element goes through one
   page surface and is clipped by the warped sheet, so it follows the painted paper.

## Self-test

`ARHeroes-Spellbook-Demo.exe -spellbookSelfTest report.txt [-spellbookRecord <dir>]` checks auto-start, selection
of all four spells by screen-point clicks, at least three loops of each with its own duration and only its own
frames, layout, clipping, fonts, that no buttons exist and that the console has no errors, then quits. Keep the
window in focus: an unfocused player pauses. Result on the v0.1-demo build at 1920×1080: 62/62 PASS.

## Contest video mode

`-spellbookTrailer` starts a separate staged mode used to record the contest video (timed captions, held frames,
dashes instead of "Not set"). It does nothing in a normal launch. Its music and a third-party store screenshot are
kept out of Git and out of the release build (`Assets/Spellbook/Trailer/Private/`, git-ignored), so in the public
build this mode runs without them.

## Project structure

```
Assets/Spellbook/
  Scenes/  SpellbookScene.unity (only start scene, generated by the builder)
  Scripts/ LoopPlayer.cs (frame loop, per-frame delays), SpellSelector.cs (medallion clicks, auto-start),
           PageSurface.cs / PageWarp.cs (page geometry), SpriteMirror.cs,
           SpellbookSelfTest.cs (only with -spellbookSelfTest), TrailerDirector.cs (only with -spellbookTrailer)
  Editor/  SpellbookSceneBuilder.cs
  Frames/  animation frames of the four spells (transparent PNG, 157 in total)
  Art/     Spellbook_Spread_v8.png, PageSurfaces.json, SpellClips.json (titles, descriptions, delays, layout)
  UI/      page sprites (UI_V2_* = decoration pass v2)
  Trailer/ plates for the video mode, cut from saved files (no new generation)
  Fonts/   OFL fonts with licences
processing/  generation, keying and layout scripts, prompts, raw generations, provenance
```

## Licences and provenance

- **Fonts** — SIL Open Font License 1.1; licence texts in `Assets/Spellbook/Fonts/` and in `Licenses/` of the
  release ZIP: IM FELL English SC (titles, labels), IM FELL English Italic (descriptions, stat values, page numbers),
  EB Garamond SemiBold (stat labels).
- **New work:** the code, scene and processed art were made during the hackathon window (Oct 26 – Nov 4, 2026);
  no artwork, code or scenes from the existing ARHeroes game are used. Exceptions: the Unity URP template files,
  and the author's Light Magic image (`processing/grimoire_v2/ref/`), a pre-existing image used only as a style
  reference input.

## Submission

- Demo video (about 3 min): https://www.youtube.com/watch?v=AgeCmKbe4a0
- Cerebral Valley submission — TODO: link.
- Author: Remelt Games (Arxideon), solo, Indie track, "Build for Your World".
