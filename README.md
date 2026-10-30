# Котики Зеленоградска — Unity

Игра по настоящей карте Зеленоградска для Матвея. ТЗ и передача — в Obsidian:
`01_Projects/Котики Зеленоградска — игра для Матвея/Unity — ТЗ для Claude Code.md`.

Идём по плану ТЗ. Сейчас сделан **шаг 1** (данные + импортёр карты).

## Что где

```
data/data.json        собранная карта (1908 домов, 1383 линии, 23 водоёма, 18 котиков)
data/preview.png      превью карты, нарисованное по data.json
tools/                сборка карты: OSM -> data.json (+ превью)
unity-src/Assets/…    скрипты Unity: импортёр и вспомогательный код
osm_raw/              исходный PBF (в git не хранится, качается скриптом)
```

## Сборка карты

Overpass-зеркала из `tools/fetch_osm.sh` сейчас не отвечают (maps.mail.ru — 504,
overpass-api.de — 406), поэтому источник — официальный экстракт Калининграда
с Geofabrik, а `build_data.py` работает без изменений.

```bash
curl -L -o osm_raw/kaliningrad-latest.osm.pbf \
  https://download.geofabrik.de/russia/kaliningrad-latest.osm.pbf

python3 -m venv .venv && .venv/bin/pip install osmium matplotlib

.venv/bin/python tools/pbf_to_osmjson.py osm_raw/kaliningrad-latest.osm.pbf osm
python3 tools/build_data.py osm data/data.json
.venv/bin/python tools/preview.py data/data.json data/preview.png
```

Контрольные цифры (30.10.2026): 1908 домов, 1383 линии дорог, 23 водоёма,
17 ориентиров, 18 котиков, дом Тургенева 14А — в `home`, не `[0,0,10]`.

Если Overpass оживёт, `bash tools/fetch_osm.sh osm && python3 tools/build_data.py osm data/data.json`
даст то же самое без конвертера.

## Unity

1. Поставить Unity Hub и редактор **Unity 6 LTS (6000.x)** с модулями iOS и Android.
2. Новый проект: шаблон **Universal 3D (URP)**, имя `Kotiki`, путь — эта папка
   (`~/Desktop/kotiki-zelenogradsk`). Получится `~/Desktop/kotiki-zelenogradsk/Kotiki`.
3. Скопировать содержимое `unity-src/Assets` в `Kotiki/Assets` (папку `Kotiki` целиком).
4. В редакторе: меню **Котики → 1. Импортировать карту (data.json)**.

Импортёр создаёт сцену `Assets/Kotiki/Scenes/Zelenogradsk.unity`: земля, море и
пруды, пляж, дороги, дома с вальмовыми крышами, деревья в лесах и парках,
солнце с мягкими тенями, туман, ACES-тонмаппинг, камера у нашего дома.

Проверка шага 1: дом на Тургенева жёлтый, рядом красный маячок, в консоли
`[Котики] наш дом найден: высота …`.

Без редактора (headless, после активации лицензии):

```bash
"/Applications/Unity/Hub/Editor/6000.x.x/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit -projectPath ~/Desktop/kotiki-zelenogradsk/Kotiki \
  -executeMethod Kotiki.EditorTools.MapImporter.ImportHeadless -logFile -
```

## Соглашения

- Координаты: `world = (x_data, y, −z_data)`, 1 единица = 1 метр, север = +Z.
- Слои-плоскости лежат друг над другом с шагом 1–2 см, чтобы не мерцали:
  земля 0, зелень 0.004, пляж 0.02, вода 0.03, дороги 0.06, пирс 0.12.
- Меши нарезаны по чанкам 200 × 200 м — это же пригодится для LOD и отсечения.
