# -*- coding: utf-8 -*-
"""
harita_sim.py'nin genellestirilmis hali: duz "hepsi ayni degerde oz" yerine
TIP'e gore deger/maliyet farkli node'lar + zamanla artan zindan zorlugu
(node SILINMEZ, sadece gec gidilirse daha pahaliya mal olur) + zorunlu
(mandatory) node destegi.

Akademik karsiligi: Orienteering Problem (OP) + Team Orienteering Problem (TOP,
coklu gun/butce) + Prize-Collecting TSP (PCTSP, zorunlu node = ana gorev
zindanlari). Zorlasan-ama-kaybolmayan zindan maliyeti klasik "time window"
(OPTW) DEGIL - "time-dependent cost" OP'ye yakin bir varyant (bkz. Fomin &
Lingas gibi zaman-bagimli maliyetli rota calismalari; bizim durumumuzda
"deadline" yerine "gec kalinca risk/maliyet artisi" var, node hicbir zaman
yok olmuyor). Survey: Vansteenwegen ve ark., "The Orienteering Problem: A
Survey", EJOR 2011.

Node tipleri:
  - essence  : dusuk deger, dusuk toplama maliyeti, cok sayida, zorlasmiyor
  - dungeon  : orta-yuksek deger/maliyet (savas suresi). GUN >= difficulty_day
               olunca maliyeti difficulty_multiplier ile CARPILIR (daha riskli/
               uzun savas) - ama asla "kayip" olmaz, oyuncu isterse gec de gitse
               toplayabilir, sadece daha pahaliya patlar.
  - mandatory: ana gorev - HER ZAMAN bilinir (sis'ten bagimsiz, haritada pin
               gibi), yuksek deger, zorluk artisindan ETKILENMEZ (hikaye
               ilerlemesi garanti kalsin diye).
"""
import random
from harita_sim import neighbors, build_grid, bfs_all_dist, visible_from  # noqa: F401


class Node:
    def __init__(self, coord, ntype, value, ap_cost, mandatory=False):
        self.coord = coord
        self.type = ntype
        self.value = value
        self.ap_cost = ap_cost
        self.mandatory = mandatory


def place_nodes(tiles, seed,
                 n_essence=30, essence_value=(1, 3), essence_ap=1,
                 n_dungeon=6, dungeon_value=(8, 15), dungeon_ap=(3, 6),
                 n_mandatory=3, mandatory_value=20, mandatory_ap=5):
    rnd = random.Random(seed)
    pool = list(tiles)
    rnd.shuffle(pool)
    nodes = []
    idx = 0
    for _ in range(n_mandatory):
        nodes.append(Node(pool[idx], 'mandatory', mandatory_value, mandatory_ap, True))
        idx += 1
    for _ in range(n_dungeon):
        v = rnd.randint(*dungeon_value)
        a = rnd.randint(*dungeon_ap)
        nodes.append(Node(pool[idx], 'dungeon', v, a, False))
        idx += 1
    for _ in range(n_essence):
        v = rnd.randint(*essence_value)
        nodes.append(Node(pool[idx], 'essence', v, essence_ap, False))
        idx += 1
    return nodes


def effective_cost(n, arrival_day, difficulty_day, difficulty_multiplier):
    """Zindan/savas node'lari icin gec-kalinca-zorlasir mantigi. Essence/mandatory etkilenmez."""
    if n.type == 'dungeon' and difficulty_day is not None and arrival_day >= difficulty_day:
        return n.ap_cost * difficulty_multiplier
    return n.ap_cost


def build_doom_schedule(nodes, schedule, seed=99):
    """schedule: [(day, kumulatif_silinen_sayisi), ...] artan gunlere gore.
    mandatory node'lar hicbir zaman silinmez (hikaye guvencesi). Doner: {coord: silinecegi_gun}."""
    rnd = random.Random(seed)
    pool = [n.coord for n in nodes if not n.mandatory]
    rnd.shuffle(pool)
    doom_map = {}
    prev_count = 0
    for day, cum_count in schedule:
        cum_count = min(cum_count, len(pool))
        for c in pool[prev_count:cum_count]:
            doom_map[c] = day
        prev_count = cum_count
    return doom_map


