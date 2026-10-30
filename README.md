# Котики Зеленоградска — Unity

Игра по настоящей карте Зеленоградска для Матвея. ТЗ и передача — в Obsidian:
`01_Projects/Котики Зеленоградска — игра для Матвея/Unity — ТЗ для Claude Code.md`.

Идём по плану ТЗ. Сейчас сделан **шаг 1** (данные + импортёр карты).

## Что где

```
data/data.json        собранная карта (1908 домов, 1383 линии, 23 водоёма, 18 котиков)
data/preview.png      превью карты, нарисованное по data.json
tools/                сборка карты: OSM -> data.json (+ превью)
Kotiki/               проект Unity 6000.6.7f1 (URP); скрипты — Kotiki/Assets/Kotiki/Scripts
previews/             снимки сцены, которые рисует пункт меню «2. Сделать снимки»
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

Проект уже создан: `Kotiki/` (Unity 6000.6.7f1, шаблон Universal 3D).

1. Unity Hub → вкладка **Projects** → **Add project from disk** → выбрать
   `~/Desktop/kotiki-zelenogradsk/Kotiki`. Новый проект с именем `kotiki`
   создавать не надо — macOS не различает регистр, и Hub скажет, что такой уже есть.
2. В редакторе: меню **Котики → 1. Импортировать карту (data.json)**.
3. Меню **Котики → 2. Сделать снимки сцены** — кладёт 6 PNG в `previews/`.

Импортёр создаёт сцену `Assets/Kotiki/Scenes/Zelenogradsk.unity`: земля, море и
пруды, пляж, дороги, дома с вальмовыми крышами, деревья в лесах и парках,
солнце с мягкими тенями, туман, ACES-тонмаппинг, камера у нашего дома.

Проверка шага 1: дом на Тургенева жёлтый (31 м, две длинные секции), на его
крыше красный маячок, в консоли `[Котики] наш дом найден: высота 31.2 м`.

Сцена и материалы — генерируемые, в git их нет: после клонирования достаточно
пункта 2 (это ~1 минута). Без редактора (headless, после активации лицензии):

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity"

$UNITY -batchmode -quit -projectPath ~/Desktop/kotiki-zelenogradsk/Kotiki \
  -executeMethod Kotiki.EditorTools.MapImporter.ImportHeadless -logFile /tmp/import.log

$UNITY -batchmode -quit -projectPath ~/Desktop/kotiki-zelenogradsk/Kotiki \
  -executeMethod Kotiki.EditorTools.ScenePreview.RenderHeadless -logFile /tmp/render.log
```

## Соглашения

- Координаты: 1 единица = 1 метр, север = +Z. В `data.json` север записан как
  −z (так выходит из широты), поэтому `MapData.Load` один раз переворачивает
  ось z, и дальше весь код, включая меши, живёт в `world = (x, y, z)`.
- Слои-плоскости лежат друг над другом с шагом 1–2 см, чтобы не мерцали:
  земля 0, зелень 0.004, пляж 0.02, вода 0.03, дороги 0.06, пирс 0.12.
- Меши нарезаны по чанкам 200 × 200 м — это же пригодится для LOD и отсечения.
