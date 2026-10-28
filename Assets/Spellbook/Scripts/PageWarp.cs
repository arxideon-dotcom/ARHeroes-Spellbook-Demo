// ARHeroes-Spellbook-Demo — hackathon build (Oct 2026).
// Сажает UI-элемент на лист: вершины меша (картинки, текста, маски) переводятся в локальные координаты
// страницы и проходят через общую поверхность PageSurface. Все элементы страницы деформируются одной
// геометрией, поэтому лента и текст на ней, картуш и его подписи, рамка и медальон изгибаются вместе.
// Четырёхугольники дробятся на сетку, чтобы крупные элементы шли по изгибу, а не по хорде.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class PageWarp : BaseMeshEffect
{
    [Tooltip("На сколько частей по каждой оси дробить каждый четырёхугольник меша")]
    [Range(1, 32)] public int subdivisions = 2;

    private PageSurface _surface;
    private readonly List<UIVertex> _in = new List<UIVertex>();
    private readonly List<UIVertex> _out = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        if (_surface == null) _surface = GetComponentInParent<PageSurface>();
        Canvas canvas = graphic.canvas;
        if (_surface == null || canvas == null) return;

        Matrix4x4 toPage = canvas.transform.worldToLocalMatrix * transform.localToWorldMatrix;
        Matrix4x4 fromPage = toPage.inverse;

        _in.Clear();
        _out.Clear();
        vh.GetUIVertexStream(_in);
        int n = Mathf.Max(1, subdivisions);
        bool quads = _in.Count % 6 == 0;

        if (quads)
        {
            // Поток UI: по два треугольника на четырёхугольник, v0 v1 v2 v2 v3 v0
            for (int q = 0; q < _in.Count; q += 6)
            {
                UIVertex a = _in[q], b = _in[q + 1], c = _in[q + 2], d = _in[q + 4];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                    {
                        float s0 = (float)i / n, s1 = (float)(i + 1) / n, t0 = (float)j / n, t1 = (float)(j + 1) / n;
                        UIVertex p00 = Bilerp(a, b, c, d, s0, t0), p01 = Bilerp(a, b, c, d, s0, t1);
                        UIVertex p11 = Bilerp(a, b, c, d, s1, t1), p10 = Bilerp(a, b, c, d, s1, t0);
                        _out.Add(p00); _out.Add(p01); _out.Add(p11);
                        _out.Add(p11); _out.Add(p10); _out.Add(p00);
                    }
            }
        }
        else
        {
            _out.AddRange(_in);
        }

        for (int k = 0; k < _out.Count; k++)
        {
            UIVertex v = _out[k];
            Vector3 local = toPage.MultiplyPoint3x4(v.position);
            Vector2 m = _surface.Map(new Vector2(local.x, local.y));
            v.position = fromPage.MultiplyPoint3x4(new Vector3(m.x, m.y, local.z));
            _out[k] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(_out);
    }

    // a — начало, b — по второй оси, d — по первой, c — напротив a
    private static UIVertex Bilerp(UIVertex a, UIVertex b, UIVertex c, UIVertex d, float s, float t)
    {
        return Lerp(Lerp(a, d, s), Lerp(b, c, s), t);
    }

    private static UIVertex Lerp(UIVertex x, UIVertex y, float k)
    {
        return new UIVertex
        {
            position = Vector3.LerpUnclamped(x.position, y.position, k),
            normal = Vector3.LerpUnclamped(x.normal, y.normal, k),
            tangent = Vector4.LerpUnclamped(x.tangent, y.tangent, k),
            color = Color32.LerpUnclamped(x.color, y.color, k),
            uv0 = Vector4.LerpUnclamped(x.uv0, y.uv0, k),
            uv1 = Vector4.LerpUnclamped(x.uv1, y.uv1, k),
            uv2 = Vector4.LerpUnclamped(x.uv2, y.uv2, k),
            uv3 = Vector4.LerpUnclamped(x.uv3, y.uv3, k),
        };
    }
}
