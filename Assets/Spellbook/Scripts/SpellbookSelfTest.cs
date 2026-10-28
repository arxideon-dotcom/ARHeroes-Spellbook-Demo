// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Самопроверка собранного плеера. Включается только аргументом командной строки:
//   ARHeroes-Spellbook-Demo.exe -spellbookSelfTest <report.txt>
// Нажимает кнопки через EventSystem (как настоящий клик), пишет отчёт и скриншот, выходит.
// Без аргумента ничего не делает.
using System;
using System.Collections;
using System.Collections.Generic;
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

        Check("4 frames, fps 12, loop on",
            player.frames != null && player.frames.Length == 4 && Mathf.Approximately(player.fps, 12f) && player.loop);

        var cam = Camera.main;
        Check("Right page visible to camera", cam != null && IsVisible(cam, spriteRenderer.bounds));

        // До нажатия кадр стоит на первом
        Sprite idle = spriteRenderer.sprite;
        yield return new WaitForSeconds(0.5f);
        Check("Idle before Play shows frame 0 and does not change",
            idle == player.frames[0] && spriteRenderer.sprite == idle);

        // «Проиграть цикл»
        Click("Button_PlayLoop");
        var seen = new HashSet<Sprite>();
        float until = Time.unscaledTime + 1.2f;
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

        var title = GameObject.Find("Title");
        var titleText = title != null ? title.GetComponent<Text>() : null;
        Check("Title text is 'Ледяная буря'", titleText != null && titleText.text == "Ледяная буря");

        yield return new WaitForSeconds(0.2f);
        Finish();
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
