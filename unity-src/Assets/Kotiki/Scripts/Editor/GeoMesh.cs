using System.Collections.Generic;
using UnityEngine;

namespace Kotiki.EditorTools
{
    /// <summary>Сборка геометрии карты: полигоны, ленты дорог, стены, крыши, деревья.</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly List<int> Tris = new List<int>();

        public bool IsEmpty => Tris.Count == 0;

        public void AddTwoSided(Vector3 a, Vector3 b, Vector3 c, Vector3 wantNormal)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, wantNormal) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp;
            }
            int i = Verts.Count;
            Verts.Add(a); Verts.Add(b); Verts.Add(c);
            Vector2 ua = new Vector2(a.x, a.z) * 0.02f;
            UVs.Add(ua); UVs.Add(new Vector2(b.x, b.z) * 0.02f); UVs.Add(new Vector2(c.x, c.z) * 0.02f);
            Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 wantNormal)
        {
            AddTwoSided(a, b, c, wantNormal);
            AddTwoSided(a, c, d, wantNormal);
        }

        /// <summary>Горизонтальный полигон (нормаль вверх).</summary>
        public void AddFlatPolygon(IList<Vector2> pts, float y)
        {
            if (pts.Count < 3) return;
            var ring = GeoMath.CCW(pts);
            foreach (var tri in GeoMath.Triangulate(ring))
            {
                AddTwoSided(new Vector3(ring[tri.a].x, y, ring[tri.a].y),
                            new Vector3(ring[tri.b].x, y, ring[tri.b].y),
                            new Vector3(ring[tri.c].x, y, ring[tri.c].y), Vector3.up);
            }
        }

        public void AddWall(Vector3 bottomA, Vector3 bottomB, float height, Vector3 outward)
        {
            Vector3 topA = bottomA + Vector3.up * height;
            Vector3 topB = bottomB + Vector3.up * height;
            AddQuad(bottomA, bottomB, topB, topA, outward);
        }

        /// <summary>Лента постоянной ширины по ломаной (дороги, реки, ж/д).</summary>
        public void AddRibbon(IList<Vector2> pts, float width, float y)
        {
            if (pts.Count < 2) return;
            float half = width * 0.5f;
            var left = new Vector3[pts.Count];
            var right = new Vector3[pts.Count];

            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 dir;
                if (i == 0) dir = pts[1] - pts[0];
                else if (i == pts.Count - 1) dir = pts[i] - pts[i - 1];
                else dir = (pts[i + 1] - pts[i]).normalized + (pts[i] - pts[i - 1]).normalized;

                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
                dir.Normalize();
                var side = new Vector2(-dir.y, dir.x) * half;

                left[i] = new Vector3(pts[i].x + side.x, y, pts[i].y + side.y);
                right[i] = new Vector3(pts[i].x - side.x, y, pts[i].y - side.y);
            }

            for (int i = 0; i < pts.Count - 1; i++)
                AddQuad(left[i], right[i], right[i + 1], left[i + 1], Vector3.up);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (Verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(Verts);
            mesh.SetUVs(0, UVs);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    public struct Tri
    {
        public int a, b, c;
        public Tri(int a, int b, int c) { this.a = a; this.b = b; this.c = c; }
    }

    public static class GeoMath
    {
        /// <summary>Площадь со знаком в плоскости XZ (x вправо, z «вверх» на карте).</summary>
        public static float SignedArea(IList<Vector2> pts)
        {
            float a = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i], q = pts[(i + 1) % pts.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a * 0.5f;
        }

        public static List<Vector2> CCW(IList<Vector2> pts)
        {
            var list = new List<Vector2>(pts);
            if (SignedArea(list) < 0f) list.Reverse();
            return list;
        }

        public static Vector2 Centroid(IList<Vector2> pts)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < pts.Count; i++) sum += pts[i];
            return sum / Mathf.Max(1, pts.Count);
        }

        public static bool PointInPolygon(IList<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Отсечение ушей — простой полигон без дырок, на вход CCW.</summary>
        public static List<Tri> Triangulate(List<Vector2> pts)
        {
            var result = new List<Tri>();
            if (pts.Count < 3) return result;

            var idx = new List<int>();
            for (int i = 0; i < pts.Count; i++) idx.Add(i);

            int guard = idx.Count * idx.Count + 16;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count];
                    int i1 = idx[i];
                    int i2 = idx[(i + 1) % idx.Count];

                    Vector2 a = pts[i0], b = pts[i1], c = pts[i2];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross <= 1e-6f) continue; // не выпуклая вершина

                    bool contains = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int ik = idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (PointInTriangle(pts[ik], a, b, c)) { contains = true; break; }
                    }
                    if (contains) continue;

                    result.Add(new Tri(i0, i1, i2));
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // самопересекающийся контур — берём что есть
            }

            if (idx.Count == 3) result.Add(new Tri(idx[0], idx[1], idx[2]));
            return result;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        public static float DistanceToPolygon(IList<Vector2> poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                best = Mathf.Min(best, DistanceToSegment(p, a, b));
            }
            return best;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