def simulate_greedy(tiles, start, nodes, vision, ap_budget, move_cost, ap_per_day,
                     difficulty_day=None, difficulty_multiplier=1.0, mode='value_ratio', doom_map=None):
    """mode='value_ratio' -> her adimda bilinen adaylar arasindan deger/maliyet orani
    en iyi olani secer (deger-farkindaligi olan ortalama oyuncu). mode='nearest' ->
    eski davranis, sadece en yakina gider (saf-mesafe, deger-kor oyuncu, alt sinir)."""
    tiles_set = set(tiles)
    node_by_coord = {n.coord: n for n in nodes}
    pos = start
    ap_used = 0
    remaining = set(node_by_coord.keys())
    known = {n.coord for n in nodes if n.mandatory}
    visited_tiles = {start}
    collected_value = 0
    mandatory_done = 0
    breakdown = {}

    def update_known(p):
        for c in visible_from(p, vision, tiles_set):
            if c in node_by_coord:
                known.add(c)

    update_known(pos)
    while True:
        day = ap_used // ap_per_day
        if doom_map:
            for c in list(remaining):
                dday = doom_map.get(c)
                if dday is not None and day >= dday:
                    remaining.discard(c)
        candidates = list(known & remaining)
        dist = bfs_all_dist(pos, tiles_set)
        if not candidates:
            unexplored = [t for t in tiles if t not in visited_tiles]
            if not unexplored:
                break
            target = min(unexplored, key=lambda t: dist.get(t, 10 ** 9))
            d = dist.get(target)
            if d is None or ap_used + d * move_cost > ap_budget:
                break
            ap_used += d * move_cost
            pos = target
            visited_tiles.add(pos)
            update_known(pos)
            continue

        scored = []
        for c in candidates:
            d = dist.get(c)
            if d is None:
                continue
            n = node_by_coord[c]
            arrival_day = (ap_used + d * move_cost) // ap_per_day
            cost = d * move_cost + effective_cost(n, arrival_day, difficulty_day, difficulty_multiplier)
            if ap_used + cost > ap_budget:
                continue
            key = (n.value / cost) if mode == 'value_ratio' else (-d)
            scored.append((key, c, cost, n))
        if not scored:
            break
        scored.sort(key=lambda x: x[0], reverse=True)
        _, target, cost, n = scored[0]
        ap_used += cost
        pos = target
        visited_tiles.add(pos)
        remaining.discard(target)
        collected_value += n.value
        breakdown[n.type] = breakdown.get(n.type, 0) + n.value
        if getattr(n, 'essence_type', None):
            key = f"essence_{n.essence_type}"
            breakdown[key] = breakdown.get(key, 0) + n.value
        if n.mandatory:
            mandatory_done += 1
        update_known(pos)
    return collected_value, mandatory_done, breakdown


def simulate_planned(tiles, start, nodes, move_cost, ap_budget, ap_per_day,
                      difficulty_day=None, difficulty_multiplier=1.0, iters=300, seed=42, doom_map=None):
    tiles_set = set(tiles)
    node_by_coord = {n.coord: n for n in nodes}
    coords = list(node_by_coord.keys())
    all_dist = {t: bfs_all_dist(t, tiles_set) for t in coords + [start]}

    def route_value(order):
        pos = start
        ap_used = 0
        total_value = 0
        mand = 0
        breakdown = {}
        for t in order:
            d = all_dist[pos].get(t)
            if d is None:
                continue
            n = node_by_coord[t]
            arrival_day = (ap_used + d * move_cost) // ap_per_day
            if doom_map:
                dday = doom_map.get(t)
                if dday is not None and arrival_day >= dday:
                    continue  # bu node collapse ile silinmis - atla, pos degismez
            cost = d * move_cost + effective_cost(n, arrival_day, difficulty_day, difficulty_multiplier)
            if ap_used + cost > ap_budget:
                break
            ap_used += cost
            pos = t
            total_value += n.value
            breakdown[n.type] = breakdown.get(n.type, 0) + n.value
            if getattr(n, 'essence_type', None):
                key = f"essence_{n.essence_type}"
                breakdown[key] = breakdown.get(key, 0) + n.value
            if n.mandatory:
                mand += 1
        return total_value, mand, breakdown

    order = []
    pos = start
    remaining = set(coords)
    while remaining:
        nxt = min(remaining, key=lambda t: all_dist[pos].get(t, 10 ** 9))
        order.append(nxt)
        remaining.discard(nxt)
        pos = nxt

    best_val, best_mand, best_breakdown = route_value(order)
    rnd = random.Random(seed)
    for _ in range(iters):
        if len(order) < 2:
            break
        i, j = sorted(rnd.sample(range(len(order)), 2))
        cand = order[:i] + order[i:j + 1][::-1] + order[j + 1:]
        v, m, b = route_value(cand)
        if v >= best_val:
            order, best_val, best_mand, best_breakdown = cand, v, m, b
    return best_val, best_mand, best_breakdown


