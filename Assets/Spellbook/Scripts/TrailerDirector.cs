// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Staged mode for the 3-minute contest video. OFF in a normal run: it only starts with the command-line flag
//   -spellbookTrailer [section 1-9]     (no number = the whole 180 s sequence)
//   -trailerRange <from> <to>            (any time range, e.g. for short previews)
//   -trailerRecord <dir>                 (30 fps JPG frames + voiceover.srt + clip_frames.txt, then quit)
//   -trailerStills <dir> <t1,t2,...>     (one still per time code, for layout checks, then quit)
//   -trailerTimecode                     (technical overlay: timecode and section name)
// or from the Editor menu Spellbook ▸ Play Trailer. Keys: Space pause/resume, R restart, 1-9 jump to a section.
//
// Story: idea and problem without the book -> ready-made options -> the empty spread appears -> its elements
// appear group by group -> flat layout vs mapped to the page, then text, ornaments and interaction -> the
// finished, still spread -> first motion -> the four spells -> continuous working product -> finale.
// The build-up is a reconstruction of the work stages from the real scene layers (marked "Process
// reconstruction"); real Muse results are shown as saved files.
//
// Everything is a pure function of time (Apply), so seeking or restarting restores layer visibility, geometry
// and playback completely. Only runtime colours, PageWarp.enabled, grid visibility and LoopPlayer pause are
// touched; nothing is saved, and none of this runs in a normal launch (the demo's auto-start is unchanged).
// Spell selection goes through SpellSelector.SelectAtScreen — the same path as a real mouse click.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TrailerDirector : MonoBehaviour
{
    public const string Flag = "-spellbookTrailer";
#if UNITY_EDITOR
    public const string EditorKey = "SpellbookTrailerPlay";
#endif

    [Header("Fonts")]
    public Font titleFont;
    public Font textFont;
    [Header("Real materials (Assets/Spellbook/Trailer, processing/trailer/make_trailer_plates.py)")]
    public Texture2D ribbonRaw;
    public Texture2D ribbonCleaned;

    // =====================================================================================================
    // The one place for timings, captions and voice-over (seconds). Total 180.
    // =====================================================================================================
    private struct Section { public string name; public float start, end; }
    private static readonly Section[] Sections =
    {
        new Section { name = "1 Idea and problem", start = 0f, end = 20f },
        new Section { name = "2 Available options", start = 20f, end = 35f },
        new Section { name = "3 The empty book appears", start = 35f, end = 50f },
        new Section { name = "4 Elements appear", start = 50f, end = 75f },
        new Section { name = "5 Assembly on the paper", start = 75f, end = 100f },
        new Section { name = "6 Before motion", start = 100f, end = 110f },
        new Section { name = "7 First motion and spells", start = 110f, end = 145f },
        new Section { name = "8 Working product", start = 145f, end = 165f },
        new Section { name = "9 Finale", start = 165f, end = 180f },
    };

    // Short on-screen cards (big type) — the story reads without a voice
    private static readonly (float start, float end, string text, Vector2 pos, int size)[] Cards =
    {
        (1.8f, 5.2f, "A fantasy world of my own.", new Vector2(-330f, 90f), 72),
        (4.8f, 7.8f, "One developer.", new Vector2(360f, -20f), 72),
        (7.4f, 11.8f, "No dedicated art team.", new Vector2(-120f, -120f), 72),
        (12.4f, 19.6f, "A spellbook with its own visual identity.", new Vector2(0f, 40f), 76),
        (21.0f, 26.4f, "Ready-made assets\nor commissioned artwork.", new Vector2(-520f, 60f), 56),
        (26.2f, 34.0f, "I wanted a spellbook\ndesigned for my world.", new Vector2(-520f, 60f), 56),
    };

    // Stage captions (top centre)
    private static readonly (float start, float end, string text)[] Captions =
    {
        (21.0f, 34.4f, "Asset Store example"),
        (37.5f, 49.6f, "Building the visual world with Muse"),
        (63.8f, 69.4f, "Generated with Muse"),
        (77.0f, 81.0f, "Flat layout"),
        (81.0f, 85.2f, "Mapped to the page"),
        (85.5f, 99.4f, "Layers, typography, and interaction"),
        (101.5f, 108.5f, "Then, bring it to life."),
        (145.6f, 154.6f, "Interactive Unity demo"),
        (155.0f, 164.4f, "Real-time spell selection and playback"),
    };
    // Small corner note over the reconstructed build-up
    private static readonly (float start, float end)[] Reconstruction = { (50.2f, 63.4f), (69.8f, 99.6f) };

    // Voice-over as recorded: subtitles = the words actually spoken, from the first word to the last word + 0.35 s.
    // Generated from processing/trailer/voice_lines.json (make_audio.py places the recordings at these times)
    private static readonly (float start, float end, string text)[] Voice =
    {
        (2.50f, 4.73f, "I’m creating a fantasy game on my own."),
        (4.92f, 9.79f, "I know the world I want to build, but I don’t have a dedicated art or animation team."),
        (21.50f, 27.05f, "Ready-made assets can give me a starting point, but I wanted something that felt like part of my own world."),
        (35.60f, 38.75f, "So I started building a living spellbook with Muse."),
        (39.60f, 44.27f, "With Muse, I developed the artwork for the book, its decoration, and its spells."),
        (52.00f, 56.43f, "Each element was generated, selected, and cleaned before it went onto the page."),
        (77.00f, 80.41f, "I mapped every layer to the curvature of the illustrated pages,"),
        (86.00f, 89.21f, "then added typography, ornaments, and interaction."),
        (104.50f, 107.27f, "Muse also created the animated spell sequences."),
        (111.00f, 113.25f, "Ice Storm brings the page into motion."),
        (121.00f, 123.19f, "Frost Breath reveals an icy dragon."),
        (129.50f, 131.73f, "Ice Comet trails frozen energy."),
        (138.00f, 140.97f, "Absolute Zero freezes and shatters a knight."),
        (146.50f, 150.15f, "This is the working Unity application, running in real time."),
        (165.50f, 169.61f, "AI made this visual production achievable without a dedicated art team."),
        (172.00f, 174.65f, "I kept control of the world and its presentation."),
    };

    // Book build-up (the scene's own layers)
    private const float SilhouetteIn = 36f, SilhouetteFull = 39.5f, MaterialsIn = 39.5f, MaterialsFull = 44.5f;
    private const float ShadowIn = 41f, ShadowFull = 45f;
    // Generation block: the cleaned asset on parchment is the main shot; the raw output with its keying
    // background is a small inset (under 15 % of the frame) for about one second; then the cleaned asset
    // shrinks toward the right page, where the same ribbon appears in the book
    private const float PlatesIn = 63.5f, RawInsetIn = 64.0f, RawInsetOut = 65.7f, MoveIn = 67.6f, PlatesOut = 69.6f;
    private static readonly (float from, float to, bool flat)[] Geometry = { (77f, 81f, true), (81f, 300f, false) };
    private const float GridIn = 77f, GridOut = 83.5f;
    // group -> (start, duration, mode, stagger per element); mode 0 = ink silhouette then colour, 1 = soft fade, 2 = text
    private static readonly Dictionary<string, (float t0, float dur, int mode, float step)> Reveal =
        new Dictionary<string, (float, float, int, float)>
    {
        { "frame", (50.5f, 2.2f, 0, 0.5f) },
        { "tabs", (54.4f, 1.6f, 1, 0.12f) },
        { "ribbon", (55.0f, 2.6f, 0, 0f) },
        { "schoolText", (56.8f, 1.4f, 2, 0f) },
        { "banner", (58.4f, 2.2f, 0, 0.4f) },
        { "medal", (59.4f, 2.6f, 0, 0.3f) },
        { "bannerText", (60.2f, 1.4f, 2, 0.35f) },
        { "titleBanner", (69.9f, 2.0f, 0, 0f) },
        { "spellTitle", (71.3f, 1.4f, 2, 0f) },
        { "spellArt", (71.5f, 3.0f, 0, 0f) },
        { "desc", (85.6f, 2.0f, 2, 0f) },
        { "divider", (86.2f, 2.0f, 0, 0f) },
        { "stats", (88.0f, 2.0f, 0, 0.12f) },
        { "statsText", (88.8f, 1.6f, 2, 0.08f) },
        { "decorL", (90.0f, 3.0f, 0, 0.12f) },
        { "decorR", (91.0f, 3.0f, 0, 0.3f) },
        { "chrome", (93.0f, 3.0f, 0, 0.1f) },
        { "glow", (96.6f, 2.0f, 1, 0f) },
    };
    private static readonly Color InkColor = new Color(0.16f, 0.1f, 0.06f, 1f);

    // Motion: every clip is held on its first frame until LiveStart
    private const float LiveStart = 110f;
    private struct Shot { public int spell; public float start, end; }
    private static readonly Shot[] Spells =
    {
        new Shot { spell = 1, start = 118f, end = 127f },
        new Shot { spell = 2, start = 127f, end = 135f },
        new Shot { spell = 3, start = 135f, end = 145f },
    };
    private const float ClickAfter = 0.8f, PushFrom = 1.2f, PushTo = 2.7f, BackFor = 1.5f;
    private static readonly (float time, int spell)[] ProofClicks = { (147f, 1), (151.5f, 2), (156f, 3), (160.5f, 0) };
    private const float CursorTravel = 1.1f;

    // Camera poses (book units; the book faces the camera, see SpellbookSceneBuilder.SetupCamera)
    private static readonly Vector3 Wide = new Vector3(0f, 0.02f, -2.1f);
    private static readonly Vector3 TableA = new Vector3(-0.07f, 0.03f, -2.25f);
    private static readonly Vector3 TableB = new Vector3(0.07f, 0.0f, -2.25f);
    private static readonly Vector3 High = new Vector3(0f, 0.02f, -2.55f);
    private static readonly Vector3 RightFull = new Vector3(0.43f, 0.03f, -1.72f);
    private static readonly Vector3 ArtClose = new Vector3(0.43f, 0.18f, -1.42f);
    private static readonly Vector3 ProofPos = new Vector3(0f, 0.06f, -2.3f);
    private static readonly Vector3 FinalPos = new Vector3(0f, 0.10f, -2.8f);
    private static readonly Vector3 FinalDrift = new Vector3(0f, 0.10f, -2.9f);

    // =====================================================================================================
    private float _t, _rangeStart, _rangeEnd;
    private bool _paused, _timecode, _live;
    private string _recordDir, _stillsDir;
    private float[] _stills;
    private int _frame;
    private Camera _cam;
    private SpellSelector _sel;
    private LoopPlayer _player;
    private SpriteRenderer _clipRenderer;
    private SpriteRenderer _spread, _shadow;
    private Color _spreadColor, _shadowColor;
    private PageWarp[] _warps;
    private Image[] _grids;
    private readonly List<(Graphic g, Color c, string group, int index)> _layers = new List<(Graphic, Color, string, int)>();
    private readonly List<(float time, int spell)> _clicks = new List<(float, int)>();
    private int _nextClick;
    private float _clickAt = -10f;
    private readonly StringBuilder _clipLog = new StringBuilder();

    private RectTransform _overlay;
    private Image _dim, _black, _backdrop, _lowBand, _topBand, _cursor, _ring;
    private RawImage _store, _ribbonRaw, _ribbonClean;
    private Text _card, _stage, _note, _title, _title2, _subtitle, _tech, _rawCap, _cleanCap;
    private AudioSource _music;
    private Texture2D _storeTex;

    private void Awake()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool on = Array.IndexOf(args, Flag) >= 0;
#if UNITY_EDITOR
        if (UnityEditor.SessionState.GetBool(EditorKey, false)) { on = true; UnityEditor.SessionState.SetBool(EditorKey, false); }
#endif
        int st = Array.IndexOf(args, "-trailerStills");
        if (st >= 0) on = true;
        if (!on) { Destroy(gameObject); return; }

        int section = 0;
        int i = Array.IndexOf(args, Flag);
        if (i >= 0 && i + 1 < args.Length) int.TryParse(args[i + 1], out section);
        _rangeStart = section >= 1 && section <= Sections.Length ? Sections[section - 1].start : 0f;
        _rangeEnd = section >= 1 && section <= Sections.Length ? Sections[section - 1].end : Sections[Sections.Length - 1].end;
        int rg = Array.IndexOf(args, "-trailerRange");
        if (rg >= 0 && rg + 2 < args.Length)
        {
            _rangeStart = F(args[rg + 1]);
            _rangeEnd = F(args[rg + 2]);
        }
        _timecode = Array.IndexOf(args, "-trailerTimecode") >= 0;
        int r = Array.IndexOf(args, "-trailerRecord");
        _recordDir = r >= 0 && r + 1 < args.Length ? args[r + 1] : null;
        if (st >= 0 && st + 2 < args.Length)
        {
            _stillsDir = args[st + 1];
            _stills = Array.ConvertAll(args[st + 2].Split(','), F);
        }

        Application.runInBackground = true;          // a lost focus must not pause the take
        Cursor.visible = false;
        string dir = _recordDir ?? _stillsDir;
        if (dir != null)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "voiceover.srt"), Srt(), new UTF8Encoding(false));
        }
        if (_recordDir != null) Time.captureFramerate = 30;   // exact 1/30 s per frame: clips keep their own delays
    }

    private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

    private IEnumerator Start()
    {
        yield return null;                           // let SpellSelector.Start run first
        _cam = Camera.main;
        _sel = FindAnyObjectByType<SpellSelector>();
        _player = _sel.player;
        _clipRenderer = _player.GetComponent<SpriteRenderer>();
        var book = GameObject.Find("Book");
        _spread = GameObject.Find("Spread").GetComponent<SpriteRenderer>();
        _shadow = GameObject.Find("BookShadow").GetComponent<SpriteRenderer>();
        _spreadColor = _spread.color; _shadowColor = _shadow.color;
        _warps = book.GetComponentsInChildren<PageWarp>(true);
        _grids = new[] { GridImage("LeftPageGrid"), GridImage("RightPageGrid") };
        _storeTex = Resources.Load<Texture2D>("AssetStoreExample");
        // Stats stay on the page; values that are not defined read as an em dash in the video
        var stats = GameObject.Find("Stats");
        if (stats != null)
            foreach (var t in stats.GetComponentsInChildren<Text>(true))
                if (t.name == "Value") t.text = "—";
        CollectLayers(book.transform);
        BuildOverlay();
        // Soundtrack of the staged mode: music + recorded voice mixed in one file (Assets/Spellbook/Trailer/Private/
        // Resources, made by processing/trailer/make_audio.py). It follows the trailer clock; a recording muxes the same file
        // offline (encode_take.py), because frame capture runs slower than real time
        var clip = Resources.Load<AudioClip>("TrailerAudio");
        if (clip != null && _recordDir == null && _stills == null)
        {
            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = clip; _music.playOnAwake = false; _music.loop = false; _music.volume = 1f;
        }
        foreach (var s in Spells) _clicks.Add((s.start + ClickAfter, s.spell));
        _clicks.AddRange(ProofClicks);
        if (_stills != null) { StartCoroutine(Stills()); yield break; }
        Seek(_rangeStart);
    }

    // Every page graphic, grouped by what it is; masks, clip rects and the technical grid are left alone
    private void CollectLayers(Transform book)
    {
        var counts = new Dictionary<string, int>();
        foreach (var g in book.GetComponentsInChildren<Graphic>(true))
        {
            string group = GroupOf(g);
            if (group == null) continue;
            counts.TryGetValue(group, out int n);
            counts[group] = n + 1;
            _layers.Add((g, g.color, group, n));
        }
    }

    private static string GroupOf(Graphic g)
    {
        if (g.GetComponent<Mask>() != null || g.name == "SheetClip") return null;
        var canvas = g.GetComponentInParent<Canvas>();
        string c = canvas != null ? canvas.name : "";
        if (c.EndsWith("Grid")) return null;
        if (c.EndsWith("Tabs")) return "tabs";
        if (c.EndsWith("Back")) return c.StartsWith("Left") ? "decorL" : "decorR";
        if (c.EndsWith("Art")) return "spellArt";
        if (Under(g, "SelectedGlow")) return "glow";
        if (g.name == "Frame") return "frame";
        if (Under(g, "Banner")) return g.name == "BannerTitle" ? "bannerText" : "banner";
        if (g.name == "Art" || g.name == "Underlay") return "medal";
        if (Under(g, "Orn_TitleRibbon")) return g.name == "SchoolTitle" ? "schoolText" : "ribbon";
        if (Under(g, "TitleBanner")) return g.name == "SpellTitle" ? "spellTitle" : "titleBanner";
        if (g.name == "Description") return "desc";
        if (g.name == "Orn_Divider") return "divider";
        if (Under(g, "Stats")) return g is Text ? "statsText" : "stats";
        return "chrome";
    }

    private static bool Under(Component c, string name)
    {
        for (var t = c.transform; t != null; t = t.parent) if (t.name == name) return true;
        return false;
    }

    private IEnumerator Stills()
    {
        _paused = true;
        foreach (var t in _stills)
        {
            Seek(t);
            yield return null;
            Apply(t);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_stillsDir, "still_" + t.ToString("000.0", System.Globalization.CultureInfo.InvariantCulture) + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
        Application.Quit();
    }

    private static Image GridImage(string name)
    {
        var g = GameObject.Find(name);
        return g != null ? g.GetComponentInChildren<Image>(true) : null;
    }

    private void Click(int spell)
    {
        _sel.SelectAtScreen(_sel.ScreenCenter(spell));
        _clickAt = _t;
    }

    private void Seek(float t)
    {
        _t = t;
        int last = 0;
        _nextClick = 0;
        for (int k = 0; k < _clicks.Count; k++)
            if (_clicks[k].time <= t) { last = _clicks[k].spell; _nextClick = k + 1; }
        _sel.SelectAtScreen(_sel.ScreenCenter(last));
        _live = t >= LiveStart;
        if (!_live) _player.Pause();                 // first frame, held
        _clickAt = -10f;
        SyncMusic(true);
        Apply(t);
    }

    private void SyncMusic(bool hard)
    {
        if (_music == null) return;
        float len = _music.clip.length;
        if (_paused || _t >= len - 0.05f) { if (_music.isPlaying) _music.Pause(); return; }
        if (!_music.isPlaying) { _music.time = _t; _music.Play(); return; }
        if (hard || Mathf.Abs(_music.time - _t) > 0.08f) _music.time = _t;
    }

    private void Update()
    {
        if (_sel == null || _stills != null) return;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) { _paused = !_paused; Time.timeScale = _paused ? 0f : 1f; SyncMusic(true); }
            if (kb.rKey.wasPressedThisFrame) { _paused = false; Time.timeScale = 1f; Seek(_rangeStart); return; }
            for (int s = 0; s < Sections.Length; s++)
                if (kb[(Key)((int)Key.Digit1 + s)].wasPressedThisFrame)
                {
                    _rangeStart = Sections[s].start; _rangeEnd = Sections[s].end;
                    _paused = false; Time.timeScale = 1f; Seek(_rangeStart); return;
                }
        }
        if (_paused) return;
        _t += Time.deltaTime;
        // First motion: the held first frame simply starts playing — same picture, no cut
        if (!_live && _t >= LiveStart) { _live = true; _player.Resume(); }
        while (_nextClick < _clicks.Count && _clicks[_nextClick].time <= _t) Click(_clicks[_nextClick++].spell);
        if (_t >= _rangeEnd)
        {
            _t = _rangeEnd;
            Apply(_t);
            if (_recordDir != null)
            {
                File.WriteAllText(Path.Combine(_recordDir, "clip_frames.txt"), _clipLog.ToString());
                Application.Quit();
                return;
            }
            _paused = true; Time.timeScale = 0f;
            SyncMusic(true);
            return;
        }
        SyncMusic(false);
        Apply(_t);
    }

    private void LateUpdate()
    {
        if (_recordDir == null || _sel == null || _paused || _t >= _rangeEnd) return;
        _clipLog.Append(_frame).Append(' ').Append(_t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
                .Append(' ').Append(_clipRenderer.sprite != null ? _clipRenderer.sprite.name : "-").Append('\n');
        StartCoroutine(Capture(_frame++));
    }

    private IEnumerator Capture(int i)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(_recordDir, "f_" + i.ToString("D5") + ".jpg"), tex.EncodeToJPG(92));
        Destroy(tex);
    }

    // ---------- everything visual is a pure function of time ----------
    private void Apply(float t)
    {
        _cam.transform.position = CameraAt(t);
        _cam.transform.rotation = Quaternion.identity;

        // The spread: nothing before 36 s, then an ink silhouette, then its own materials; the contact shadow last.
        // Only the single spread sprite fades, so no inner layer can show through it
        float sil = S(t, SilhouetteIn, SilhouetteFull), mat = S(t, MaterialsIn, MaterialsFull);
        var sc = Color.Lerp(InkColor, _spreadColor, mat);
        sc.a = _spreadColor.a * sil;
        _spread.color = sc;
        _spread.enabled = sil > 0.001f;
        var shc = _shadowColor; shc.a *= S(t, ShadowIn, ShadowFull);
        _shadow.color = shc;
        _shadow.enabled = shc.a > 0.001f;

        // Page layers, group by group
        foreach (var (g, c, group, index) in _layers)
        {
            if (g == null) continue;
            var rv = Reveal.TryGetValue(group, out var v) ? v : Reveal["chrome"];
            float t0 = rv.t0 + rv.step * index;
            Color col = c;
            if (rv.mode == 0)
            {
                float a = S(t, t0, t0 + rv.dur * 0.45f), b = S(t, t0 + rv.dur * 0.2f, t0 + rv.dur);
                col = Color.Lerp(new Color(InkColor.r, InkColor.g, InkColor.b, c.a), c, b);
                col.a = c.a * a;
            }
            else col.a = c.a * S(t, t0, t0 + rv.dur);
            if (g.color != col) g.color = col;
        }

        bool grid = t >= GridIn && t < GridOut;
        foreach (var gr in _grids) if (gr != null && gr.enabled != grid) gr.enabled = grid;
        bool flat = false;
        foreach (var gm in Geometry) if (t >= gm.from && t < gm.to) flat = gm.flat;
        foreach (var w in _warps) if (w != null && w.enabled == flat) w.enabled = !flat;

        // Overlay: calm dark surroundings for the opening, black in/out
        _dim.color = new Color(0f, 0f, 0f, 0.74f * (1f - S(t, 34.5f, 37.5f)));
        _black.color = new Color(0f, 0f, 0f, Mathf.Max(1f - S(t, 0f, 2.5f), S(t, 178.6f, 180f)));

        float store = Band(t, 20.4f, 34.6f, 0.8f, 1.0f);
        Alpha(_store, store);
        _store.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.97f, 1.03f, S(t, 20.4f, 34.6f));
        float plates = Band(t, PlatesIn, PlatesOut, 0.6f, 1.0f);
        _backdrop.color = new Color(0.035f, 0.03f, 0.03f, plates);
        float clean = Band(t, PlatesIn, PlatesOut - 0.1f, 0.6f, 0.9f);
        float move = S(t, MoveIn, PlatesOut - 0.2f);
        Alpha(_ribbonClean, clean);
        _ribbonClean.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(0f, 130f), new Vector2(380f, -40f), move);
        _ribbonClean.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, move);
        Alpha(_cleanCap, clean * (1f - S(t, MoveIn - 0.4f, MoveIn)));
        float raw = Band(t, RawInsetIn, RawInsetOut, 0.35f, 0.35f);
        Alpha(_ribbonRaw, raw); Alpha(_rawCap, raw);

        // Cards
        string card = null; float ca = 0f; Vector2 cp = Vector2.zero; int cs = 72;
        foreach (var c in Cards)
            if (t >= c.start && t < c.end) { card = c.text; ca = Band(t, c.start, c.end, 0.7f, 0.6f); cp = c.pos; cs = c.size; }
        _card.text = card ?? "";
        _card.fontSize = cs;
        _card.alignment = TextAnchor.MiddleCenter;
        _card.rectTransform.sizeDelta = new Vector2(cp.x < -400f ? 760f : 1500f, 260f);
        _card.rectTransform.anchoredPosition = cp + new Vector2(0f, -14f * (1f - S(ca, 0f, 1f)));
        Alpha(_card, ca);

        string stage = null; float sa = 0f;
        foreach (var c in Captions)
            if (t >= c.start && t < c.end) { stage = c.text; sa = Band(t, c.start, c.end, 0.5f, 0.5f); }
        _stage.text = stage ?? "";
        _stage.fontSize = stage == "Then, bring it to life." ? 66 : 52;
        Alpha(_stage, sa);
        if (stage == "Asset Store example") _stage.rectTransform.anchoredPosition = new Vector2(330f, 478f);
        else _stage.rectTransform.anchoredPosition = new Vector2(0f, 468f);

        float note = 0f;
        foreach (var r in Reconstruction) note = Mathf.Max(note, Band(t, r.start, r.end, 0.5f, 0.5f));
        Alpha(_note, note * 0.9f);

        float tf = Band(t, 167.5f, 180f, 2f, 1.4f);
        _title.text = "ARHeroes — Living Spellbook";
        Alpha(_title, tf);
        Alpha(_title2, tf);

        float top = Mathf.Max(sa, tf);
        _topBand.color = new Color(0f, 0f, 0f, 0.6f * top); _topBand.enabled = top > 0.001f;

        string line = ""; float la = 0f;
        foreach (var v in Voice)
            if (t >= v.start && t < v.end) { line = v.text; la = Band(t, v.start, v.end, 0.25f, 0.25f); }
        _subtitle.text = line;
        Alpha(_subtitle, la);
        _lowBand.color = new Color(0f, 0f, 0f, 0.62f * la); _lowBand.enabled = la > 0.001f;

        UpdateCursor(t);

        if (_timecode)
            _tech.text = string.Format("{0}:{1:00.0}  {2}{3}", (int)(t / 60f), t % 60f, SectionName(t), _paused ? "  [paused]" : "");
    }

    private Vector3 CameraAt(float t)
    {
        if (t < 35f) return Vector3.Lerp(TableA, TableB, S(t, 0f, 35f));
        if (t < 50f) return Vector3.Lerp(Vector3.Lerp(TableB, High, S(t, 35f, 36.5f)), Wide, S(t, 36.5f, 50f));
        if (t >= 69.6f && t < 77f) return Vector3.Lerp(Wide, RightFull, S(t, 69.6f, 71.6f) * (1f - S(t, 75f, 77f)));
        if (t >= 100f && t < 110f) return Vector3.Lerp(Wide, ArtClose, S(t, 100.5f, 110f));
        if (t >= 110f && t < 118f) return Vector3.Lerp(Vector3.Lerp(ArtClose, RightFull, S(t, 110f, 113f)), Wide, S(t, 116.5f, 118f));
        foreach (var s in Spells)
            if (t >= s.start && t < s.end)
            {
                float u = t - s.start;
                return Vector3.Lerp(Wide, RightFull, S(u, PushFrom, PushTo) * (1f - S(u, s.end - s.start - BackFor, s.end - s.start)));
            }
        if (t >= 145f && t < 165f) return Vector3.Lerp(Wide, ProofPos, S(t, 145f, 147f));
        if (t >= 165f) return Vector3.Lerp(Vector3.Lerp(ProofPos, FinalPos, S(t, 165f, 169.5f)), FinalDrift, S(t, 169.5f, 180f));
        return Wide;
    }

    private void UpdateCursor(float t)
    {
        float alpha = Band(t, 145.6f, 164.4f, 0.5f, 0.5f);
        foreach (var s in Spells) alpha = Mathf.Max(alpha, Band(t, s.start - 0.6f, s.start + ClickAfter + 1.2f, 0.4f, 0.5f));
        Vector2 pos = ScreenToOverlay(_sel.ScreenCenter(0));
        for (int k = 0; k < _clicks.Count; k++)
        {
            Vector2 p = ScreenToOverlay(_sel.ScreenCenter(_clicks[k].spell));
            if (t < _clicks[k].time) { pos = Vector2.Lerp(pos, p, S(t, _clicks[k].time - CursorTravel, _clicks[k].time)); break; }
            pos = p;
        }
        _cursor.rectTransform.anchoredPosition = pos;
        _cursor.color = new Color(1f, 0.93f, 0.75f, alpha);
        float since = t - _clickAt;
        float ring = _clickAt > 0f && since >= 0f && since < 0.7f ? 1f - since / 0.7f : 0f;
        _ring.rectTransform.anchoredPosition = pos;
        _ring.rectTransform.localScale = Vector3.one * (0.6f + Mathf.Max(0f, since) * 1.5f);
        _ring.color = new Color(1f, 0.85f, 0.5f, ring * alpha);
    }

    private static string SectionName(float t)
    {
        foreach (var s in Sections) if (t >= s.start && t < s.end) return s.name;
        return "end";
    }

    private static float S(float t, float a, float b)
    {
        float x = Mathf.Clamp01((t - a) / (b - a));
        return x * x * (3f - 2f * x);
    }

    private static float Band(float t, float a, float b, float fin, float fout) => S(t, a, a + fin) * (1f - S(t, b - fout, b));

    private static void Alpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color; c.a = a; g.color = c;
        g.enabled = a > 0.001f;
    }

    private Vector2 ScreenToOverlay(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, null, out var local);
        return local;
    }

    private static string Srt()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Voice.Length; i++)
            sb.Append(i + 1).Append('\n').Append(Tc(Voice[i].start)).Append(" --> ").Append(Tc(Voice[i].end)).Append('\n')
              .Append(Voice[i].text).Append("\n\n");
        return sb.ToString();
    }

    private static string Tc(float s)
    {
        var ts = TimeSpan.FromSeconds(s);
        return string.Format("{0:00}:{1:00}:{2:00},{3:000}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
    }

    // ---------- overlay UI (screen space, above the scene; 1920x1080 reference) ----------
    private void BuildOverlay()
    {
        var go = new GameObject("TrailerOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        _overlay = (RectTransform)go.transform;

        _dim = Panel("Dim", Color.clear);
        _backdrop = Panel("Backdrop", Color.clear);

        // Store page on the right half, the cards on the left
        _store = Raw("AssetStore", _storeTex);
        if (_storeTex != null)
        {
            _store.rectTransform.sizeDelta = new Vector2(860f * _storeTex.width / _storeTex.height, 860f);
            _store.rectTransform.anchoredPosition = new Vector2(330f, -10f);
        }
        // One asset: the cleaned version large on parchment, the raw output as a small labelled inset
        _ribbonClean = Raw("RibbonCleaned", ribbonCleaned); SizeW(_ribbonClean, 1300f);
        _ribbonRaw = Raw("RibbonRaw", ribbonRaw); SizeW(_ribbonRaw, 640f); _ribbonRaw.rectTransform.anchoredPosition = new Vector2(600f, -330f);

        _topBand = Panel("TopBand", Color.clear, Gradient(false));
        _topBand.rectTransform.anchorMin = new Vector2(0f, 1f);
        _topBand.rectTransform.pivot = new Vector2(0.5f, 1f);
        _topBand.rectTransform.sizeDelta = new Vector2(0f, 230f);
        _lowBand = Panel("LowBand", Color.clear, Gradient(true));
        _lowBand.rectTransform.anchorMax = new Vector2(1f, 0f);
        _lowBand.rectTransform.pivot = new Vector2(0.5f, 0f);
        _lowBand.rectTransform.sizeDelta = new Vector2(0f, 270f);

        _card = Label("Card", "", titleFont, 72, Vector2.zero, TextAnchor.MiddleCenter, new Vector2(1500f, 260f));
        _card.color = new Color(0.97f, 0.9f, 0.74f, 0f);
        _card.lineSpacing = 1.1f;
        _stage = Label("Stage", "", titleFont, 52, new Vector2(0f, 468f), TextAnchor.MiddleCenter, new Vector2(1700f, 80f));
        _cleanCap = Label("CleanCaption", "Cleaned asset \u00B7 transparent, ready for Unity", textFont, 34, new Vector2(-260f, -168f), TextAnchor.MiddleCenter, new Vector2(900f, 50f));
        _rawCap = Label("RawCaption", "Raw output \u00B7 chroma background", textFont, 30, new Vector2(600f, -172f), TextAnchor.MiddleCenter, new Vector2(640f, 44f));
        _note = Label("ProcessReconstruction", "Process reconstruction", textFont, 28, new Vector2(760f, 498f), TextAnchor.MiddleRight, new Vector2(380f, 40f));
        _title = Label("Title", "", titleFont, 70, new Vector2(0f, 478f), TextAnchor.MiddleCenter, new Vector2(1700f, 100f));
        _title.color = new Color(0.97f, 0.9f, 0.74f, 0f);
        _title2 = Label("Title2", "Built for a solo developer’s world", textFont, 42, new Vector2(0f, 412f), TextAnchor.MiddleCenter, new Vector2(1500f, 60f));
        _subtitle = Label("Subtitle", "", textFont, 46, new Vector2(0f, -448f), TextAnchor.MiddleCenter, new Vector2(1560f, 130f));

        _cursor = Panel("Cursor", Color.clear, Disc(64, 0.42f, 0f));
        Point(_cursor.rectTransform, 34f);
        _ring = Panel("ClickRing", Color.clear, Disc(128, 0.46f, 0.38f));
        Point(_ring.rectTransform, 96f);
        _black = Panel("Black", Color.black);
        _tech = Label("Timecode", "", textFont, 26, new Vector2(-940f, 520f), TextAnchor.UpperLeft, new Vector2(900f, 40f));
        _tech.rectTransform.pivot = new Vector2(0f, 1f);
        _tech.color = new Color(1f, 1f, 0.6f, _timecode ? 1f : 0f);
        _tech.enabled = _timecode;
    }

    private static void Point(RectTransform rt, float size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
    }

    private Image Panel(string name, Color c, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_overlay, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.color = c; img.raycastTarget = false;
        return img;
    }

    private RawImage Raw(string name, Texture tex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(_overlay, false);
        var img = go.GetComponent<RawImage>();
        img.texture = tex; img.raycastTarget = false; img.color = new Color(1f, 1f, 1f, 0f); img.enabled = false;
        return img;
    }

    private static void SizeW(RawImage img, float w)
    {
        var tex = img.texture;
        img.rectTransform.sizeDelta = tex != null ? new Vector2(w, w * tex.height / tex.width) : new Vector2(w, w);
    }

    private Text Label(string name, string value, Font font, int size, Vector2 pos, TextAnchor align, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(_overlay, false);
        var t = go.GetComponent<Text>();
        t.text = value; t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.alignment = align; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = new Color(0.97f, 0.94f, 0.87f, 0f); t.enabled = false;
        var ol = go.GetComponent<Outline>();
        ol.effectColor = new Color(0f, 0f, 0f, 0.9f); ol.effectDistance = new Vector2(2f, -2f);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = box;
        return t;
    }

    private static Sprite Gradient(bool fromBottom)
    {
        var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[4 * 64];
        for (int y = 0; y < 64; y++)
        {
            float a = fromBottom ? 1f - y / 63f : y / 63f;
            for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)(255 * a));
        }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite Disc(int size, float outer, float inner)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / size;
                float a = Mathf.Clamp01((outer - d) * size) * (inner > 0f ? Mathf.Clamp01((d - inner) * size) : 1f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
