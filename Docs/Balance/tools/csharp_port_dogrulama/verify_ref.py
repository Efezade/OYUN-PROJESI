# -*- coding: utf-8 -*-
"""Referans (Python) terrain ciktisi — C# portuyla birebir karsilastirma icin."""
import sys, os
sys.path.insert(0, r"C:\3D OYUN\OYUN\Docs\Balance\tools")
from harita_terrain_v2 import generate_terrain

SEEDS = [89, 7, 20, 108, 219, 64, 173, 283, 141, 286]
w, h = 22, 25

out = []
for seed in SEEDS:
    terrain, _ = generate_terrain(w, h, seed=seed, obstacle_pct=0.20)
    for q in range(w):
        for r in range(h):
            out.append(f"{seed} {q} {r} {terrain[(q, r)]}")

sys.stdout.write("\n".join(out) + "\n")
