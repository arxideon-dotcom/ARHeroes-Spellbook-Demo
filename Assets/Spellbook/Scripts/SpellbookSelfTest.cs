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

            Bounds art = spriteRenderer.bounds;
            Check("Art fits inside the spread", b.Contains(new Vector3(art.min.x, art.min.y, b.center.z)) &&
                                                b.Contains(new Vector3(art.max.x, art.max.y, b.center.z)));

            // Левая страница: 5 вкладок (первая активна), 2x2 мини-окна (выбрано первое)
            int tabsOk = 0;
            for (int i = 0; i < 5; i++)
            {
                var tab = GameObject.Find("Tab_" + i);
                var img = tab != null ? tab.GetComponent<Image>() : null;
                bool active = img != null && img.sprite != null && img.sprite.name == "UI_TabActive";
                if (img != null && OnPage(b, tab.transform, false) && active == (i == 0)) tabsOk++;
            }
            Check("5 school tabs on the left page, only the first active", tabsOk == 5);

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
                bool ok = i == 0
                    ? glow && artImage != null && artImage.sprite == player.frames[0] &&
                      labelText != null && labelText.text == "Ледяная буря"
                    : !glow && artImage == null && labelText == null && IsSprite(w.transform.Find("Mask/LetterDisc"), "UI_LetterDisc");
                if (ok) windowsOk++;
            }
            Check("4 mini-windows on the left page: Ice Storm selected, 3 empty rune slots", windowsOk == 4);

            // Правая страница: баннер с названием, описание, 4 стата
            var spellTitle = GameObject.Find("SpellTitle");
            var spellTitleText = spellTitle != null ? spellTitle.GetComponent<Text>() : null;
            Check("Title banner 'Ледяная буря' on the right page, below the art",
                spellTitleText != null && spellTitleText.text == "Ледяная буря" && OnPage(b, spellTitle.transform, true) &&
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
        }

        // Никаких новых названий и цифр: в сцене только заданные автором тексты
        var allowed = new System.Collections.Generic.HashSet<string>
        {
            "Ледяная буря", SpellbookTextDescription(), "Мана", "Сила", "Длительность", "Дальность", "—",
            "1", "2", "Проиграть цикл", "Повтор", "Перелистнуть",
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

        yield return new WaitForSeconds(0.2f);
        Finish();
    }

    // Текст описания задан автором; самопроверка — рантайм-код и не видит редакторский сборщик
    private static string SpellbookTextDescription() =>
        "Призывает вихрь ледяных осколков, который замедляет врагов в области.";

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
