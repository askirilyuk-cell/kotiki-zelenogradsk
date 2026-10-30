import json, math, sys, os, random
D = sys.argv[1]
OUT = sys.argv[2]
LAT0, LON0 = 54.957, 20.482
KX = math.cos(math.radians(LAT0)) * 111320.0
KZ = 111200.0

def P(lat, lon):
    return (round((lon - LON0) * KX, 1), round(-(lat - LAT0) * KZ, 1))

def load(n):
    p = os.path.join(D, n)
    return json.load(open(p))['elements'] if os.path.exists(p) else []

def flat(pts):
    o = []
    for x, z in pts:
        o += [x, z]
    return o

def ring_area(pts):
    a = 0
    for i in range(len(pts)):
        x1, z1 = pts[i]; x2, z2 = pts[(i + 1) % len(pts)]
        a += x1 * z2 - x2 * z1
    return a / 2

def stitch(ways):
    ways = [list(w) for w in ways if len(w) >= 2]
    rings = []
    while ways:
        cur = ways.pop(0)
        changed = True
        while cur[0] != cur[-1] and changed:
            changed = False
            for i, w in enumerate(ways):
                if w[0] == cur[-1]:
                    cur += w[1:]; ways.pop(i); changed = True; break
                if w[-1] == cur[-1]:
                    cur += w[::-1][1:]; ways.pop(i); changed = True; break
        if len(cur) >= 4:
            rings.append(cur)
    return rings

def geom_pts(g):
    return [P(p['lat'], p['lon']) for p in g]

def polys_of(e):
    """returns list of (outer, [holes])"""
    if e['type'] == 'way':
        g = e.get('geometry') or []
        if len(g) >= 4 and g[0] == g[-1]:
            return [(geom_pts(g)[:-1], [])]
        return []
    outers = stitch([[(p['lat'], p['lon']) for p in m.get('geometry', [])] for m in e['members'] if m['role'] == 'outer'])
    inners = stitch([[(p['lat'], p['lon']) for p in m.get('geometry', [])] for m in e['members'] if m['role'] == 'inner'])
    res = []
    for o in outers:
        res.append(([P(*a) for a in o][:-1], [[P(*a) for a in i][:-1] for i in inners]))
    return res

random.seed(7)
data = {}

# ---------- buildings
B = []
home = None
for e in load('bld.json'):
    t = e.get('tags', {})
    g = e.get('geometry')
    if not g or len(g) < 4:
        continue
    pts = geom_pts(g)[:-1] if g[0] == g[-1] else geom_pts(g)
    if len(pts) < 3:
        continue
    bt = t.get('building', 'yes')
    lv = t.get('building:levels')
    try:
        lv = float(str(lv).split(';')[0].replace(',', '.'))
    except Exception:
        lv = None
    area = abs(ring_area(pts))
    kind = 0  # 0 house,1 apartments,2 shed/garage,3 public,4 church
    if bt in ('garage', 'garages', 'shed', 'hut', 'roof', 'carport', 'kiosk', 'greenhouse'):
        kind = 2
    elif bt in ('apartments', 'residential', 'dormitory', 'hotel') or 'tourism' in t:
        kind = 1
    elif bt in ('church', 'chapel', 'cathedral') or t.get('amenity') == 'place_of_worship':
        kind = 4
    elif bt in ('retail', 'commercial', 'school', 'public', 'civic', 'office', 'kindergarten', 'train_station', 'hospital') or 'amenity' in t:
        kind = 3
    if lv:
        h = lv * 3.0 + 1.2
    else:
        if kind == 2:
            h = 3.0
        elif kind == 1:
            h = 13.0 if area > 300 else 9.0
        elif kind == 4:
            h = 12.0
        elif kind == 3:
            h = 8.0
        else:
            h = 6.0 + (3.0 if area > 150 else 0) + random.random() * 1.5
    if t.get('height'):
        try:
            h = float(str(t['height']).replace('m', '').replace(',', '.'))
        except Exception:
            pass
    roof = 1 if (kind == 0 and area < 400 and h < 13) else 0  # 1 = pitched-looking hip roof
    ishome = 1 if (t.get('addr:street', '').find('Тургенева') >= 0 and t.get('addr:housenumber') == '14А') else 0
    if ishome:
        cx = sum(p[0] for p in pts) / len(pts); cz = sum(p[1] for p in pts) / len(pts)
        home = [round(cx, 1), round(cz, 1), round(h, 1), pts]
    B.append([round(h, 1), kind, roof, ishome, flat(pts)])
