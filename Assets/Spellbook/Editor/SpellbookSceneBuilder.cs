// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Собирает сцену книги заклинаний на развороте v8 и Windows-сборку.
// Меню: Spellbook ▸ Build Scene / Build Windows Player.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    // Три заклинания из готовых APNG: кадры извлекает processing/ice_spells/extract_apng.py,
    // он же пишет сюда названия, описания и задержки кадров из файлов
    public const string ClipsPath = ArtFolder + "/SpellClips.json";
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

    // Анимация 1.3x: кадр 581x658 -> 826 x 936 px листа. Видимая часть всех 36 кадров — 784 x 893 px,
    // её верх на 718 px от центра листа, под верхней рамкой
    private const float ArtHeight = 936f;
    private const float ArtWidth = ArtHeight * 581f / 658f;
    private const float ArtCenterY = 718f + 21f - ArtHeight * 0.5f;
    private const float MiniRing = 480f;
    private const float MiniSpacing = 250f;
    private static readonly float[] MiniRows = { 320f, -250f };
    // Окно иллюстрации медальона: радиус отверстия рамки на листе (UI_FrameRing 512 px -> 480 px, отверстие r 173 px)
    private const float MiniHoleRadius = 162f;

    // Камера: книга без пустого места снизу под управление
    private const float CameraDistance = 2.1f;
    private const float CameraY = 0.02f;

    // Декор под медальонами — элементы предоставленной композиции (processing/make_decor_v2.py).
    // Её отверстия r 62 px совпадают с окнами медальонов при x2.61; остальные элементы — свои масштабы
    private const float DecorHoleRadius = 62f;
    // Орбита (UI_V2_OrbitRing 1539 px): внешний край r 762 px -> ~330 px листа, внутренние кольца под рамкой
    private const float OrbitScale = 0.40f;

    // Ленты названий (processing/grimoire_v2/make_name_ribbons.py, layout.json "NameRibbon"): одна генерация,
    // широкая 1827x470 с пустым полем 1308x242, компактная 1645x470 (середина короче на 10 %) с полем 1126x243.
    // Центр поля на 16 px ниже и на 13.5 px правее середины картинки
    private static readonly Vector2 WideRibbonSize = new Vector2(700f, 700f * 470f / 1827f);
    private static readonly Vector2 WideRibbonFace = new Vector2(13.5f, -16f) * (700f / 1827f);
    // Компактная — середина короче на 5 % (1736x470, поле 1217 px); под подписью орбиты погашены
    // (make_grimoire_v2.py clear_label, та же точка: x 3.3, y -247 от центра медальона)
    private static readonly Vector2 CompactRibbonSize = new Vector2(420f, 420f * 470f / 1736f);
    // Центр свободного поля компактной ленты (пустая середина между внутренними контурами, 1217x241 px спрайта)
    private static readonly Vector2 CompactRibbonFace = new Vector2(13.5f, -16.5f) * (420f / 1736f);
    // Подписи медальонов — тёмно-коричневая тушь, темнее основного текста
    private static readonly Color LabelInk = new Color(0.12f, 0.06f, 0.025f);
    // Мелкие элементы декора экспортированы по 3 px текстуры на px листа (make_grimoire_v2.py)
    private const float Px3 = 1f / 3f;

    // Закладки: язычок 128 px шириной, его центр на 40 px выше верхнего края листа. 9-slice: сверху 50 px
    // рваного края с линией рамки, снизу 132 px — нижняя линия рамки и хвостик между листами
    private const float TabRise = 40f;
    private const float TabWidth = 128f;
    private static readonly Vector4 TabBorder = new Vector4(0f, 132f, 0f, 50f);
    private const float TabIconFromTop = 56f;

    // Лента заголовка (UI_V2_TitleRibbon 1855x498, Muse): свободное поле 1243x223 px, его середина на 23.5 px
    // ниже и на 28 px правее середины картинки (processing/grimoire_v2/layout.json)
    private const float TitleRibbonY = 642f;
    private const float TitleRibbonWidth = 720f;
    private const float TitleRibbonScale = TitleRibbonWidth / 1855f;
    private const float TitleBandWidth = 1243f * TitleRibbonScale - 20f;
    private const float TitleBandOffsetX = 28f * TitleRibbonScale;
    private const float TitleBandOffsetY = -23.5f * TitleRibbonScale;

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
        Sprite[] frames = ImportFrames(FramePrefix);
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
        var spells = LoadSpells(frames);
        var windows = CreateLeftPage(left.transform, spells);
        CreateRightPage(right.transform);
        ClipToSheet(left.transform);
        ClipToSheet(right.transform);
        WarpPage(left.transform);
        WarpPage(right.transform);
        CreateSelector(player, left, windows, spells);
        // Кнопок нет: Ice Storm запускается сам, медальон сразу запускает своё заклинание

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
        // Прямо перед разворотом; кнопок внизу больше нет, поэтому книга с закладками занимает кадр
        // (низ книги -0.67, верх активной закладки ~0.71 юнита) с небольшим полем стола вокруг
        cam.transform.position = new Vector3(0f, CameraY, -CameraDistance);
        cam.transform.LookAt(new Vector3(0f, CameraY, 0f));
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

    // Всё наложенное на лист обрезается по самому листу: в каждом слое страницы (подложка, анимация, лицевой
    // слой) содержимое уходит под маску размером с весь лист. Маска — такая же графика страницы и проходит через
    // тот же PageWarp, поэтому её край — деформированный край верхнего листа, а не прямоугольник холста.
    // Закладки (намеренно выступают за лист) и техническая сетка в маску не входят
    private static void ClipToSheet(Transform page)
    {
        var surface = page.GetComponent<PageSurface>();
        foreach (var canvas in page.GetComponentsInChildren<Canvas>(true))
        {
            string n = canvas.name;
            if (n.EndsWith("Tabs") || n.EndsWith("Grid")) continue;
            var children = new List<Transform>();
            foreach (Transform c in canvas.transform) children.Add(c);
            var clip = CreateImage("SheetClip", canvas.transform, null, Vector2.zero,
                                   new Vector2(surface.width, surface.height), Color.white);
            clip.preserveAspect = false;
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            foreach (var c in children) c.SetParent(clip.transform, false);
        }
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

    private static (RectTransform[] roots, GameObject[] glows) CreateLeftPage(Transform page, SpellSelector.Spell[] spells)
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
        Place(tabs, tabsCanvas, new Vector2(0f, top + TabRise), new Vector2(760f, 180f));
        string[] icons = { "UI_InkSnowflake", "UI_InkGlyph0", "UI_InkGlyph1", "UI_InkGlyph2", "UI_InkGlyph3" };
        // Язычок режется на 9 частей: верхний рваный край и низ с хвостиком не тянутся, удлиняется
        // только поле пергамента. Знак остаётся своего размера
        Sprite tongue = UiSliced("UI_TabTongue", TabBorder);
        float baseH = ByWidth(tongue, TabWidth).y;
        float visible = TabRise + baseH * 0.5f;          // сколько обычная закладка выступает над листом
        for (int i = 0; i < icons.Length; i++)
        {
            bool active = i == 0;
            var x = (i - 2) * 150f;
            // Активная выступает на 30% дальше; низ остаётся на месте, между листами
            float h = active ? baseH + 0.3f * visible : baseH;
            float y = (h - baseH) * 0.5f;
            float iconY = y + h * 0.5f - TabIconFromTop;
            if (active)
                CreateImage("TabGlow", tabs.transform, Ui("UI_V2_SoftGlow"), new Vector2(x, iconY + 4f), new Vector2(190f, 190f),
                            new Color(1f, 0.92f, 0.7f, 0.6f));
            var tab = CreateImage("Tab_" + i, tabs.transform, tongue, new Vector2(x, y), new Vector2(TabWidth, h),
                                  active ? new Color(1f, 0.95f, 0.82f, 1f) : new Color(0.84f, 0.79f, 0.73f, 1f));
            tab.type = Image.Type.Sliced;
            tab.preserveAspect = false;
            tab.pixelsPerUnitMultiplier = tongue.rect.width / TabWidth;
            CreateImage("Icon", tab.transform, Ui(icons[i]), new Vector2(0f, iconY - y), new Vector2(70f, 70f),
                        active ? InkGold : new Color(Ink.r, Ink.g, Ink.b, 0.85f));
        }

        // Заголовок школы — в свободном поле ленты (UI_V2_TitleRibbon, Muse), на той же поверхности листа
        var ribbon = PutW("Orn_TitleRibbon", canvas, "UI_V2_TitleRibbon", 0f, TitleRibbonY, TitleRibbonWidth);
        var schoolTitle = CreateLabel("SchoolTitle", ribbon.transform, SchoolTitle, 64, FontStyle.Bold,
                                      new Vector2(TitleBandOffsetX, TitleBandOffsetY), new Vector2(TitleBandWidth, 80f),
                                      TextAnchor.MiddleCenter, RedInk);
        schoolTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

        // Медальоны 2x2 на декоративном слое из предоставленной композиции
        CreateMedallionDecor(back);
        var grid = new GameObject("MiniWindows", typeof(RectTransform));
        Place(grid, canvas, Vector2.zero, new Vector2(1100f, 1300f));
        // Миниатюры — статичные кадры самих анимаций (processing/ice_spells/make_medal_thumbs.py)
        string[] art = { "UI_MedalStorm", "UI_MedalFrostBreath", "UI_MedalIceComet", "UI_MedalAbsoluteZero" };
        // Подложки внутри окон — тематическая тушь под иллюстрацией (Muse, processing/grimoire_v2), по маске окна
        string[] under = { "UI_V2_UnderStorm", "UI_V2_UnderFrostBreath", "UI_V2_UnderIceComet", "UI_V2_UnderAbsoluteZero" };
        var roots = new RectTransform[4];
        var glows = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            (roots[i], glows[i]) = CreateMiniWindow(grid.transform, i, MiniCenter(i), Ui(art[i]), Ui(under[i]), spells[i].title, i == 0);
        }

        // Поля: заметки у внешнего края; низ — города и чаша из той же композиции
        PutH("Margin_Column", back, "UI_Notes2", -470f, -470f, 210f, 0.5f);

        CreatePageChrome(canvas, -1f, "1", top);
        return (roots, glows);
    }

    private static Vector2 MiniCenter(int i) =>
        new Vector2(i % 2 == 0 ? -MiniSpacing : MiniSpacing, MiniRows[i < 2 ? 0 : 1]);

    // Декоративный слой под медальонами (Muse, processing/grimoire_v2): одна и та же орбита вокруг каждого
    // медальона (у верхнего ряда — вариант с погашенным верхом, чтобы не идти за заголовком), эмблема в центре
    // креста, лучи вверх и вниз, чаша, города в нижних углах и звёзды-компасы у заголовка
    private static void CreateMedallionDecor(Transform back)
    {
        for (int i = 0; i < 4; i++)
            PutPx("Decor_Orbit" + i, back, i < 2 ? "UI_V2_OrbitRingTop" : "UI_V2_OrbitRing", MiniCenter(i), OrbitScale, 0.8f);
        float cy = (MiniRows[0] + MiniRows[1]) * 0.5f;
        var emblem = PutPx("Decor_Emblem", back, "UI_V2_CenterEmblem", new Vector2(0f, cy), Px3, 1f);
        float er = emblem.rectTransform.sizeDelta.y * 0.22f;
        var up = PutPx("Decor_ArmUp", back, "UI_V2_Connector", Vector2.zero, Px3, 0.95f);
        up.rectTransform.anchoredPosition = new Vector2(0f, cy + er + up.rectTransform.sizeDelta.y * 0.5f);
        var down = PutPx("Decor_ArmDown", back, "UI_V2_Connector", Vector2.zero, Px3, 0.95f);
        down.rectTransform.anchoredPosition = new Vector2(0f, cy - er - down.rectTransform.sizeDelta.y * 0.5f);
        PutPx("Decor_Chalice", back, "UI_V2_Chalice", new Vector2(0f, -598f), Px3, 1f);
        PutPx("Decor_CityL", back, "UI_V2_CityLeft", new Vector2(-322f, -598f), Px3, 1f);
        PutPx("Decor_CityR", back, "UI_V2_CityRight", new Vector2(334f, -598f), Px3, 1f);
        PutPx("Decor_CompassL", back, "UI_V2_Compass", new Vector2(-452f, 622f), Px3, 1f);
        PutPx("Decor_CompassR", back, "UI_V2_Compass", new Vector2(452f, 622f), Px3, 1f, true);
    }

    // Размещение элемента декора по пикселям исходника: size = px * k, без растяжения по одной оси
    private static Image PutPx(string name, Transform parent, string sprite, Vector2 pos, float k, float alpha, bool flipX = false)
    {
        Sprite sp = Ui(sprite);
        Vector2 size = sp != null ? sp.rect.size * k : Vector2.one * 100f;
        var img = CreateImage(name, parent, sp, pos, size, new Color(1f, 1f, 1f, alpha));
        if (flipX) img.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        return img;
    }

    private static Sprite UiSliced(string name, Vector4 border)
    {
        Sprite sp = Ui(name);
        var importer = (TextureImporter)AssetImporter.GetAtPath(UiFolder + "/" + name + ".png");
        if (importer != null && importer.spriteBorder != border)
        {
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            sp = AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "/" + name + ".png");
        }
        return sp;
    }

    private static (RectTransform, GameObject) CreateMiniWindow(Transform parent, int index, Vector2 pos, Sprite art,
                                                                Sprite underlay, string title, bool selected)
    {
        var root = new GameObject("MiniWindow_" + index, typeof(RectTransform));
        // Корень окна стоит в центре кольца, лента под рамкой; круг сокращается перспективой листа
        Place(root, parent, pos, new Vector2(MiniRing, MiniRing + 120f));
        const float ring = MiniRing;
        GameObject glowGo;

        // Иллюстрация уже вырезана кругом под отверстие рамки; маска страхует края
        var mask = CreateImage("Mask", root.transform, Ui("UI_CircleMask"), Vector2.zero, new Vector2(ring, ring), Color.white);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        CreateImage("Underlay", mask.transform, underlay, Vector2.zero, new Vector2(ring, ring), Color.white);
        CreateImage("Art", mask.transform, art, Vector2.zero, new Vector2(ring, ring), Color.white);

        CreateImage("Frame", root.transform, Ui("UI_FrameRing"), Vector2.zero, new Vector2(ring, ring), Color.white);

        // Узкое золотое свечение по ободку и несколько бликов на нём — у каждого окна, видно только у выбранного
        {
            var glow = CreateImage("SelectedGlow", root.transform, Ui("UI_V2_RimGlow"), Vector2.zero, new Vector2(ring, ring),
                                   new Color(1f, 0.95f, 0.8f, 0.8f));
            foreach (var (deg, size) in new[] { (50f, 46f), (200f, 34f), (300f, 40f), (120f, 26f) })
            {
                float r = 0.395f * ring;
                CreateImage("Glint", glow.transform, Ui("UI_V2_Sparkle"),
                            new Vector2(r * Mathf.Cos(deg * Mathf.Deg2Rad), r * Mathf.Sin(deg * Mathf.Deg2Rad)),
                            new Vector2(size, size), Color.white);
            }
            glowGo = glow.gameObject;
            glowGo.SetActive(selected);
        }

        // Одна лента на все четыре окна (компактная лента тушью, Muse): тот же спрайт, размер и отступ от рамки.
        // Середина прозрачная — под названием видна бумага страницы; текст по центру пустого поля
        var banner = CreateImage("Banner", root.transform, Ui("UI_V2_NameRibbonCompact"),
                                 new Vector2(0f, -250f), CompactRibbonSize, Color.white);
        var bannerTitle = CreateLabel("BannerTitle", banner.transform, title, 41, FontStyle.Bold, CompactRibbonFace,
                                      new Vector2(292f, 64f), TextAnchor.MiddleCenter, LabelInk);
        bannerTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        // Плотнее того же IM FELL SC: синтезированное полужирное начертание (отдельного файла у шрифта нет)
        bannerTitle.fontStyle = FontStyle.Bold;
        CenterVisible(bannerTitle, CompactRibbonFace);
        return ((RectTransform)root.transform, glowGo);
    }

    // Одно правило для всех подписей лент: центр видимых границ букв (объединение прямоугольников глифов,
    // как их строит сам Text) ставится в центр свободного поля ленты. Считается в плоских координатах листа
    // до PageWarp; метрики шрифта (подъём строки над капителями) и пустые поля спрайта на центровку не влияют
    private static void CenterVisible(Text label, Vector2 fieldCenter)
    {
        var rt = label.rectTransform;
        var gen = new TextGenerator();
        gen.Populate(label.text, label.GetGenerationSettings(rt.rect.size));
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        var verts = gen.verts;
        for (int q = 0; q + 3 < verts.Count; q += 4)
        {
            Vector3 a = verts[q].position, c = verts[q + 2].position;
            if (Mathf.Abs(a.x - c.x) < 0.01f || Mathf.Abs(a.y - c.y) < 0.01f) continue;   // пробелы
            x0 = Mathf.Min(x0, Mathf.Min(a.x, c.x)); x1 = Mathf.Max(x1, Mathf.Max(a.x, c.x));
            y0 = Mathf.Min(y0, Mathf.Min(a.y, c.y)); y1 = Mathf.Max(y1, Mathf.Max(a.y, c.y));
        }
        if (x0 > x1) { rt.anchoredPosition = fieldCenter; return; }
        // Вершины генератора — в единицах текста относительно опорной точки прямоугольника (центр)
        var visibleCenter = new Vector2(x0 + x1, y0 + y1) * 0.5f / label.pixelsPerUnit;
        rt.anchoredPosition = fieldCenter - visibleCenter;
    }

    private static void CreateRightPage(Transform page)
    {
        var surface = page.GetComponent<PageSurface>();
        float top = surface.height * 0.5f;
        var back = CreateSheetLayer(page, "RightPageBack", BackDecorOrder);
        var canvas = CreateSheetLayer(page, "RightPageCanvas", FrontOrder);
        CreateSheetBase(page, "Right");

        // Статичный слой оформления — отдельно от кадров анимации, на той же поверхности листа (Muse,
        // processing/grimoire_v2: одна генерация страницы, разрезанная на части). Крупный тонколинейный чертёж
        // за областью анимации (частично скрыт ею), этюды и заметки на обоих полях; под описанием пусто
        PutPx("Decor_RightDiagram", back, "UI_V2_RightDiagram", new Vector2(12f, 286f), 0.975f, 1f);
        // Поля асимметричны: у корешка узкая колонка (кольца до x ~ -487), снаружи — эскизы дракона и кометы
        PutPx("Margin_NotesSpine", back, "UI_V2_RightNotesSpine", new Vector2(-440f, 360f), Px3, 1f);
        PutPx("Margin_Planets", back, "UI_V2_Planets", new Vector2(-440f, 60f), Px3, 0.95f);
        PutPx("Margin_NotesOuter", back, "UI_V2_RightNotesOuter", new Vector2(440f, 330f), 0.36f, 1f);
        PutH("Margin_Column", back, "UI_Notes2", 480f, -470f, 200f, 0.5f);

        // Лицевой слой — только ниже области анимации, движущиеся части ничем не закрыты
        // Широкая лента тушью из того же комплекта (Muse); её верхний завиток кончается у низа области анимации
        var banner = CreateImage("TitleBanner", canvas, Ui("UI_V2_NameRibbonWide"), new Vector2(0f, -272f), WideRibbonSize, Color.white);
        // Поле — пустая середина ленты; один размер для всех названий: при 82 «Absolute Zero» заходило на концы
        var spellTitle = CreateLabel("SpellTitle", banner.transform, SpellName, 74, FontStyle.Bold, WideRibbonFace,
                                     new Vector2(520f, 92f), TextAnchor.MiddleCenter, RedInk);
        spellTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

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

    private static Sprite[] ImportFrames(string prefix)
    {
        var guids = AssetDatabase.FindAssets(prefix + " t:Texture2D", new[] { FramesFolder });
        var paths = new List<string>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path).StartsWith(prefix)) paths.Add(path);
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
        if (sprites.Length == 0) Debug.LogWarning("[Spellbook] No " + prefix + "* frames in " + FramesFolder);
        return sprites;
    }

    private static LoopPlayer CreateArtSlot(Transform page, Material spriteMaterial, Sprite[] frames)
    {
        // LoopPlayer крутит кадры выбранного заклинания в своём SpriteRenderer.
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
        // Цикл запускает SpellSelector: при открытии Ice Storm, дальше — выбранный медальон
        player.playOnStart = false;

        var artCanvas = CreateSheetLayer(page, "RightPageArt", ArtOrder);
        var surfaceArt = CreateImage("SpellArtSurface", artCanvas, sr.sprite, new Vector2(0f, ArtCenterY),
                                     new Vector2(ArtWidth, ArtHeight), Color.white);
        surfaceArt.preserveAspect = false;
        surfaceArt.gameObject.AddComponent<SpriteMirror>().source = sr;
        return player;
    }

    [System.Serializable] private class ClipEntry
    {
        public string prefix, title, description, source;
        public int frames;
        public float[] delays;
        public float[] artCenter, artSize;
    }
    [System.Serializable] private class ClipFile { public ClipEntry[] clips; }

    // Ice Storm — как был, он же ориентир размера. Остальные три — по SpellClips.json: каждый со своими
    // задержками кадров и своим прямоугольником на листе. Прямоугольник считает
    // processing/ice_spells/layout_frames_v2.py по основной фигуре клипа (не по холсту PNG), свой масштаб
    // для каждого, без растяжения; редкие частицы за областью Ice Storm гаснут в самих кадрах
    private static SpellSelector.Spell[] LoadSpells(Sprite[] stormFrames)
    {
        var list = new List<SpellSelector.Spell>
        {
            new SpellSelector.Spell
            {
                title = SpellName, description = SpellDescription, frames = stormFrames, fps = FramesPerSecond,
                frameDurations = new float[0], artCenter = new Vector2(0f, ArtCenterY), artSize = new Vector2(ArtWidth, ArtHeight),
            },
        };
        var file = JsonUtility.FromJson<ClipFile>(File.ReadAllText(ClipsPath));
        foreach (var c in file.clips)
        {
            Sprite[] frames = ImportFrames(c.prefix + "_");
            if (frames.Length != c.frames || c.delays.Length != c.frames)
                Debug.LogError("[Spellbook] " + c.prefix + ": " + frames.Length + " frames, " + c.delays.Length +
                               " delays, expected " + c.frames);
            list.Add(new SpellSelector.Spell
            {
                title = c.title, description = c.description, frames = frames, fps = FramesPerSecond,
                frameDurations = c.delays,
                artCenter = new Vector2(c.artCenter[0], c.artCenter[1]), artSize = new Vector2(c.artSize[0], c.artSize[1]),
            });
        }
        return list.ToArray();
    }

    private static void CreateSelector(LoopPlayer player, GameObject left,
                                       (RectTransform[] roots, GameObject[] glows) windows, SpellSelector.Spell[] spells)
    {
        var selector = new GameObject("SpellSelector").AddComponent<SpellSelector>();
        selector.player = player;
        selector.surfaceArt = GameObject.Find("SpellArtSurface").GetComponent<Image>();
        selector.title = GameObject.Find("SpellTitle").GetComponent<Text>();
        selector.description = GameObject.Find("Description").GetComponent<Text>();
        selector.leftSurface = left.GetComponent<PageSurface>();
        selector.windows = windows.roots;
        selector.glows = windows.glows;
        selector.spells = spells;
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

    private static Font LegacyFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private static Font PageFont(FontStyle style)
    {
        // Bold — заголовки и подписи лент (плотные старинные капители), Italic — каллиграфический курсив описания,
        // Normal — простой плотный serif подписей
        string file = style == FontStyle.Bold ? "IMFellEnglishSC-Regular.ttf"
                    : style == FontStyle.Italic ? "IMFellEnglish-Italic.ttf"
                    : "EBGaramond-SemiBold.ttf";
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontsFolder + "/" + file);
        if (font == null) Debug.LogError("[Spellbook] Page font not found: " + FontsFolder + "/" + file);
        return font != null ? font : LegacyFont();
    }
}
