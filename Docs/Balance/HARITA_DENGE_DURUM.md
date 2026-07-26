# Harita / Öz Ekonomisi Denge Çalışması — Devam Eden Durum (Sherlock oturum notu)

> Bu dosya bir **oturum devamlılık notudur** — "nerede kalmıştık" dendiğinde buradan devam
> edilir. İçindeki HİÇBİR SAYI KİLİTLİ DEĞİL, hepsi tartışma/simülasyon aşamasında taslak.
> Watson'ın kodundaki mevcut değerler (AP=9/dilim, 10×10 harita, görüş=2, GAME_DESIGN §5
> taslak statlar vb.) de aynı şekilde ilk-geçiş/taslak kararlar — sabit kısıt olarak
> alınmıyor, sadece kaba referans (bkz. kullanıcı hatırlatması 2026-07-26).

## Hedef
- Toplam oynanış süresi: **10-15 saat**
- **~8 bölüm/harita** (TAM NET DEĞİL — mevcut "Bölüm 1 = 9 harita" yapısının ~8'e inmesi mi,
  yoksa çoklu-chapter yapısı mı, teyit edilmedi — bkz. açık sorular).
- **Çekirdek zevk unsuru hikaye DEĞİL.** Oyuncunun kafasında çözdüğü bir "optimizasyon
  problemi": hangi rotayı seçersem hangi özleri/encounter'ları/interaction tile'ları
  alabilirim. Hikaye bunun üstüne sonradan yazılacak (henüz net değil, bilinçli olarak
  şimdilik hikayesiz ilerliyoruz — bkz GAME_DESIGN.md kapsam ayrımı notu).

## Kurulan yöntem — simülasyon
Harita = "orienteering problem" (bütçeli ödül toplama, hex grid) + kısmi görüş (fog of war).
İki politika karşılaştırılıyor:
- **greedy**: sadece o an görünen (görüş menzilinde bilinen) en yakın öze giden, ortalama/
  ilk-kez-oynayan oyuncu.
- **planned**: tam harita bilgisiyle (ezberlemiş/tecrübeli gibi) nearest-neighbor + 2-opt
  ile rota optimize eden iyi oyuncu.

**gap% = (planned − greedy) / planned** → rota seçiminin ne kadar önemli olduğunun ölçüsü.
- gap ~%0 → nereye gidersen git fark etmiyor (sıkıcı, puzzle yok).
- gap çok yüksek (>%50-60) → greedy feci başarısız oluyor (frustrasyon riski).
- Hipotez: ~%20-35 civarı makul bir "tatlı nokta".

**Script:** `Docs/Balance/tools/harita_sim.py` (repoda, çalıştırılabilir).
- Tam sweep: `python harita_sim.py` → `sim_results.csv` üretir.
- Tekil senaryo: `from harita_sim import run_experiment; run_experiment(w, h, density, vision, collect_cost, ap_budget)`

## Şimdiye kadarki bulgular
1. **Küçük harita (10×10) + bol bütçe (162 AP = 3 gün):** greedy %100 kapsama, gap %0 —
   bu ölçekte hiç "optimizasyon problemi" yok, her şeyi toplayabiliyorsun.
2. **Görüş menzili çok güçlü bir lever:** görüş arttıkça gap hızla eriyor (görüş 3+'te
   15×15/20×20 haritada harita pratikte tam görünür oluyor, puzzle kayboluyor). Görüş=2
   (mevcut kod) orta ölçekli haritalarda (%15×15-20×20) gap'i %20-30 bandında tutuyor —
   ama bu bir onay değil, sadece gözlem (kod değeri taslak, istersek değiştiririz).
3. **Kullanıcının önerdiği 22×25 (550 hex) harita + 24 tur/gün senaryosu test edildi:**
   - Free-pickup (collect_cost=0) varsayımıyla ~8.2 günde ~%40 kapsama — kullanıcının elle
     hesabıyla örtüştü, simülasyon doğruladı.
   - **1 tur harcayarak toplama (collect_cost=1):** aynı 8.2 günde kapsama %40.2→%37.8
     (~%6 göreceli düşüş), aynı %40'a ulaşmak için ~1.5-2 gün daha gerekiyor — dramatik değil.
   - **Önemli/beklenmedik bulgu:** collect_cost artışı gap%'i büyütmüyor (bazen küçültüyor)
     — yani "1 tur harcama" kararı asıl ROTA PLANLAMASININ önemini artırmıyor, daha çok genel
     ekonomiyi/tempoyu yavaşlatan bir "vergi" gibi çalışıyor. Puzzle derinliği için asıl
     lever görüş menzili + harita/bütçe oranı.
   - Bu senaryoda gap zaten %40-60 aralığında (gün 7'de %59) — güçlü, hatta erken günlerde
     greedy oyuncu için biraz sert olabilir. Kullanıcının "gün 7 sonrası encounter
     zorlaşması" fikri bu noktada dengeleyici bir unsur olabilir (henüz simüle edilmedi).

## Açık sorular / bir sonraki adım (KALDIĞIMIZ YER)
1. **"8 bölüm" ne demek?** Toplam 8 harita mı (mevcut 9-harita yapısının küçültülmüşü),
   yoksa çoklu-chapter yapısı mı? Kullanıcıya iki kez soruldu, net cevap alınmadı —
   **tekrar sorulmalı**, per-harita süre bütçesi buna bağlı.
2. **Encounter/diğer interaction tile'ları simülasyona AP-vergisi olarak eklemek** — şu an
   simülasyon sadece öz topluyor; gerçekte AP'nin bir kısmı savaşa/HAN-ŞİFACI-MARKET'e
   gidiyor, bu da öz-rotalama için kullanılabilir bütçeyi daraltır (gerçek sayı muhtemelen
   şu anki tahminlerden daha küçük bir harita/bütçe gerektirir). Önerilen sıradaki iş.
3. **AP/tur'u gerçek dakikaya çevirmek** — saniye/aksiyon verisi yok, playtest'ten gelmeli
   (Watson/Efe'den bir oturumda N tur oynayıp geçen gerçek süreyi ölçmesi istenebilir).
4. **Kullanıcıya son sorulan, cevap bekleniyor:** "Encounter'ları AP-vergisi olarak modele
   eklemeye devam mı edelim, yoksa 22×25/24-tur'u geçici baz alıp 8 haritalık kampanyanın
   toplam saatine mi geçelim?"

## Paralel/arka plan durumu
- Watson'daki **TASK-003** (DECISION_LOG.md temizlik geçişi) `Docs/INBOX_TASKS.md`'de
  pending — dönünce kontrol et, diff review bekliyor olabilir.
- Watson'a hazırlanmış ama henüz iletilmemiş bir onboarding promptu vardı (isimlendirme +
  ilk görevler) — muhtemelen artık gereksiz, Watson zaten ismini biliyor ve INBOX akışını
  kullanıyor (TASK-001/002 tamamladı).
- Dosya sahipliği kuralı aktif: DECISION_LOG.md=Watson, INBOX_TASKS.md=append-only,
  GAME_DESIGN.md=Sherlock (bkz CLAUDE.md §9).
- Repo local kopyası (`C:\Users\doala\OneDrive\Desktop\Proje_Oyun`) son bilinen durumda
  origin/main ile senkron, working tree temiz.

## Devam etmek için ilk adım
Yeni oturumda: `git pull` yap (Watson TASK-003'ü bitirmiş olabilir, DECISION_LOG.md
değişmiş olabilir), sonra bu dosyanın "Açık sorular" bölümündeki 4. maddeden devam et.
