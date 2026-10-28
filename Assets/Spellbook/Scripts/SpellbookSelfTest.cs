// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Самопроверка собранного плеера. Включается только аргументом командной строки:
//   ARHeroes-Spellbook-Demo.exe -spellbookSelfTest <report.txt>
// Нажимает кнопки через EventSystem (как настоящий клик), пишет отчёт и скриншот, выходит.
// Без аргумента ничего не делает.
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellbookSelfTest : MonoBehaviour
{
    private const string Flag = "-spellbookSelfTest";

    private string _reportPath;
    private readonly StringBuilder _report = new StringBuilder();
    private int _errorCount;
    private bool _allPassed = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, Flag);
        if (i < 0) return;

        var host = new GameObject("SpellbookSelfTest");
        var test = host.AddComponent<SpellbookSelfTest>();
        test._reportPath = i + 1 < args.Length
            ? args[i + 1]
            : Path.Combine(Application.persistentDataPath, "selftest.txt");
    }

    private void Awake()
    {
        Application.logMessageReceived += OnLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
    }

    private void OnLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            _errorCount++;
            _report.AppendLine("LOG " + type + ": " + message);
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        var player = FindAnyObjectByType<LoopPlayer>();
        var spriteRenderer = player != null ? player.GetComponent<SpriteRenderer>() : null;
        Check("LoopPlayer found", player != null && spriteRenderer != null);
        if (player == null || spriteRenderer == null) { Finish(); yield break; }

        // Слот «Ледяной бури»: 36 кадров, все на месте
        bool framesOk = player.frames != null && player.frames.Length == 36 &&
                        System.Array.TrueForAll(player.frames, f => f != null);
        Check("36 Ice Storm frames, fps 9, loop on, playOnStart off",
            framesOk && Mathf.Approximately(player.fps, 9f) && player.loop && !player.playOnStart);
        if (!framesOk) { Finish(); yield break; }

        var cam = Camera.main;
        var spreadGo = GameObject.Find("Spread");
        var spread = spreadGo != null ? spreadGo.GetComponent<SpriteRenderer>() : null;
        Check("Spread sprite present and visible to camera",
            spread != null && spread.sprite != null && cam != null && IsVisible(cam, spread.bounds));

        if (spread != null)
        {
            Bounds b = spread.bounds;
            var leftSurface = GameObject.Find("PageLeft") != null ? GameObject.Find("PageLeft").GetComponent<PageSurface>() : null;
            var rightSurface = GameObject.Find("PageRight") != null ? GameObject.Find("PageRight").GetComponent<PageSurface>() : null;
            Check("Both pages have a page surface", leftSurface != null && rightSurface != null);
            if (leftSurface == null || rightSurface == null) { Finish(); yield break; }

            // Технический режим: показать равномерную сетку листа
            if (ArgValue("-spellbookGrid") != null || Array.IndexOf(Environment.GetCommandLineArgs(), "-spellbookGrid") >= 0)
                foreach (var g in new[] { "LeftPageGrid", "RightPageGrid" })
                {
                    var grid = GameObject.Find(g);
                    if (grid != null) grid.GetComponentInChildren<Image>(true).enabled = true;
                }

            // Диагностика медальонов: одинаковые круги с крестом вместо рисунков (-spellbookMedalTest)
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-spellbookMedalTest") >= 0)
            {
                Sprite test = CrossedCircle(512, 0.336f);
                for (int i = 0; i < 4; i++)
                {
                    var art = GameObject.Find("MiniWindow_" + i)?.transform.Find("Mask/Art");
                    if (art != null) art.GetComponent<Image>().sprite = test;
                }
            }

            // Всё содержимое страниц садится на лист одной поверхностью; отдельного изгиба текста нет
            int unwarped = 0, graphics = 0;
            foreach (var surface in new[] { leftSurface, rightSurface })
                foreach (var g in surface.GetComponentsInChildren<Graphic>(true))
                {
                    graphics++;
                    var w = g.GetComponent<PageWarp>();
                    if (w == null || !w.enabled) { unwarped++; _report.AppendLine("  not on the page surface: " + g.name); }
                }
            Check("Every page element follows the page surface (" + graphics + " elements, unwarped: " + unwarped + ")",
                unwarped == 0 && graphics > 50);

            // Анимация: LoopPlayer крутит кадры, на листе их показывает Image с той же деформацией
            var surfaceArtGo = GameObject.Find("SpellArtSurface");
            var surfaceArt = surfaceArtGo != null ? surfaceArtGo.GetComponent<Image>() : null;
            Check("Animation is shown on the page surface (LoopPlayer frames mirrored, renderer hidden)",
                surfaceArt != null && surfaceArt.GetComponent<PageWarp>() != null && surfaceArt.GetComponent<SpriteMirror>() != null &&
                !spriteRenderer.enabled && surfaceArt.sprite == spriteRenderer.sprite);

            // Видимая часть анимации по контурам всех 36 кадров — в локальных координатах листа
            Rect content = AnimationContentLocal(player, surfaceArt);
            _report.AppendLine("  animation content (page local px): " + content.ToString("F0"));
            Vector2 half = rightSurface.HalfSize;
            Check("Animation stays inside the right sheet frame",
                content.xMin > -half.x + 45f && content.xMax < half.x - 45f && content.yMax < half.y - 45f && content.yMin > -half.y);
            float worldMinX = float.MaxValue, worldMaxY = float.MinValue, worldMinY = float.MaxValue, worldMaxX = float.MinValue;
            var artCanvas = surfaceArt.canvas.transform;
            for (int k = 0; k <= 20; k++)
            {
                float f = k / 20f;
                foreach (var q in new[] { new Vector2(content.xMin, Mathf.Lerp(content.yMin, content.yMax, f)),
                                          new Vector2(content.xMax, Mathf.Lerp(content.yMin, content.yMax, f)),
                                          new Vector2(Mathf.Lerp(content.xMin, content.xMax, f), content.yMin),
                                          new Vector2(Mathf.Lerp(content.xMin, content.xMax, f), content.yMax) })
                {
                    Vector3 wpt = artCanvas.TransformPoint(rightSurface.Map(q));
                    worldMinX = Mathf.Min(worldMinX, wpt.x); worldMaxX = Mathf.Max(worldMaxX, wpt.x);
                    worldMinY = Mathf.Min(worldMinY, wpt.y); worldMaxY = Mathf.Max(worldMaxY, wpt.y);
                }
            }
            Check("Animation clears the spine rings and stays inside the spread",
                worldMinX > b.center.x + 0.09f && worldMaxX < b.max.x && worldMinY > b.min.y && worldMaxY < b.max.y);

            // Левая страница: 5 пергаментных закладок между листами, подсвечена только первая
            var tabsCanvas = GameObject.Find("LeftPageTabs");
            var tabGlow = GameObject.Find("TabGlow");
            int tabsOk = 0;
            float leftTop = leftSurface.HalfSize.y;
            for (int i = 0; i < 5; i++)
            {
                var tab = GameObject.Find("Tab_" + i);
                if (tab == null || !IsSprite(tab.transform, "UI_TabTongue")) continue;
                Rect r = LocalRect(tab.GetComponent<Graphic>());
                bool tucked = r.yMin < leftTop && r.yMax > leftTop;
                bool lit = tabGlow != null && Mathf.Abs(tabGlow.transform.position.x - tab.transform.position.x) < 0.01f;
                if (tucked && lit == (i == 0)) tabsOk++;
            }
            Check("5 parchment tabs on the left sheet's top edge, tucked between the sheets, only the first lit",
                tabsOk == 5 && tabsCanvas != null && tabsCanvas.GetComponent<Canvas>().sortingOrder < spread.sortingOrder);

            int windowsOk = 0;
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.Find("MiniWindow_" + i);
                if (w == null || !OnSheet(w.transform, leftSurface) || w.transform.Find("Frame") == null) continue;
                bool glow = w.transform.Find("SelectedGlow") != null;
                var miniArt = w.transform.Find("Mask/Art");
                var artImage = miniArt != null ? miniArt.GetComponent<Image>() : null;
                var label = w.transform.Find("Banner/BannerTitle");
                var labelText = label != null ? label.GetComponent<Text>() : null;
                string expected = i == 0 ? "UI_MedalStorm" : "UI_Medal" + i;
                bool filled = artImage != null && artImage.sprite != null && artImage.sprite.name == expected;
                bool ok = i == 0
                    ? glow && filled && labelText != null && labelText.text == "Ice Storm"
                    : !glow && filled && labelText == null;
                if (ok) windowsOk++;
            }
            Check("4 filled medallions on the left sheet, each with its own art; Ice Storm lit", windowsOk == 4);

            // Связка рамка + маска + рисунок + лента одинакова у всех четырёх: размер, масштаб, поворот, отступ, лента
            int sameSet = 0;
            RectTransform f0 = null, b0 = null;
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.Find("MiniWindow_" + i);
                var f = w != null ? (RectTransform)w.transform.Find("Frame") : null;
                var mk = w != null ? (RectTransform)w.transform.Find("Mask") : null;
                var bn = w != null ? (RectTransform)w.transform.Find("Banner") : null;
                if (f == null || mk == null || bn == null || !IsSprite(bn, "UI_Banner")) continue;
                if (i == 0) { f0 = f; b0 = bn; }
                bool flat = f.localRotation == Quaternion.identity && f.localScale == Vector3.one &&
                            ((RectTransform)w.transform).localRotation == Quaternion.identity && w.transform.localScale == Vector3.one &&
                            Mathf.Abs(f.rect.width - f.rect.height) < 0.01f && mk.rect.size == f.rect.size;
                bool same = f0 != null && f.rect.size == f0.rect.size && bn.rect.size == b0.rect.size &&
                            bn.anchoredPosition == b0.anchoredPosition && bn.GetComponent<Image>().color == b0.GetComponent<Image>().color;
                if (flat && same) sameSet++;
            }
            Check("Four identical medallion sets (round frame, mask, one light banner, same offset)", sameSet == 4);

            // Ряды и интервалы медальонов — равные в координатах листа
            var m = new Vector2[4];
            for (int i = 0; i < 4; i++) { var w = GameObject.Find("MiniWindow_" + i); m[i] = w != null ? Local(w.transform) : Vector2.zero; }
            Check("Medallion rows and spacing are equal on the sheet",
                Mathf.Abs(m[0].y - m[1].y) < 0.5f && Mathf.Abs(m[2].y - m[3].y) < 0.5f &&
                Mathf.Abs((m[1].x - m[0].x) - (m[3].x - m[2].x)) < 0.5f && Mathf.Abs(m[0].x - m[2].x) < 0.5f);

            var school = GameObject.Find("SchoolTitle");
            Check("School title on the left sheet", school != null && OnSheet(school.transform, leftSurface) &&
                                                   !string.IsNullOrEmpty(school.GetComponent<Text>().text));

            // Правая страница: баннер с названием, описание, 4 стата — сверху вниз под анимацией
            var spellTitle = GameObject.Find("SpellTitle");
            var spellTitleText = spellTitle != null ? spellTitle.GetComponent<Text>() : null;
            Check("Title banner 'Ice Storm' on the right sheet, below the art",
                spellTitleText != null && spellTitleText.text == "Ice Storm" && OnSheet(spellTitle.transform, rightSurface) &&
                LocalRect(spellTitleText).yMax < content.yMin);

            var description = GameObject.Find("Description");
            var descriptionText = description != null ? description.GetComponent<Text>() : null;
            Check("Description on the right sheet, no PLACEHOLDER marker",
                descriptionText != null && !string.IsNullOrEmpty(descriptionText.text) &&
                !descriptionText.text.Contains("PLACEHOLDER") && OnSheet(description.transform, rightSurface) &&
                spellTitle != null && Local(description.transform).y < Local(spellTitle.transform).y);

            int statsOk = 0;
            foreach (var stat in new[] { "Stat_Mana", "Stat_Power", "Stat_Duration", "Stat_Range" })
            {
                var go = GameObject.Find(stat);
                if (go == null || !OnSheet(go.transform, rightSurface)) continue;
                var icon = go.transform.Find("Icon");
                var value = go.transform.Find("Value");
                if (icon != null && icon.GetComponent<Image>().sprite != null &&
                    value != null && !string.IsNullOrEmpty(value.GetComponent<Text>().text) &&
                    description != null && Local(go.transform).y < Local(description.transform).y)
                    statsOk++;
            }
            Check("4 stats with icons under the description", statsOk == 4);

            // Знаки вкладок: плоские чернильные; снежинка золотая, остальные приглушены
            int glyphsOk = 0;
            for (int i = 0; i < 5; i++)
            {
                var tabGo = GameObject.Find("Tab_" + i);
                var icon = tabGo != null ? tabGo.transform.Find("Icon") : null;
                var img = icon != null ? icon.GetComponent<Image>() : null;
                if (img == null || img.sprite == null || !img.sprite.name.StartsWith("UI_Ink")) continue;
                bool gold = img.color.r > img.color.b + 0.3f && img.color.a > 0.99f;
                if (i == 0 ? gold && img.sprite.name == "UI_InkSnowflake" : !gold && img.color.a < 0.99f) glyphsOk++;
            }
            Check("Tab glyphs are flat ink (snowflake gold, 4 dimmed)", glyphsOk == 5);

            int gridsOk = 0;
            foreach (var g in new[] { "LeftPageGrid", "RightPageGrid" })
            {
                var grid = GameObject.Find(g);
                var img = grid != null ? grid.GetComponentInChildren<Image>(true) : null;
                if (img != null && img.GetComponent<PageWarp>() != null) gridsOk++;
            }
            Check("Surface grid present on both sheets (off unless -spellbookGrid)", gridsOk == 2);

            var table = GameObject.Find("Table");
            var tableSr = table != null ? table.GetComponent<SpriteRenderer>() : null;
            var shadowGo = GameObject.Find("BookShadow");
            var shadowSr = shadowGo != null ? shadowGo.GetComponent<SpriteRenderer>() : null;
            bool tableCovers = false;
            if (tableSr != null && cam != null)
            {
                float dist = tableSr.transform.position.z - cam.transform.position.z;
                Vector3 lo = cam.ViewportToWorldPoint(new Vector3(0f, 0f, dist));
                Vector3 hi = cam.ViewportToWorldPoint(new Vector3(1f, 1f, dist));
                Bounds tb = tableSr.bounds;
                tableCovers = tb.min.x <= lo.x && tb.min.y <= lo.y && tb.max.x >= hi.x && tb.max.y >= hi.y;
            }
            Check("Table fills the frame behind the book, contact shadow under it",
                tableCovers && tableSr.sortingOrder < spread.sortingOrder &&
                shadowSr != null && shadowSr.sprite != null && shadowSr.sortingOrder < spread.sortingOrder &&
                shadowSr.sortingOrder > tableSr.sortingOrder);

            Check("Fonts: titles IM FELL SC, description IM FELL Italic, stat labels EB Garamond",
                FontIs("SpellTitle", "IMFellEnglishSC") && FontIs("SchoolTitle", "IMFellEnglishSC") &&
                FontIs("Description", "IMFellEnglish-Italic") && FontIs("Stat_Mana/Label", "EBGaramond"));
            // Открытая строка характеристик с небольшими разделителями, без замкнутой рамки
            var statsRow = GameObject.Find("Stats");
            int separators = 0;
            if (statsRow != null)
                foreach (Transform c in statsRow.transform)
                    if (c.name == "Separator" && IsSprite(c, "UI_OrnTick")) separators++;
            Check("Open stat row with 3 small separators, no closed frame",
                separators == 3 && GameObject.Find("StatsCartouche") == null);

            // Орнаментальная система: цепочки на внешних полях, заставка заголовка, цепочка знаков,
            // нижние линии с небольшим симметричным просветом вокруг номера страницы
            int ornamentsOk = 0;
            foreach (var canvasName in new[] { "LeftPageCanvas", "RightPageCanvas" })
            {
                var c = GameObject.Find(canvasName);
                if (c == null) continue;
                var bl = c.transform.Find("Orn_Bottom_L"); var br = c.transform.Find("Orn_Bottom_R");
                var num = c.transform.Find("PageNumber");
                if (c.transform.Find("Orn_Chain") == null || bl == null || br == null || num == null) continue;
                float gapL = Local(num).x - LocalRect(bl.GetComponent<Graphic>()).xMax;
                float gapR = LocalRect(br.GetComponent<Graphic>()).xMin - Local(num).x;
                if (gapL > 0f && gapR > 0f && Mathf.Abs(gapL - gapR) < 1f && gapL < 45f) ornamentsOk++;
            }
            Check("Ornaments: outer chains, bottom rules with a small symmetric page-number gap",
                ornamentsOk == 2 && GameObject.Find("Orn_Headpiece") != null && GameObject.Find("Orn_SignChain") != null);

            var rim = GameObject.Find("MiniWindow_0") != null ? GameObject.Find("MiniWindow_0").transform.Find("SelectedGlow") : null;
            Check("Active medallion: narrow rim glow with glints",
                rim != null && IsSprite(rim, "UI_RimGlow") && rim.childCount >= 3);

            // Слои: подложка под анимацией, лицевой слой над ней, и ничто лицевое не заходит на анимацию.
            // Поверхность одна и та же, поэтому непересечение на листе = непересечение на экране
            var backCanvas = GameObject.Find("RightPageBack");
            var frontCanvas = GameObject.Find("RightPageCanvas");
            int backOrder = backCanvas != null ? backCanvas.GetComponent<Canvas>().sortingOrder : int.MaxValue;
            int frontOrder = frontCanvas != null ? frontCanvas.GetComponent<Canvas>().sortingOrder : int.MinValue;
            int artOrder = surfaceArt.canvas.sortingOrder;
            Check("Decor layers: back < animation < front", backOrder < artOrder && artOrder < frontOrder);
            int overlapping = 0;
            if (frontCanvas != null)
                foreach (var g in frontCanvas.GetComponentsInChildren<Graphic>())
                    if (LocalRect(g).Overlaps(content)) { overlapping++; _report.AppendLine("  over the animation: " + g.name); }
            Check("No front decor or text over the animation, all 36 frames (overlapping: " + overlapping + ")", overlapping == 0);

            // Кнопки в стиле книги: пергаментная табличка, небольшие
            int buttonsOk = 0;
            foreach (var n in new[] { "Button_PlayLoop", "Button_Replay", "Button_FlipPage" })
            {
                var btn = FindButton(n);
                var rt = btn != null ? (RectTransform)btn.transform : null;
                if (btn != null && IsSprite(btn.transform, "UI_ButtonPlate") && rt.rect.width <= 240f) buttonsOk++;
            }
            Check("Small book-style buttons", buttonsOk == 3);
        }

        // Никаких новых названий и цифр: в сцене только заданные автором тексты
        var allowed = new System.Collections.Generic.HashSet<string>
        {
            "Ice Storm", SpellbookTextDescription(), "Mana", "Power", "Duration", "Range", "Not set",
            "ICE MAGIC", "1", "2", "Play", "Replay", "Next Page",
        };
        var stray = new System.Collections.Generic.List<string>();
        foreach (var t in FindObjectsByType<Text>())
            if (!allowed.Contains(t.text)) stray.Add(t.text);
        Check("No new names or numbers (stray texts: " + stray.Count + ")", stray.Count == 0);
        foreach (var t in stray) _report.AppendLine("  stray: " + t);

        // До нажатия кадр стоит на первом
        Sprite idle = spriteRenderer.sprite;
        yield return new WaitForSeconds(0.5f);
        Check("Idle before Play shows frame 0 and does not change",
            idle == player.frames[0] && spriteRenderer.sprite == idle);

        // «Проиграть цикл»: цикл длится frames / fps (4 с), с запасом проходят все кадры
        Click("Button_PlayLoop");
        var seen = new System.Collections.Generic.HashSet<Sprite>();
        float until = Time.unscaledTime + player.frames.Length / player.fps + 0.4f;
        while (Time.unscaledTime < until)
        {
            seen.Add(spriteRenderer.sprite);
            yield return null;
        }
        Check("Play cycles frames (distinct seen: " + seen.Count + ")", seen.Count == player.frames.Length);

        string shot = Path.ChangeExtension(_reportPath, ".png");
        ScreenCapture.CaptureScreenshot(shot);
        yield return new WaitForSeconds(0.5f);

        // «Повтор» — сразу первый кадр
        Click("Button_Replay");
        Check("Replay restarts from frame 0", spriteRenderer.sprite == player.frames[0]);
        yield return new WaitForSeconds(0.2f);
        Check("Animation continues after Replay", spriteRenderer.sprite != player.frames[0]);

        // «Перелистнуть» неактивна
        var flip = FindButton("Button_FlipPage");
        Check("Flip button exists and is not interactable", flip != null && !flip.interactable);

        Check("No floating title above the book", GameObject.Find("Title") == null);

        // Запись движения: -spellbookRecord <папка> — кадры экрана с отметками времени
        string recordDir = ArgValue("-spellbookRecord");
        if (recordDir != null)
        {
            Directory.CreateDirectory(recordDir);
            var times = new StringBuilder();
            Click("Button_PlayLoop");
            float t0 = Time.unscaledTime;
            // Число кадров, а не время: если окно на миг теряет фокус, запись всё равно полная
            for (int i = 0; i < 80; i++)
            {
                if (i == 45) { Click("Button_Replay"); times.AppendLine("replay " + (Time.unscaledTime - t0).ToString("F3")); }
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(recordDir, "rec_" + i.ToString("D3") + ".jpg"), tex.EncodeToJPG(80));
                Destroy(tex);
                times.AppendLine(i + " " + (Time.unscaledTime - t0).ToString("F3") + " " + spriteRenderer.sprite.name);
                yield return new WaitForSeconds(0.07f);
            }
            File.WriteAllText(Path.Combine(recordDir, "times.txt"), times.ToString());
        }

        yield return new WaitForSeconds(0.2f);
        Finish();
    }

    // Текст описания задан автором; самопроверка — рантайм-код и не видит редакторский сборщик
    private static string SpellbookTextDescription() =>
        "Summons a vortex of ice shards that slows enemies in an area.";


    private static bool FontIs(string path, string fontName)
    {
        var go = path.Contains("/") ? GameObject.Find(path.Split('/')[0])?.transform.Find(path.Split('/')[1])?.gameObject
                                    : GameObject.Find(path);
        var t = go != null ? go.GetComponent<Text>() : null;
        return t != null && t.font != null && t.font.name.StartsWith(fontName);
    }

    // Точка/прямоугольник элемента в плоских локальных координатах его листа (до деформации)
    private static Vector2 Local(Transform t)
    {
        var g = t.GetComponentInParent<Canvas>();
        Vector3 p = g.transform.InverseTransformPoint(t.position);
        return new Vector2(p.x, p.y);
    }

    private static Rect LocalRect(Graphic g)
    {
        var corners = new Vector3[4];
        g.rectTransform.GetWorldCorners(corners);
        Transform c = g.canvas.transform;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var w in corners)
        {
            Vector3 p = c.InverseTransformPoint(w);
            x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    private static bool OnSheet(Transform t, PageSurface surface)
    {
        Vector2 p = Local(t), h = surface.HalfSize;
        return t.GetComponentInParent<PageSurface>() == surface && Mathf.Abs(p.x) < h.x && Mathf.Abs(p.y) < h.y;
    }

    // Объединение контуров спрайтов всех кадров (tight mesh), переведённое в прямоугольник Image на листе
    private static Rect AnimationContentLocal(LoopPlayer player, Image art)
    {
        if (art == null) return new Rect();
        Rect r = LocalRect(art);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var frame in player.frames)
        {
            Bounds fb = frame.bounds;
            foreach (var v in frame.vertices)
            {
                float nx = (v.x - fb.min.x) / fb.size.x, ny = (v.y - fb.min.y) / fb.size.y;
                x0 = Mathf.Min(x0, nx); y0 = Mathf.Min(y0, ny); x1 = Mathf.Max(x1, nx); y1 = Mathf.Max(y1, ny);
            }
        }
        return Rect.MinMaxRect(r.xMin + x0 * r.width, r.yMin + y0 * r.height, r.xMin + x1 * r.width, r.yMin + y1 * r.height);
    }

    // Тестовая мишень: светлый круг по отверстию рамки, тёмный контур, крест и вписанный квадрат
    private static Sprite CrossedCircle(int size, float holeFrac)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = size * 0.5f, r = size * holeFrac + 3f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 col = new Color32(0, 0, 0, 0);
                if (d <= r)
                {
                    col = new Color32(236, 226, 205, 255);
                    bool rim = d > r - 7f, cross = Mathf.Abs(dx) < 3f || Mathf.Abs(dy) < 3f;
                    bool square = (Mathf.Abs(Mathf.Abs(dx) - r * 0.7071f) < 2.5f && Mathf.Abs(dy) <= r * 0.7071f) ||
                                  (Mathf.Abs(Mathf.Abs(dy) - r * 0.7071f) < 2.5f && Mathf.Abs(dx) <= r * 0.7071f);
                    if (rim || cross) col = new Color32(40, 24, 12, 255);
                    else if (square) col = new Color32(170, 40, 30, 255);
                }
                px[y * size + x] = col;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static string ArgValue(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static bool IsSprite(Transform t, string name)
    {
        var img = t != null ? t.GetComponent<Image>() : null;
        return img != null && img.sprite != null && img.sprite.name == name;
    }



    private static bool IsVisible(Camera cam, Bounds b)
    {
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        return GeometryUtility.TestPlanesAABB(planes, b);
    }

    private static Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private void Click(string name)
    {
        var button = FindButton(name);
        if (button == null) { Check("Button " + name + " exists", false); return; }
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        _report.AppendLine("CLICK " + name);
    }

    private void Check(string label, bool ok)
    {
        if (!ok) _allPassed = false;
        _report.AppendLine((ok ? "PASS " : "FAIL ") + label);
    }

    private void Finish()
    {
        Check("No errors in console (count: " + _errorCount + ")", _errorCount == 0);
        _report.AppendLine(_allPassed ? "RESULT: PASS" : "RESULT: FAIL");
        File.WriteAllText(_reportPath, _report.ToString(), Encoding.UTF8);
        Application.Quit(_allPassed ? 0 : 1);
    }
}
