using System.Collections.Generic;
using UnityEngine;

namespace Kotiki.EditorTools
{
    /// <summary>Процедурные дома и деревья.</summary>
    public static class Props
    {
        /// <summary>Стены, крыша и цоколь одного дома.</summary>
        public static void AddBuilding(MeshBuilder walls, MeshBuilder roof, MeshBuilder plinth, Building b)
        {
            var fp = GeoMath.CCW(b.Footprint);
            if (fp.Count < 3) return;

            float h = Mathf.Max(2.5f, b.Height);
            int floors = Mathf.Clamp(Mathf.RoundToInt(h / 3.0f), 1, 12);

            for (int i = 0; i < fp.Count; i++)
            {
                Vector2 p = fp[i], q = fp[(i + 1) % fp.Count];
                Vector2 edge = q - p;
                if (edge.sqrMagnitude < 1e-4f) continue;

                var outward = new Vector3(edge.y, 0f, -edge.x).normalized;
                int bays = Mathf.Clamp(Mathf.RoundToInt(edge.magnitude / 3.4f), 1, 14);

                walls.AddWall(new Vector3(p.x, 0f, p.y), new Vector3(q.x, 0f, q.y), h, outward, bays, floors);

                // цоколь: та же стена, вынесенная наружу на 5 см
                Vector2 o = new Vector2(outward.x, outward.z) * 0.05f;
                plinth.AddWall(new Vector3(p.x + o.x, 0f, p.y + o.y),
                               new Vector3(q.x + o.x, 0f, q.y + o.y), 0.75f, outward, 1, 1);
            }

            if (b.HipRoof) AddHipRoof(roof, fp, h, b.Kind == 0);
            else roof.AddFlatPolygon(fp, h + 0.05f);
        }

        /// <summary>
        /// Вальмовая крыша: конёк вдоль длинной оси, торцы сведены к двум точкам.
        /// Для почти квадратных контуров вырождается в пирамиду — это и нужно.
        /// </summary>
        static void AddHipRoof(MeshBuilder mb, List<Vector2> fp, float h, bool chimney)
        {
            Vector2 p0 = fp[0], p1 = fp[1];
            float best = -1f;
            for (int i = 0; i < fp.Count; i++)
            {
                Vector2 a = fp[i], c = fp[(i + 1) % fp.Count];
                float len = (c - a).sqrMagnitude;
                if (len > best) { best = len; p0 = a; p1 = c; }
            }

            Vector2 dir = (p1 - p0).normalized;
            if (dir.sqrMagnitude < 1e-4f) return;
            var perp = new Vector2(-dir.y, dir.x);

            float minT = float.MaxValue, maxT = float.MinValue;
            float minS = float.MaxValue, maxS = float.MinValue;
            foreach (var p in fp)
            {
                Vector2 rel = p - p0;
                float t = Vector2.Dot(rel, dir), s = Vector2.Dot(rel, perp);
                minT = Mathf.Min(minT, t); maxT = Mathf.Max(maxT, t);
                minS = Mathf.Min(minS, s); maxS = Mathf.Max(maxS, s);
            }

            float depth = Mathf.Max(0.5f, maxS - minS);
            float rh = Mathf.Clamp(depth * 0.35f, 1.2f, 4.5f);
            float sMid = (minS + maxS) * 0.5f;
            float ridgeStart = minT + depth * 0.5f;
            float ridgeEnd = Mathf.Max(ridgeStart, maxT - depth * 0.5f);

            Vector2 Top(Vector2 p)
            {
                Vector2 rel = p - p0;
                float t = Mathf.Clamp(Vector2.Dot(rel, dir), ridgeStart, ridgeEnd);
                return p0 + dir * t + perp * sMid;
            }

            for (int i = 0; i < fp.Count; i++)
            {
                Vector2 a = fp[i], b2 = fp[(i + 1) % fp.Count];
                Vector2 ta = Top(a), tb = Top(b2);

                Vector3 A = new Vector3(a.x, h, a.y);
                Vector3 B = new Vector3(b2.x, h, b2.y);
                Vector3 C = new Vector3(tb.x, h + rh, tb.y);
                Vector3 D = new Vector3(ta.x, h + rh, ta.y);

                Vector2 edge = b2 - a;
                if (edge.sqrMagnitude < 1e-4f) continue;
                var want = new Vector3(edge.y, 0f, -edge.x).normalized;

                if ((ta - tb).sqrMagnitude < 0.01f) mb.AddTwoSided(A, B, C, want);
                else mb.AddQuad(A, B, C, D, want);
            }

            if (chimney && rh > 1.4f)
            {
                Vector2 ridge = p0 + dir * ((ridgeStart + ridgeEnd) * 0.5f) + perp * (sMid + depth * 0.16f);
                mb.AddBox(new Vector3(ridge.x, h + rh * 0.45f, ridge.y),
                          new Vector3(0.75f, 1.8f, 0.75f), Vector3.up);
            }
        }

