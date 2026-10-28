// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Собирает сцену книги заклинаний на развороте v8 и Windows-сборку.
// Меню: Spellbook ▸ Build Scene / Build Windows Player.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class SpellbookSceneBuilder
{
    public const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
    public const string ScenePath = "Assets/Spellbook/Scenes/SpellbookScene.unity";
    public const string ArtFolder = "Assets/Spellbook/Art";
    public const string SpreadPath = ArtFolder + "/Spellbook_Spread_v8.png";
    public const string FramesFolder = "Assets/Spellbook/Frames";
    public const string FramePrefix = "IceStorm_";
    public const string UiFolder = "Assets/Spellbook/UI";
    public const string BuildPath = "Builds/Windows/ARHeroes-Spellbook-Demo.exe";
    private const string SpriteMaterialPath =
        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // 1680x964 px разворота -> 2.33 x 1.34 юнита, примерно как прежняя обложка
    private const float SpreadPixelsPerUnit = 720f;

    // Кадры «Ледяной бури» 581x658 -> 0.625 x 0.708 юнита: ровно ширина правой области страницы
    private const float FramePixelsPerUnit = 581f / 0.625f;

    // Исходная анимация идёт по 110 мс на кадр
    private const float FramesPerSecond = 9f;

    // Ровные области страниц в юнитах разворота (центр спрайта = 0,0),
    // с отступами от краёв, колец и корешка. Сняты по превью 2560x1440.
    public static readonly Vector2 PageSize = new Vector2(0.625f, 0.875f);
    public static readonly Vector2 LeftPageCenter = new Vector2(-0.4472f, 0.0625f);
    public static readonly Vector2 RightPageCenter = new Vector2(0.4278f, 0.0625f);

    // Раскладка страниц в пикселях холста: 900 px по ширине = PageSize.x
    private const float CanvasWidth = 900f;
    private const float CanvasHeight = CanvasWidth * 0.875f / 0.625f;

    // Большой арт на правой странице: верхние 650 px холста
    private const float ArtTopPx = 630f;
    private const float ArtBottomPx = -20f;

    public const string SpellName = "Ледяная буря";
    public const string SpellDescription =
        "Призывает вихрь ледяных осколков, который замедляет врагов в области.";
    // Значения статов задаст автор; до тех пор — прочерки
    public const string StatPlaceholder = "—";

    private static readonly Color Ink = new Color(0.23f, 0.13f, 0.07f);
    private static readonly Color Background = new Color(0.05f, 0.04f, 0.05f);
    private static readonly Color WarmLight = new Color(1f, 0.82f, 0.6f);

    [MenuItem("Spellbook/Build Scene")]
    public static void BuildScene()
    {
        Sprite spread = ImportSpread();
        Sprite[] frames = ImportFrames();
        var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);

        // SampleScene сохраняется как SpellbookScene, дальше правим копию
        var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath, true);
        scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith("PLACEHOLDER_") || root.name == "CandleLight" || root.name == "Book" ||
                root.name == "UI" || root.name == "EventSystem")
                Object.DestroyImmediate(root);
        }

        SetupCamera();
        SetupLights();

        // Перспектива уже нарисована в развороте, поэтому книга стоит лицом к камере без наклона
        var book = new GameObject("Book");
        book.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var spreadGo = new GameObject("Spread");
        spreadGo.transform.SetParent(book.transform, false);
        var spreadRenderer = spreadGo.AddComponent<SpriteRenderer>();
        spreadRenderer.sprite = spread;
        if (spriteMaterial != null) spreadRenderer.sharedMaterial = spriteMaterial;
        spreadRenderer.sortingOrder = 0;

        var left = CreatePage("PageLeft", book.transform, LeftPageCenter);
        var right = CreatePage("PageRight", book.transform, RightPageCenter);

        LoopPlayer player = CreateArtSlot(right.transform, spriteMaterial, frames);
        CreateLeftPage(left.transform, frames.Length > 0 ? frames[0] : null);
        CreateRightPage(right.transform);

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

    private static Sprite ImportSpread()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpreadPath);
        if (importer == null)
        {
            Debug.LogError("[Spellbook] Spread texture not found: " + SpreadPath);
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, SpreadPixelsPerUnit) ||
            importer.maxTextureSize != 2048 ||
            importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpreadPixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpreadPath);
    }

    private static void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        // Прямо перед разворотом: сверху остаётся место под заголовок, снизу под кнопки
        cam.transform.position = new Vector3(0f, 0f, -2.6f);
        cam.transform.LookAt(Vector3.zero);
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        // Разворот — готовая живопись: тонмаппинг из Global Volume гасил светлые тона пергамента на ~15%
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

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

        var candle = new GameObject("CandleLight").AddComponent<Light>();
        candle.type = LightType.Point;
        candle.color = new Color(1f, 0.7f, 0.4f);
        candle.intensity = 2.5f;
        candle.range = 5f;
        candle.transform.position = new Vector3(-1.2f, 1.0f, -1.2f);
    }

    private static GameObject CreatePage(string name, Transform book, Vector2 center)
    {
        // Пустой якорь ровной области страницы, без масштаба, чтобы дети (текст, спрайт) не искажались
        var page = new GameObject(name);
        page.transform.SetParent(book, false);
        page.transform.localPosition = new Vector3(center.x, center.y, 0f);
        return page;
    }

    private static Sprite Ui(string name)
    {
        string path = UiFolder + "/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogError("[Spellbook] UI sprite not found: " + path);
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite || !importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            // Мипмапы: на мировом холсте спрайты сильно уменьшаются
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static RectTransform CreatePageCanvas(Transform page, string name)
    {
        var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(page, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Поверх разворота (sortingOrder 0) и арта (1)
        canvas.sortingOrder = 2;
        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        rt.localScale = Vector3.one * (PageSize.x / CanvasWidth);
        rt.localPosition = new Vector3(0f, 0f, -0.005f);
        return rt;
    }

    private static RectTransform Place(GameObject go, Transform parent, Vector2 pos, Vector2 size)
    {
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Place(go, parent, pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    private static Text CreateLabel(string name, Transform parent, string value, int size, FontStyle style,
                                    Vector2 pos, Vector2 box, TextAnchor align, Color color)
    {
        var text = CreateText(name, parent, size, color);
        text.text = value;
        text.fontStyle = style;
        text.alignment = align;
        Place(text.gameObject, parent, pos, box);
        return text;
    }

    private static void CreateCorners(Transform canvas, float inset)
    {
        // Лёгкие маргинальные орнаменты по углам; спрайт нарисован для левого верхнего угла
        Sprite flourish = Ui("UI_CornerFlourish");
        float x = CanvasWidth / 2f - inset, y = CanvasHeight / 2f - inset;
        var corners = new[] { (-1, 1, "TL"), (1, 1, "TR"), (-1, -1, "BL"), (1, -1, "BR") };
        foreach (var (sx, sy, tag) in corners)
        {
            var img = CreateImage("Corner_" + tag, canvas, flourish, new Vector2(sx * x, sy * y),
                                  new Vector2(100f, 100f), new Color(1f, 1f, 1f, 0.8f));
            img.rectTransform.localScale = new Vector3(-sx, sy, 1f);
        }
    }

    private static void CreateLeftPage(Transform page, Sprite miniArt)
    {
        var canvas = CreatePageCanvas(page, "LeftPageCanvas");

        // Полоса вкладок школ: первая активна, остальные приглушены. Названий школ нет — только знаки
        var tabs = new GameObject("SchoolTabs", typeof(RectTransform));
        Place(tabs, canvas, new Vector2(0f, 555f), new Vector2(700f, 150f));
        string[] icons = { "UI_IconSnowflake", "UI_IconGlyph0", "UI_IconGlyph1", "UI_IconGlyph2", "UI_IconGlyph3" };
        for (int i = 0; i < icons.Length; i++)
        {
            bool active = i == 0;
            var tab = CreateImage("Tab_" + i, tabs.transform, Ui(active ? "UI_TabActive" : "UI_Tab"),
                                  new Vector2((i - 2) * 140f, 0f), new Vector2(124f, 150f),
                                  active ? Color.white : new Color(1f, 1f, 1f, 0.75f));
            tab.preserveAspect = false;
            CreateImage("Icon", tab.transform, Ui(icons[i]), new Vector2(0f, 4f), new Vector2(80f, 80f),
                        active ? new Color(0.25f, 0.16f, 0.06f, 1f) : new Color(0.2f, 0.14f, 0.09f, 0.55f));
        }
        CreateImage("TabsRule", canvas, Ui("UI_Divider"), new Vector2(0f, 462f), new Vector2(760f, 40f), Color.white);

        // Сетка 2x2 мини-окон: заполнено только первое
        var grid = new GameObject("MiniWindows", typeof(RectTransform));
        Place(grid, canvas, Vector2.zero, new Vector2(900f, 1000f));
        for (int i = 0; i < 4; i++)
        {
            var pos = new Vector2(i % 2 == 0 ? -225f : 225f, i < 2 ? 235f : -255f);
            CreateMiniWindow(grid.transform, i, pos, i == 0 ? miniArt : null, i == 0 ? SpellName : null);
        }

        CreateCorners(canvas, 60f);
        CreateLabel("PageNumber", canvas, "1", 34, FontStyle.Italic, new Vector2(0f, -590f),
                    new Vector2(200f, 50f), TextAnchor.MiddleCenter, Ink);
    }

    private static void CreateMiniWindow(Transform parent, int index, Vector2 pos, Sprite art, string title)
    {
        bool filled = art != null;
        var root = new GameObject("MiniWindow_" + index, typeof(RectTransform));
        // Корень окна стоит в центре кольца, баннер заходит на низ рамки
        Place(root, parent, pos, new Vector2(420f, 500f));
        const float ring = 400f;
        var ringPos = Vector2.zero;

        if (filled)
            CreateImage("SelectedGlow", root.transform, Ui("UI_FrameGlow"), ringPos, new Vector2(ring * 1.3f, ring * 1.3f),
                        new Color(1f, 0.9f, 0.6f, 0.95f));

        // Круглая маска: внутри либо кадр цикла, либо нейтральный рунический узор
        var mask = CreateImage("Mask", root.transform, Ui("UI_CircleMask"), ringPos, new Vector2(ring, ring), Color.white);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        if (filled)
        {
            CreateImage("Wash", mask.transform, Ui("UI_CircleMask"), Vector2.zero, new Vector2(ring, ring),
                        new Color(0.78f, 0.88f, 1f, 0.35f));
            CreateImage("Art", mask.transform, art, new Vector2(0f, -6f), new Vector2(ring * 0.86f, ring * 0.86f), Color.white);
        }
        else
        {
            CreateImage("Runes", mask.transform, Ui("UI_RuneFill"), Vector2.zero, new Vector2(ring, ring),
                        new Color(1f, 1f, 1f, 0.8f));
        }

        CreateImage("Frame", root.transform, Ui("UI_FrameRing"), ringPos, new Vector2(ring, ring),
                    filled ? Color.white : new Color(0.82f, 0.78f, 0.72f, 0.85f));

        var banner = CreateImage("Banner", root.transform, Ui(filled ? "UI_Banner" : "UI_BannerDim"),
                                 new Vector2(0f, -205f), new Vector2(420f, 92f),
                                 filled ? Color.white : new Color(1f, 1f, 1f, 0.7f));
        if (filled)
            CreateLabel("BannerTitle", banner.transform, title, 40, FontStyle.Bold, new Vector2(0f, 7f),
                        new Vector2(340f, 60f), TextAnchor.MiddleCenter, Ink);
    }

    private static void CreateRightPage(Transform page)
    {
        var canvas = CreatePageCanvas(page, "RightPageCanvas");

        var banner = CreateImage("TitleBanner", canvas, Ui("UI_Banner"), new Vector2(0f, -95f), new Vector2(700f, 140f), Color.white);
        CreateLabel("SpellTitle", banner.transform, SpellName, 62, FontStyle.Bold, new Vector2(0f, 11f),
                    new Vector2(560f, 84f), TextAnchor.MiddleCenter, new Color(0.36f, 0.1f, 0.06f));

        var description = CreateLabel("Description", canvas, SpellDescription, 46, FontStyle.Italic,
                                      new Vector2(0f, -248f), new Vector2(800f, 150f), TextAnchor.MiddleCenter, Ink);
        description.lineSpacing = 1.1f;

        CreateImage("StatsRule", canvas, Ui("UI_Divider"), new Vector2(0f, -340f), new Vector2(760f, 40f), Color.white);

        var stats = new GameObject("Stats", typeof(RectTransform));
        Place(stats, canvas, new Vector2(0f, -430f), new Vector2(848f, 160f));
        var rows = new[]
        {
            ("Stat_Mana", "UI_StatMana", "Мана"),
            ("Stat_Power", "UI_StatPower", "Сила"),
            ("Stat_Duration", "UI_StatDuration", "Длительность"),
            ("Stat_Range", "UI_StatRange", "Дальность"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, icon, label) = rows[i];
            var cell = new GameObject(name, typeof(RectTransform));
            // Иконка сверху, подпись и значение под ней: так «Длительность» помещается без переноса
            Place(cell, stats.transform, new Vector2((i - 1.5f) * 212f, 0f), new Vector2(210f, 160f));
            CreateImage("Icon", cell.transform, Ui(icon), new Vector2(0f, 42f), new Vector2(66f, 66f), Color.white);
            var caption = CreateLabel("Label", cell.transform, label, 30, FontStyle.Normal, new Vector2(0f, -10f),
                                      new Vector2(210f, 40f), TextAnchor.MiddleCenter, Ink);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            CreateLabel("Value", cell.transform, StatPlaceholder, 40, FontStyle.Bold, new Vector2(0f, -52f),
                        new Vector2(210f, 50f), TextAnchor.MiddleCenter, Ink);
        }

        CreateCorners(canvas, 60f);
        CreateLabel("PageNumber", canvas, "2", 34, FontStyle.Italic, new Vector2(0f, -590f),
                    new Vector2(200f, 50f), TextAnchor.MiddleCenter, Ink);
    }

    private static Sprite[] ImportFrames()
    {
        var guids = AssetDatabase.FindAssets(FramePrefix + " t:Texture2D", new[] { FramesFolder });
        var paths = new List<string>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path).StartsWith(FramePrefix)) paths.Add(path);
        }
        paths.Sort(System.StringComparer.Ordinal);

        var sprites = new Sprite[paths.Count];
        for (int i = 0; i < paths.Count; i++)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, FramePixelsPerUnit) ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = FramePixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
        }
        if (sprites.Length == 0) Debug.LogWarning("[Spellbook] No " + FramePrefix + "* frames in " + FramesFolder);
        return sprites;
    }

    private static LoopPlayer CreateArtSlot(Transform page, Material spriteMaterial, Sprite[] frames)
    {
        // Большой арт заклинания в верхней части правой страницы, под ним баннер, описание и статы
        var go = new GameObject("SpellArt");
        go.transform.SetParent(page, false);
        float unitsPerPx = PageSize.x / CanvasWidth;
        float artHeight = (ArtTopPx - ArtBottomPx) * unitsPerPx;
        float spriteHeight = frames.Length > 0 ? frames[0].bounds.size.y : artHeight;
        go.transform.localPosition = new Vector3(0f, (ArtTopPx + ArtBottomPx) * 0.5f * unitsPerPx, -0.01f);
        go.transform.localScale = Vector3.one * (artHeight / spriteHeight);

        var sr = go.AddComponent<SpriteRenderer>();
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        sr.sortingOrder = 1;
        sr.sprite = frames.Length > 0 ? frames[0] : null;

        var player = go.AddComponent<LoopPlayer>();
        player.frames = frames;
        player.fps = FramesPerSecond;
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

        // Плавающего заголовка над книгой больше нет: название живёт на баннере правой страницы
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
