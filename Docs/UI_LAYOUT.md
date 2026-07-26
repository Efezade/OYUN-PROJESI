# Game UI Düzeni — 6 Ekran Taslağı

> **Kaynak:** `C:\3D OYUN\Gerekli belgeler\game UI.pdf` (6 sayfa, elle çizilmiş taslak mockup).
> Bu dosya, Unity tarafı Claude ("Watson") hafızasındaki `reference-ui-layout` notunun repo yedeğidir
> (TASK-001, 2026-07-26). Hafıza kaynak olarak kalır; bu dosya git'e işlenen kalıcı yedektir.
> Render için: `python` + PyMuPDF (kurulu) → PDF'i PNG'ye çevir. Tasarım/mekanik için bkz `GAME_DESIGN.md`.

**ÖNEMLİ NOT (kullanıcıdan):** Mockup'taki ana karakter karolara göre fazla BÜYÜK çizilmiş; oyunda ana
karakter o kadar büyük gözükmeyecek (karoya oranla küçük).

## Uygulama durumu (2026-07-26)
- **uGUI** ile kuruluyor (IMGUI değil). ✅ Gezinme iskeleti (sağ-alt KİTAP/ÇANTA/HARİTA sekmeleri +
  sağ-üst ⚙ + tam-ekran panel geçişi, Esc kapat) derlendi+çalıştı.
- ✅ **KİTAP**: ÖZ DEPOSU 3 canlı sayaç + sınıf roster (WARRIOR/RANGER gerçek, HEALER/MAGE kilitli).
  Evrim/kart etkileşimi + kart-detay (s.4) henüz yok (roster seviye state'i gerekli).
- ✅ **ÇANTA / HARİTA / AYARLAR**: parşömen estetiğinde gerçek içerik kuruldu (valiz + POTLAR/KAM KARTLARI;
  parşömen harita + PINS + 3×3 snake düğümleri canlı; ses/parlaklık/kalite ayarları).
- Menü açıkken overworld IMGUI öz paneli gizlenir; zaman çarkı her ekranda kalır.

## Kalıcı öğeler (her ekranda)
- **Sol üst:** zaman çarkı + "GÜN N" (gün/dilim göstergesi; Kut/zaman mekaniği).
- **Sağ üst:** ayarlar (dişli ikonları).
- **Sağ alt:** 3 ana menü sekmesi → **KİTAP · ÇANTA · HARİTA** (her ekrandan erişilir).

## Ekranlar
1. **Overworld (ana oyun):** Hex grid üzerinde ana karakter (Kam). Harita objeleri: kule kalıntısı,
   evler, yel değirmeni, köprü, ağaç/çalı. = mevcut Unity sahnesi.
2. **HARİTA:** Parşömen harita. Sol **PINS** paneli: **HAN, ŞİFACI, MARKET** (overworld hizmet noktaları).
   Konum pinleri (kuleler, X = bilinmeyen/tehlike, kilit = kilitli bölge). Sağ altta pusula.
3. **KİTAP (ana açılım) — asker kartı/evrim yönetimi:** Üst orta **ÖZ DEPOSU**: 3 öz sayacı (15, 20, ?).
   Açık kitap sınıf bölümleri — sol sayfa **WARRIOR / HEALER**, sağ sayfa **MAGE / RANGER**. Her sınıf:
   portre kartı + stat + bir sıra kart slotu (bazıları **kilitli asma kilit = Level 1/2/3 evrim**).
   Sağ kenar **LEVEL/EVO/ÖZ** paneli (+4 göstergesi, yükseltme kontrolü). Sayfa no 1-2 (çevrilebilir kitap).
4. **KİTAP — kart detay:** Tek karakter kartı yakınlaştırılmış. Sol sayfa: büyük portre + statlar +
   level rozeti. Sağ sayfa: **skiller** (yetenek) listesi + **level** bölümü. Sayfa no 6-7 (çok sayfalı).
5. **ÇANTA — eşya + kartlar:** Bavul arayüzü. Sol dikey sekmeler (yıldız, pot, el, kalkan, …). Aktif sekme:
   sol **POTS** (15 HEAL, 00/MANA, ?), sağ **KARTLAR** = Kam'ın büyü kartları (DRAGON, FIREBALL + boş slotlar).
6. **ÇANTA — SKILL TREE:** Çanta'nın **el ikonu** sekmesinden. Dallanan yetenek ağacı; kilitli (asma kilit)
   + açık düğümler (kılıç/kalkan ikonları). Alt etiket "SKILL TREE". = Kam'ın yetenek ağacı (öz ile açılır).
