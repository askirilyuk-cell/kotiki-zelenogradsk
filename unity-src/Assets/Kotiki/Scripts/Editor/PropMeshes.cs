using System.Collections.Generic;
using UnityEngine;

namespace Kotiki.EditorTools
{
    /// <summary>Простые процедурные модели: дом-коробка с крышей, деревья.</summary>
    public static class Props
    {
        public static void AddBuilding(MeshBuilder walls, MeshBuilder roof, Building b)
        {
            var fp = GeoMath.CCW(b.Footprint);
            if (fp.Count < 3) return;

            float h = Mathf.Max(2.5f, b.Height);
            for (int i = 0; i < fp.Count; i++)
            {
                Vector2 p = fp[i], q = fp[(i + 1) % fp.Count];
                Vector2 edge = q - p;
                if (edge.sqrMagnitude < 1e-4f) continue;
                var outward = new Vector3(edge.y, 0f, -edge.x).normalized;
                walls.AddWall(new Vector3(p.x, 0f, p.y), new Vector3(q.x, 0f, q.y), h, outward);
            }

            if (b.HipRoof) AddHipRoof(roof, fp, h);
            else roof.AddFlatPolygon(fp, h + 0.05f);
        }

        static void AddHipRoof(MeshBuilder mb, List<Vector2> fp, float h)
        {
            Vector2 c = GeoMath.Centroid(fp);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in fp)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
            }
            float minSide = Mathf.Min(maxX - minX, maxZ - minZ);
            float rh = Mathf.Clamp(minSide * 0.28f + 0.8f, 1.4f, 4.5f);
            const float inset = 0.34f;

            var top = new List<Vector2>(fp.Count);
            foreach (var p in fp) top.Add(Vector2.Lerp(p, c, inset));

            for (int i = 0; i < fp.Count; i++)
            {
                int j = (i + 1) % fp.Count;
                Vector3 a = new Vector3(fp[i].x, h, fp[i].y);
                Vector3 b = new Vector3(fp[j].x, h, fp[j].y);
                Vector3 cc = new Vector3(top[j].x, h + rh, top[j].y);
                Vector3 d = new Vector3(top[i].x, h + rh, top[i].y);
                Vector3 want = new Vector3((fp[i].x + fp[j].x) * 0.5f - c.x, 0f, (fp[i].y + fp[j].y) * 0.5f - c.y);
                if (want.sqrMagnitude < 1e-4f) want = Vector3.up;
                mb.AddQuad(a, b, cc, d, want.normalized);
            }
            mb.AddFlatPolygon(top, h + rh + 0.02f);
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
