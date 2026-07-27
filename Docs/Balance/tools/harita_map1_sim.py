# -*- coding: utf-8 -*-
"""Harita 1 (ilk harita) ekonomi kontrolu: terrain'den turetilen tas/doga oz arzi
+ zorunlu (3x kurtarma), zindan, encounter node'lariyla gercekci greedy/planned
oyuncunun essence toplamda ~70 hedefine ne kadar yaklastigini olcer."""
import random
import sys
import os

sys.path.insert(0, os.path.dirname(__file__))
from harita_terrain_v2 import generate_terrain, largest_connected_component, terrain_stats, essence_supply, ESSENCE_TABLE
from harita_sim_v2 import Node, run_experiment

TARGET_ESSENCE_SPEND = 70


def build_nodes(terrain, walkable_comp, start, seed,
                 n_mandatory=3, mandatory_value=20, mandatory_ap=5,
                 n_zindan=6, zindan_value=(8, 15), zindan_ap=(3, 6),
                 n_encounter=8, encounter_value=(3, 6), encounter_ap=(1, 2)):
    rnd = random.Random(seed + 1000)
    nodes = []

    for t in walkable_comp:
        ttype = terrain[t]
        val, ess = ESSENCE_TABLE.get(ttype, (0, None))
        if ess:
            n = Node(t, 'essence', val, 1, False)
            n.essence_type = ess
            nodes.append(n)

    ova_pool = [t for t in walkable_comp if terrain[t] == 'ova' and t != start]
    rnd.shuffle(ova_pool)
    idx = 0
    for _ in range(n_mandatory):
        nodes.append(Node(ova_pool[idx], 'mandatory', mandatory_value, mandatory_ap, True))
        idx += 1
    for _ in range(n_zindan):
        v = rnd.randint(*zindan_value)
        a = rnd.randint(*zindan_ap)
        nodes.append(Node(ova_pool[idx], 'zindan', v, a, False))
        idx += 1
    for _ in range(n_encounter):
        v = rnd.randint(*encounter_value)
        a = rnd.randint(*encounter_ap)
        nodes.append(Node(ova_pool[idx], 'encounter', v, a, False))
        idx += 1
    return nodes


if __name__ == "__main__":
    w, h = 22, 25
    ap_per_day = 24
    obstacle_pct = 0.20
    seed = 7

    terrain, walkable_all = generate_terrain(w, h, seed=seed, obstacle_pct=obstacle_pct)
    comp, start = largest_connected_component(walkable_all, (w // 2, h // 2))
    comp_set = set(comp)

    stats = terrain_stats(terrain)
    print("terrain dagilimi (tum harita):", {k: f"{v['pct']}%" for k, v in stats.items()})
    print(f"yururunur (baglantili bilesen): {len(comp)}/{w*h} (%{100*len(comp)/(w*h):.1f})")

    supply, counts = essence_supply(terrain, comp_set)
    total_supply = supply['tas'] + supply['doga']
    print(f"\nOZ ARZI (yalniz erisilebilir bilesende): tas={supply['tas']} ({counts['tas']} karo), "
          f"doga={supply['doga']} ({counts['doga']} karo), TOPLAM={total_supply}")
    print(f"HEDEF (harcanmasi beklenen): {TARGET_ESSENCE_SPEND} -> arz/hedef orani = {total_supply/TARGET_ESSENCE_SPEND:.2f}x\n")

    nodes = build_nodes(terrain, comp, start, seed)
    n_essence = sum(1 for n in nodes if n.type == 'essence')
    n_zindan = sum(1 for n in nodes if n.type == 'zindan')
    n_encounter = sum(1 for n in nodes if n.type == 'encounter')
    n_mandatory = sum(1 for n in nodes if n.mandatory)
    print(f"node sayilari: essence={n_essence} zindan={n_zindan} encounter={n_encounter} mandatory={n_mandatory}\n")

    print("-- gerçekçi (value_ratio) greedy oyuncu vs planned --")
    for vision in [2, 3]:
        for days in [6, 8, 10, 12]:
            r = run_experiment(w, h, vision, days * ap_per_day, ap_per_day,
                                tiles=comp, start=start, nodes=nodes, greedy_mode='value_ratio')
            ge = r['greedy_breakdown'].get('essence', 0)
            pe = r['planned_breakdown'].get('essence', 0)
            gt = r['greedy_breakdown'].get('essence_tas', 0)
            gd = r['greedy_breakdown'].get('essence_doga', 0)
            print(f"vision={vision} gun={days:2d} | greedy TOPLAM={r['greedy_value']:3d} (oz={ge:3d} "
                  f"[tas={gt},doga={gd}]) | planned TOPLAM={r['planned_value']:3d} (oz={pe:3d}) | "
                  f"gap={r['gap_pct']:5.1f}% | zorunlu={r['greedy_mandatory']}")