def run_experiment(w, h, vision, ap_budget, ap_per_day, move_cost=1, seed=1, node_kwargs=None,
                    difficulty_day=None, difficulty_multiplier=1.0, greedy_mode='value_ratio',
                    tiles=None, start=None, nodes=None, doom_map=None):
    if tiles is None:
        tiles = build_grid(w, h)
    if start is None:
        start = (w // 2, h // 2)
    if nodes is None:
        nodes = place_nodes(tiles, seed, **(node_kwargs or {}))
    n_mandatory = sum(1 for n in nodes if n.mandatory)

    g_val, g_mand, g_break = simulate_greedy(tiles, start, nodes, vision, ap_budget, move_cost, ap_per_day,
                                              difficulty_day, difficulty_multiplier, mode=greedy_mode,
                                              doom_map=doom_map)
    p_val, p_mand, p_break = simulate_planned(tiles, start, nodes, move_cost, ap_budget, ap_per_day,
                                               difficulty_day, difficulty_multiplier, doom_map=doom_map)
    p_val = max(p_val, g_val)
    gap = (p_val - g_val) / p_val if p_val else 0
    return {
        "w": w, "h": h, "vision": vision, "ap_budget": ap_budget, "days": round(ap_budget / ap_per_day, 1),
        "greedy_value": g_val, "planned_value": p_val, "gap_pct": round(gap * 100, 1),
        "greedy_mandatory": f"{g_mand}/{n_mandatory}", "planned_mandatory": f"{p_mand}/{n_mandatory}",
        "greedy_breakdown": g_break, "planned_breakdown": p_break,
    }


if __name__ == "__main__":
    w, h = 22, 25
    ap_per_day = 24
    node_kwargs = dict(
        n_essence=30, essence_value=(1, 3), essence_ap=1,
        n_dungeon=6, dungeon_value=(8, 15), dungeon_ap=(3, 6),
        n_mandatory=3, mandatory_value=20, mandatory_ap=5,
    )
    difficulty_day = 10   # gun 10'dan itibaren zindanlar zorlasiyor
    difficulty_multiplier = 2.0  # maliyetleri 2 katina cikiyor (silinmiyor)

    print(f"harita {w}x{h}, ap_per_day={ap_per_day}, difficulty_day={difficulty_day}, "
          f"multiplier={difficulty_multiplier}\n")
    print("-- value_ratio greedy (deger-farkinda ortalama oyuncu) --")
    for vision in [1, 2, 3]:
        for days in [6, 8, 10, 12, 14]:
            r = run_experiment(w, h, vision, days * ap_per_day, ap_per_day, node_kwargs=node_kwargs,
                                difficulty_day=difficulty_day, difficulty_multiplier=difficulty_multiplier,
                                greedy_mode='value_ratio')
            print(f"vision={vision} gun={days:2d} | greedy={r['greedy_value']:3d} planned={r['planned_value']:3d} "
                  f"gap={r['gap_pct']:5.1f}% | zorunlu greedy={r['greedy_mandatory']:>4} "
                  f"planned={r['planned_mandatory']:>4}")

    print("\n-- nearest greedy (deger-kor, alt sinir oyuncu) karsilastirma --")
    for vision in [2]:
        for days in [6, 8, 10, 12, 14]:
            r = run_experiment(w, h, vision, days * ap_per_day, ap_per_day, node_kwargs=node_kwargs,
                                difficulty_day=difficulty_day, difficulty_multiplier=difficulty_multiplier,
                                greedy_mode='nearest')
            print(f"vision={vision} gun={days:2d} | greedy={r['greedy_value']:3d} planned={r['planned_value']:3d} "
                  f"gap={r['gap_pct']:5.1f}% | zorunlu greedy={r['greedy_mandatory']:>4} "
                  f"planned={r['planned_mandatory']:>4}")
