// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Поверхность верхнего листа страницы — единая система координат для всего, что на ней лежит.
// Элементы раскладываются в плоских локальных координатах страницы (px, начало — центр листа, y вверх),
// а Map переводит такую точку на нарисованный лист. Середина листа — плоскость в перспективе
// (гомография по четырём углам листа), к ней добавляется изгиб верхнего и нижнего краёв, который
// затухает к середине листа, и сжатие у корешка по всей высоте. Данные считает
// processing/make_page_surface.py — формула там та же.
using UnityEngine;

public class PageSurface : MonoBehaviour
{
    [Tooltip("Верхний край листа слева направо, px разворота")]
    public Vector2[] top;
    [Tooltip("Нижний край листа слева направо, px разворота")]
    public Vector2[] bottom;
    [Tooltip("Гомография (u, v) -> px разворота по углам листа, 3x3 по строкам")]
    public float[] h = new float[9];
    [Tooltip("Доля высоты листа, на которой затухает изгиб краёв")]
    public float falloff = 0.35f;
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
        Vector2 p = H(u, v);
        Vector2 dT = Sample(top, u) - H(u, 0f);
        Vector2 dB = Sample(bottom, u) - H(u, 1f);
        p.x += (1f - v) * dT.x + v * dB.x;
        p.y += Fade(v) * dT.y + Fade(1f - v) * dB.y;
        return new Vector2((p.x - center.x) * CanvasPerSpreadPx, (center.y - p.y) * CanvasPerSpreadPx);
    }

    private Vector2 H(float u, float v)
    {
        float w = h[6] * u + h[7] * v + h[8];
        return new Vector2((h[0] * u + h[1] * v + h[2]) / w, (h[3] * u + h[4] * v + h[5]) / w);
    }

    private float Fade(float t)
    {
        float k = Mathf.Max(0f, 1f - t / falloff);
        return k * k;
    }

    private static Vector2 Sample(Vector2[] edge, float u)
    {
        int n = edge.Length - 1;
        float f = u * n;
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, n - 1);
        return Vector2.LerpUnclamped(edge[i], edge[i + 1], f - i);
    }
}
