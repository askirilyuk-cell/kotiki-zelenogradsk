#!/usr/bin/env bash
# Выгрузка OSM для «Котики Зеленоградска». Зеркало maps.mail.ru (overpass-api.de часто не отвечает).
# Запросы мелкими кусками, с повторами: сервер часто отдаёт 504 на большие.
set -u
OUT="${1:-osm}"; mkdir -p "$OUT"; cd "$OUT"
U=https://maps.mail.ru/osm/tools/overpass/api/interpreter
B='54.940,20.452,54.973,20.512'
get(){ for i in 1 2 3 4 5; do code=$(curl -s -m 200 --data-urlencode "data=[out:json][timeout:180];($2);out geom;" $U -o "$1.json" -w "%{http_code}"); echo "$1 try$i $code"; [ "$code" = 200 ] && return 0; sleep 8; done; return 1; }
get bld   "way[\"building\"]($B);"
get road  "way[\"highway\"]($B);way[\"railway\"=\"rail\"]($B);"
get water "way[\"natural\"=\"water\"]($B);relation[\"natural\"=\"water\"]($B);way[\"waterway\"]($B);way[\"natural\"~\"beach|sand|coastline\"]($B);"
get poi1  "way[\"man_made\"~\"pier|water_tower\"]($B);way[\"amenity\"=\"place_of_worship\"]($B);"
get poi2  "node[\"tourism\"]($B);node[\"historic\"]($B);node[\"amenity\"=\"fountain\"]($B);"
# зелень двумя половинами (юг в прошлый раз не отдался ни разу)
get g_north "way[\"natural\"=\"wood\"](54.957,20.452,54.973,20.512);way[\"landuse\"~\"forest|grass|meadow|recreation_ground\"](54.957,20.452,54.973,20.512);way[\"leisure\"~\"park|garden|pitch\"](54.957,20.452,54.973,20.512);"
get g_south "way[\"natural\"=\"wood\"](54.940,20.452,54.957,20.512);way[\"landuse\"~\"forest|grass|meadow|recreation_ground\"](54.940,20.452,54.957,20.512);way[\"leisure\"~\"park|garden|pitch\"](54.940,20.452,54.957,20.512);"
ls -la
