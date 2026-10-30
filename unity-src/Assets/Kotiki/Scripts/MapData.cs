using System.Collections.Generic;
using UnityEngine;

namespace Kotiki
{
    public class Building
    {
        public float Height;
        public int Kind;          // 0 частный, 1 многоквартирный, 2 сарай, 3 общественное, 4 храм
        public bool HipRoof;      // вальмовая крыша
        public bool IsHome;
        public Vector2[] Footprint;
    }

    public class RoadLine
    {
        public int Cls;           // 1 дорога, 2 тротуар, 3 пешеходная, 0 ж/д, 4 река
        public float Width;
        public Vector2[] Points;
    }

    public class WaterBody
    {
        public Vector2[] Outer;
        public List<Vector2[]> Holes = new List<Vector2[]>();
    }

    public class GreenArea
    {
        public int Kind;          // 0 газон/парк, 1 лес, 2 спортплощадка
        public Vector2[] Contour;
    }

    public class MapLabel
    {
        public string Name;
        public Vector2 Pos;
        public int Kind;
    }

    public class CatSpot
    {
        public string Name;
        public Vector2 Pos;
        public string Where;
    }

    /// <summary>Разбор data.json: компактные массивы из build_data.py.</summary>
    public class MapData
    {
        public List<Building> Buildings = new List<Building>();
        public List<RoadLine> Roads = new List<RoadLine>();
        public List<WaterBody> Waters = new List<WaterBody>();
        public List<Vector2[]> Beach = new List<Vector2[]>();
        public List<GreenArea> Green = new List<GreenArea>();
        public List<MapLabel> Labels = new List<MapLabel>();
        public List<CatSpot> Cats = new List<CatSpot>();

        public Vector2[] Sea;
        public Vector2[] Pier;
        public float[] Home = new float[3];     // x, z, высота
        public Vector2 BoundsMin;               // x_min, z_min
        public Vector2 BoundsMax;               // x_max, z_max

        public static MapData Load(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var data = new MapData();

            if (root.TryGetValue("b", out var b))
                foreach (var el in (List<object>)b)
                    data.Buildings.Add(ParseBuilding(el));

            if (root.TryGetValue("r", out var r))
                foreach (var el in (List<object>)r)
                {
                    var a = (List<object>)el;
                    data.Roads.Add(new RoadLine
                    {
                        Cls = (int)Num(a[0]),
                        Width = (float)Num(a[1]),
                        Points = Flat((List<object>)a[2])
                    });
                }

            if (root.TryGetValue("w", out var w))
                foreach (var el in (List<object>)w)
                {
                    var a = (List<object>)el;
                    var body = new WaterBody { Outer = Flat((List<object>)a[0]) };
                    foreach (var h in (List<object>)a[1]) body.Holes.Add(Flat((List<object>)h));
                    data.Waters.Add(body);
                }

            if (root.TryGetValue("beach", out var beach))
                foreach (var el in (List<object>)beach) data.Beach.Add(Flat((List<object>)el));

            if (root.TryGetValue("g", out var g))
                foreach (var el in (List<object>)g)
                {
                    var a = (List<object>)el;
                    data.Green.Add(new GreenArea { Kind = (int)Num(a[0]), Contour = Flat((List<object>)a[1]) });
                }

            if (root.TryGetValue("lab", out var lab))
                foreach (var el in (List<object>)lab)
                {
                    var a = (List<object>)el;
                    data.Labels.Add(new MapLabel
                    {
                        Name = (string)a[0],
                        Pos = new Vector2((float)Num(a[1]), (float)Num(a[2])),
                        Kind = (int)Num(a[3])
                    });
                }

            if (root.TryGetValue("cats", out var cats))
                foreach (var el in (List<object>)cats)
                {
                    var a = (List<object>)el;
                    data.Cats.Add(new CatSpot
                    {
                        Name = (string)a[0],
                        Pos = new Vector2((float)Num(a[1]), (float)Num(a[2])),
                        Where = a.Count > 3 ? (string)a[3] : ""
                    });
                }

            if (root.TryGetValue("sea", out var sea)) data.Sea = Flat((List<object>)sea);
            if (root.TryGetValue("pier", out var pier)) data.Pier = Flat((List<object>)pier);

            if (root.TryGetValue("home", out var home))
            {
                var h = (List<object>)home;
                data.Home = new[] { (float)Num(h[0]), (float)Num(h[1]), (float)Num(h[2]) };
            }

            if (root.TryGetValue("bounds", out var bounds))
            {
                var bd = (List<object>)bounds;
                var p0 = (List<object>)bd[0];
                var p1 = (List<object>)bd[1];
                data.BoundsMin = new Vector2((float)Num(p0[0]), (float)Num(p1[1]));
                data.BoundsMax = new Vector2((float)Num(p1[0]), (float)Num(p0[1]));
            }

            return data;
        }

        static Building ParseBuilding(object el)
        {
            var a = (List<object>)el;
            return new Building
            {
                Height = (float)Num(a[0]),
                Kind = (int)Num(a[1]),
                HipRoof = Num(a[2]) > 0.5,
                IsHome = Num(a[3]) > 0.5,
                Footprint = Flat((List<object>)a[4])
            };
        }

        static double Num(object o) => o is double d ? d : (o is long l ? l : 0.0);

        static Vector2[] Flat(List<object> flat)
        {
            var pts = new Vector2[flat.Count / 2];
            for (int i = 0; i < pts.Length; i++)
                pts[i] = new Vector2((float)Num(flat[i * 2]), (float)Num(flat[i * 2 + 1]));
            return pts;
        }
    }
}
