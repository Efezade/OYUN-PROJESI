# -*- coding: utf-8 -*-
"""Engelli (orman/kaya/nehir) terrain'i sim'e sokup baseline (acik grid) ile
karsilastirir + JSON dump eder (harita gorseli icin)."""
import json
import sys
import os

sys.path.insert(0, os.path.dirname(__file__))
from harita_terrain import generate_terrain, largest_connected_component, terrain_stats
from harita_sim_v2 import run_experiment, place_nodes

w, h = 22, 25
ap_per_day = 24
node_kwargs = dict(
    n_essence=30, essence_value=(1, 3), essence_ap=1,
    n_dungeon=6, dungeon_value=(8, 15), dungeon_ap=(3, 6),
    n_mandatory=3, mandatory_value=20, mandatory_ap=5,
)
difficulty_day = 10
difficulty_multiplier = 2.0

for obstacle_pct in [0.10, 0.20, 0.30]:
    terrain, walkable_all = generate_terrain(w, h, seed=7, obstacle_pct=obstacle_pct)
    comp, start = largest_connected_component(walkable_all, (w // 2, h // 2))
    stats = terrain_stats(terrain)
    print(f"\n=== obstacle_pct hedef={obstacle_pct} ===")
    print("terrain dagilimi:", {k: f"{v['pct']}%" for k, v in stats.items()})
    print(f"toplam yururunur (baglantili bilesen): {len(comp)}/{w*h} (%{100*len(comp)/(w*h):.1f})")

    for vision in [2]:
        for days in [8, 10, 12]:
            base = run_experiment(w, h, vision, days * ap_per_day, ap_per_day, node_kwargs=node_kwargs,
                                   difficulty_day=difficulty_day, difficulty_multiplier=difficulty_multiplier,
                                   greedy_mode='value_ratio')
            terr = run_experiment(w, h, vision, days * ap_per_day, ap_per_day, node_kwargs=node_kwargs,
                                   difficulty_day=difficulty_day, difficulty_multiplier=difficulty_multiplier,
                                   greedy_mode='value_ratio', tiles=comp, start=start)
            print(f"  gun={days:2d} | ACIK GRID greedy={base['greedy_value']:3d} planned={base['planned_value']:3d} "
                  f"gap={base['gap_pct']:5.1f}%  ||  ENGELLI greedy={terr['greedy_value']:3d} "
                  f"planned={terr['planned_value']:3d} gap={terr['gap_pct']:5.1f}%")

# Gorsellestirme icin bir tanesini JSON'a dok (obstacle_pct=0.20, seed=7)
terrain, walkable_all = generate_terrain(w, h, seed=7, obstacle_pct=0.20)
comp, start = largest_connected_component(walkable_all, (w // 2, h // 2))
nodes = place_nodes(comp, seed=1, **node_kwargs)

dump = {
    "w": w, "h": h, "start": start,
    "terrain": [[q, r, t] for (q, r), t in terrain.items()],
    "nodes": [
        {"q": n.coord[0], "r": n.coord[1], "type": n.type, "value": n.value,
         "ap_cost": n.ap_cost, "mandatory": n.mandatory}
        for n in nodes
    ],
}
out_path = os.path.join(os.path.dirname(__file__), "..", "harita_taslak_data.json")
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(dump, f, ensure_ascii=False, indent=1)
print(f"\nJSON yazildi: {out_path}")
