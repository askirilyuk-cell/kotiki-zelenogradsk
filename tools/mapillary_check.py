"""Проверка покрытия Mapillary в Зеленоградске и выгрузка кадров для фасадов.

Токен (бесплатный, scope «Read»): https://www.mapillary.com/dashboard/developers
Положить в файл .mapillary-token в корне проекта или передать --token.

    python3 tools/mapillary_check.py                  # только статистика покрытия
    python3 tools/mapillary_check.py --download 40    # + скачать 40 превью в mapillary/
"""
import argparse
import collections
import datetime as dt
import json
import os
import sys
import urllib.parse
import urllib.request

API = "https://graph.mapillary.com/images"
FIELDS = "id,captured_at,compass_angle,geometry,thumb_1024_url"
# Зеленоградск: 54.940,20.452 – 54.973,20.512
BBOX = "20.452,54.940,20.512,54.973"


def load_token(explicit):
    if explicit:
        return explicit
    path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), ".mapillary-token")
    if os.path.exists(path):
        return open(path).read().strip()
    sys.exit("Нет токена: положите его в .mapillary-token или передайте --token")


def fetch_images(token, bbox, page_limit=2000):
    images, after = [], None
    while True:
        params = {"access_token": token, "bbox": bbox, "fields": FIELDS, "limit": str(page_limit)}
        if after:
            params["after"] = after
        url = API + "?" + urllib.parse.urlencode(params)
        try:
            with urllib.request.urlopen(url, timeout=120) as r:
                data = json.load(r)
        except urllib.error.HTTPError as e:
            sys.exit(f"Mapillary ответил {e.code}: {e.read()[:300].decode('utf-8', 'replace')}")

        batch = data.get("data", [])
        images.extend(batch)
        after = (data.get("paging") or {}).get("cursors", {}).get("after")
        print(f"  ...получено {len(images)}")
        if not after or not batch:
            return images


def cell_of(lon, lat, size_m=400.0):
    kx = 111320.0 * 0.5736  # cos(54.95°)
    kz = 111200.0
    return (int(lon * kx / size_m), int(lat * kz / size_m))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--token")
    ap.add_argument("--bbox", default=BBOX, help="minLon,minLat,maxLon,maxLat")
    ap.add_argument("--download", type=int, default=0, help="сколько превью скачать")
    args = ap.parse_args()

    token = load_token(args.token)
    print(f"Запрашиваю Mapillary по bbox {args.bbox} ...")
    images = fetch_images(token, args.bbox)
    print(f"\nВсего кадров: {len(images)}")
    if not images:
        print("Покрытия нет — фасады снимаем сами или остаёмся на процедурных.")
        return

    dates = sorted(i.get("captured_at", 0) for i in images if i.get("captured_at"))
    if dates:
        print("Съёмка:", dt.datetime.fromtimestamp(dates[0] / 1000).date(),
              "—", dt.datetime.fromtimestamp(dates[-1] / 1000).date())

    cells = collections.Counter()
    for img in images:
        coords = (img.get("geometry") or {}).get("coordinates")
        if coords:
            cells[cell_of(coords[0], coords[1])] += 1
    print(f"Квадратов 400×400 м с кадрами: {len(cells)}")
    for (cx, cz), n in cells.most_common(12):
        print(f"   клетка {cx},{cz}: {n} кадров")

    if args.download:
        out = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "mapillary")
        os.makedirs(out, exist_ok=True)
        picked = [i for i in images if i.get("thumb_1024_url")][:args.download]
        for n, img in enumerate(picked, 1):
            path = os.path.join(out, f"{img['id']}.jpg")
            try:
                urllib.request.urlretrieve(img["thumb_1024_url"], path)
                print(f"   {n}/{len(picked)} {path}")
            except Exception as e:  # noqa: BLE001
                print(f"   не скачалось {img['id']}: {e}")
        print(f"\nСложено в {out}")


if __name__ == "__main__":
    main()
