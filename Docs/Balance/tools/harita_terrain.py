# -*- coding: utf-8 -*-
"""
Harita simulasyonuna ISLEVSIZ (yururunemez) karo uretimi: sik orman/kaya
blob'lari + dolanan nehir (birkac kopru ile gecilebilir). Amac: 22x25 gibi
bir haritanin GERCEKTE ne kadarinin yururunur oldugunu ve bunun rota
mesafelerini/gap%'i nasil etkiledigini olcmek.

Not: yuzdeler/blob sayilari TASLAK - tasarim karari degil, ayarlanabilir
varsayilan (bkz kullanici hatirlatmasi, HARITA_DENGE_DURUM.md basligi).
"""
import random
from collections import deque

DIRS = [(1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)]


def neighbors(q, r):
    for dq, dr in DIRS:
        yield (q + dq, r + dr)


def generate_terrain(w, h, seed, obstacle_pct=0.20, river_count=1, bridges_per_river=2,
                      blob_size_range=(8, 30)):
    rnd = random.Random(seed)
    all_tiles = [(q, r) for q in range(w) for r in range(h)]
    tiles_set = set(all_tiles)
    terrain = {t: 'plain' for t in all_tiles}
    total = len(all_tiles)
    target_obstacles = int(total * obstacle_pct)
    obstacle_tiles = set()

    # 1) Nehir: soldan saga dolanarak gecen yol, birkac koprulu gecit
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
            if terrain[t] == 'plain':
                terrain[t] = 'river'
                obstacle_tiles.add(t)
        bridge_spots = rnd.sample(path, min(bridges_per_river, len(path)))
        for b in bridge_spots:
            terrain[b] = 'bridge'
            obstacle_tiles.discard(b)

    # 2) Orman/kaya blob'lari - hedef yuzdeye ulasana kadar
    attempts = 0
    while len(obstacle_tiles) < target_obstacles and attempts < 300:
        attempts += 1
        seed_tile = rnd.choice(all_tiles)
        if terrain[seed_tile] != 'plain':
            continue
        blob_type = rnd.choice(['forest', 'rock'])
        size = rnd.randint(*blob_size_range)
        blob = {seed_tile}
        frontier = [seed_tile]
        while frontier and len(blob) < size:
            cur = frontier.pop(rnd.randrange(len(frontier)))
            nbs = [nb for nb in neighbors(*cur) if nb in tiles_set and nb not in blob and terrain.get(nb) == 'plain']
            rnd.shuffle(nbs)
            for nb in nbs[:2]:
                blob.add(nb)
                frontier.append(nb)
                if len(blob) >= size:
                    break
        for t in blob:
            if terrain[t] == 'plain':
                terrain[t] = blob_type
                obstacle_tiles.add(t)

    walkable_all = [t for t in all_tiles if terrain[t] not in ('forest', 'rock', 'river')]
    return terrain, walkable_all


def largest_connected_component(walkable_all, start):
    wset = set(walkable_all)
    if start not in wset:
        # en yakin yururunur karoyu bul
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
