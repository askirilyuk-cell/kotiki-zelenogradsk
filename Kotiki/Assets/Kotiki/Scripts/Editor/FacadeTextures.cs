using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kotiki.EditorTools
{
    /// <summary>
    /// Текстуры фасадов: одна плитка = один этаж × одна оконная ось (UV считаются в окнах).
    /// Рисуются в светлых тонах, цвет стены задаётся материалом (_BaseColor), поэтому
    /// один рисунок даёт несколько окрасов города.
    /// </summary>
    public static class FacadeTextures
    {
        const string Dir = "Assets/Kotiki/Textures";
        const int Size = 192;

        public static readonly string[] Kinds = { "частная", "панельная", "общественная", "храм" };

        public static Texture2D Get(string kind)
        {
            var path = $"{Dir}/Фасад {kind}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Dir);
            var tex = Draw(kind);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Draw(string kind)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Фасад " + kind };
            var px = new Color[Size * Size];

            Color baseColor;
            switch (kind)
            {
                case "панельная": baseColor = new Color(0.92f, 0.93f, 0.94f); break;
                case "общественная": baseColor = new Color(0.95f, 0.93f, 0.88f); break;
                case "храм": baseColor = new Color(0.88f, 0.78f, 0.70f); break;
                default: baseColor = new Color(0.93f, 0.90f, 0.84f); break;
            }

            var rng = new System.Random(kind.GetHashCode());
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float n = (float)rng.NextDouble() * 0.035f - 0.0175f;
                    px[y * Size + x] = new Color(baseColor.r + n, baseColor.g + n, baseColor.b + n);
                }

            int U(float v) => Mathf.RoundToInt(v * (Size - 1));

            if (kind == "храм")
            {
                // кирпичная кладка
                for (int y = 0; y < Size; y += 12)
                    Fill(px, 0, y, Size, y + 1, new Color(0.82f, 0.71f, 0.63f));
                for (int y = 0; y < Size; y += 12)
                    for (int x = ((y / 12) % 2) * 16; x < Size; x += 32)
                        Fill(px, x, y, x + 1, Mathf.Min(y + 12, Size), new Color(0.84f, 0.74f, 0.66f));
                ArchedWindow(px, 0.33f, 0.36f, 0.67f, 0.78f);
            }
            else if (kind == "панельная")
            {
                // шов панели и балкон
                Fill(px, 0, 0, 3, Size, new Color(0.80f, 0.81f, 0.82f));
                Fill(px, 0, 0, Size, 2, new Color(0.78f, 0.79f, 0.80f));
                Fill(px, U(0.62f), U(0.05f), U(0.98f), U(0.44f), new Color(0.84f, 0.85f, 0.86f));
                for (int i = 0; i < 6; i++)
                {
                    float xx = 0.64f + i * 0.058f;
                    Fill(px, U(xx), U(0.08f), U(xx + 0.012f), U(0.42f), new Color(0.62f, 0.64f, 0.66f));
                }
                Fill(px, U(0.62f), U(0.42f), U(0.98f), U(0.44f), new Color(0.70f, 0.72f, 0.74f));
                Window(px, 0.18f, 0.36f, 0.55f, 0.80f);
            }
            else if (kind == "общественная")
            {
                Fill(px, 0, 0, Size, 3, new Color(0.86f, 0.84f, 0.80f));
                Window(px, 0.10f, 0.32f, 0.90f, 0.84f);
                for (int i = 1; i < 3; i++)
                {
                    float xx = 0.10f + i * 0.2667f;
                    Fill(px, U(xx), U(0.32f), U(xx + 0.008f), U(0.84f), new Color(0.92f, 0.90f, 0.86f));
                }
            }
            else
            {
                // частный дом: окно с наличником и подоконником
                Fill(px, 0, 0, Size, 3, new Color(0.86f, 0.82f, 0.76f));
                Window(px, 0.30f, 0.34f, 0.70f, 0.80f);
                Fill(px, U(0.28f), U(0.30f), U(0.72f), U(0.34f), new Color(0.96f, 0.95f, 0.92f));
                Fill(px, U(0.26f), U(0.34f), U(0.30f), U(0.82f), new Color(0.96f, 0.95f, 0.92f));
                Fill(px, U(0.70f), U(0.34f), U(0.74f), U(0.82f), new Color(0.96f, 0.95f, 0.92f));
                Fill(px, U(0.26f), U(0.80f), U(0.74f), U(0.84f), new Color(0.96f, 0.95f, 0.92f));
            }

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void Window(Color[] px, float x0, float y0, float x1, float y1)
        {
            int X(float v) => Mathf.RoundToInt(v * (Size - 1));
            Fill(px, X(x0) - 2, X(y0) - 2, X(x1) + 2, X(y1) + 2, new Color(0.97f, 0.96f, 0.93f));
            Fill(px, X(x0), X(y0), X(x1), X(y1), new Color(0.16f, 0.21f, 0.27f));
            // блик и переплёт
            Fill(px, X(x0 + (x1 - x0) * 0.1f), X(y0 + (y1 - y0) * 0.55f),
                      X(x0 + (x1 - x0) * 0.45f), X(y0 + (y1 - y0) * 0.9f), new Color(0.28f, 0.36f, 0.44f));
            Fill(px, X((x0 + x1) * 0.5f) - 1, X(y0), X((x0 + x1) * 0.5f) + 1, X(y1), new Color(0.93f, 0.92f, 0.89f));
            Fill(px, X(x0), X((y0 + y1) * 0.5f) - 1, X(x1), X((y0 + y1) * 0.5f) + 1, new Color(0.93f, 0.92f, 0.89f));
        }

        static void ArchedWindow(Color[] px, float x0, float y0, float x1, float y1)
        {
            Window(px, x0, y0, x1, y1 - 0.08f);
            float cx = (x0 + x1) * 0.5f;
            float r = (x1 - x0) * 0.5f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float nxp = x / (float)(Size - 1), nyp = y / (float)(Size - 1);
                    float dx = nxp - cx, dy = nyp - (y1 - 0.08f);
                    if (dx * dx + dy * dy <= r * r && nyp >= y1 - 0.08f && nyp <= y1)
                        px[y * Size + x] = new Color(0.16f, 0.21f, 0.27f);
                }
        }

        static void Fill(Color[] px, int x0, int y0, int x1, int y1, Color c)
        {
            x0 = Mathf.Clamp(x0, 0, Size); x1 = Mathf.Clamp(x1, 0, Size);
            y0 = Mathf.Clamp(y0, 0, Size); y1 = Mathf.Clamp(y1, 0, Size);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    px[y * Size + x] = c;
        }
    }
}
