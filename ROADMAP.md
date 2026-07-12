# OYUN — BÖLÜM 1 FİNAL YOL HARİTASI (18 Hafta · 3 Kişi)

> **Güncelleme:** 2026-07-12 — Bu belge eski "28 Günlük Vertical Slice" planının yerine geçer
> (o planın kapsamı büyük ölçüde tamamlandı; aşağıda "Bugüne Kadar Yapılanlar").
> **FINAL tanımı:** 🏁 **15 Kasım 2026 — BÖLÜM 1 v1.0**: 9 haritalık dünyada tam roguelite döngüsü
> (keşif → savaş → ganimet → evrim → run sonu → meta-ilerleme), final görseller + gerçek UI +
> hikaye sunumu + boss + ses. Bölüm 2+ kapsam DIŞI (bkz. §6).
> Hikaye değişikliği serbesttir → **Hafta 4 sonundaki HİKAYE KİLİDİ'ne kadar** (bkz. §5 Riskler).

---

## 0) BUGÜNE KADAR YAPILANLAR ✅ (whitebox kalitesinde çalışıyor)

**Keşif / Overworld**
- ✅ Hex grid (10x10, pointy-top) + A* yürüme + tıklama girişi
- ✅ Savaş Sisi: 2 karo görüş, karartma + fiziksel sis kapağı, Kule ile kalıcı açılım
- ✅ Kut (AP) + Zaman motoru (3 AP = 1 dilim, 6 dilim = 1 gün) + Gün 4'te Dünyanın Çöküşü (karo silinmesi)
- ✅ **BÖLÜM 1 dünyası: 9 harita, 3x3 snake dizilim** + kenardan harita geçişi + TAB minimap
- ✅ Töz/Öz sistemi: 3 tip (Ateş/Su/Toprak), haritaya elle boyama (Essence Painter), "Topla (1 AP)"

**Savaş**
- ✅ Overworld ↔ Savaş durum makinesi (tek sahne) + görev düğümü + yakınlık kapısı
- ✅ Deployment: Kam zorunlu/otomatik iner; özle üretilen birimler mavi pedlere yerleştirilir
- ✅ Tarifle birim üretimi (Savaşçı = 2 Ateş+1 Toprak, Ranger = 2 Su+1 Toprak)
- ✅ Tur sistemi: hıza göre initiative, hareket (BFS), saldırı, düşman AI, win/lose, permadeath
- ✅ Kam komutan: 3 büyü (Ateş Topu / Şifa / Ruh Kalkanı) + mana havuzu; **yenilgi = Kam ölümü**
- ✅ Savaş nameplate'leri: isim + bölümlü can barı + hasar flaşı + "-N" sayısı

