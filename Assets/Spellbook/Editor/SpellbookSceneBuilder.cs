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
    // IM FELL English SC / IM FELL English Italic / EB Garamond SemiBold — SIL OFL 1.1, лицензии рядом (Fonts/OFL_*.txt)
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

    // Всё содержимое страниц раскладывается в локальных координатах листа (px, начало — центр листа,
    // y вверх) и переносится на нарисованный лист общей поверхностью PageSurface (см. PageSurfaces.json).
    public const string SurfacesPath = ArtFolder + "/PageSurfaces.json";
    // Рамка листа (совпадает с processing/make_page_frames.py)
    private const float FrameInset = 45f;
    private const float TitleY = 650f;

    // Анимация 1.3x: кадр 581x658 -> 826 x 936 px листа. Видимая часть всех 36 кадров — 784 x 893 px,
    // её верх на 718 px от центра листа, под верхней рамкой
    private const float ArtHeight = 936f;
    private const float ArtWidth = ArtHeight * 581f / 658f;
    private const float ArtCenterY = 718f + 21f - ArtHeight * 0.5f;
    private const float MiniRing = 480f;
    private const float MiniSpacing = 250f;

    // Порядок отрисовки: стол, тень, закладки (между листами — под разворотом), разворот,
    // подложка страниц с рамкой, анимация, лицевой слой, техническая сетка
    private const int TableOrder = -10;
    private const int ShadowOrder = -5;
    private const int TabsOrder = -1;
    private const int BackDecorOrder = 2;
    private const int ArtOrder = 3;
    private const int FrontOrder = 4;
    private const int GridOrder = 50;

    // Тексты продукта — на английском
    public const string SpellName = "Ice Storm";
    public const string SpellDescription = "Summons a vortex of ice shards that slows enemies in an area.";
    // Значений статов в данных демо нет: так и пишем
    public const string StatPlaceholder = "Not set";
    // Рабочее название школы от автора
    public const string SchoolTitle = "ICE MAGIC";

    // Тёмно-коричневые чернила: основной текст не теряется на пергаменте
    private static readonly Color Ink = new Color(0.18f, 0.1f, 0.05f);
    private static readonly Color RedInk = new Color(0.33f, 0.08f, 0.04f);
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

        CreateSurroundings(book.transform, spriteMaterial);

        var left = CreatePage("PageLeft", book.transform, LeftPageCenter);
        var right = CreatePage("PageRight", book.transform, RightPageCenter);

        LoopPlayer player = CreateArtSlot(right.transform, spriteMaterial, frames);
        CreateLeftPage(left.transform);
        CreateRightPage(right.transform);
        WarpPage(left.transform);
        WarpPage(right.transform);

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
        // Якорь страницы без масштаба; его PageSurface — геометрия верхнего листа для всего содержимого
        var page = new GameObject(name);
        page.transform.SetParent(book, false);
        page.transform.localPosition = new Vector3(center.x, center.y, 0f);
        LoadSurface(page.AddComponent<PageSurface>(), name == "PageLeft" ? "Left" : "Right");
        return page;
    }

    [System.Serializable] private class SurfacePoint { public float x, y; }
    [System.Serializable] private class SurfacePage
    {
        public string name; public SurfacePoint[] top, bottom; public float w0, w1, width, height, falloff;
        public float[] h;
        public SurfacePoint center;
    }
    [System.Serializable] private class SurfaceFile { public SurfacePage[] pages; }

    private static void LoadSurface(PageSurface surface, string pageName)
    {
        var file = JsonUtility.FromJson<SurfaceFile>(File.ReadAllText(SurfacesPath));
        var page = System.Array.Find(file.pages, x => x.name == pageName);
        surface.top = System.Array.ConvertAll(page.top, q => new Vector2(q.x, q.y));
        surface.bottom = System.Array.ConvertAll(page.bottom, q => new Vector2(q.x, q.y));
        surface.h = page.h;
        surface.falloff = page.falloff;
        surface.center = new Vector2(page.center.x, page.center.y);
        surface.width = page.width;
        surface.height = page.height;
    }

    // Каждый элемент страницы садится на лист общей поверхностью; крупные дробятся мельче
    private static void WarpPage(Transform page)
    {
        foreach (var g in page.GetComponentsInChildren<Graphic>(true))
        {
            var warp = g.gameObject.AddComponent<PageWarp>();
            Vector2 size = g.rectTransform.rect.size;
            warp.subdivisions = g is Text ? 2 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(size.x, size.y) / 45f), 2, 32);
        }
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

    private static void CreateSurroundings(Transform book, Material spriteMaterial)
    {
        // Контактная тень под книгой
        var shadow = new GameObject("BookShadow").AddComponent<SpriteRenderer>();
        shadow.transform.SetParent(book, false);
        shadow.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        shadow.sprite = ImportExact(UiFolder + "/UI_BookShadow.png", SpreadPixelsPerUnit, 2048);
        shadow.sortingOrder = ShadowOrder;
        if (spriteMaterial != null) shadow.sharedMaterial = spriteMaterial;

        // Приглушённый тёмный стол с тёплым светом и затемнением к краям — на всё поле кадра
        var table = new GameObject("Table").AddComponent<SpriteRenderer>();
        table.transform.position = new Vector3(0f, 0f, 1f);
        Sprite tableSprite = ImportExact(UiFolder + "/UI_Table.png", 1536f / 4.9f, 2048);
        table.sprite = tableSprite;
        table.sortingOrder = TableOrder;
        if (spriteMaterial != null) table.sharedMaterial = spriteMaterial;
    }

    private static Sprite ImportExact(string path, float ppu, int maxSize)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            Debug.LogError("[Spellbook] Texture not found: " + path);
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu) ||
            importer.maxTextureSize != maxSize || importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static RectTransform CreateSheetLayer(Transform page, string name, int order)
    {
        // Холст страницы; его координаты — локальные координаты листа, PageWarp переносит их на лист
        return CreatePageCanvas(page, name, order);
    }

    private static void CreateSheetBase(Transform page, string side)
    {
        var surface = page.GetComponent<PageSurface>();
        // Техническая сетка листа — плоская картинка на весь лист, деформируется как всё остальное
        var gridCanvas = CreateSheetLayer(page, side + "PageGrid", GridOrder);
        var grid = CreateImage("SurfaceGrid", gridCanvas, ImportUiFull("UI_SurfaceGrid_" + side), Vector2.zero,
                               new Vector2(surface.width, surface.height), new Color(1f, 1f, 1f, 0.85f));
        grid.preserveAspect = false;
        // Сетка выключена; самопроверка включает её флагом -spellbookGrid для технического превью
        grid.enabled = false;
    }

    private static Sprite ImportUiFull(string name)
    {
        // Листовые картинки во всю страницу: без ограничения 2048 и без мипмапов
        return ImportExact(UiFolder + "/" + name + ".png", 100f, 2048);
    }

    // Размещение на листе: по ширине или высоте с пропорциями спрайта
    private static Image PutW(string name, Transform parent, string sprite, float x, float y, float w, float alpha = 1f, bool flipX = false)
    {
        Sprite sp = Ui(sprite);
        var img = CreateImage(name, parent, sp, new Vector2(x, y), ByWidth(sp, w), new Color(1f, 1f, 1f, alpha));
        if (flipX) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        return img;
    }

    private static Image PutH(string name, Transform parent, string sprite, float x, float y, float h, float alpha = 1f, bool flipX = false)
    {
        Sprite sp = Ui(sprite);
        var img = CreateImage(name, parent, sp, new Vector2(x, y), ByHeight(sp, h), new Color(1f, 1f, 1f, alpha));
        if (flipX) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        return img;
    }

    // Общие для обеих страниц элементы: цепочка на внешнем поле, угловые орнаменты внешних углов,
    // нижняя линия с небольшим просветом под номер страницы и сам номер
    private static void CreatePageChrome(Transform canvas, float outer, string number, float top)
    {
        PutH("Orn_Chain", canvas, "UI_OrnChain", outer * 548f, -20f, 1180f, 0.9f, outer > 0f);
        foreach (var sy in new[] { 1f, -1f })
        {
            var c = PutW(sy > 0 ? "Orn_Corner_Top" : "Orn_Corner_Bottom", canvas, "UI_OrnCorner", outer * 520f, sy * 700f, 120f, 0.9f);
            c.rectTransform.localScale = new Vector3(outer > 0f ? -1f : 1f, sy, 1f);
        }
        const float gap = 34f, half = 395f;
        PutW("Orn_Bottom_L", canvas, "UI_OrnBorderL", -gap - half * 0.5f, -705f, half);
        PutW("Orn_Bottom_R", canvas, "UI_OrnBorderR", gap + half * 0.5f, -705f, half);
        CreateLabel("PageNumber", canvas, number, 46, FontStyle.Italic, new Vector2(0f, -708f),
                    new Vector2(60f, 56f), TextAnchor.MiddleCenter, Ink);
    }

    private static void CreateLeftPage(Transform page)
    {
        var surface = page.GetComponent<PageSurface>();
        float top = surface.height * 0.5f;
        var back = CreateSheetLayer(page, "LeftPageBack", BackDecorOrder);
        var canvas = CreateSheetLayer(page, "LeftPageCanvas", FrontOrder);
        CreateSheetBase(page, "Left");

        // Закладки школ — по локальному верхнему краю листа, между листами: слой под разворотом,
        // поэтому их низ уходит под верхний лист. Названий школ нет
        var tabsCanvas = CreateSheetLayer(page, "LeftPageTabs", TabsOrder);
        var tabs = new GameObject("SchoolTabs", typeof(RectTransform));
        Place(tabs, tabsCanvas, new Vector2(0f, top + 40f), new Vector2(760f, 180f));
        string[] icons = { "UI_InkSnowflake", "UI_InkGlyph0", "UI_InkGlyph1", "UI_InkGlyph2", "UI_InkGlyph3" };
        Sprite tongue = Ui("UI_TabTongue");
        for (int i = 0; i < icons.Length; i++)
        {
            bool active = i == 0;
            var x = (i - 2) * 150f;
            if (active)
                CreateImage("TabGlow", tabs.transform, Ui("UI_SoftGlow"), new Vector2(x, 25f), new Vector2(200f, 200f),
                            new Color(1f, 0.82f, 0.42f, 1f));
            var tab = CreateImage("Tab_" + i, tabs.transform, tongue, new Vector2(x, 0f), ByWidth(tongue, 128f),
                                  active ? new Color(1f, 0.93f, 0.78f, 1f) : new Color(0.8f, 0.76f, 0.72f, 1f));
            CreateImage("Icon", tab.transform, Ui(icons[i]), new Vector2(0f, 22f), new Vector2(70f, 70f),
                        active ? InkGold : new Color(Ink.r, Ink.g, Ink.b, 0.85f));
        }

        // Заголовок школы: компактная заставка над ним, короткая линия под ним
        PutW("Orn_Headpiece", canvas, "UI_OrnTitle", 0f, 712f, 560f, 0.95f);
        CreateLabel("SchoolTitle", canvas, SchoolTitle, 78, FontStyle.Bold, new Vector2(0f, 660f),
                    new Vector2(560f, 90f), TextAnchor.MiddleCenter, RedInk);
        PutW("Orn_TitleRule_L", canvas, "UI_OrnBorderL", -150f, 612f, 300f, 0.95f);
        PutW("Orn_TitleRule_R", canvas, "UI_OrnBorderR", 150f, 612f, 300f, 0.95f);

        // Медальоны 2x2 и между колонками — второстепенная цепочка знаков
        PutH("Orn_SignChain", back, "UI_OrnSigns", 0f, 35f, 375f);
        var grid = new GameObject("MiniWindows", typeof(RectTransform));
        Place(grid, canvas, Vector2.zero, new Vector2(1100f, 1300f));
        string[] art = { "UI_MedalStorm", "UI_Medal1", "UI_Medal2", "UI_Medal3" };
        for (int i = 0; i < 4; i++)
        {
            var pos = new Vector2(i % 2 == 0 ? -MiniSpacing : MiniSpacing, i < 2 ? 320f : -250f);
            CreateMiniWindow(grid.transform, i, pos, Ui(art[i]), i == 0 ? SpellName : null);
        }

        // Поля: плотнее к краям, свободно у подписей
        PutW("Sketch_Shards", back, "UI_SketchShards", -470f, 640f, 130f, 0.6f);
        PutH("Diagram_Lattice", back, "UI_DiagramLattice", 470f, 650f, 110f, 0.45f);
        PutH("Margin_Column", back, "UI_Notes2", -470f, -470f, 210f, 0.5f);
        PutW("Sketch_Cliffs", back, "UI_SketchCliffs", -330f, -650f, 250f, 0.7f);
        PutW("Margin_Notes", back, "UI_MarginNotes", 360f, -655f, 210f, 0.5f);

        CreatePageChrome(canvas, -1f, "1", top);
    }

    private static void CreateMiniWindow(Transform parent, int index, Vector2 pos, Sprite art, string title)
    {
        bool selected = title != null;
        var root = new GameObject("MiniWindow_" + index, typeof(RectTransform));
        // Корень окна стоит в центре кольца, лента под рамкой; круг сокращается перспективой листа
        Place(root, parent, pos, new Vector2(MiniRing, MiniRing + 120f));
        const float ring = MiniRing;

        // Иллюстрация уже вырезана кругом под отверстие рамки; маска страхует края
        var mask = CreateImage("Mask", root.transform, Ui("UI_CircleMask"), Vector2.zero, new Vector2(ring, ring), Color.white);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        CreateImage("Art", mask.transform, art, Vector2.zero, new Vector2(ring, ring), Color.white);

        CreateImage("Frame", root.transform, Ui("UI_FrameRing"), Vector2.zero, new Vector2(ring, ring), Color.white);

        if (selected)
        {
            // Узкое золотое свечение по ободку и несколько бликов на нём
            var glow = CreateImage("SelectedGlow", root.transform, Ui("UI_RimGlow"), Vector2.zero, new Vector2(ring, ring), Color.white);
            foreach (var (deg, size) in new[] { (50f, 46f), (200f, 34f), (300f, 40f), (120f, 26f) })
            {
                float r = 0.395f * ring;
                CreateImage("Glint", glow.transform, Ui("UI_Sparkle"),
                            new Vector2(r * Mathf.Cos(deg * Mathf.Deg2Rad), r * Mathf.Sin(deg * Mathf.Deg2Rad)),
                            new Vector2(size, size), Color.white);
            }
        }

        // Одна лента на все четыре окна: тот же материал, форма, размер и отступ от рамки; текст — только у названных
        var banner = CreateImage("Banner", root.transform, Ui("UI_Banner"),
                                 new Vector2(0f, -250f), new Vector2(410f, 106f), Color.white);
        banner.preserveAspect = false;
        if (selected)
            CreateLabel("BannerTitle", banner.transform, title, 50, FontStyle.Bold, new Vector2(0f, 8f),
                        new Vector2(340f, 64f), TextAnchor.MiddleCenter, Ink);
    }

    private static void CreateRightPage(Transform page)
    {
        var surface = page.GetComponent<PageSurface>();
        float top = surface.height * 0.5f;
        var back = CreateSheetLayer(page, "RightPageBack", BackDecorOrder);
        var canvas = CreateSheetLayer(page, "RightPageCanvas", FrontOrder);
        CreateSheetBase(page, "Right");

        // Подложка вокруг анимации: мягкий холодный свет, небольшой законченный чертёж и зарисовки на полях
        CreateImage("ArtGlow", back, Ui("UI_SoftGlow"), new Vector2(-20f, ArtCenterY - 30f), new Vector2(760f, 760f),
                    new Color(0.67f, 0.84f, 0.96f, 0.4f));
        PutW("Art_Schematic", back, "UI_Schematic", -395f, 600f, 210f, 0.6f);
        PutH("Art_Lattice", back, "UI_DiagramLattice", 400f, 620f, 120f, 0.4f);
        PutW("Sketch_Shards", back, "UI_SketchShards", 480f, 60f, 150f, 0.6f);
        PutH("Margin_Column", back, "UI_Notes2", 480f, -470f, 200f, 0.5f);

        // Лицевой слой — только ниже области анимации, движущиеся части ничем не закрыты
        var banner = CreateImage("TitleBanner", canvas, Ui("UI_Banner"), new Vector2(0f, -264f), new Vector2(740f, 154f), Color.white);
        banner.preserveAspect = false;
        CreateLabel("SpellTitle", banner.transform, SpellName, 82, FontStyle.Bold, new Vector2(0f, 8f),
                    new Vector2(580f, 92f), TextAnchor.MiddleCenter, RedInk);

        var description = CreateLabel("Description", canvas, SpellDescription, 54, FontStyle.Italic,
                                      new Vector2(0f, -408f), new Vector2(840f, 128f), TextAnchor.MiddleCenter, Ink);
        description.lineSpacing = 1.0f;
        PutW("Orn_Divider", canvas, "UI_OrnDivider", 0f, -486f, 620f, 0.95f);

        // Характеристики — открытая строка из четырёх групп с небольшими разделителями
        var stats = new GameObject("Stats", typeof(RectTransform));
        Place(stats, canvas, new Vector2(0f, -590f), new Vector2(820f, 140f));
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
            var x = (i - 1.5f) * 205f;
            var cell = new GameObject(name, typeof(RectTransform));
            Place(cell, stats.transform, new Vector2(x, 0f), new Vector2(200f, 140f));
            CreateImage("Icon", cell.transform, Ui(icon), new Vector2(0f, 45f), new Vector2(60f, 60f), Color.white);
            var caption = CreateLabel("Label", cell.transform, label, 36, FontStyle.Normal, new Vector2(0f, -8f),
                                      new Vector2(200f, 44f), TextAnchor.MiddleCenter, Ink);
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            CreateLabel("Value", cell.transform, StatPlaceholder, 32, FontStyle.Italic, new Vector2(0f, -48f),
                        new Vector2(200f, 40f), TextAnchor.MiddleCenter, new Color(0.27f, 0.18f, 0.1f));
            if (i > 0)
                PutH("Separator", stats.transform, "UI_OrnTick", x - 102.5f, 0f, 96f, 0.9f);
        }

        CreatePageChrome(canvas, 1f, "2", top);
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
        // LoopPlayer и его SpriteRenderer не меняются: те же 36 кадров, тайминг, цикл, Play/Replay.
        // Сам SpriteRenderer не рисуется — его текущий кадр показывает Image на поверхности листа
        var go = new GameObject("SpellArt");
        go.transform.SetParent(page, false);
        float unitsPerPx = PageSize.x / CanvasWidth;
        float spriteHeight = frames.Length > 0 ? frames[0].bounds.size.y : ArtHeight * unitsPerPx;
        go.transform.localPosition = new Vector3(0f, ArtCenterY * unitsPerPx, -0.01f);
        go.transform.localScale = Vector3.one * (ArtHeight * unitsPerPx / spriteHeight);

        var sr = go.AddComponent<SpriteRenderer>();
        if (spriteMaterial != null) sr.sharedMaterial = spriteMaterial;
        sr.sortingOrder = ArtOrder;
        sr.sprite = frames.Length > 0 ? frames[0] : null;
        sr.enabled = false;

        var player = go.AddComponent<LoopPlayer>();
        player.frames = frames;
        player.fps = FramesPerSecond;
        player.loop = true;
        // Цикл запускается кнопкой «Проиграть цикл»
        player.playOnStart = false;

        var artCanvas = CreateSheetLayer(page, "RightPageArt", ArtOrder);
        var surfaceArt = CreateImage("SpellArtSurface", artCanvas, sr.sprite, new Vector2(0f, ArtCenterY),
                                     new Vector2(ArtWidth, ArtHeight), Color.white);
        surfaceArt.preserveAspect = false;
        surfaceArt.gameObject.AddComponent<SpriteMirror>().source = sr;
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
        text.fontSize = 22;
        text.color = Ink;
        // Кнопки лежат на столе: мягкая тень
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(3f, -4f);
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
        // Bold — заголовки и кнопки (плотные старинные капители), Italic — каллиграфический курсив описания,
        // Normal — простой плотный serif подписей
        string file = style == FontStyle.Bold ? "IMFellEnglishSC-Regular.ttf"
                    : style == FontStyle.Italic ? "IMFellEnglish-Italic.ttf"
                    : "EBGaramond-SemiBold.ttf";
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
