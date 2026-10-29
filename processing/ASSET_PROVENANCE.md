# Asset provenance — SpellbookScene (state of Oct 29 2026, v0.1-demo build)

Model for every image generated through the API: **muse-image-1.0** (Meta Model API, `https://api.meta.ai/v1/images/generations`
and `/v1/images/edits`). Python is used only for keying, cleanup, masks, crops, layout and export.
Text (titles, descriptions, stats, page numbers) is editable Unity text in OFL fonts, not part of any image.

| Visible element | Sprite / file in Unity | Origin | Prompt / raw source | Processing |
|---|---|---|---|---|
| Book spread (pages, cover, rings) | Art/Spellbook_Spread_v8.png | Muse, /images/edits chain v1–v8 | processing/muse_book_v1/prompt_book*.txt, book_v*_raw.png | crop/alpha, none painted |
| Wooden table | UI/UI_Table.png | Muse /images/generations | processing/ui_prompts.json "UI_Table", processing/muse_ui_v1/UI_Table.png | make_page_frames.py: darken, vignette |
| Page clipping mask | SheetClip (Mask on every page layer) | code, invisible stencil through PageWarp | — | — |
| Book contact shadow | UI/UI_BookShadow.png | **code** (make_page_frames.py, blurred silhouette) | — | a shadow, not decoration |
| Spell animations (4) | Frames/IceStorm_*, FrostBreath_*, IceComet_*, AbsoluteZero_* | Ice Storm: 36-frame APNG loop, see "Animations in the build" below (the creating tool is not recorded in these files). Frost Breath, Ice Comet, Absolute Zero: **Artwork and APNG animation sequences created with Muse; subsequent processing and Unity integration performed for this project. Animation origin confirmed by the creator.** | APNG copies: processing/ice_spells/apng (originals: icedragon-v1, icecomet-v1, frozenknight-shatter). Confirmation: see below | extract_apng.py (compositing, own delays), fix_apng_v1.py (edge fades, ghost haze, green fringe, fade/pause/appear loop), layout_frames_v2.py |
| Medallion thumbnails (4) | UI/UI_Medal{Storm,FrostBreath,IceComet,AbsoluteZero}.png | frames of the animations above | make_medal_thumbs.py | unchanged |
| Medallion underlays (4) | UI/UI_V2_Under{Storm,FrostBreath,IceComet,AbsoluteZero}.png | **Muse /images/edits** (style ref: reference crop) | grimoire_v2/prompts.json, grimoire_v2/raw/Under*.png/.json | key, sepia recolour, fit to window r 176 |
| Medallion frames (one sprite x4) | UI/UI_FrameRing.png | Muse /images/generations | ui_prompts.json "UI_FrameRing", muse_ui_v1/UI_FrameRing.png | make_ui_sprites.py |
| Medallion window mask | UI/UI_CircleMask.png | code (invisible stencil mask) | — | — |
| Selection rim glow + glints | UI/UI_V2_RimGlow.png, UI_V2_Sparkle.png | **Muse /images/generations** | grimoire_v2 RimGlow / Sparkle | luminance key, fit to radius |
| Spell-name ribbons: wide (right page) and compact (x4 medallions) | UI/UI_V2_NameRibbonWide.png, UI_V2_NameRibbonCompact.png | **Muse /images/edits** (style ref: the ICE MAGIC ribbon) | grimoire_v2 NameRibbonWide (NameRibbonCompact generation came out full length, kept as record only) | make_name_ribbons.py: both from one generation, compact = 5 % shorter middle, denser contour |
| ICE MAGIC title ribbon | UI/UI_V2_TitleRibbon.png | **Muse /images/edits** | grimoire_v2 TitleRibbon | key, sepia recolour; the user's own ribbon (decor_provided/title_frame_src.png) is no longer used |
| School tabs (one sprite x5) | UI/UI_TabTongue.png (9-slice) | Muse /images/generations | ui_prompts.json "UI_TabTongue" | make_ui_sprites.py |
| School glyphs | UI/UI_InkSnowflake, UI_InkGlyph0-3 | Muse /images/generations | ui_prompts.json | make_ui_sprites.py (alpha mask, tinted) |
| Active tab glow | UI/UI_V2_SoftGlow.png | **Muse /images/generations** | grimoire_v2 SoftGlow | luminance key |
| Orbits around medallions | UI/UI_V2_OrbitRing(.Top).png | **Muse /images/edits** | grimoire_v2 OrbitRing | key; soft clear field under the medallion label; top variant fades its upper quarter |
| Centre emblem, vertical connectors | UI/UI_V2_CenterEmblem, UI_V2_Connector | **Muse /images/edits** | grimoire_v2 CenterEmblem (retry after one HTTP 500), Connector | key, 3 px/page px export |
| Cities, chalice, compass roses, moon phases | UI/UI_V2_CityLeft/Right, Chalice, Compass, Planets | **Muse /images/edits** | grimoire_v2 raw | key, 3 px/page px export |
| Right page diagram + margin studies/notes | UI/UI_V2_RightDiagram, RightNotesSpine, RightNotesOuter | **Muse /images/edits** (one page drawing, cut in three) | grimoire_v2 RightPage | key, circle/outside masks |
| Margin note columns | UI/UI_Notes2.png | Muse /images/generations | ui_prompts.json "UI_Notes2" | make_ui_sprites.py |
| Outer chains, corners, bottom rules, divider, stat ticks | UI/UI_OrnChain, UI_OrnCorner, UI_OrnBorderL/R, UI_OrnDivider, UI_OrnTick | Muse /images/generations | ui_prompts.json (grimoire section) | make_grimoire_ornaments.py |
| Stat icons | UI/UI_StatMana, Power, Duration, Range | Muse /images/generations | ui_prompts.json | make_ui_sprites.py |
| Technical surface grid (hidden unless -spellbookGrid) | UI/UI_SurfaceGrid_Left/Right.png | **code** | — | not decoration |

