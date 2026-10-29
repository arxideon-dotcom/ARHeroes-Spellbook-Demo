// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Самопроверка собранного плеера. Включается только аргументом командной строки:
//   ARHeroes-Spellbook-Demo.exe -spellbookSelfTest <report.txt>
// Кнопок нет: проверяет автозапуск, выбор медальонов кликом по экранной точке и циклы, пишет отчёт, выходит.
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
        Check("36 Ice Storm frames, fps 9, loop on",
            framesOk && Mathf.Approximately(player.fps, 9f) && player.loop);
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

            var selector = FindAnyObjectByType<SpellSelector>();
            Check("Spell selector with 4 spells", selector != null && selector.spells != null && selector.spells.Length == 4);
            if (selector == null || selector.spells == null || selector.spells.Length != 4) { Finish(); yield break; }

            // Видимая часть анимации по контурам всех кадров каждого заклинания — в локальных координатах листа.
            // Ice Storm проверяется последним, после цикла выбран он, как при запуске
            Vector2 half = rightSurface.HalfSize;
            var frontCanvas = GameObject.Find("RightPageCanvas");
            var titleGo = GameObject.Find("SpellTitle");
            Rect content = default;
            int insideFrame = 0, clearsSpine = 0, clearOfFront = 0, titleBelow = 0, textsFit = 0;
            for (int si = selector.spells.Length - 1; si >= 0; si--)
            {
                selector.Select(si);
                string spellName = selector.spells[si].title;
                content = AnimationContentLocal(player, surfaceArt);
                _report.AppendLine("  " + spellName + ": " + player.frames.Length + " frames, content (page local px): " + content.ToString("F0"));
                if (content.xMin > -half.x + 45f && content.xMax < half.x - 45f && content.yMax < half.y - 45f && content.yMin > -half.y)
                    insideFrame++;
                else _report.AppendLine("  outside the sheet frame: " + spellName);
                if (ClearsSpine(content, rightSurface, surfaceArt, b)) clearsSpine++;
                else _report.AppendLine("  over the spine or outside the spread: " + spellName);
                int over = 0;
                if (frontCanvas != null)
                    foreach (var g in frontCanvas.GetComponentsInChildren<Graphic>())
                        if (g.name != "SheetClip" && LocalRect(g).Overlaps(content)) { over++; _report.AppendLine("  over the " + spellName + " animation: " + g.name); }
                if (over == 0) clearOfFront++;
                if (titleGo != null && LocalRect(titleGo.GetComponent<Graphic>()).yMax < content.yMin) titleBelow++;
                var descGo = GameObject.Find("Description");
                if (titleGo != null && descGo != null && Fits(titleGo.GetComponent<Text>()) && Fits(descGo.GetComponent<Text>(), 2)) textsFit++;
                else _report.AppendLine("  title or description does not fit: " + spellName);
            }
            Check("Every spell's animation (all frames) stays inside the right sheet frame (" + insideFrame + "/4)", insideFrame == 4);
            Check("Every spell's animation clears the spine rings and stays inside the spread (" + clearsSpine + "/4)", clearsSpine == 4);
            Check("No front decor or text over any spell's animation, all frames (" + clearOfFront + "/4)", clearOfFront == 4);
            Check("Title banner below every spell's animation (" + titleBelow + "/4)", titleBelow == 4);
            Check("Every spell's title fits its banner, description in at most two lines (" + textsFit + "/4)", textsFit == 4);

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
            // Активная закладка выступает над листом на 30 % дальше, низ на месте, знак того же размера
            var tab0 = GameObject.Find("Tab_0");
            var tab1 = GameObject.Find("Tab_1");
            if (tab0 != null && tab1 != null)
            {
                Rect t0r = LocalRect(tab0.GetComponent<Graphic>()), t1r = LocalRect(tab1.GetComponent<Graphic>());
                float out0 = t0r.yMax - leftTop, out1 = t1r.yMax - leftTop;
                var i0 = (RectTransform)tab0.transform.Find("Icon");
                var i1 = (RectTransform)tab1.transform.Find("Icon");
                _report.AppendLine("  tab above the sheet: active " + out0.ToString("F1") + " px, others " + out1.ToString("F1") + " px");
                Check("Active tab sticks out ~30% further, same bottom, same glyph size",
                    out1 > 0f && Mathf.Abs(out0 / out1 - 1.3f) < 0.03f && Mathf.Abs(t0r.yMin - t1r.yMin) < 0.5f &&
                    i0 != null && i1 != null && i0.rect.size == i1.rect.size);
            }
            else Check("Active tab sticks out ~30% further", false);

            int windowsOk = 0;
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.Find("MiniWindow_" + i);
                if (w == null || !OnSheet(w.transform, leftSurface) || w.transform.Find("Frame") == null) continue;
                var glow = w.transform.Find("SelectedGlow");
                var miniArt = w.transform.Find("Mask/Art");
                var artImage = miniArt != null ? miniArt.GetComponent<Image>() : null;
                var label = w.transform.Find("Banner/BannerTitle");
                var labelText = label != null ? label.GetComponent<Text>() : null;
                bool filled = artImage != null && artImage.sprite != null && artImage.sprite.name == MedalArt[i];
                if (glow != null && glow.gameObject.activeSelf == (i == 0) && filled &&
                    labelText != null && labelText.text == SpellTitles[i])
                    windowsOk++;
            }
            Check("4 medallions with the spells' own thumbnails and named banners; only Ice Storm lit", windowsOk == 4);

            // Подписи помещаются: ширина строки шрифта не больше поля текста, у всех лент один размер
            int labelsFit = 0, labelSize = -1;
            bool sameSize = true;
            for (int i = 0; i < 4; i++)
            {
                var label = GameObject.Find("MiniWindow_" + i)?.transform.Find("Banner/BannerTitle")?.GetComponent<Text>();
                if (label == null) continue;
                if (labelSize < 0) labelSize = label.fontSize; else sameSize &= label.fontSize == labelSize;
                if (Fits(label)) labelsFit++; else _report.AppendLine("  does not fit its banner: " + label.text);
            }
            Check("Every medallion label fits its banner, one font size (" + labelsFit + "/4)", labelsFit == 4 && sameSize);

            // Связка рамка + маска + рисунок + лента одинакова у всех четырёх: размер, масштаб, поворот, отступ, лента
            int sameSet = 0;
            RectTransform f0 = null, b0 = null;
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.Find("MiniWindow_" + i);
                var f = w != null ? (RectTransform)w.transform.Find("Frame") : null;
                var mk = w != null ? (RectTransform)w.transform.Find("Mask") : null;
                var bn = w != null ? (RectTransform)w.transform.Find("Banner") : null;
                if (f == null || mk == null || bn == null || !IsSprite(bn, "UI_V2_NameRibbonCompact")) continue;
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
                var bl = c.transform.Find("SheetClip/Orn_Bottom_L"); var br = c.transform.Find("SheetClip/Orn_Bottom_R");
                var num = c.transform.Find("SheetClip/PageNumber");
                if (c.transform.Find("SheetClip/Orn_Chain") == null || bl == null || br == null || num == null) continue;
                float gapL = Local(num).x - LocalRect(bl.GetComponent<Graphic>()).xMax;
                float gapR = LocalRect(br.GetComponent<Graphic>()).xMin - Local(num).x;
                if (gapL > 0f && gapR > 0f && Mathf.Abs(gapL - gapR) < 1f && gapL < 45f) ornamentsOk++;
            }
            Check("Ornaments: outer chains, bottom rules with a small symmetric page-number gap",
                ornamentsOk == 2 && GameObject.Find("Orn_TitleRibbon") != null && GameObject.Find("Decor_Emblem") != null);
            // Подложки внутри медальонов: под иллюстрацией, внутри маски окна
            int underOk = 0;
            for (int i = 0; i < 4; i++)
            {
                var mw = GameObject.Find("MiniWindow_" + i);
                var mask = mw != null ? mw.transform.Find("Mask") : null;
                var u = mask != null ? mask.Find("Underlay") : null;
                var a = mask != null ? mask.Find("Art") : null;
                if (u != null && a != null && u.GetSiblingIndex() < a.GetSiblingIndex() && IsSprite(u, UnderArt[i])) underOk++;
            }
            Check("Each medallion has its own underlay under the art, inside the window mask (" + underOk + "/4)", underOk == 4);

            var rim = GameObject.Find("MiniWindow_0") != null ? GameObject.Find("MiniWindow_0").transform.Find("SelectedGlow") : null;
            Check("Active medallion: narrow rim glow with glints",
                rim != null && IsSprite(rim, "UI_V2_RimGlow") && rim.childCount >= 3);

            // Слои: подложка под анимацией, лицевой слой над ней, и ничто лицевое не заходит на анимацию.
            // Поверхность одна и та же, поэтому непересечение на листе = непересечение на экране
            var backCanvas = GameObject.Find("RightPageBack");
            int backOrder = backCanvas != null ? backCanvas.GetComponent<Canvas>().sortingOrder : int.MaxValue;
            int frontOrder = frontCanvas != null ? frontCanvas.GetComponent<Canvas>().sortingOrder : int.MinValue;
            int artOrder = surfaceArt.canvas.sortingOrder;
            Check("Decor layers: back < animation < front", backOrder < artOrder && artOrder < frontOrder);
            // Обрезка по листу: в каждом слое страницы (кроме закладок и сетки) всё лежит под одной маской размером
            // с лист, и маска сама идёт по поверхности листа (PageWarp), то есть режет по деформированному краю
            int clipped = 0, layers = 0, outside = 0;
            foreach (var surface in new[] { leftSurface, rightSurface })
                foreach (var canvasC in surface.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvasC.name.EndsWith("Tabs") || canvasC.name.EndsWith("Grid")) continue;
                    layers++;
                    var clip = canvasC.transform.Find("SheetClip");
                    var clipRt = clip != null ? (RectTransform)clip : null;
                    bool ok = clip != null && canvasC.transform.childCount == 1 && clip.GetComponent<Mask>() != null &&
                              clip.GetComponent<PageWarp>() != null &&
                              Mathf.Abs(clipRt.rect.width - surface.width) < 0.5f && Mathf.Abs(clipRt.rect.height - surface.height) < 0.5f;
                    foreach (var g in canvasC.GetComponentsInChildren<Graphic>(true))
                        if (g.transform != clip && (clip == null || !g.transform.IsChildOf(clip))) outside++;
                    if (ok) clipped++;
                }
            Check("Every page layer is clipped by the warped sheet mask, tabs excluded (" + clipped + "/" + layers + ", outside: " + outside + ")",
                layers == 5 && clipped == layers && outside == 0 &&
                GameObject.Find("LeftPageTabs") != null && GameObject.Find("LeftPageTabs").GetComponentInChildren<Mask>(true) == null);
            // Кнопок и экранного UI нет — ни видимых, ни невидимых областей клика
            Check("No Play / Replay / Next Page buttons and no screen-space UI",
                FindObjectsByType<Button>().Length == 0 && FindAnyObjectByType<EventSystem>() == null &&
                GameObject.Find("UI") == null);
        }

        // Никаких новых названий и цифр: в сцене только заданные автором тексты
        var allowed = new System.Collections.Generic.HashSet<string>
        {
            "Mana", "Power", "Duration", "Range", "Not set",
            "ICE MAGIC", "1", "2",
        };
        allowed.UnionWith(SpellTitles);
        allowed.UnionWith(SpellDescriptions);
        var stray = new System.Collections.Generic.List<string>();
        foreach (var t in FindObjectsByType<Text>())
            if (!allowed.Contains(t.text)) stray.Add(t.text);
        Check("No new names or numbers (stray texts: " + stray.Count + ")", stray.Count == 0);
        foreach (var t in stray) _report.AppendLine("  stray: " + t);

        // При открытии Ice Storm уже играет сам
        var sel0 = FindAnyObjectByType<SpellSelector>();
        Check("Ice Storm is selected and playing on open, no input",
            sel0 != null && sel0.Selected == 0 && player.IsPlaying && player.frames == sel0.spells[0].frames);
        var seen = new System.Collections.Generic.HashSet<Sprite>();
        float until = Time.unscaledTime + player.frames.Length / player.fps + 0.4f;
        while (Time.unscaledTime < until) { seen.Add(spriteRenderer.sprite); yield return null; }
        Check("Ice Storm cycles all its frames by itself (distinct seen: " + seen.Count + ")", seen.Count == player.frames.Length);

        string shot = Path.ChangeExtension(_reportPath, ".png");
        ScreenCapture.CaptureScreenshot(shot);
        yield return new WaitForSeconds(0.5f);

        Check("No floating title above the book", GameObject.Find("Title") == null);

        // Выбор медальона кликом по точке экрана: та же проверка, что у мыши в SpellSelector.Update.
        // Перед выбором играет прежнее заклинание — оно должно остановиться без следа
        var sel = FindAnyObjectByType<SpellSelector>();
        var artSurface = GameObject.Find("SpellArtSurface").GetComponent<Image>();
        var titleText = GameObject.Find("SpellTitle").GetComponent<Text>();
        var descText = GameObject.Find("Description").GetComponent<Text>();
        Vector2 between = (sel.ScreenCenter(0) + sel.ScreenCenter(1)) * 0.5f;
        Check("A click between medallions selects nothing", !sel.SelectAtScreen(between) && sel.Selected == 0);
        foreach (int i in new[] { 1, 2, 3, 0 })
        {
            string n = SpellTitles[i];
            var spell = sel.spells[i];
            yield return new WaitForSeconds(0.35f);
            bool hit = sel.SelectAtScreen(sel.ScreenCenter(i));
            bool glowsOk = true;
            for (int k = 0; k < sel.glows.Length; k++) glowsOk &= sel.glows[k].activeSelf == (k == i);
            Check(n + ": click on its medallion selects it, only its rim lit", hit && sel.Selected == i && glowsOk);
            Check(n + ": title and description updated", titleText.text == n && descText.text == SpellDescriptions[i]);
            Check(n + ": previous animation stopped, own clip starts at frame 0 and plays",
                player.frames == spell.frames && player.IsPlaying &&
                spriteRenderer.sprite == spell.frames[0] && artSurface.sprite == spell.frames[0]);

            // Три полных цикла без ввода: все кадры, ни одного чужого, кадры идут только вперёд
            // (для Absolute Zero — без обратного хода осколков), длительность цикла из задержек
            float expected = 0f;
            for (int k = 0; k < spell.frames.Length; k++)
                expected += spell.frameDurations != null && spell.frameDurations.Length == spell.frames.Length
                    ? spell.frameDurations[k] : 1f / spell.fps;
            var index = new System.Collections.Generic.Dictionary<Sprite, int>();
            for (int k = 0; k < spell.frames.Length; k++) index[spell.frames[k]] = k;
            var seenSpell = new System.Collections.Generic.HashSet<Sprite>();
            bool foreign = false, backwards = false;
            int prev = 0, wraps = 0;
            float t0 = Time.unscaledTime, firstWrap = -1f, lastWrap = -1f, end = t0 + 3f * expected + 0.5f;
            while (Time.unscaledTime < end)
            {
                yield return null;
                Sprite cur = spriteRenderer.sprite;
                int idx;
                if (!index.TryGetValue(cur, out idx) || !index.ContainsKey(artSurface.sprite)) { foreign = true; continue; }
                seenSpell.Add(cur);
                if (idx < prev)
                {
                    // назад можно только через начало цикла: с конца клипа на его первые кадры
                    if (prev >= spell.frames.Length - 3 && idx <= 2)
                    {
                        wraps++;
                        if (firstWrap < 0f) firstWrap = Time.unscaledTime; else lastWrap = Time.unscaledTime;
                    }
                    else backwards = true;
                }
                prev = idx;
            }
            float loopTime = wraps >= 2 ? (lastWrap - firstWrap) / (wraps - 1) : -1f;
            _report.AppendLine("  " + n + ": " + spell.frames.Length + " frames, " + wraps + " loop restarts, loop " +
                               loopTime.ToString("F2") + " s measured, " + expected.ToString("F2") + " s from frame delays");
            Check(n + ": plays all its frames and no other (distinct: " + seenSpell.Count + ")",
                seenSpell.Count == spell.frames.Length && !foreign);
            Check(n + ": repeats by itself, at least 3 cycles, frames only move forward", wraps >= 3 && !backwards);
            Check(n + ": loop takes its own duration", loopTime > 0f && Mathf.Abs(loopTime - expected) < 0.2f);
        }

        // Запись движения: -spellbookRecord <папка> — кадры экрана с отметками времени
        string recordDir = ArgValue("-spellbookRecord");
        if (recordDir != null)
        {
            Directory.CreateDirectory(recordDir);
            var times = new StringBuilder();
            var recSel = FindAnyObjectByType<SpellSelector>();
            float t0 = Time.unscaledTime;
            int shotIndex = 0;
            // Каждый медальон: клик и два автоматических цикла.
            // Число кадров, а не время: если окно на миг теряет фокус, запись всё равно полная
            foreach (int i in new[] { 1, 2, 3, 0 })
            {
                recSel.SelectAtScreen(recSel.ScreenCenter(i));
                times.AppendLine("select " + SpellTitles[i] + " " + (Time.unscaledTime - t0).ToString("F3"));
                var sp = recSel.spells[i];
                float cycle = 0f;
                for (int k = 0; k < sp.frames.Length; k++)
                    cycle += sp.frameDurations != null && sp.frameDurations.Length == sp.frames.Length ? sp.frameDurations[k] : 1f / sp.fps;
                // Два полных цикла — видно и стык повтора
                float recUntil = Time.unscaledTime + 2f * cycle + 0.3f;
                while (Time.unscaledTime < recUntil) yield return Shot(recordDir, shotIndex++, times, t0, spriteRenderer);
            }
            File.WriteAllText(Path.Combine(recordDir, "times.txt"), times.ToString());
        }

        yield return new WaitForSeconds(0.2f);
        Finish();
    }

    // Тексты заданы автором; самопроверка — рантайм-код и не видит редакторский сборщик
    private static readonly string[] SpellTitles = { "Ice Storm", "Frost Breath", "Ice Comet", "Absolute Zero" };
    private static readonly string[] SpellDescriptions =
    {
        "Summons a vortex of ice shards that slows enemies in an area.",
        "An icy dragon exhales a torrent of freezing breath.",
        "A frozen comet streaks through the air, trailing ice shards.",
        "Extreme cold freezes a knight until the figure shatters.",
    };
    private static readonly string[] MedalArt = { "UI_MedalStorm", "UI_MedalFrostBreath", "UI_MedalIceComet", "UI_MedalAbsoluteZero" };
    private static readonly string[] UnderArt = { "UI_V2_UnderStorm", "UI_V2_UnderFrostBreath", "UI_V2_UnderIceComet", "UI_V2_UnderAbsoluteZero" };

    // Текст в своём поле: строка без переноса не шире поля; текст с переносом — не больше maxLines строк
    private static bool Fits(Text t, int maxLines = 1)
    {
        if (t == null) return false;
        Rect r = t.rectTransform.rect;
        if (t.horizontalOverflow == HorizontalWrapMode.Overflow)
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, t.GetGenerationSettings(Vector2.zero)) / t.pixelsPerUnit <= r.width + 0.5f;
        var gen = new TextGenerator();
        gen.Populate(t.text, t.GetGenerationSettings(r.size));
        return gen.lineCount <= maxLines;
    }

    private IEnumerator Shot(string dir, int i, StringBuilder times, float t0, SpriteRenderer sr)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, "rec_" + i.ToString("D3") + ".jpg"), tex.EncodeToJPG(80));
        Destroy(tex);
        times.AppendLine(i + " " + (Time.unscaledTime - t0).ToString("F3") + " " + sr.sprite.name);
        yield return new WaitForSeconds(0.07f);
    }

    // Контур видимой части анимации, пропущенный через поверхность листа: правее колец корешка и внутри разворота
    private static bool ClearsSpine(Rect content, PageSurface surface, Image art, Bounds spread)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        var canvas = art.canvas.transform;
        for (int k = 0; k <= 20; k++)
        {
            float f = k / 20f;
            foreach (var q in new[] { new Vector2(content.xMin, Mathf.Lerp(content.yMin, content.yMax, f)),
                                      new Vector2(content.xMax, Mathf.Lerp(content.yMin, content.yMax, f)),
                                      new Vector2(Mathf.Lerp(content.xMin, content.xMax, f), content.yMin),
                                      new Vector2(Mathf.Lerp(content.xMin, content.xMax, f), content.yMax) })
            {
                Vector3 w = canvas.TransformPoint(surface.Map(q));
                minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x);
                minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
            }
        }
        return minX > spread.center.x + 0.09f && maxX < spread.max.x && minY > spread.min.y && maxY < spread.max.y;
    }


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