data['b'] = B

# ---------- roads & rail
R = []
WID = {'primary': 9, 'primary_link': 7, 'secondary': 8, 'tertiary': 7, 'residential': 6, 'unclassified': 6,
       'living_street': 5, 'service': 4, 'pedestrian': 6, 'footway': 2.4, 'path': 1.8, 'cycleway': 2.2,
       'steps': 2.4, 'track': 3}
for e in load('road.json'):
    t = e.get('tags', {})
    g = e.get('geometry')
    if not g:
        continue
    if t.get('railway') == 'rail':
        R.append([0, 2.8, flat(geom_pts(g))])
        continue
    hw = t.get('highway')
    if hw not in WID:
        continue
    cls = 1 if hw in ('primary', 'primary_link', 'secondary', 'tertiary', 'residential', 'unclassified', 'living_street', 'service', 'track') else 2
    if hw == 'pedestrian':
        cls = 3
    if t.get('area') == 'yes':
        continue
    R.append([cls, WID[hw], flat(geom_pts(g))])
data['r'] = R

# ---------- water, beach, sea
W = []; BE = []; NAMED = []
coast = []
for e in load('water.json'):
    t = e.get('tags', {})
    if t.get('natural') == 'coastline':
        coast.append([(p['lat'], p['lon']) for p in e['geometry']])
        continue
    if t.get('natural') == 'water' or e['type'] == 'relation':
        for o, hs in polys_of(e):
            W.append([flat(o), [flat(h) for h in hs]])
            if t.get('name'):
                cx = sum(p[0] for p in o) / len(o); cz = sum(p[1] for p in o) / len(o)
                NAMED.append([t['name'], round(cx, 1), round(cz, 1)])
    elif t.get('natural') == 'beach':
        for o, hs in polys_of(e):
            BE.append(flat(o))
    elif t.get('waterway') in ('river', 'stream', 'canal'):
        R.append([4, 4 if t['waterway'] != 'stream' else 2.5, flat(geom_pts(e['geometry']))])
data['w'] = W
data['beach'] = BE
# stitch coastline into one line NE->SW, close to the north
line = stitch(coast)
c = max(stitch(coast) or [[]], key=len) if line else []
# stitch() closes rings only; for open chain, do manual chain
segs = [s for s in coast]
chain = None
# start from segment whose start isn't an end of any other
ends = {tuple(s[-1]) for s in segs}
starts = [s for s in segs if tuple(s[0]) not in ends]
used = set()
best = []
for st in starts:
    ch = list(st); used = {id(st)}
    while True:
        nxt = next((s for s in segs if id(s) not in used and s[0] == ch[-1]), None)
        if not nxt:
            break
        ch += nxt[1:]; used.add(id(nxt))
    if len(ch) > len(best):
        best = ch
sea = [P(*a) for a in best]
# close polygon via the north (sea side is to the right of coastline direction)
sx0, sz0 = sea[0]; sx1, sz1 = sea[-1]
sea_poly = sea + [(sx1 - 3000, sz1), (-5000, -6000), (5000, -6000), (sx0 + 3000, sz0 - 3000)]
data['sea'] = flat(sea_poly)

# ---------- green
G = []
for fn in sorted(os.listdir(D)):
    if not fn.startswith('g_'):
        continue
    try:
        els = json.load(open(os.path.join(D, fn)))['elements']
    except Exception:
        continue
    for e in els:
        t = e.get('tags', {})
        k = 0
        if t.get('natural') == 'wood' or t.get('landuse') == 'forest':
            k = 1
        elif t.get('leisure') == 'pitch':
            k = 2
        for o, hs in polys_of(e):
            G.append([k, flat(o)])
data['g'] = G

# ---------- POIs / labels
LAB = []
def add(name, lat, lon, kind=0):
    x, z = P(lat, lon); LAB.append([name, x, z, kind])
