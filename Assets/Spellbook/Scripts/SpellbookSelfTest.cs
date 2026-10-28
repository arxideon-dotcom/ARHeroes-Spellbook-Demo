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
                    : !glow && artImage == null && labelText == null && w.transform.Find("Mask/Runes") != null;
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
        }

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
