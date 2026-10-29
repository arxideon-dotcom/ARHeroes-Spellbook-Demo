# ARHeroes — Living Spellbook: voice-over and shot list (final cut v5, ElevenLabs TTS voice)

Source of truth: `Assets/Spellbook/Scripts/TrailerDirector.cs` (`Sections`, `Cards`, `Captions`, `Voice`, `Reveal`
and the timing constants). Runs with `-trailerRecord` or `-trailerStills` write `voiceover.srt`; a copy is
`processing/trailer/voiceover.srt`. Recording also writes `clip_frames.txt` (spell frame shown on every video
frame) to prove that nothing moves before 1:50.

Run: `ARHeroes-Spellbook-Demo.exe -spellbookTrailer [1-9]`, `-trailerRange <from> <to>`,
`-trailerRecord <dir>`, `-trailerStills <dir> <t1,t2,...>`, `-trailerTimecode`; or Unity menu Spellbook ▸ Play
Trailer. Keys: Space pause, R restart, 1–9 jump to a section. Encode: `processing/trailer/encode_take.py`.

Attribution kept in the text: **Muse** — the artwork (book, decoration, spell art) and the animated spell
sequences (confirmed by the creator; no claim that muse-image-1.0 itself makes video). **The developer** —
direction, selection, processing and fixes, page geometry, interface and Unity integration. No claims of saved
time or money.

Audio is trailer production only, not a feature of the demo: the voice-over is ElevenLabs TTS and the music
is an original composition written by the author's wife (confirmed by the author; no public credit is added yet).
No audio files are in the repository or in the release build.

The build-up 0:50–1:40 is a reconstruction of the work stages from the scene's real layers and is marked
"Process reconstruction". Real Muse output is shown only as saved files (the ribbon at 1:03–1:10: cleaned version large, raw output as a small labelled inset).

| Time | Section | Picture, cards and captions (on screen) |
|---|---|---|
| 0:00–0:20 | Idea and problem | no book; dark table, slow pan; cards: "A fantasy world of my own." → "One developer." → "No dedicated art team." → "A spellbook with its own visual identity." |
| 0:20–0:35 | Available options | Unity Asset Store page (product "Stylized Magic Druid Book – Game Ready Fantasy Spellbook", author Alexander Shmidtheisler), "Asset Store example"; cards "Ready-made assets or commissioned artwork." → "I wanted a spellbook designed for my world." |
| 0:35–0:50 | The empty book appears | ink silhouette of the spread, then leather, brass and parchment, then the contact shadow; empty pages; "Building the visual world with Muse" |
| 0:50–1:15 | Elements appear | frames → tabs and school title → ribbons and still spell images; "Generated with Muse": cleaned ribbon on parchment + small raw inset "Raw output · chroma background"; the ribbon, title and still Ice Storm appear on the right page; "Process reconstruction" |
| 1:15–1:40 | Assembly on the paper | grid, "Flat layout" → "Mapped to the page"; "Layers, typography, and interaction": description, stats ("—"), drawings, ornaments, selection glow |
| 1:40–1:50 | Before motion | finished still spread, push onto Ice Storm, "Then, bring it to life." |
| 1:50–2:25 | First motion and spells | Ice Storm starts from the same frame; Frost Breath → Ice Comet → Absolute Zero with the cursor marker, full right page, full cycles |
| 2:25–2:45 | Working product | continuous wide shot, four real-path clicks; "Interactive Unity demo", "Real-time spell selection and playback" |
| 2:45–3:00 | Finale | wide shot, small pull-back; "ARHeroes — Living Spellbook" / "Built for a solo developer’s world" |

## Voice-over: ElevenLabs TTS (as spoken; subtitles show from the first to the last word + 0.35 s)

ElevenLabs TTS files (local only, not in Git): `voice_intro.mp3` (16.4 s, phrases 1–4) and `voice_main.mp3` (58.6 s; it repeats the intro and starts
with "Welcome!" — only its phrases 5–16 are used, so nothing is doubled). Text and word timings from a local
speech-recognition pass (`voice_timing.py` → `voice_words.json`; low-confidence words there: "Ready-made",
"spellbook", "Muse", "Ice Storm", "Ice Comet", "knight" — spelled here as in the script). Cuts only in pauses,
no speed change (`make_audio.py`).

| Spoken | Text |
|---|---|
| 0:02.5–0:04.4 | I’m creating a fantasy game on my own. |
| 0:04.9–0:09.4 | I know the world I want to build, but I don’t have a dedicated art or animation team. |
| 0:21.5–0:26.7 | Ready-made assets can give me a starting point, but I wanted something that felt like part of my own world. |
| 0:35.6–0:38.4 | So I started building a living spellbook with Muse. |
| 0:39.6–0:43.9 | With Muse, I developed the artwork for the book, its decoration, and its spells. |
| 0:52.0–0:56.1 | Each element was generated, selected, and cleaned before it went onto the page. |
| 1:17.0–1:20.1 | I mapped every layer to the curvature of the illustrated pages, |
| 1:26.0–1:28.9 | then added typography, ornaments, and interaction. |
| 1:44.5–1:46.9 | Muse also created the animated spell sequences. |
| 1:51.0–1:52.9 | Ice Storm brings the page into motion. |
| 2:01.0–2:02.8 | Frost Breath reveals an icy dragon. |
| 2:09.5–2:11.4 | Ice Comet trails frozen energy. |
| 2:18.0–2:20.6 | Absolute Zero freezes and shatters a knight. |
| 2:26.5–2:29.8 | This is the working Unity application, running in real time. |
| 2:45.5–2:49.3 | AI made this visual production achievable without a dedicated art team. |
| 2:52.0–2:54.3 | I kept control of the world and its presentation. |

## Materials

- Store page: `Assets/Spellbook/Trailer/Private/Resources/AssetStoreExample.png` (third-party, git-ignored; check
  the store's terms before publishing).
- Real asset process: `processing/grimoire_v2/raw/NameRibbonWide.png` + `.json` (muse-image-1.0, /v1/images/edits)
  and the same image keyed by `make_grimoire_v2.key_ink` → `T_Ribbon_Raw.png`, `T_Ribbon_Processed.png`
  (`processing/trailer/make_trailer_plates.py`); in the book it is `UI_V2_NameRibbonWide`.
- Everything else is the live scene. The Light Magic reference is not used in this cut.

## Music and mix

`the-menu-knows-your-name.wav`: original music written by the author's wife (confirmed by the author); local only,
not in Git.
204.8 s; the first 180 s are used, no splice; fade-in 1.5 s, fade-out over the last 3.5 s. Music bed −27 dBFS RMS,
−10 dB under every phrase (down 0.6–0.25 s before the first word, back 0.3–1.2 s after the last); voice −25.5 dBFS
RMS; mix peak −3.8 dBFS. One file `Assets/Spellbook/Trailer/Private/Resources/TrailerAudio.wav` drives both the
staged mode and the exported MP4. The earlier music (the-kingdom-revealed) is no longer used.
