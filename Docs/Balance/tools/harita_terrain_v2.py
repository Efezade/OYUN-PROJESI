# -*- coding: utf-8 -*-
"""
Gercek tasarim taksonomisiyle terrain uretimi (kullanici tanimi, 2026-07-27):

YURUNEMEZ: sik orman, dag (daglik alan), gol, nehir (birkac koprulu gecit).
YURUNUR + OZ TASIYAN (oz artik ayri bir "node" degil, KARONUN KENDISI):
  ova (oz yok), taslik ova (1 tas), bol taslik ova (2 tas),
  az agacli ova (1 doga), orman (2 doga), nadir yuksek orman (3 doga, nadir).

Oz TEK SEFERLIK - toplaninca karo tukeniyor (kullanici onayi).
Bu haritada (ilk harita) SADECE 2 oz tipi var: tas + doga (ates/su/toprak
sadece deneme icindi, iptal - ileride baska maplerde farkli ozler gelecek).
"""
import random
from collections import deque

# ── Komsuluk: odd-r OFFSET (2026-08-05 duzeltmesi) ──────────────────────────
# Onceden `from harita_sim import neighbors` kullaniliyordu; o tablo duz AXIAL
# ({1,0},{1,-1},{0,-1},{-1,0},{-1,1},{0,1}) ve (q, r) dizisini axial bir ESKENAR DORTGEN
# sayiyor. Oyun tahtasi ise odd-r OFFSET bir DIKDORTGEN (Unity: HexGridManager.GenerateGrid
# -> HexCoordinate.FromOffset). Iki uzay ayni degil: bu yuzden uretilen nehir tahtada kopuk
# kopuk, bloblar delikli cikiyordu ve "bagli" denen alan tahtada bagli olmayabiliyordu.
# Tablolar eski axial siralamayla AYNI SIRADA (aday listesi sirasi RNG cekimini etkiler).
#   sira: sag · sag-ust · sol-ust · sol · sol-alt · sag-alt
DIRS_EVEN = [(1, 0), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1)]
DIRS_ODD  = [(1, 0), (1, -1), (0, -1),  (-1, 0), (0, 1),  (1, 1)]


def neighbors(q, r):
    """(sutun, satir) icin odd-r offset komsulari."""
    for dq, dr in (DIRS_EVEN if r % 2 == 0 else DIRS_ODD):
        yield (q + dq, r + dr)


IMPASSABLE_BLOBS = ['sik_orman', 'dag', 'gol']

ESSENCE_TABLE = {
    'ova': (0, None),
    'taslik_ova': (1, 'tas'),
    'bol_taslik_ova': (2, 'tas'),
    'az_agacli_ova': (1, 'doga'),
    'orman': (2, 'doga'),
    'nadir_yuksek_orman': (3, 'doga'),
}

DEFAULT_SUBTYPE_WEIGHTS = {
    'ova': 0.55,
    'taslik_ova': 0.12,
    'bol_taslik_ova': 0.05,
    'az_agacli_ova': 0.15,
    'orman': 0.10,
    'nadir_yuksek_orman': 0.03,
}


def generate_terrain(w, h, seed, obstacle_pct=0.20, river_count=1, bridges_per_river=2,
                      blob_size_range=(8, 30), subtype_weights=None):
    rnd = random.Random(seed)
    all_tiles = [(q, r) for q in range(w) for r in range(h)]
    tiles_set = set(all_tiles)
    terrain = {t: None for t in all_tiles}  # None = henuz atanmadi (acik alan)
    total = len(all_tiles)
    target_obstacles = int(total * obstacle_pct)
    obstacle_tiles = set()

    # 1) Nehir
    for _ in range(river_count):
        r0 = rnd.randint(0, h - 1)
        q, r = 0, r0
        path = [(q, r)]
        steps = 0
        while q < w - 1 and steps < w * 4:
            steps += 1
            candidates = [nb for nb in neighbors(q, r) if nb in tiles_set and nb[0] >= q]
            if not candidates:
                candidates = [nb for nb in neighbors(q, r) if nb in tiles_set]
            q, r = rnd.choice(candidates)
            path.append((q, r))
        for t in path:
            if terrain[t] is None:
                terrain[t] = 'nehir'
                obstacle_tiles.add(t)
        bridge_spots = rnd.sample(path, min(bridges_per_river, len(path)))
        for b in bridge_spots:
            terrain[b] = 'kopru'
            obstacle_tiles.discard(b)

    # 2) sik orman / dag / gol bloklari - hedef yuzdeye ulasana kadar, esit agirlik
    attempts = 0
    while len(obstacle_tiles) < target_obstacles and attempts < 300:
        attempts += 1
        seed_tile = rnd.choice(all_tiles)
        if terrain[seed_tile] is not None:
            continue
        blob_type = rnd.choice(IMPASSABLE_BLOBS)
        size = rnd.randint(*blob_size_range)
        blob = {seed_tile}
        frontier = [seed_tile]
        while frontier and len(blob) < size:
            cur = frontier.pop(rnd.randrange(len(frontier)))
            nbs = [nb for nb in neighbors(*cur) if nb in tiles_set and nb not in blob and terrain.get(nb) is None]
            rnd.shuffle(nbs)
            for nb in nbs[:2]:
                blob.add(nb)
                frontier.append(nb)
                if len(blob) >= size:
                    break
        for t in blob:
            if terrain[t] is None:
                terrain[t] = blob_type
                obstacle_tiles.add(t)

    # 3) Kalan acik alanlari yururunur alt-tiplere agirlikli dagit
    weights = subtype_weights or DEFAULT_SUBTYPE_WEIGHTS
    keys = list(weights.keys())
    probs = list(weights.values())
    for t in all_tiles:
        if terrain[t] is None:
            terrain[t] = rnd.choices(keys, weights=probs, k=1)[0]

    walkable_all = [t for t, v in terrain.items() if v not in ('sik_orman', 'dag', 'gol', 'nehir')]
    return terrain, walkable_all


def largest_connected_component(walkable_all, start):
    wset = set(walkable_all)
    if start not in wset:
        start = min(wset, key=lambda t: abs(t[0] - start[0]) + abs(t[1] - start[1]))
    seen = {start}
    dq = deque([start])
    while dq:
        cur = dq.popleft()
        for nb in neighbors(*cur):
            if nb in wset and nb not in seen:
                seen.add(nb)
                dq.append(nb)
    return list(seen), start


def terrain_stats(terrain):
    total = len(terrain)
    counts = {}
    for v in terrain.values():
        counts[v] = counts.get(v, 0) + 1
    return {k: {"n": v, "pct": round(100 * v / total, 1)} for k, v in counts.items()}


def essence_supply(terrain, tiles=None):
    """Verilen tile kumesindeki (varsayilan: hepsi) toplam tas/doga oz arzi."""
    allowed = set(tiles) if tiles is not None else None
    supply = {'tas': 0, 'doga': 0}
    counts = {'tas': 0, 'doga': 0}
    for t, ttype in terrain.items():
        if allowed is not None and t not in allowed:
            continue
        val, ess = ESSENCE_TABLE.get(ttype, (0, None))
        if ess:
            supply[ess] += val
            counts[ess] += 1
    return supply, counts
