// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Поверхность верхнего листа страницы — единая система координат для всего, что на ней лежит.
// Элементы раскладываются в плоских локальных координатах страницы (px, начало — центр листа, y вверх),
// а Map переводит такую точку на нарисованный лист: изогнутые верх и низ, перспективный скос внешнего края,
// сжатие у корешка и перспективное сокращение рядов. Данные считает processing/make_page_surface.py.
using UnityEngine;

public class PageSurface : MonoBehaviour
{
    [Tooltip("Верхний край листа слева направо, px разворота")]
    public Vector2[] top;
    [Tooltip("Нижний край листа слева направо, px разворота")]
    public Vector2[] bottom;
    [Tooltip("Ширина листа по верхнему и нижнему краю, px разворота: задаёт перспективу рядов")]
    public float w0, w1;
    [Tooltip("Центр холста страницы, px разворота")]
    public Vector2 center;
    [Tooltip("Размер листа в локальных px страницы")]
    public float width, height;

    // Холст страницы: 1 px разворота = 2 px холста (0.625 юнита на 900 px против 720 px на юнит)
    private const float CanvasPerSpreadPx = 2f;

    public Vector2 HalfSize => new Vector2(width * 0.5f, height * 0.5f);

    /// Локальная точка страницы -> точка на холсте страницы (там она видна на нарисованном листе).
    public Vector2 Map(Vector2 local)
    {
        float u = local.x / width + 0.5f;
        float v = 0.5f - local.y / height;
        float vp = v * w0 / (w1 - v * (w1 - w0));
        Vector2 p = Vector2.LerpUnclamped(Sample(top, u), Sample(bottom, u), vp);
        return new Vector2((p.x - center.x) * CanvasPerSpreadPx, (center.y - p.y) * CanvasPerSpreadPx);
    }

    private static Vector2 Sample(Vector2[] edge, float u)
    {
        int n = edge.Length - 1;
        float f = u * n;
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, n - 1);
        return Vector2.LerpUnclamped(edge[i], edge[i + 1], f - i);
    }
}