for e in load('poi1.json'):
    t = e['tags']; b = e['bounds']
    lat = (b['minlat'] + b['maxlat']) / 2; lon = (b['minlon'] + b['maxlon']) / 2
    if t.get('name') == 'Мурариум':
        add('Мурариум', lat, lon, 1)
    elif t.get('man_made') == 'pier':
        x0, z0 = P(b['minlat'], b['minlon']); x1, z1 = P(b['maxlat'], b['maxlon'])
        data['pier'] = flat(geom_pts(e['geometry']))
        add('Пирс', b['maxlat'] - 0.0002, lon, 0)
    elif t.get('name', '').startswith('Храм'):
        add('Храм', lat, lon, 2)
for e in load('poi2.json'):
    t = e.get('tags', {}); n = t.get('name', '')
    if n in ('Кирха Святого Адальберта',):
        add('Кирха', e['lat'], e['lon'], 3)
    if n in ('Колесо времени',):
        add('Фонтан', e['lat'], e['lon'], 4)
    if n in ('Мухоморы', 'Белый гриб', 'Лисички', 'Опята', 'Сыроежка', 'Маслёнок', 'Грузди', 'Бледные поганки'):
        add(n, e['lat'], e['lon'], 5)  # mushroom sculpture
    if n == 'Маяк и чайки':
        add('Маяк', e['lat'], e['lon'], 6)
    if n == 'Императорский дуб':
        add('Императорский дуб', e['lat'], e['lon'], 7)
    if n == 'Кит':
        add('Кит', e['lat'], e['lon'], 8)
for n, x, z in NAMED:
    LAB.append([n, x, z, 9])
data['lab'] = LAB

# ---------- cats
CATS = [
    ('Жора', None, None, 'Жора ждёт дома!'),
    ('Мурзик', 54.95960, 20.48270, 'у Мурариума'),
    ('Светофорчик', 54.96035, 20.47695, 'у кошачьего светофора'),
    ('Колодезный', 54.96165, 20.48160, 'у колодца с котами'),
    ('Совушкин', 54.96212, 20.48100, 'у кота с совой'),
    ('Тортилла', 54.96370, 20.49020, 'у Тортилина пруда'),
    ('Грибник', 54.96372, 20.49020, 'у грибов на променаде'),
    ('Морячок', 54.96320, 20.47430, 'на пирсе'),
    ('Утёнок', 54.95400, 20.48420, 'у пруда рядом с домом'),
    ('Камыш', 54.95130, 20.47720, 'у лесного пруда'),
    ('Кувшинка', 54.95500, 20.48560, 'у маленького пруда'),
    ('Звонарь', 54.96030, 20.48470, 'у кирхи'),
    ('Батюшка', 54.95995, 20.47860, 'у храма'),
    ('Луиза', 54.95676, 20.47807, 'у дома королевы Луизы'),
    ('Маячок', 54.97205, 20.50850, 'у маяка с чайками'),
    ('Музыкант', 54.96492, 20.48600, 'у собак-музыкантов'),
    ('Дубок', 54.96344, 20.49085, 'у Императорского дуба'),
    ('Пескарь', 54.96120, 20.46980, 'на пляже'),
]
# remove duplicate Тортилла/Грибник position — put Грибник on mushrooms
CATS[6] = ('Грибник', 54.9633088, 20.4891862, 'у грибов на променаде')
cats = []
for name, lat, lon, where in CATS:
    if lat is None:
        x, z = home[0], home[1]
    elif name == 'Тортилла':
        t = next((n for n in NAMED if n[0].startswith('Тортилин')), None)
        x, z = (t[1], t[2]) if t else P(lat, lon)
    else:
        x, z = P(lat, lon)
    cats.append([name, round(x, 1), round(z, 1), where])
data['cats'] = cats
data['home'] = home[:3] if home else [0, 0, 10]
data['bounds'] = [P(54.940, 20.452), P(54.973, 20.512)]

s = json.dumps(data, ensure_ascii=False, separators=(',', ':'))
open(OUT, 'w').write(s)
print('bytes', len(s), 'buildings', len(B), 'roads', len(R), 'water', len(W), 'green', len(G), 'labels', len(LAB), 'sea pts', len(sea), 'home', data['home'])
