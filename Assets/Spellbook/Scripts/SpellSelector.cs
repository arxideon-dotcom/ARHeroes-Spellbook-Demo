// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Выбор заклинания медальоном левой страницы. Медальоны нарисованы через PageWarp, поэтому прямоугольный
// Raycast UI не совпадает с видимым кругом: попадание проверяется по контуру рамки и ленты, пропущенному
// через ту же PageSurface.Map, что и сам рисунок. Выбор подсвечивает рамку, останавливает прежнюю анимацию,
// ставит клип выбранного заклинания (его кадры, тайминг и место на правой странице), меняет название
// и описание и сразу запускает его бесконечный цикл с первого кадра. Кнопок нет: при открытии играет Ice Storm.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SpellSelector : MonoBehaviour
{
    [System.Serializable]
    public class Spell
    {
        public string title;
        public string description;
        public Sprite[] frames;
        [Tooltip("Если задано по значению на кадр — переопределяет fps (задержки APNG)")]
        public float[] frameDurations;
        public float fps = 9f;
        [Tooltip("Прямоугольник анимации в локальных координатах правого листа (центр, размер)")]
        public Vector2 artCenter;
        public Vector2 artSize;
    }

    public LoopPlayer player;
    public Image surfaceArt;
    public Text title;
    public Text description;
    public PageSurface leftSurface;
    [Tooltip("Корни медальонов на холсте левого листа, по индексу заклинания")]
    public RectTransform[] windows;
    [Tooltip("Подсветка рамки каждого медальона")]
    public GameObject[] glows;
    public Spell[] spells;
    [Tooltip("Радиус кликабельного круга (доля стороны рамки) и прямоугольник ленты под ним, px листа")]
    public float hitRadius = 0.45f;
    public Vector2 bannerOffset = new Vector2(0f, -250f);
    public Vector2 bannerSize = new Vector2(420f, 114f);

    public int Selected { get; private set; } = -1;

    private const int CircleSteps = 32;

    private void Start()
    {
        Select(0);
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            SelectAtScreen(mouse.position.ReadValue());
    }

    /// Выбор медальона под точкой экрана; false — если точка не на медальоне.
    public bool SelectAtScreen(Vector2 screen)
    {
        for (int i = 0; i < windows.Length; i++)
            if (PointInPolygon(screen, Outline(i, true)) || PointInPolygon(screen, Outline(i, false)))
            {
                Select(i);
                return true;
            }
        return false;
    }

    /// Центр круга медальона на экране — для самопроверки «настоящим» кликом по координатам.
    public Vector2 ScreenCenter(int i) => ToScreen(LocalCenter(i));

    public void Select(int i)
    {
        if (i < 0 || i >= spells.Length) return;
        Selected = i;
        for (int k = 0; k < glows.Length; k++)
            if (glows[k] != null) glows[k].SetActive(k == i);

        Spell s = spells[i];
        // Прежний клип останавливается сразу, новый идёт с первого кадра и повторяется бесконечно
        player.SetClip(s.frames, s.fps, s.frameDurations);
        player.loop = true;
        player.Play();
        var rt = surfaceArt.rectTransform;
        rt.anchoredPosition = s.artCenter;
        rt.sizeDelta = s.artSize;
        // Зеркало кадра обновляется в LateUpdate; ставим кадр сразу, чтобы не мелькнул прежний
        surfaceArt.sprite = s.frames.Length > 0 ? s.frames[0] : null;
        surfaceArt.SetVerticesDirty();
        title.text = s.title;
        description.text = s.description;
    }

    private Vector2 LocalCenter(int i)
    {
        Transform canvas = windows[i].GetComponentInParent<Canvas>().transform;
        Vector3 p = canvas.InverseTransformPoint(windows[i].position);
        return new Vector2(p.x, p.y);
    }

    // Контур круга рамки или ленты в локальных координатах листа -> экран через поверхность листа
    private Vector2[] Outline(int i, bool circle)
    {
        Vector2 c = LocalCenter(i);
        if (circle)
        {
            float r = hitRadius * windows[i].rect.width;
            var pts = new Vector2[CircleSteps];
            for (int k = 0; k < CircleSteps; k++)
            {
                float a = k * Mathf.PI * 2f / CircleSteps;
                pts[k] = ToScreen(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts;
        }
        Vector2 b = c + bannerOffset, h = bannerSize * 0.5f;
        return new[]
        {
            ToScreen(b + new Vector2(-h.x, -h.y)), ToScreen(b + new Vector2(h.x, -h.y)),
            ToScreen(b + new Vector2(h.x, h.y)), ToScreen(b + new Vector2(-h.x, h.y)),
        };
    }

    private Vector2 ToScreen(Vector2 local)
    {
        Transform canvas = windows[0].GetComponentInParent<Canvas>().transform;
        Vector2 m = leftSurface.Map(local);
        Vector3 world = canvas.TransformPoint(new Vector3(m.x, m.y, 0f));
        Camera cam = Camera.main;
        return cam != null ? (Vector2)cam.WorldToScreenPoint(world) : Vector2.zero;
    }

    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int a = 0, b = poly.Length - 1; a < poly.Length; b = a++)
            if ((poly[a].y > p.y) != (poly[b].y > p.y) &&
                p.x < (poly[b].x - poly[a].x) * (p.y - poly[a].y) / (poly[b].y - poly[a].y) + poly[a].x)
                inside = !inside;
        return inside;
    }
}
