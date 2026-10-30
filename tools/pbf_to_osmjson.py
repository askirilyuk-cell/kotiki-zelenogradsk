"""Конвертер Geofabrik PBF -> те же 7 JSON-файлов, что отдаёт fetch_osm.sh (Overpass).

Нужен, когда Overpass-зеркала недоступны: результат того же вида, поэтому
build_data.py работает без изменений.

    python3 pbf_to_osmjson.py osm_raw/kaliningrad-latest.osm.pbf osm
    python3 build_data.py osm data/data.json
"""
import json, os, sys
import osmium

S, W, N, E = 54.940, 20.452, 54.973, 20.512
SPLIT = 54.957  # граница g_north / g_south, как в fetch_osm.sh


def inside(lat, lon):
    return S <= lat <= N and W <= lon <= E


def geom_of(way):
    out = []
    for n in way.nodes:
        loc = n.location
        if loc.valid():
            out.append({"lat": round(loc.lat, 7), "lon": round(loc.lon, 7)})
    return out


def any_node_inside(way):
    for n in way.nodes:
        loc = n.location
        if loc.valid() and inside(loc.lat, loc.lon):
            return True
    return False


def centroid_lat(geom):
    return sum(p["lat"] for p in geom) / len(geom)


MARGIN = 0.004  # ~450 м: береговая линия нужна чуть за границей города


def clip_line(geom, margin=MARGIN):
    """Режет ломаную по рамке bbox+margin (Liang–Barsky) на непрерывные куски."""
    s, w_, n, e = S - margin, W - margin, N + margin, E + margin

    def clip_seg(p, q):
        x0, y0, x1, y1 = p["lon"], p["lat"], q["lon"], q["lat"]
        dx, dy = x1 - x0, y1 - y0
        t0, t1 = 0.0, 1.0
        for pp, qq in ((-dx, x0 - w_), (dx, e - x0), (-dy, y0 - s), (dy, n - y0)):
            if pp == 0:
                if qq < 0:
                    return None
            else:
                t = qq / pp
                if pp < 0:
                    if t > t1: return None
                    t0 = max(t0, t)
                else:
                    if t < t0: return None
                    t1 = min(t1, t)
        return ({"lat": round(y0 + t0 * dy, 7), "lon": round(x0 + t0 * dx, 7)},
                {"lat": round(y0 + t1 * dy, 7), "lon": round(x0 + t1 * dx, 7)})

    pieces, cur = [], []
    for i in range(len(geom) - 1):
        r = clip_seg(geom[i], geom[i + 1])
        if r is None:
            if len(cur) >= 2:
                pieces.append(cur)
            cur = []
            continue
        a, b = r
        if cur and abs(cur[-1]["lat"] - a["lat"]) < 1e-7 and abs(cur[-1]["lon"] - a["lon"]) < 1e-7:
            cur.append(b)
        else:
            if len(cur) >= 2:
                pieces.append(cur)
            cur = [a, b]
    if len(cur) >= 2:
        pieces.append(cur)
    return pieces


class RelationIds(osmium.SimpleHandler):
    """Проход 1: id водных отношений и их членов."""

    def __init__(self):
        super().__init__()
        self.rels = []  # [(id, tags, [(role, way_id)])]
        self.members = set()

    def relation(self, r):
        tags = dict(r.tags)
        if tags.get("natural") != "water":
            return
        members = [((m.role or "outer"), m.ref) for m in r.members if m.type == "w"]
        if not members:
            return
        self.rels.append((r.id, tags, members))
        self.members.update(ref for _, ref in members)


class Collector(osmium.SimpleHandler):
    def __init__(self, member_ids):
        super().__init__()
        self.member_ids = member_ids
        self.bld, self.road, self.water, self.poi1, self.poi2 = [], [], [], [], []
        self.g_north, self.g_south = [], []
        self.way_geom = {}  # way_id -> geom, только для членов отношений

    def node(self, n):
        t = dict(n.tags)
        if not t or not inside(n.lat, n.lon):
            return
        if "tourism" in t or "historic" in t or t.get("amenity") == "fountain":
            self.poi2.append({"type": "node", "id": n.id, "lat": n.lat, "lon": n.lon, "tags": t})

    def way(self, w):
        t = dict(w.tags)
        need_geom = w.id in self.member_ids
        if not need_geom and not t:
            return
        if not need_geom and not any_node_inside(w):
            return
        geom = geom_of(w)
        if len(geom) < 2:
            return
        if need_geom:
            self.way_geom[w.id] = geom
        if not t:
            return
        closed = len(geom) >= 4 and geom[0] == geom[-1]

        el = {"type": "way", "id": w.id, "tags": t, "geometry": geom}
        if "building" in t and closed:
            self.bld.append(el)
        if "highway" in t or t.get("railway") == "rail":
            self.road.append(el)
        nat = t.get("natural")
        if nat == "coastline":
            for piece in clip_line(geom):
                self.water.append({"type": "way", "id": w.id, "tags": t, "geometry": piece})
        elif nat in ("water", "beach", "sand") or t.get("waterway") in ("river", "stream", "canal"):
            self.water.append(el)
        if t.get("man_made") in ("pier", "water_tower") or t.get("amenity") == "place_of_worship":
            lats = [p["lat"] for p in geom]; lons = [p["lon"] for p in geom]
            el = dict(el, bounds={"minlat": min(lats), "maxlat": max(lats),
                                  "minlon": min(lons), "maxlon": max(lons)})
            self.poi1.append(el)
        green = (nat == "wood" or t.get("landuse") in ("forest", "grass", "meadow", "recreation_ground")
                 or t.get("leisure") in ("park", "garden", "pitch"))
        if green and closed:
            (self.g_north if centroid_lat(geom) >= SPLIT else self.g_south).append(el)

    def relation(self, r):
        pass


def main():
    src, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)

    ids = RelationIds()
    ids.apply_file(src, locations=True)

    col = Collector(ids.members)
    col.apply_file(src, locations=True)

    water = list(col.water)
    skipped = 0
    for rid, tags, members in ids.rels:
        mem = []
        for role, ref in members:
            g = col.way_geom.get(ref)
            if g:
                mem.append({"type": "way", "ref": ref, "role": role, "geometry": g})
        touches = any(inside(p["lat"], p["lon"]) for m in mem for p in m["geometry"])
        if touches and any(m["role"] == "outer" for m in mem):
            water.append({"type": "relation", "id": rid, "tags": tags, "members": mem})
        else:
            skipped += 1

    files = {"bld": col.bld, "road": col.road, "water": water, "poi1": col.poi1,
             "poi2": col.poi2, "g_north": col.g_north, "g_south": col.g_south}
    for name, els in files.items():
        p = os.path.join(out, name + ".json")
        with open(p, "w") as f:
            json.dump({"elements": els}, f, ensure_ascii=False)
        print(f"{name:8s} {len(els):6d} -> {p}")
    if skipped:
        print(f"пропущено водных отношений без outer: {skipped}")

    coast = sum(1 for e in col.water if e["type"] == "way" and e["tags"].get("natural") == "coastline")
    print(f"береговых линий: {coast}")


if __name__ == "__main__":
    main()