**Boru hatları / araçlar (içerik üretimi için hazır)**
- ✅ Tile Painter + "Klasörü Tara" karo import hattı (güvenlik kalkanlı) — 11 karo v1 içeride
- ✅ Essence Painter (öz boyama) · TAM KURULUM (tek tıkla temiz sahne kurulumu)
- ✅ Karakter FBX bağlama (overworld bake + savaş runtime binder)
- 🟡 Yürüme animasyon sistemi (@'li FBX hattı + Idle↔Walk) — **kodlandı, bilinen sıkıntılar var → H1'de çözülecek**

**Bilinen açık işler (H1-H2'ye dağıtıldı):** animasyon sıkıntıları · bulut sisi görseli (`SS's/bulut.png`) ·
öz-top geçiş animasyonu · harita oluşma efekti (H12) · 9 harita tasarımı (H9-H11) · küp kalıntısı kod temizliği.

---

## 1) TAKIM VE ROLLER

| Rol | Kısaltma | Sorumluluk alanı (birbirinden ayrık) |
|---|---|---|
| 🎨 **Tasarımcı** | T | 3D art (karolar, karakterler, yapılar), animasyon kaynakları (Mixamo/elle), UI görsel dili (kitap/parşömen/ikon), VFX görsel kaynakları, hikaye metni + anlatı, ses listesi/seçimi. **Unity'ye kod/entegrasyon yapmaz — asset + spec üretir.** |
| ⚙️ **Developer 1 — Sistem/Gameplay** | D1 | Savaş mekanikleri, ganimet/evrim/skill tree backend'i, sınıf yetenekleri, AI, ekonomi/denge, save/load, performans. |
| 🖥️ **Developer 2 — UI/UX & Entegrasyon** | D2 | IMGUI → uGUI dönüşümü, 6 ekran (Kitap/Çanta/Harita/Skill Tree/Ana Menü/Ganimet), HUD'lar, tasarımcı asset'lerinin oyuna entegrasyonu (karo/karakter/VFX/ses), harita boyama, build. |

**Teslim kuralı:** T'nin ürettiği her asset bir "teslim klasörü"ne spec ile düşer (karo: `Assets/Art/Models/Tiles/` · karakter animasyonu: `Assets/Art/Models/Characters/<Ad>/` `model@Anim.fbx`) → D2 aynı hafta entegre eder. Kod Claude Code ile yazılır; D1/D2 yönlendirir, test eder, birleştirir.

---

## 2) KİLOMETRE TAŞLARI

| Taş | Tarih | Tanım (Definition of Done) |
|---|---|---|
| **M1 — Vertical Slice** | H4 sonu · **9 Ağu** | Harita 1 final karolarla; tam döngü oynanır: keşif→öz→savaş→**ganimet draft**→evrim→devam. Animasyonlu Kam. Hikaye kilidi v1. |
| **M2 — Sistemler Tamam** | H8 sonu · **6 Eyl** | Tüm mekanikler + 6 ekran (whitebox uGUI) çalışır: kitap/çanta/skill tree/harita/ana menü, pot-eşya, meta-öz, run reset, save/load v1. |
| **M3 — İçerik Tamam** | H12 sonu · **4 Eki** | 9 harita boyalı, 5 sınıf + Aldacılar + boss oyunda, hikaye sunumu içeride, UI re-skin bitti, ses v1. İçerik eklemeye SON. |
| **M4 — Beta** | H16 sonu · **1 Kas** | Denge + cila + VFX + ses final; dış oyuncu testi (5-10 kişi) yapıldı; kritik bug 0. |
| 🏁 **FINAL — Bölüm 1 v1.0** | H18 sonu · **15 Kas** | Geri bildirim işlendi, RC build, tanıtım materyali (ekran görüntüleri/kısa video). |

---

## 3) HAFTA HAFTA PLAN

### FAZ A — Sağlamlaştırma + Görsel Temel (H1-H4) → M1 Vertical Slice

**H1 · 13-19 Tem**
- 🎨 T: Mevcut 11 karonun **final re-export'u, parti 1: zemin karoları** (standart/çimen/orman/kum/su/lav) — Blender checklist: Join → Delete Loose → Apply All Transforms → -Z Forward/Y Up → 1K doku, ≤5MB (spec: `Docs/Karo_Tasarim_Klavuzu_GUNCEL.docx`).
- ⚙️ D1: **Animasyon sıkıntılarını bitir** (in-place/root motion, duruş, geçiş pürüzleri) + Attack/Death tetik altyapısı (savaş kodu kancaları).
- 🖥️ D2: **Bulut sisi** (`SS's/bulut.png` → FogTile) + **öz-top harita geçiş animasyonu** (TransitionRoutine Adım B/C).
- **Hafta çıktısı:** Kam pürüzsüz animasyonla yürüyor; sis bulut görünümlü; geçişler şık; ilk final karolar oyunda.

**H2 · 20-26 Tem**
- 🎨 T: Karo final **parti 2: yapılar** (kule, köprü, ağaç/çalı varyasyonları, mantar/çiçek) + karo kenar/çerçeve görsel önerisi.
- ⚙️ D1: **Savaş hissi paketi:** min-max hasar + kritik vuruş + mana savaş-başı reset kararının uygulanması + menzilli saldırı desteği (Okçu'ya hazırlık).
- 🖥️ D2: **uGUI altyapısı**: Canvas mimarisi + tema (font/renk/9-slice) + zaman çarkı gerçek UI (sol üst) + IMGUI HUD'ların taşınma sırası.
- **Hafta çıktısı:** Savaş sayıları "oyun gibi" hissettiriyor; UI iskeleti kuruldu; yapı karoları içeride. + Teknik temizlik: küp kalıntı kodları + `soyguncu_karakteri.fbx` silinir.

**H3 · 27 Tem - 2 Ağu**
- 🎨 T: Karo final **parti 3: görev yapıları** (mezarlık, tapınak, ev, yel değirmeni) + **9 haritanın kâğıt üstünde bölge tasarımı** (biyom dağılımı, kule/görev/öz/HAN-ŞİFACI-MARKET yerleşimi — hikayeyle uyumlu).
- ⚙️ D1: **Ganimet/Draft backend:** savaş sonu 3 ödül havuzu (pot / öz paketi / eşya), ağırlıklı seçim, envantere yazma.
- 🖥️ D2: **Ganimet seçim ekranı** (3 kart UI + hover/seçim) + savaş giriş/çıkış fade geçişleri + zafer/yenilgi ekranı cilası.
- **Hafta çıktısı:** Savaş kazanınca 3 ödülden biri seçiliyor; karo seti tamama yakın; 9 haritanın planı onaylı.

**H4 · 3-9 Ağu — 🎯 M1**
- 🎨 T: **HİKAYE KİLİDİ v1** — hikaye metni tamamlanır (değişiklikler dahil): intro anlatısı, bölüm akışı, Bölüm 1 finali/boss kurgusu, görev metinleri. (Bundan sonra yapı değişmez, sadece cümle düzeyi düzeltme.)
- ⚙️ D1: **Evrim backend:** kart L1/2/3, öz feda ile seviye, pasif kanca sistemi (etkiler H9'da içerik olarak dolar).
- 🖥️ D2: **Harita 1'i final karolarla boya** (T'nin H3 planına göre) + görev/öz/kule yerleşimi + M1 build'i derle.
- **M1 KABUL TESTİ (Cuma, 3 kişi):** Baştan oynanış: keşif→toplama→savaş→draft→evrim→devam. Pürüz listesi çıkar → H5'e girer.

### FAZ B — Sistemler + Gerçek UI (H5-H8) → M2 Sistemler Tamam

**H5 · 10-16 Ağu**
- 🎨 T: **Karakter tasarımları 1:** Kam final model + rig + animasyon seti (`@Idle/@Walking/@Attack/@Death` — gerekirse `@Cast`) + **Barbar** modeli.
- ⚙️ D1: **Sınıf aktif yetenek çerçevesi** (data-driven): Barbar "2 vuruş", Okçu "yüksek karo avantajı", Büyücü "eylemsizlik" vb. belge statlarıyla.
- 🖥️ D2: **KİTAP ekranı whitebox uGUI:** öz deposu 3 sayaç, sınıf sayfaları (WARRIOR/HEALER | MAGE/RANGER), kart slotları + evrim kilitleri — D1 backend'ine bağlı.
- **Hafta çıktısı:** Kitap açılıyor, kart/evrim gerçek veriyle; Kam final görünümde.

**H6 · 17-23 Ağu**
- 🎨 T: **Karakter tasarımları 2:** Okçu + Büyücü modelleri/animasyonları + **UI görsel stil kılavuzu** (parşömen/kitap dokusu, çerçeveler, buton dili).
- ⚙️ D1: **Kam Skill Tree backend:** düğüm grafı, kilit/açma (öz), pasif + aktif büyü açılımı.
- 🖥️ D2: **ÇANTA ekranı** (sekmeler: pot/eşya/büyü kartları) + **SKILL TREE ekranı** (whitebox) + sağ-alt KİTAP·ÇANTA·HARİTA kalıcı sekme navigasyonu.
- **Hafta çıktısı:** Kam büyüleri skill tree'den açılıyor; çanta çalışıyor; 3 sekme her ekrandan erişilir.

**H7 · 24-30 Ağu**
- 🎨 T: **Karakter tasarımları 3:** Rahip + Serseri + **Aldacı (temel düşman) + 1 varyant** modelleri/animasyonları + ikon seti 1 (yetenek/öz/pot ikonları).
- ⚙️ D1: **Pot/eşya sistemi** (kullanım + savaşta etki) + **HAN/ŞİFACI/MARKET** hizmet düğümleri (harita üstü etkileşim + ekonomi).
- 🖥️ D2: **HARİTA ekranı** (parşömen görünüm, PINS paneli, pusula) + minimap'in gerçek UI'ya taşınması + 5 sınıfın deployment/kitap entegrasyonu.
- **Hafta çıktısı:** 5 sınıf + temel düşmanlar oyunda; potlar içiliyor; harita ekranı gezilebilir.

**H8 · 31 Ağu - 6 Eyl — 🎯 M2**
- 🎨 T: **Boss modeli** (Bölüm 1 finali — hikaye kilidindeki kurguya göre) + animasyonları + **KİTAP UI final görselleri** (parti 1).
- ⚙️ D1: **Roguelite döngüsü:** run sonu (ölüm/süre/çöküş) → Meta-Öz aktarımı → ana menüde kalıcı kilit açma + **Save/Load v1** (run arası).
- 🖥️ D2: **Ana Menü** (öz deposu paneli, kilitli sınıflar) + run-sonu ekranı + ayarlar (ses/çözünürlük/dil iskeleti).
- **M2 KABUL TESTİ:** Öl → meta-öz kazan → menüden kilit aç → yeni run. Tam roguelite döngüsü + tüm ekranlar (whitebox) çalışıyor.

### FAZ C — İçerik Üretimi (H9-H12) → M3 İçerik Tamam

**H9 · 7-13 Eyl**
- 🎨 T: **UI final görselleri parti 2** (çanta/skill tree/harita) + 5 sınıf + Kam **portreleri** (kitap kartları için).
- ⚙️ D1: **5 sınıfın L1/2/3 pasifleri** (belgeden: Barbar alan hasarı/can çalma, Okçu çift hedef/zehir, Serseri zıplama/görünmezlik…) + düşman çeşitleri (menzilli/tank Aldacı).
- 🖥️ D2: **Harita 2-3-4 boyama** + görev/öz yerleşimi + **UI re-skin başlangıcı** (T'nin görselleriyle kitap ekranı).
- **Hafta çıktısı:** Evrim pasifleri gerçekten hissediliyor; dünyanın 4 haritası hazır.

**H10 · 14-20 Eyl**
- 🎨 T: **VFX görsel kaynakları** (büyü efektleri: ateş topu/şifa/kalkan; vuruş/ölüm efektleri; harita oluşma görseli) + kalan UI art.
- ⚙️ D1: **Boss savaşı mekanikleri** + Bölüm 1 final görevi scripting (özel kurallar/fazlar).
- 🖥️ D2: **Harita 5-6-7 boyama** + **hikaye sunumu entegrasyonu** (intro paneli, görev diyalog/anlatı kutuları — T'nin metinleriyle).
- **Hafta çıktısı:** Boss dövüşülebilir; hikaye oyunun içinde anlatılıyor.

**H11 · 21-27 Eyl**
- 🎨 T: **Ses listesi + seçim/temin** (müzik: overworld/savaş/boss/menü; SFX: UI, vuruş, büyü, toplama; ambiyans) — stok kaynak veya ısmarlama.
- ⚙️ D1: **Ekonomi/denge geçişi 1:** öz kazanım oranları, tarif/evrim maliyetleri, AP-zaman baskısı, çöküş hızı, draft ödül ağırlıkları (telemetri/istatistik logu ile).
- 🖥️ D2: **Harita 8-9 boyama** + tüm ekran akışlarının pürüzsüzleştirilmesi (geçiş animasyonları, KİTAP↔ÇANTA↔HARİTA).
- **Hafta çıktısı:** 9 haritanın TAMAMI oynanır; ilk gerçek denge ayarı yapıldı.

**H12 · 28 Eyl - 4 Eki — 🎯 M3**
- 🎨 T: Eksik art kapama turu (playtest'te göze batanlar) + VFX/animasyon cila listesi çıkarma.
- ⚙️ D1: **Save/Load v2** (run ORTASI kayıt) + bug temizliği.
- 🖥️ D2: **Ses entegrasyonu v1** (müzik + temel SFX) + **harita oluşma efekti** (karo karo geliş/deprem) + VFX entegrasyonu başlangıç.
- **M3 KABUL TESTİ:** Baştan sona Bölüm 1: 9 harita + boss + hikaye + tüm UI final görsellerle. **İçerik dondurulur** — bundan sonra sadece düzeltme/denge/cila.

### FAZ D — Cila + Denge + Yayın (H13-H18) → M4 Beta → 🏁 FINAL

**H13 · 5-11 Eki** — **Tam playtest haftası:** 3 kişi ayrı ayrı full run × en az 2'şer kez → tek bug/denge/UX listesi (öncelikli sıralı).
- 🎨 T: görsel tutarlılık turu (renk/ölçek/ışık uyumu). ⚙️ D1: denge geçişi 2 + kritik buglar. 🖥️ D2: UX düzeltmeleri (tooltip'ler, onay pencereleri, geri bildirim eksikleri).

**H14 · 12-18 Eki**
- 🎨 T: ışık + post-processing final ayarı (D2 ile eşli çalışma günü) + tanıtım görselleri taslağı.
- ⚙️ D1: **performans**: profiling, object pooling eksikleri, GC temizliği, yükleme süreleri.
- 🖥️ D2: **VFX final entegrasyonu** (tüm büyüler/vuruşlar/ölümler efektli) + ekran titremesi/sarsıntı gibi juice dokunuşları.

**H15 · 19-25 Eki**
- 🎨 T: son art düzeltmeleri + Steam/tanıtım kapak görselleri.
- ⚙️ D1: **zorluk eğrisi + roguelite meta-denge** (meta-öz kazanım hızı, kilit açma maliyetleri) + son sistem bugları.
- 🖥️ D2: **ses final** (miks, eksik SFX) + ana menü cila + **build pipeline** (versiyonlama, Windows build, hedefe göre Steam demo hazırlığı).

**H16 · 26 Eki - 1 Kas — 🎯 M4 BETA**
- Beta build → **dış test: 5-10 oyuncu** (form + gözlem). Takım: herkes geri bildirim toplar; D1/D2 kritik bug nöbeti; T gözlemden görsel/anlaşılırlık notları.

**H17 · 2-8 Kas** — Geri bildirim işleme: önceliklendir → düzelt → mini regresyon testi. (Tampon: FAZ C/D'den sarkan işler buraya.)

**H18 · 9-15 Kas — 🏁 FINAL**
- RC build → 3 kişi son full-run onayı → **Bölüm 1 v1.0** etiketi + tanıtım materyali (ekran görüntüleri, 60-90 sn video). Kutlama 🎉 + Bölüm 2 retrosu/planlaması.

---

## 4) ÇALIŞMA RİTÜELLERİ

1. **Cuma build günü:** her hafta çalışan bir build + 30 dk ortak playtest → 5 maddelik pürüz listesi → ertesi haftaya.
2. **Whitebox önce, görsel sonra:** D'ler placeholder ile fonksiyonu bitirir; T'nin asset'i gelince re-skin. Hiçbir iş "art bekliyor" diye durmaz.
3. **TAM KURULUM disiplini:** sahne/wiring değişiklikleri her zaman SceneSetupTool üzerinden — elle sahne kurcalanmaz.
4. **Commit disiplini:** her Cuma build'i öncesi working tree temiz; özellik dalı → main (format: `feat:/fix:/art:`).
5. **Teslim sözleşmesi (T→D2):** karo = 1.90m footprint spec; karakter = `model@Anim.fbx` seti; UI = 9-slice/PNG + ölçü notu. Spec dışı asset entegre edilmez, geri döner (pipeline kalkanları zaten reddeder).

## 5) RİSKLER VE B PLANLARI

| Risk | Etki | B planı |
|---|---|---|
| **Hikaye değişikliği H4'ten sonra gelirse** | Görev metni/boss kurgusu/harita teması kayar | H4 HİKAYE KİLİDİ: yapı donar; sonrası yalnız metin düzeltmesi. Büyük değişiklik isteği → kapsamdan eşdeğer iş çıkar (takas kuralı). |
| Tasarımcı asset temposu yetişmez (5 sınıf + boss + UI art ağır) | M2/M3 kayar | Öncelik sırası: karolar > Kam > Barbar/Okçu > düşman > diğerleri. Yetişmeyen sınıf "Bölüm 1'de kilitli" olarak sevk edilir (roguelite kilidi zaten var!). Mixamo animasyon kütüphanesi serbest. |
| Denge oturmaz | Beta kötü geçer | H11 + H13 + H15: 3 ayrı denge geçişi planda; telemetri logu H11'de girer. |
| Kapsam şişmesi ("şunu da ekleyelim") | Final kayar | M3 = içerik dondurma. Yeni fikirler `Docs/BOLUM2_FIKIRLER.md`'ye yazılır, tartışma H18 retrosunda. |
| Tek geliştiricinin bloklanması | Hafta boşa düşer | D1/D2 görevleri haftalık bağımsız seçildi; blok olan Cuma listesinden iş çeker. |

## 6) KAPSAM DIŞI (Bölüm 2+ / sonrası)
Çok bölümlü kampanya, yeni biyomlar, ek sınıflar (kilit sistemi hazır olsa da içerikleri), çok dillilik (iskelet H8'de girer, çeviri sonrası), Steam sayfası/wishlist kampanyası (H15'te yalnız hazırlık), mod desteği, co-op.