## Animations in the build (checked Oct 29 2026 against the files)

157 frames in `Assets/Spellbook/Frames`, all referenced by the scene; timings from `Art/SpellClips.json` and the
LoopPlayer, confirmed by the self-test on the v0.1-demo build (measured loop = loop from frame delays).

| Spell | Frames | Timing | Loop | Source file in the build | Source APNG |
|---|---|---|---|---|---|
| Ice Storm | 36 | 9 fps | 4.00 s | Frames/IceStorm_000-035 | 36-frame APNG, 581×658, 0.11 s per frame; green spill removed, 3-frame seam crossfade (commit 1d22603) |
| Frost Breath | 40 | 0.11 s, frame 36 = 0.22 s | 4.51 s | fix_v1/icedragon-v1_fix_v1.apng | apng/icedragon-v1.apng: 36 frames, 700×700, 0.11 s |
| Ice Comet | 36 | 0.11 s | 3.96 s | fix_v1/icecomet-v1_fix_v1.apng | apng/icecomet-v1.apng: 36 frames, 688×699, 0.11 s |
| Absolute Zero | 45 | 0.12 s, two frames 0.36 s | 5.64 s | fix_v1/frozenknight-shatter_fix_v1.apng | apng/frozenknight-shatter.apng: 40 frames, 700×700, 0.12 s |

Ice Storm source: the Ice Storm frames match, by canvas size (581×658) and frame count (36), an APNG kept outside
the repository whose local file time is Oct 27 2026, 23:33 PT (the time the file was saved on this machine, not a
generation time). The repository holds no request record for this sequence; which tool created it is not
documented here.

History, not in the build: commit db6679f (Oct 28 2026) had a 12-frame Ice Storm loop made from the v2 tornado
still (`muse_book_v1/ice_storm_v2_raw.png`) with muse-image-1.0 `/images/edits`, played at 12 fps. Commit 1d22603
replaced it with the 36-frame loop above; the 12-frame version is no longer used. The static tornado stills
(`muse_book_v1/ice_storm_raw.png`, `ice_storm_v2_raw.png`) are not in the build either. The A–Z letter disc and
the rune fill of empty medallions (commit 8eccfcc) were removed when all four medallions got their spells.
`frozenknight-v1.apng` (another Absolute Zero variant) was not used.

## Replaced in this pass (kept on disk, no longer in the scene)

Procedural (code-drawn): UI_MedalUnder*.png, UI_RightPageInk.png (processing/make_inkwork_v1.py), UI_RimGlow.png,
UI_SoftGlow.png, UI_Sparkle.png (make_ui_sprites.py drew them).
Previous name ribbon UI_Banner.png (Muse, light filled parchment) replaced by the ink ribbons above.
Cut from the user-provided composition: UI_Decor*.png, UI_TitleRibbon.png (processing/make_decor_v2.py).
Backup of these sprites, the previous scene and builder: processing/backup_pre_grimoire_v2/.

## Muse requests in this pass

19 items (17 + NameRibbonWide/NameRibbonCompact) + 1 retry (CenterEmblem returned HTTP 500 once), all `muse-image-1.0`; per-item JSON next to each
raw image records endpoint, size, inputs, full prompt, usage and time. Style reference:
grimoire_v2/ref/light_magic_reference.png (user's Light Magic image) and its crops.

## APNG origin confirmation

The creator confirmed (Oct 28 2026) that the source artwork of Frost Breath, Ice Comet and Absolute Zero was made
with Muse and that their animation sequences were assembled with Muse in a separate Muse chat; the APNG files were
then copied into this project. How Muse assembled the frames is not documented here and is not claimed
(muse-image-1.0 itself produces still images). Evidence, kept locally and out of Git because the screenshot also
shows unrelated chat UI: `processing/provenance_private/muse_apng_confirmation_full.png` (as provided) and
`muse_apng_confirmation_messages.png` (crop of the message column only).

Work done in this project on those animations: frame compositing with the files' own delays, edge fades,
removal of a dark ghost haze and a green key fringe, a fade-out / pause / fade-in loop seam, sizing on the page,
playback and selection in Unity.

## Trailer materials

- `Assets/Spellbook/Trailer/T_*.png`: plates cut from the files above by `processing/trailer/make_trailer_plates.py`
  (no new generation). The Light Magic image (`grimoire_v2/ref/light_magic_reference.png`) is a pre-existing visual
  reference used as style input, not a result of this project.
- `Assets/Spellbook/Trailer/Private/` (git-ignored): crop of the author's screenshot of a third-party Unity Asset
  Store page (Stylized Magic Druid Book – Game Ready Fantasy Spellbook, by Alexander Shmidtheisler), shown only as
  context "Asset Store example". Check the store's terms before publishing the video.
- Trailer music `the-kingdom-revealed.wav`: provided by the creator; its origin and licence are not documented
  here. Kept out of Git (`processing/trailer_private/`, `Assets/Spellbook/Trailer/Private/`) until the terms are
  clear. Edited for length and level by `processing/trailer/make_music.py` (the original copy is unchanged).
