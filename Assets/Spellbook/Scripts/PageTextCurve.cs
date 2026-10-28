// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Лёгкая посадка текста на изогнутую бумагу. Текст остаётся обычным редактируемым UI Text:
// меняется только геометрия меша. Каждая буква сдвигается по пологой дуге и поворачивается
// по касательной к ней, поэтому сохраняет пропорции; вертикали наклоняются по перспективе страницы.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class PageTextCurve : BaseMeshEffect
{
    [Tooltip("Насколько середина строки выше краёв, px холста. Небольшое значение: это изгиб, не арка.")]
    public float arc = 6f;

    [Tooltip("Наклон вертикалей по перспективе страницы, градусы: + низ уходит вправо (правая страница), − влево.")]
    public float lean = 0f;

    private readonly List<UIVertex> _verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        _verts.Clear();
        vh.GetUIVertexStream(_verts);

        Rect r = graphic.rectTransform.rect;
        float half = Mathf.Max(1f, r.width * 0.5f);
        float shear = Mathf.Tan(lean * Mathf.Deg2Rad);

        // Поток вершин — по два треугольника (6 вершин) на букву
        for (int i = 0; i + 5 < _verts.Count; i += 6)
        {
            Vector3 c = Vector3.zero;
            for (int k = 0; k < 6; k++) c += _verts[i + k].position;
            c /= 6f;

            float t = Mathf.Clamp((c.x - r.center.x) / half, -1f, 1f);
            float dy = arc * (1f - t * t);
            float angle = Mathf.Atan(-2f * arc * t / half);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

            for (int k = 0; k < 6; k++)
            {
                UIVertex v = _verts[i + k];
                Vector3 p = v.position - c;
                p = new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos, p.z);
                p += c + new Vector3(0f, dy, 0f);
                p.x -= shear * (p.y - r.center.y);
                v.position = p;
                _verts[i + k] = v;
            }
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(_verts);
    }
}
