// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Собирает сцену-каркас книги заклинаний на плейсхолдерах и Windows-сборку.
// Меню: Spellbook ▸ Build Placeholder Scene / Build Windows Player.
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class SpellbookSceneBuilder
{
    public const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
    public const string ScenePath = "Assets/Spellbook/Scenes/SpellbookScene.unity";
    public const string FramesFolder = "Assets/Spellbook/Frames";
    public const string ArtFolder = "Assets/Spellbook/Art";
    public const string BuildPath = "Builds/Windows/ARHeroes-Spellbook-Demo.exe";

    private const int FrameSize = 256;

    // Плейсхолдер-цвета кадров: оттенки льда, чтобы смена кадров была заметна
    private static readonly Color32[] FrameColors =
    {
        new Color32(40, 90, 200, 255),
        new Color32(70, 190, 230, 255),
        new Color32(220, 245, 255, 255),
        new Color32(120, 80, 210, 255),
    };

    private static readonly Color Parchment = new Color(0.93f, 0.86f, 0.68f);
    private static readonly Color Leather = new Color(0.22f, 0.11f, 0.06f);
    private static readonly Color Background = new Color(0.05f, 0.04f, 0.05f);
    private static readonly Color WarmLight = new Color(1f, 0.82f, 0.6f);

    [MenuItem("Spellbook/Build Placeholder Scene")]
    public static void BuildScene()
    {
        Sprite[] frames = CreatePlaceholderFrames();
        Material paper = CreateMaterial("PLACEHOLDER_Parchment", Parchment);
        Material cover = CreateMaterial("PLACEHOLDER_Cover", Leather);

        // SampleScene сохраняется как SpellbookScene, дальше правим копию
        var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath, true);
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith("PLACEHOLDER_") || root.name == "Book" ||
                root.name == "UI" || root.name == "EventSystem")
                Object.DestroyImmediate(root);
        }

        SetupCamera();
        SetupLights();

        var book = new GameObject("Book");
        // Книга стоит на «пюпитре»: верх слегка отклонён от камеры
        book.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(25f, 0f, 0f));

        var coverQuad = CreateQuad("PLACEHOLDER_Cover", book.transform, cover);
        // Обложка заметно позади: внутренние края страниц при развороте уходят назад на ~0.06
        coverQuad.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        coverQuad.transform.localScale = new Vector3(2.3f, 1.55f, 1f);

        var left = CreatePage("PLACEHOLDER_PageLeft", book.transform, -0.535f, -6f, paper);
        var right = CreatePage("PLACEHOLDER_PageRight", book.transform, 0.535f, 6f, paper);

        CreateDescription(left.transform);
        LoopPlayer player = CreateAnimation(right.transform, frames);

        CreateUi(player);
        CreateEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Spellbook] Scene built: " + ScenePath);
    }

    [MenuItem("Spellbook/Build Windows Player")]
    public static BuildReport BuildWindows()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[Spellbook] Build " + report.summary.result + ", errors: " + report.summary.totalErrors +
                  ", size: " + report.summary.totalSize + ", path: " + Path.GetFullPath(BuildPath));
        return report;
    }

    private static Sprite[] CreatePlaceholderFrames()
    {
        var sprites = new Sprite[FrameColors.Length];
        for (int i = 0; i < FrameColors.Length; i++)
        {
            string path = FramesFolder + "/PLACEHOLDER_" + i + ".png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[FrameSize * FrameSize];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = FrameColors[i];
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, FrameSize))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = FrameSize;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return sprites;
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = ArtFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        // Чуть сбоку и сверху, смотрит в центр разворота
        cam.transform.position = new Vector3(0.45f, 0.75f, -3.1f);
        cam.transform.LookAt(new Vector3(0f, 0.05f, 0f));
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.12f, 0.1f);
    }

    private static void SetupLights()
    {
        var sun = Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            sun.color = WarmLight;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
        }

        var candle = new GameObject("PLACEHOLDER_CandleLight").AddComponent<Light>();
        candle.type = LightType.Point;
        candle.color = new Color(1f, 0.7f, 0.4f);
        candle.intensity = 2.5f;
        candle.range = 5f;
        candle.transform.position = new Vector3(-1.2f, 1.0f, -1.2f);
    }

    private static GameObject CreatePage(string name, Transform book, float x, float yaw, Material paper)
    {
        // Корень страницы без масштаба, чтобы дети (текст, спрайт) не искажались
        var page = new GameObject(name);
        page.transform.SetParent(book, false);
        page.transform.localPosition = new Vector3(x, 0f, 0f);
        page.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        var quad = CreateQuad("Paper", page.transform, paper);
        quad.transform.localScale = new Vector3(1.05f, 1.4f, 1f);
        return page;
    }

    private static GameObject CreateQuad(string name, Transform parent, Material mat)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return quad;
    }

    private static void CreateDescription(Transform page)
    {
        var canvasGo = new GameObject("PLACEHOLDER_DescriptionCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(page, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = new Vector2(900f, 1200f);
        rt.localScale = Vector3.one * 0.001f;
        rt.localPosition = new Vector3(0f, 0f, -0.005f);

        var text = CreateText("PLACEHOLDER_Description", canvasGo.transform, 56, new Color(0.25f, 0.15f, 0.08f));
        text.alignment = TextAnchor.UpperLeft;
        text.text = "Ледяная буря\n\nPLACEHOLDER: описание заклинания.\n\n" +
                    "Призывает вихрь ледяных осколков, который замедляет врагов в области.";
        Stretch((RectTransform)text.transform, 60f);
    }

    private static LoopPlayer CreateAnimation(Transform page, Sprite[] frames)
    {
        var go = new GameObject("PLACEHOLDER_SpellAnimation");
        go.transform.SetParent(page, false);
        go.transform.localPosition = new Vector3(0f, 0.05f, -0.01f);
        go.transform.localScale = Vector3.one * 0.85f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];

        var player = go.AddComponent<LoopPlayer>();
        player.frames = frames;
        player.fps = 12f;
        player.loop = true;
        // Цикл запускается кнопкой «Проиграть цикл»
        player.playOnStart = false;
        return player;
    }

    private static void CreateUi(LoopPlayer player)
    {
        var ui = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var title = CreateText("Title", ui.transform, 72, new Color(0.85f, 0.93f, 1f));
        title.text = "Ледяная буря";
        title.alignment = TextAnchor.MiddleCenter;
        var trt = (RectTransform)title.transform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0f, 120f);
        trt.anchoredPosition = new Vector2(0f, -20f);

        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
        };

        var play = CreateButton(ui.transform, resources, "Button_PlayLoop", "Проиграть цикл", -380f);
        UnityEventTools.AddPersistentListener(play.onClick, player.Play);

        var replay = CreateButton(ui.transform, resources, "Button_Replay", "Повтор", 0f);
        UnityEventTools.AddPersistentListener(replay.onClick, player.Replay);

        // Страниц 2–3 пока нет
        var flip = CreateButton(ui.transform, resources, "Button_FlipPage", "Перелистнуть", 380f);
        flip.interactable = false;
    }

    private static Button CreateButton(Transform parent, DefaultControls.Resources resources, string name, string label, float x)
    {
        var go = DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(340f, 90f);
        rt.anchoredPosition = new Vector2(x, 40f);

        var text = go.GetComponentInChildren<Text>();
        text.text = label;
        text.font = LegacyFont();
        text.fontSize = 38;
        return go.GetComponent<Button>();
    }

    private static Text CreateText(string name, Transform parent, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = LegacyFont();
        text.fontSize = size;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static Font LegacyFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private static void CreateEventSystem()
    {
        // В проекте включён только новый Input System, поэтому InputSystemUIInputModule
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
