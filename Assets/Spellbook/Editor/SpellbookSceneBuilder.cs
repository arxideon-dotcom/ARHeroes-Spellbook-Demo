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
    // Cormorant Garamond, SIL OFL 1.1 (лицензия рядом, Fonts/OFL.txt)
    public const string FontsFolder = "Assets/Spellbook/Fonts";
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

    // Анимация на правой странице, в 1.3 раза крупнее прежних 720 px: кадр 936 px холста.
    // Видимая часть всех 36 кадров — 784 x 893 px вокруг центра 313: от 760 (ниже верха бумаги) до -133
    private const float ArtTopPx = 313f + 468f;
    private const float ArtBottomPx = 313f - 468f;
    private const float MiniRing = 470f;
    private const float MiniSpacing = 228f;

    // Порядок отрисовки: разворот 0, подложка декора 1, анимация 2, лицевой слой страниц 3
    private const int BackDecorOrder = 1;
    private const int ArtOrder = 2;
    private const int FrontOrder = 3;

    // Тексты продукта — на английском
    public const string SpellName = "Ice Storm";
    public const string SpellDescription = "Summons a vortex of ice shards that slows enemies in an area.";
    // Значений статов в данных демо нет: так и пишем
    public const string StatPlaceholder = "Not set";
    // Рабочее название школы от автора
    public const string SchoolTitle = "Ice Magic";

    private static readonly Color Ink = new Color(0.23f, 0.13f, 0.07f);
    private static readonly Color RedInk = new Color(0.36f, 0.1f, 0.06f);
    // Золотые чернила активной вкладки: темнее самой вкладки, чтобы знак читался
    private static readonly Color InkGold = new Color(0.62f, 0.4f, 0.08f, 1f);
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
        CreateLeftPage(left.transform);
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

    private static RectTransform CreatePageCanvas(Transform page, string name, int order)
    {
        var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(page, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = order;
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

    // Размер по высоте с пропорциями спрайта
    private static Vector2 ByHeight(Sprite sprite, float h) =>
        sprite != null ? new Vector2(h * sprite.rect.width / sprite.rect.height, h) : new Vector2(h, h);

    private static Vector2 ByWidth(Sprite sprite, float w) =>
        sprite != null ? new Vector2(w, w * sprite.rect.height / sprite.rect.width) : new Vector2(w, w);

    private static Image Decor(string name, Transform parent, string sprite, Vector2 pos, float width, float alpha)
    {
        Sprite sp = Ui(sprite);
        return CreateImage(name, parent, sp, pos, ByWidth(sp, width), new Color(1f, 1f, 1f, alpha));
    }

    private static Text CreateLabel(string name, Transform parent, string value, int size, FontStyle style,
                                    Vector2 pos, Vector2 box, TextAnchor align, Color color)
    {
        var text = CreateText(name, parent, size, color);
        text.text = value;
        // У шрифта страниц отдельные начертания, поэтому стиль задаёт файл, а не синтез
        text.font = PageFont(style);
        text.fontStyle = FontStyle.Normal;
        text.alignment = align;
        Place(text.gameObject, parent, pos, box);
        return text;
    }

    private static void CreateCorners(Transform canvas, bool clearTop)
    {
        // Угловые орнаменты; спрайт нарисован для левого верхнего угла
        Sprite flourish = Ui("UI_CornerFlourish");
        var corners = new[] { (-1, 1, "TL"), (1, 1, "TR"), (-1, -1, "BL"), (1, -1, "BR") };
        foreach (var (sx, sy, tag) in corners)
        {
            // clearTop: верхние углы уходят в поле бумаги, освобождая место под анимацию
            var pos = clearTop && sy > 0 ? new Vector2(sx * 470f, 700f) : new Vector2(sx * 395f, sy * 575f);
            var img = CreateImage("Corner_" + tag, canvas, flourish, pos,
                                  new Vector2(90f, 90f), new Color(1f, 1f, 1f, 0.75f));
            img.rectTransform.localScale = new Vector3(-sx, sy, 1f);
        }
    }

    private static void CreateCurls(Transform canvas, float outer, string page, bool clearTop)
    {
        // По 2 завитка у каждого внешнего угла страницы: в поле над/под углом и в боковом поле
        Sprite curl = Ui("UI_MarginCurl");
        var size = ByWidth(curl, 110f);
        foreach (var (sy, tag) in new[] { (1f, "Top"), (-1f, "Bottom") })
        {
            bool sideOnly = clearTop && sy > 0;
            var along = CreateImage("Curl_" + page + "_" + tag + "_Edge", canvas, curl,
                                    sideOnly ? new Vector2(outer * 500f, 340f) : new Vector2(outer * 360f, sy * 680f),
                                    size, new Color(1f, 1f, 1f, 0.85f));
            along.rectTransform.localScale = new Vector3(-outer, sy, 1f);
            if (sideOnly) along.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var side = CreateImage("Curl_" + page + "_" + tag + "_Side", canvas, curl,
                                   new Vector2(outer * 495f, sy * 520f), size, new Color(1f, 1f, 1f, 0.85f));
            side.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            side.rectTransform.localScale = new Vector3(sy, outer, 1f);
        }
    }

    private static void CreateLeftPage(Transform page)
    {
        var back = CreatePageCanvas(page, "LeftPageBack", BackDecorOrder);
        var canvas = CreatePageCanvas(page, "LeftPageCanvas", FrontOrder);

        // Закладки школ: пергаментные язычки над краем страницы, знаки крупные. Названий школ нет
        var tabs = new GameObject("SchoolTabs", typeof(RectTransform));
        Place(tabs, canvas, new Vector2(0f, 690f), new Vector2(760f, 180f));
        string[] icons = { "UI_InkSnowflake", "UI_InkGlyph0", "UI_InkGlyph1", "UI_InkGlyph2", "UI_InkGlyph3" };
        Sprite tongue = Ui("UI_TabTongue");
        for (int i = 0; i < icons.Length; i++)
        {
            bool active = i == 0;
            var x = (i - 2) * 150f;
            if (active)
                CreateImage("TabGlow", tabs.transform, Ui("UI_SoftGlow"), new Vector2(x, 10f), new Vector2(190f, 190f),
                            new Color(1f, 0.84f, 0.47f, 0.9f));
            var tab = CreateImage("Tab_" + i, tabs.transform, tongue, new Vector2(x, 0f), ByWidth(tongue, 128f),
                                  active ? Color.white : new Color(0.82f, 0.78f, 0.74f, 1f));
            CreateImage("Icon", tab.transform, Ui(icons[i]), new Vector2(0f, 15f), new Vector2(74f, 74f),
                        active ? InkGold : new Color(Ink.r, Ink.g, Ink.b, 0.8f));
        }

        CreateLabel("SchoolTitle", canvas, SchoolTitle, 62, FontStyle.Bold, new Vector2(0f, 568f),
                    new Vector2(820f, 80f), TextAnchor.MiddleCenter, RedInk);
        Decor("TitleRule", canvas, "UI_Divider", new Vector2(0f, 515f), 640f, 1f).preserveAspect = false;

        // Сетка 2x2 медальонов на основной площади страницы, бледная схема между ними — на подложке
        Decor("GridSchematic", back, "UI_Schematic", new Vector2(0f, -12f), 520f, 0.35f);
        var grid = new GameObject("MiniWindows", typeof(RectTransform));
        Place(grid, canvas, Vector2.zero, new Vector2(900f, 1100f));
        string[] art = { "UI_MedalStorm", "UI_Medal1", "UI_Medal2", "UI_Medal3" };
        for (int i = 0; i < 4; i++)
        {
            var pos = new Vector2(i % 2 == 0 ? -MiniSpacing : MiniSpacing, i < 2 ? 250f : -275f);
            CreateMiniWindow(grid.transform, i, pos, Ui(art[i]), i == 0 ? SpellName : null);
        }
        // Разделитель под второй строкой, шириной с сетку медальонов
        CreateImage("GridDivider", canvas, Ui("UI_Divider"), new Vector2(0f, -585f),
                    new Vector2(2f * MiniSpacing + MiniRing, 46f), Color.white).preserveAspect = false;

        // Маргиналии в полях бумаги, мимо текста и медальонов
        Sprite crystals = Ui("UI_SketchCrystals");
        CreateImage("Sketch_Crystals", canvas, crystals, new Vector2(-520f, 20f), ByHeight(crystals, 300f), new Color(1f, 1f, 1f, 0.6f));
        Decor("Margin_Notes", canvas, "UI_MarginNotes", new Vector2(-250f, -690f), 250f, 0.6f);
        Decor("Sketch_Peak", canvas, "UI_SketchPeak", new Vector2(330f, -650f), 120f, 0.75f);

        CreateCorners(canvas, false);
        CreateCurls(canvas, -1f, "Left", false);
        CreateLabel("PageNumber", canvas, "1", 40, FontStyle.Italic, new Vector2(0f, -690f),
                    new Vector2(200f, 50f), TextAnchor.MiddleCenter, Ink);
    }

    private static void CreateMiniWindow(Transform parent, int index, Vector2 pos, Sprite art, string title)
    {
        bool selected = title != null;
        var root = new GameObject("MiniWindow_" + index, typeof(RectTransform));
        // Корень окна стоит в центре кольца, лента под рамкой
        Place(root, parent, pos, new Vector2(MiniRing, MiniRing + 120f));
        const float ring = MiniRing;

        if (selected)
            CreateImage("SelectedGlow", root.transform, Ui("UI_FrameGlow"), Vector2.zero, new Vector2(ring * 1.18f, ring * 1.18f),
                        new Color(1f, 0.9f, 0.6f, 0.9f));

        // Иллюстрация уже вырезана кругом под отверстие рамки; маска страхует края
        var mask = CreateImage("Mask", root.transform, Ui("UI_CircleMask"), Vector2.zero, new Vector2(ring, ring), Color.white);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        CreateImage("Art", mask.transform, art, Vector2.zero, new Vector2(ring, ring), Color.white);

        CreateImage("Frame", root.transform, Ui("UI_FrameRing"), Vector2.zero, new Vector2(ring, ring), Color.white);

        var banner = CreateImage("Banner", root.transform, Ui(selected ? "UI_Banner" : "UI_BannerDim"),
                                 new Vector2(0f, -238f), new Vector2(400f, 104f),
                                 selected ? Color.white : new Color(1f, 1f, 1f, 0.85f));
        banner.preserveAspect = false;
        if (selected)
            CreateLabel("BannerTitle", banner.transform, title, 46, FontStyle.Bold, new Vector2(0f, 8f),
                        new Vector2(340f, 60f), TextAnchor.MiddleCenter, Ink);
    }

    private static void CreateRightPage(Transform page)
    {
        var back = CreatePageCanvas(page, "RightPageBack", BackDecorOrder);
        var canvas = CreatePageCanvas(page, "RightPageCanvas", FrontOrder);

        // Подложка под анимацией: холодное свечение, схема, рисованные осколки. Анимация рисуется поверх
        float artY = (ArtTopPx + ArtBottomPx) * 0.5f;
        CreateImage("ArtGlow", back, Ui("UI_SoftGlow"), new Vector2(0f, artY - 30f), new Vector2(820f, 820f),
                    new Color(0.67f, 0.84f, 0.96f, 0.55f));
        Decor("ArtSchematic", back, "UI_Schematic", new Vector2(0f, artY - 90f), 700f, 0.5f);
        Decor("ArtShards", back, "UI_ArtShards", new Vector2(0f, artY + 20f), 900f, 0.9f);

        // Лицевой слой — только ниже области анимации, движущиеся части ничем не закрыты
        var banner = CreateImage("TitleBanner", canvas, Ui("UI_Banner"), new Vector2(0f, -228f), new Vector2(720f, 150f), Color.white);
        banner.preserveAspect = false;
        CreateLabel("SpellTitle", banner.transform, SpellName, 70, FontStyle.Bold, new Vector2(0f, 9f),
                    new Vector2(560f, 80f), TextAnchor.MiddleCenter, RedInk);

        var description = CreateLabel("Description", canvas, SpellDescription, 48, FontStyle.Italic,
                                      new Vector2(0f, -365f), new Vector2(820f, 112f), TextAnchor.MiddleCenter, Ink);
        description.lineSpacing = 1.0f;

        Decor("StatsRule", canvas, "UI_Divider", new Vector2(0f, -445f), 760f, 1f);

        var stats = new GameObject("Stats", typeof(RectTransform));
        Place(stats, canvas, new Vector2(0f, -530f), new Vector2(872f, 130f));
        var rows = new[]
        {
            ("Stat_Mana", "UI_StatMana", "Mana"),
            ("Stat_Power", "UI_StatPower", "Power"),
            ("Stat_Duration", "UI_StatDuration", "Duration"),
            ("Stat_Range", "UI_StatRange", "Range"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, icon, label) = rows[i];
            var x = (i - 1.5f) * 218f;
            var cell = new GameObject(name, typeof(RectTransform));
            Place(cell, stats.transform, new Vector2(x, 0f), new Vector2(214f, 130f));
            CreateImage("Icon", cell.transform, Ui(icon), new Vector2(0f, 43f), new Vector2(66f, 66f), Color.white);
            var caption = CreateLabel("Label", cell.transform, label, 32, FontStyle.Normal, new Vector2(0f, -9f),
                                      new Vector2(214f, 40f), TextAnchor.MiddleCenter, Ink);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            CreateLabel("Value", cell.transform, StatPlaceholder, 27, FontStyle.Italic, new Vector2(0f, -45f),
                        new Vector2(214f, 36f), TextAnchor.MiddleCenter, new Color(0.47f, 0.36f, 0.27f));
            if (i > 0)
                CreateImage("Separator", stats.transform, null, new Vector2(x - 109f, 3f), new Vector2(2f, 115f),
                            new Color(Ink.r, Ink.g, Ink.b, 0.47f));
        }

        // Компас-диаграмма под строкой статов
        Decor("MarginCompass", canvas, "UI_MarginCompass", new Vector2(0f, -640f), 64f, 0.9f);

        // Заметки во внешнем поле, ниже линии вихря: широкое место бумаги, анимацию не задевают
        Decor("Margin_Notes", canvas, "UI_MarginNotes", new Vector2(540f, -250f), 150f, 0.55f);
        Sprite crystals = Ui("UI_SketchCrystals");
        CreateImage("Sketch_Crystals", canvas, crystals, new Vector2(525f, 150f), ByHeight(crystals, 260f), new Color(1f, 1f, 1f, 0.55f));

        // Верхние орнаменты правой страницы — в полях, мимо крупной анимации
        CreateCorners(canvas, true);
        CreateCurls(canvas, 1f, "Right", true);
        CreateLabel("PageNumber", canvas, "2", 40, FontStyle.Italic, new Vector2(0f, -720f),
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
        sr.sortingOrder = ArtOrder;
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
        // Небольшие кнопки в стиле книги: бронзовая рамка, пергамент, шрифт страниц
        var resources = new DefaultControls.Resources { standard = Ui("UI_ButtonPlate") };

        var play = CreateButton(ui.transform, resources, "Button_PlayLoop", "Play", -202f);
        UnityEventTools.AddPersistentListener(play.onClick, player.Play);

        var replay = CreateButton(ui.transform, resources, "Button_Replay", "Replay", 0f);
        UnityEventTools.AddPersistentListener(replay.onClick, player.Replay);

        // Страниц 2–3 пока нет
        var flip = CreateButton(ui.transform, resources, "Button_FlipPage", "Next Page", 202f);
        flip.interactable = false;
        flip.GetComponentInChildren<Text>().color = new Color(0.43f, 0.35f, 0.27f);
    }

    private static Button CreateButton(Transform parent, DefaultControls.Resources resources, string name, string label, float x)
    {
        var go = DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var image = go.GetComponent<Image>();
        image.type = Image.Type.Simple;
        rt.sizeDelta = ByWidth(image.sprite, 186f);
        rt.anchoredPosition = new Vector2(x, 82f);

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
        colors.pressedColor = new Color(0.85f, 0.78f, 0.66f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.55f);
        button.colors = colors;

        var text = go.GetComponentInChildren<Text>();
        text.text = label;
        text.font = PageFont(FontStyle.Bold);
        text.fontStyle = FontStyle.Normal;
        text.fontSize = 20;
        text.color = Ink;
        return button;
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

    private static Font PageFont(FontStyle style)
    {
        string file = style == FontStyle.Bold ? "CormorantGaramond-Bold.ttf"
                    : style == FontStyle.Italic ? "CormorantGaramond-MediumItalic.ttf"
                    : "CormorantGaramond-Medium.ttf";
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontsFolder + "/" + file);
        if (font == null) Debug.LogError("[Spellbook] Page font not found: " + FontsFolder + "/" + file);
        return font != null ? font : LegacyFont();
    }

    private static void CreateEventSystem()
    {
        // В проекте включён только новый Input System, поэтому InputSystemUIInputModule
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
