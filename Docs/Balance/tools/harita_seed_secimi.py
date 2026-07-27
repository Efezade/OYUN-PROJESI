# -*- coding: utf-8 -*-
"""Bolum 1 icin ~10 SABIT seed secimi: cok sayida aday seed uretip kalite
filtresinden gecirir (baglantililik, oz arzi, 70-hedefine ulasilabilirlik,
gap% makul bant). Amac: retry'de rastgele degil, ONCEDEN DOGRULANMIS bir
havuzdan (~10 harita) secim yapmak."""
import sys
import os

sys.path.insert(0, os.path.dirname(__file__))
from harita_terrain_v2 import generate_terrain, largest_connected_component, essence_supply
from harita_sim_v2 import run_experiment
from harita_map1_sim import build_nodes

w, h = 22, 25
ap_per_day = 24
TARGET_ESSENCE = 70
N_CANDIDATES = 60
N_WANTED = 10

results = []
for seed in range(1, N_CANDIDATES + 1):
    terrain, walkable_all = generate_terrain(w, h, seed=seed, obstacle_pct=0.20)
    comp, start = largest_connected_component(walkable_all, (w // 2, h // 2))
    comp_frac = len(comp) / (w * h)

    supply, counts = essence_supply(terrain, set(comp))
    total_supply = supply['tas'] + supply['doga']

    nodes = build_nodes(terrain, comp, start, seed)
    r8 = run_experiment(w, h, 2, 8 * ap_per_day, ap_per_day, tiles=comp, start=start, nodes=nodes, greedy_mode='value_ratio')
    r12 = run_experiment(w, h, 2, 12 * ap_per_day, ap_per_day, tiles=comp, start=start, nodes=nodes, greedy_mode='value_ratio')
    ge8 = r8['greedy_breakdown'].get('essence', 0)
    mand_ok = r12['greedy_mandatory'].split('/')
    mand_ok = mand_ok[0] == mand_ok[1]

    # NOT: gap% burada FILTRE DEGIL sadece raporlanan bir metrik - 60 aday uzerinde denendi,
    # gap seed'e gore COK gurultulu (obstacle_pct'ten bagimsiz cogu zaman ~0'a dusuyor, nadiren
    # >%15 cikiyor). Bunu her haritada zorlamak gercekci degil - 10'luk havuzda dogal bir cesitlilik
    # (bazi haritalar rahat, bazilari daha "bulmaca") olmasi sorun degil. Filtre sadece ADALET/
    # OYNANABILIRLIK kriterlerine bakiyor: parcalanmamis harita, yeterli oz arzi, hedefe makul surede
    # ulasilabilirlik, zorunlu gorevler erisilebilir.
    ok = (
        comp_frac >= 0.55 and                        # harita asiri parcalanmamis
        total_supply >= TARGET_ESSENCE * 1.3 and      # yeterli tampon var
        ge8 >= TARGET_ESSENCE * 0.6 and               # gun8'de hedefe makul yakinlikta
        mand_ok                                       # 3 zorunlu gorev greedy ile bile ulasilabilir
    )
    results.append(dict(seed=seed, comp_frac=comp_frac, supply=total_supply, ge8=ge8,
                         gap12=r12['gap_pct'], mand_ok=mand_ok, ok=ok))

good = [r for r in results if r['ok']]
print(f"{N_CANDIDATES} aday seed tarandi, {len(good)} tanesi adalet/oynanabilirlik filtresinden gecti.\n")

# cesitlilik icin gap12'ye gore sirala ve esit araliklarla sample'la (hepsi ayni "kolaylikta" olmasin)
good_sorted = sorted(good, key=lambda r: r['gap12'])
if len(good_sorted) >= N_WANTED:
    step = len(good_sorted) / N_WANTED
    chosen = [good_sorted[int(i * step)] for i in range(N_WANTED)]
else:
    chosen = good_sorted

print(f"Secilen {len(chosen)} seed (gap12'ye gore cesitlendirildi):")
print(f"{'seed':>5} {'baglanti%':>10} {'oz arzi':>8} {'gun8 oz':>8} {'gap12%':>7} {'zorunlu':>8}")
for r in chosen:
    print(f"{r['seed']:>5} {100*r['comp_frac']:>9.1f}% {r['supply']:>8} {r['ge8']:>8} {r['gap12']:>7.1f} {'OK' if r['mand_ok'] else 'HAYIR':>8}")

gaps = [r['gap12'] for r in chosen]
print(f"\ngap12 dagilimi: min={min(gaps):.1f} max={max(gaps):.1f} ortalama={sum(gaps)/len(gaps):.1f}")

if len(good) < N_WANTED:
    print(f"\nUYARI: sadece {len(good)} seed filtreyi gecti, {N_WANTED} hedefine ulasilamadi — "
          f"filtre gevsetilebilir ya da aday havuzu buyutulebilir.")
