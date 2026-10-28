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
            float centre = b.center.x;
            Vector3 slot = player.transform.position;
            Check("Art slot sits on the right page", slot.x > centre && b.Contains(new Vector3(slot.x, slot.y, b.center.z)));

            // Видимая часть анимации — по контурам всех 36 кадров, а не по прямоугольнику кадра 0
            Rect content = AnimationContent(player, spriteRenderer);
            _report.AppendLine("  animation content (world): " + content.ToString("F3"));
            Bounds art = new Bounds(new Vector3(content.center.x, content.center.y, spriteRenderer.bounds.center.z),
                                    new Vector3(content.width, content.height, 0.01f));
            Check("Art fits inside the spread", b.Contains(new Vector3(art.min.x, art.min.y, b.center.z)) &&
                                                b.Contains(new Vector3(art.max.x, art.max.y, b.center.z)));

            // Левая страница: 5 пергаментных закладок, подсвечена только первая; 2x2 медальона
            var tabGlow = GameObject.Find("TabGlow");
            int tabsOk = 0;
            for (int i = 0; i < 5; i++)
            {
                var tab = GameObject.Find("Tab_" + i);
                if (tab == null || !IsSprite(tab.transform, "UI_TabTongue") || !OnPage(b, tab.transform, false)) continue;
                bool lit = tabGlow != null && Mathf.Abs(tabGlow.transform.position.x - tab.transform.position.x) < 0.01f;
                if (lit == (i == 0)) tabsOk++;
            }
            Check("5 parchment school tabs on the left page, only the first lit", tabsOk == 5);

            int windowsOk = 0;
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.Find("MiniWindow_" + i);
                if (w == null || !OnPage(b, w.transform, false) || w.transform.Find("Frame") == null) continue;
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
            Check("4 filled medallions on the left page, each with its own art; Ice Storm lit", windowsOk == 4);

            var school = GameObject.Find("SchoolTitle");
            Check("School title on the left page", school != null && OnPage(b, school.transform, false) &&
                                                   !string.IsNullOrEmpty(school.GetComponent<Text>().text));

            // Правая страница: баннер с названием, описание, 4 стата
            var spellTitle = GameObject.Find("SpellTitle");
            var spellTitleText = spellTitle != null ? spellTitle.GetComponent<Text>() : null;
            Check("Title banner 'Ice Storm' on the right page, below the art",
                spellTitleText != null && spellTitleText.text == "Ice Storm" && OnPage(b, spellTitle.transform, true) &&
                spellTitle.transform.position.y < art.min.y);

            var description = GameObject.Find("Description");
            var descriptionText = description != null ? description.GetComponent<Text>() : null;
            Check("Description on the right page, no PLACEHOLDER marker",
                descriptionText != null && !string.IsNullOrEmpty(descriptionText.text) &&
                !descriptionText.text.Contains("PLACEHOLDER") && OnPage(b, description.transform, true) &&
                spellTitle != null && description.transform.position.y < spellTitle.transform.position.y);

            int statsOk = 0;
            foreach (var stat in new[] { "Stat_Mana", "Stat_Power", "Stat_Duration", "Stat_Range" })
            {
                var go = GameObject.Find(stat);
                if (go == null || !OnPage(b, go.transform, true)) continue;
                var icon = go.transform.Find("Icon");
                var value = go.transform.Find("Value");
                if (icon != null && icon.GetComponent<Image>().sprite != null &&
                    value != null && !string.IsNullOrEmpty(value.GetComponent<Text>().text) &&
                    description != null && go.transform.position.y < description.transform.position.y)
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

            // 8 завитков: по 2 у каждого внешнего угла (левая страница — слева, правая — справа)
            var curls = new System.Collections.Generic.List<Transform>();
            foreach (var t in FindObjectsByType<RectTransform>())
                if (t.name.StartsWith("Curl_")) curls.Add(t);
            var leftCanvas = GameObject.Find("LeftPageCanvas");
            var rightCanvas = GameObject.Find("RightPageCanvas");
            int curlsOk = 0;
            foreach (var c in curls)
            {
                bool left = c.name.StartsWith("Curl_Left");
                var pageCanvas = left ? leftCanvas : rightCanvas;
                if (pageCanvas == null) continue;
                Vector3 local = pageCanvas.transform.InverseTransformPoint(c.position);
                bool outerSide = left ? local.x < 0f : local.x > 0f;
                bool vertical = c.name.Contains("_Top_") ? local.y > 0f : local.y < 0f;
                if (outerSide && vertical && b.Contains(new Vector3(c.position.x, c.position.y, b.center.z))) curlsOk++;
            }
            Check("8 corner curls, 2 at each outer page corner (found " + curls.Count + ")",
                curls.Count == 8 && curlsOk == 8);

            // Компас под статами, не шире строки статов; разделитель под второй строкой окон, шириной с сетку
            var compass = GameObject.Find("MarginCompass");
            var statsRow = GameObject.Find("Stats");
            Check("Compass diagram under the stats, not wider than the stat row",
                compass != null && statsRow != null && OnPage(b, compass.transform, true) &&
                compass.transform.position.y < statsRow.transform.position.y &&
                WorldWidth(compass.transform) <= WorldWidth(statsRow.transform) + 1e-4f);

            var divider = GameObject.Find("GridDivider");
            var w2 = GameObject.Find("MiniWindow_2");
            var w3 = GameObject.Find("MiniWindow_3");
            float gridWidth = w2 != null && w3 != null
                ? (w3.transform.position.x - w2.transform.position.x) + WorldWidth(w2.transform.Find("Frame"))
                : 0f;
            Check("Divider under the second window row, as wide as the grid",
                divider != null && w2 != null && OnPage(b, divider.transform, false) &&
                divider.transform.position.y < w2.transform.position.y &&
                Mathf.Abs(WorldWidth(divider.transform) - gridWidth) <= gridWidth * 0.02f);

            // Слои: подложка под анимацией, лицевой слой над ней, и ничто лицевое не заходит на анимацию
            var backCanvas = GameObject.Find("RightPageBack");
            var frontCanvas = rightCanvas;
            int backOrder = backCanvas != null ? backCanvas.GetComponent<Canvas>().sortingOrder : int.MaxValue;
            int frontOrder = frontCanvas != null ? frontCanvas.GetComponent<Canvas>().sortingOrder : int.MinValue;
            Check("Decor layers: back < animation < front",
                backOrder < spriteRenderer.sortingOrder && spriteRenderer.sortingOrder < frontOrder);
            int overlapping = 0;
            if (frontCanvas != null)
                foreach (var g in frontCanvas.GetComponentsInChildren<Graphic>())
                {
                    var corners = new Vector3[4];
                    g.rectTransform.GetWorldCorners(corners);
                    // Повёрнутые элементы (боковые завитки): берём рамку по всем четырём углам
                    float rx0 = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
                    float rx1 = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
                    float ry0 = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                    float ry1 = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
                    var r = Rect.MinMaxRect(rx0, ry0, rx1, ry1);
                    var a = Rect.MinMaxRect(art.min.x, art.min.y, art.max.x, art.max.y);
                    if (r.Overlaps(a)) { overlapping++; _report.AppendLine("  over the animation: " + g.name); }
                }
            Check("No front decor or text over the animation, all 36 frames (overlapping: " + overlapping + ")", overlapping == 0);

            // Анимация не заходит на кольца у корешка и не выходит за разворот
            Check("Animation clears the spine rings and stays inside the spread",
                content.xMin > b.center.x + 0.09f && b.Contains(new Vector3(content.xMin, content.yMin, b.center.z)) &&
                b.Contains(new Vector3(content.xMax, content.yMax, b.center.z)));

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
            "Ice Magic", "1", "2", "Play", "Replay", "Next Page",
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

    // Объединение контуров спрайтов всех кадров (tight mesh повторяет непрозрачную часть) в мировых координатах
    private static Rect AnimationContent(LoopPlayer player, SpriteRenderer sr)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var frame in player.frames)
            foreach (var v in frame.vertices)
            {
                Vector3 w = sr.transform.TransformPoint(v);
                x0 = Mathf.Min(x0, w.x); y0 = Mathf.Min(y0, w.y);
                x1 = Mathf.Max(x1, w.x); y1 = Mathf.Max(y1, w.y);
            }
        return Rect.MinMaxRect(x0, y0, x1, y1);
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

    private static float WorldWidth(Transform t)
    {
        var rt = t as RectTransform;
        return rt != null ? rt.rect.width * rt.lossyScale.x : 0f;
    }

    // Объект внутри разворота и на нужной половине (правая — справа от корешка)
    private static bool OnPage(Bounds spread, Transform t, bool right)
    {
        Vector3 p = t.position;
        return spread.Contains(new Vector3(p.x, p.y, spread.center.z)) && (p.x > spread.center.x) == right;
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