        public static void AddTree(MeshBuilder trunk, MeshBuilder leaves, Vector2 pos, float scale, bool pine)
        {
            float trunkH = (pine ? 1.5f : 2.1f) * scale;
            AddPrism(trunk, new Vector3(pos.x, 0f, pos.y), 0.17f * scale, trunkH, 5);

            if (pine)
            {
                AddCone(leaves, new Vector3(pos.x, trunkH * 0.55f, pos.y), 1.55f * scale, 2.4f * scale, 7);
                AddCone(leaves, new Vector3(pos.x, trunkH + 0.9f * scale, pos.y), 1.25f * scale, 2.2f * scale, 7);
                AddCone(leaves, new Vector3(pos.x, trunkH + 1.9f * scale, pos.y), 0.9f * scale, 1.9f * scale, 7);
            }
            else
            {
                AddBlob(leaves, new Vector3(pos.x, trunkH + 1.1f * scale, pos.y), 1.75f * scale);
                AddBlob(leaves, new Vector3(pos.x + 0.7f * scale, trunkH + 2.0f * scale, pos.y - 0.4f * scale), 1.25f * scale);
                AddBlob(leaves, new Vector3(pos.x - 0.65f * scale, trunkH + 1.9f * scale, pos.y + 0.5f * scale), 1.1f * scale);
            }
        }

        static void AddPrism(MeshBuilder mb, Vector3 baseCenter, float radius, float height, int sides)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 want = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                mb.AddQuad(p0, p1, p1 + Vector3.up * height, p0 + Vector3.up * height, want);
            }
        }

        static void AddCone(MeshBuilder mb, Vector3 baseCenter, float radius, float height, int sides)
        {
            Vector3 apex = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Vector3 want = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0.35f, Mathf.Sin((a0 + a1) * 0.5f));
                mb.AddTwoSided(p0, p1, apex, want.normalized);
            }
        }

        static void AddBlob(MeshBuilder mb, Vector3 center, float radius)
        {
            const int rings = 2, sides = 6;
            Vector3[] prev = null;
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                float y = Mathf.Cos(phi) * radius * 0.85f;
                float rad = Mathf.Sin(phi) * radius;
                var ring = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float ang = i * Mathf.PI * 2f / sides;
                    ring[i] = center + new Vector3(Mathf.Cos(ang) * rad, y, Mathf.Sin(ang) * rad);
                }
                if (prev != null)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        int j = (i + 1) % sides;
                        Vector3 want = (prev[i] + prev[j] + ring[i] + ring[j]) * 0.25f - center;
                        if (want.sqrMagnitude < 1e-6f) want = Vector3.up;
                        mb.AddQuad(prev[i], prev[j], ring[j], ring[i], want.normalized);
                    }
                }
                prev = ring;
            }
        }
    }
}
