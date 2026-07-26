# -*- coding: utf-8 -*-
"""
Harita boyutu / oz yogunlugu / gorus menzili / toplama maliyeti icin
"optimizasyon problemi ne kadar anlamli" simulasyonu.

Fikir: harita bir "orienteering problem" (butceli oduL toplama) + kismi
gorus (fog of war). Iki oyuncu politikasi karsilastiriyoruz:
  - GREEDY (ortalama oyuncu): sadece gorus menzilinde ANLIK bilinen en
    yakin/degerli tas'a gider, ac gozlu.
  - PLANLI (iyi oyuncu / tekrar oynayan): TAM harita bilgisiyle (haritayi
    ezberlemis gibi) nearest-neighbor + 2-opt iyilestirmeyle rota kurar.
GAP = (planli - greedy) / planli  ->  rota SECIMinin ne kadar onemli
oldugunun vekil olcusu. GAP ~0 ise: nereye gidersen git fark etmiyor
(sikici). GAP cok buyukse: greedy feci basarisiz oluyor (frustrasyon
riski). Ortada bir yer hedefleniyor.
"""
import random
import itertools
from collections import deque

DIRS = [(1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1)]


def neighbors(q, r):
    for dq, dr in DIRS:
        yield (q + dq, r + dr)


def build_grid(w, h):
    return [(q, r) for q in range(w) for r in range(h)]


def bfs_all_dist(start, tiles_set):
    dist = {start: 0}
    dq = deque([start])
    while dq:
        cur = dq.popleft()
        for nb in neighbors(*cur):
            if nb in tiles_set and nb not in dist:
                dist[nb] = dist[cur] + 1
                dq.append(nb)
    return dist


def place_essences(tiles, density, seed):
    rnd = random.Random(seed)
    n = max(1, int(len(tiles) * density))
    return set(rnd.sample(tiles, n))


def visible_from(pos, vision, tiles_set):
    """vision menzilindeki (hex distance <= vision) mevcut tile'lar."""
    dist = bfs_all_dist(pos, tiles_set)
    return {t for t, d in dist.items() if d <= vision}


def simulate_greedy(tiles, start, essences, vision, ap_budget, move_cost, collect_cost):
    tiles_set = set(tiles)
    pos = start
    ap = ap_budget
    known = set()
    remaining = set(essences)
    collected = 0
    visited_all = {start}
    known |= visible_from(pos, vision, tiles_set)
    while ap > 0:
        visible_now = known & remaining
        if not visible_now:
            # bilinen oz yok -> en yakin kesfedilmemis tile'a git (kesif)
            dist = bfs_all_dist(pos, tiles_set)
            unexplored = [t for t in tiles if t not in visited_all]
            if not unexplored:
                break
            target = min(unexplored, key=lambda t: dist.get(t, 10**9))
            d = dist.get(target)
            if d is None or d * move_cost > ap:
                break
            ap -= d * move_cost
            pos = target
            visited_all.add(pos)
            known |= visible_from(pos, vision, tiles_set)
            continue
        dist = bfs_all_dist(pos, tiles_set)
        target = min(visible_now, key=lambda t: dist.get(t, 10**9))
        d = dist.get(target)
        cost = d * move_cost + collect_cost
        if d is None or cost > ap:
            break
        ap -= cost
        pos = target
        visited_all.add(pos)
        remaining.discard(target)
        collected += 1
        known |= visible_from(pos, vision, tiles_set)
    return collected


def simulate_planned(tiles, start, essences, ap_budget, move_cost, collect_cost, iters=200):
    """Tam bilgiyle nearest-neighbor + rastgele 2-opt benzeri iyilestirme.
    (Kucuk N icin iyi bir yaklasik-optimal ust sinir.)"""
    tiles_set = set(tiles)
    all_dist = {t: bfs_all_dist(t, tiles_set) for t in list(essences) + [start]}

    def route_value(order):
        pos = start
        ap = ap_budget
        cnt = 0
        for t in order:
            d = all_dist[pos].get(t)
            if d is None:
                continue
            cost = d * move_cost + collect_cost
            if cost > ap:
                break
            ap -= cost
            pos = t
            cnt += 1
        return cnt

    ess_list = list(essences)
    # nearest-neighbor baslangic
    order = []
    pos = start
    remaining = set(ess_list)
    while remaining:
        nxt = min(remaining, key=lambda t: all_dist[pos].get(t, 10**9))
        order.append(nxt)
        remaining.discard(nxt)
        pos = nxt
    best = route_value(order)
    rnd = random.Random(42)
    for _ in range(iters):
        i, j = sorted(rnd.sample(range(len(order)), 2))
        cand = order[:i] + order[i:j + 1][::-1] + order[j + 1:]
        v = route_value(cand)
        if v >= best:
            order, best = cand, v
    return best


def run_experiment(w, h, density, vision, collect_cost, ap_budget, move_cost=1, seed=1):
    tiles = build_grid(w, h)
    start = (w // 2, h // 2)
    essences = place_essences(tiles, density, seed)
    greedy = simulate_greedy(tiles, start, essences, vision, ap_budget, move_cost, collect_cost)
    planned = simulate_planned(tiles, start, essences, ap_budget, move_cost, collect_cost)
    planned = max(planned, greedy)  # planli en az greedy kadar iyi olmali
    gap = (planned - greedy) / planned if planned else 0
    return {
        "w": w, "h": h, "tiles": w * h, "density": density, "n_essence": len(essences),
        "vision": vision, "collect_cost": collect_cost, "ap_budget": ap_budget,
        "greedy": greedy, "planned": planned, "gap_pct": round(gap * 100, 1),
        "coverage_greedy_pct": round(100 * greedy / max(1, len(essences)), 1),
    }


if __name__ == "__main__":
    rows = []
    for w, h in [(8, 8), (10, 10), (12, 12)]:
        for density in [0.10, 0.15, 0.20]:
            for vision in [1, 2, 3]:
                for collect_cost in [0, 1]:
                    for ap_budget in [54, 108, 162]:
                        r = run_experiment(w, h, density, vision, collect_cost, ap_budget)
                        rows.append(r)

    import csv
    with open("sim_results.csv", "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)
    print(f"OK - {len(rows)} satir -> sim_results.csv")
