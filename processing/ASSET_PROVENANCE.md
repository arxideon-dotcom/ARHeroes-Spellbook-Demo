# Asset provenance — SpellbookScene (state of Oct 28 2026, final decoration pass)

Model for every generated image: **muse-image-1.0** (Meta Model API, `https://api.meta.ai/v1/images/generations`
and `/v1/images/edits`). Python is used only for keying, cleanup, masks, crops, layout and export.
Text (titles, descriptions, stats, page numbers) is editable Unity text in OFL fonts, not part of any image.

| Visible element | Sprite / file in Unity | Origin | Prompt / raw source | Processing |
|---|---|---|---|---|
| Book spread (pages, cover, rings) | Art/Spellbook_Spread_v8.png | Muse, /images/edits chain v1–v8 | processing/muse_book_v1/prompt_book*.txt, book_v*_raw.png | crop/alpha, none painted |
| Wooden table | UI/UI_Table.png | Muse /images/generations | processing/ui_prompts.json "UI_Table", processing/muse_ui_v1/UI_Table.png | make_page_frames.py: darken, vignette |
| Page clipping mask | SheetClip (Mask on every page layer) | code, invisible stencil through PageWarp | — | — |
| Book contact shadow | UI/UI_BookShadow.png | **code** (make_page_frames.py, blurred silhouette) | — | a shadow, not decoration |
| Spell animations (4) | Frames/IceStorm_*, FrostBreath_*, IceComet_*, AbsoluteZero_* | Ice Storm: Muse frames; other three: user-provided APNG | processing/ice_spells/apng, fix_v1 | unchanged in this pass |
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
