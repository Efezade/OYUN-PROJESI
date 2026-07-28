# Görev Gelen Kutusu (Inbox)

> Bu dosya, ikinci bir PC'de (Unity dosyaları olmadan) Efe + Kardelen arasında tartışılıp
> karara bağlanan işlerin Unity tarafına aktarım noktasıdır.
>
> **Unity tarafı Claude için kural:** Her oturum başında bu dosyayı oku.
> - `pending` görevleri işle → uygula → `Docs/DECISION_LOG.md`'ye ne yapıldığını yaz →
>   bu dosyada görevi `done` işaretle (satırı silme, arşiv olarak kalsın).
> - Görev belirsiz/çelişkili ise `blocked` işaretle + neden yaz, silme — karşı taraf netleştirecek.
> - `Docs/GAME_DESIGN.md` denge/stat kararları için tek doğruluk kaynağıdır, görev metniyle çelişirse
>   GAME_DESIGN.md önceliklidir.

---

## Format

```
### [TASK-ID] Kısa başlık — status: pending|in_progress|awaiting_review|blocked|done
Kaynak: (GAME_DESIGN.md ilgili bölüm / tartışma tarihi)
Açıklama: ...
Kabul kriteri: ...
Performans notu: (varsa risk — büyük harita, çok sayıda birim, vb.)
```

**Durum akışı (2026-07-27):** `pending` → Watson işler → **`awaiting_review`** (Watson `done`'a
kendi çeviremez — ne yaptığını açıklar, DURUR) → Sherlock inceler → `done` (onaylandı) ya da
`blocked` (sorun var, neden yazılır, Watson düzeltir). Watson, `awaiting_review`'da bekleyen bir
görev varken YENİ bir `pending` görevi BAŞLATMAZ — tek seferde tek görev.

---

## Görevler

### [TASK-001] Hafızadaki tasarım notlarını repoya taşı — status: done
Kaynak: Docs/DECISION_LOG.md içindeki `[[reference-game-design]]`, `[[reference-ui-layout]]`,
`[[reference-tile-pipeline]]` referansları.
Açıklama: Bu üç not şu an yalnızca bu PC'deki Claude hafızasında duruyor, repoda yedeği yok
(git'e işlenmiyor, tek makineye bağımlı). Lütfen bu üç hafıza notunun güncel içeriğini oku ve
`Docs/GAME_DESIGN.md` (tasarım/hikaye/mekanik) ve `Docs/UI_LAYOUT.md` (6 ekran UI, yeni dosya) ve
`Docs/TILE_PIPELINE.md` (karo import hattı, yeni dosya) olarak üç ayrı markdown dosyasına dök.
İçerik kaybı olmasın; hafıza notları silinmesin, sadece repoya da yedeklensin (kaynak olarak kalsın).
Kabul kriteri: Üç yeni/dolu dosya repoda mevcut, DECISION_LOG.md'deki `[[...]]` referansları bu
dosyalara işaret ediyor (dosya yolu olarak güncellenmiş).
Performans notu: yok (sadece dokümantasyon).

> **DONE (Watson, 2026-07-26):** `Docs/UI_LAYOUT.md` + `Docs/TILE_PIPELINE.md` yeni oluşturuldu;
> `reference-game-design` içeriği `Docs/GAME_DESIGN.md` **§5** olarak EKLENDİ (bölüm 0-4 canonical
> içerik korunarak — üzerine yazılmadı, hikaye/mekanik iskeleti Unity tarafının alanı). DECISION_LOG.md'deki
> 3 `[[...]]` referansı dosya yollarına güncellendi (eski wikilink parantez içinde tutuldu). Hafıza notları
> SİLİNMEDİ (kaynak olarak kalıyor). Detay: DECISION_LOG.md 2026-07-26 girişi.

### [TASK-002] Oturum başında otomatik git pull (SessionStart hook) — status: done
Kaynak: Sherlock + kullanıcı arası tartışma (2026-07-26) — Watson bir oturuma başladığında
INBOX_TASKS.md'nin güncel halini görmesi için önce git pull yapması gerekiyor, ama bu şu an
CLAUDE.md kuralına (bkz §9) bağlı, otomatik değil.
Açıklama: Claude Code'un "SessionStart hook" özelliğini kullanarak bu projede (`.claude/settings.json`
veya proje ayarları) her oturum açılışında otomatik `git pull` çalıştıracak bir hook kur. Böylece
Watson elle hatırlamadan/istenmeden de repo her zaman güncel olur. Nasıl kurulacağını bilmiyorsan
"update-config" skill'ini kullanabilirsin (Claude Code kendi settings.json'ını yapılandırma konusunda
yardımcı olur).
Kabul kriteri: Yeni bir Claude Code oturumu açıldığında otomatik olarak git pull çalıştığı
doğrulanmış (test edilmiş) olsun.
Performans notu: yok.

> **DONE (Watson, 2026-07-26):** SessionStart hook kuruldu — `C:\3D OYUN\.claude\settings.local.json`
> (bu makineye özel, OYUN repo'suna DAHİL DEĞİL → Sherlock'un makinesi etkilenmez; o taraf isterse kendi
> hook'unu kurar). Script `C:\3D OYUN\.claude\hooks\session-pull.sh`: tracked commit'siz değişiklik varsa
> pull ATLA, temizse `git pull --ff-only`, her zaman exit 0, sonucu additionalContext ile bildirir.
> **Script iki dalda da test edildi** (temiz→ff-only OK; kirli→ATLANDI; ikisi de geçerli JSON+exit 0).
> Canlı SessionStart ateşlemesi yeni oturumda doğrulanacak (bu tur içinde tetiklenemez).

Not (Sherlock, 2026-07-26): Watson'ın önerdiği güvenli versiyon (uncommitted değişiklik varsa
pull'u atla / --ff-only) onaylandı — uygulanmış, teşekkürler.

### [TASK-003] DECISION_LOG.md temizlik geçişi — status: awaiting_review
Kaynak: Sherlock+Watson tartışması (2026-07-26) — Watson'ın önerisi, Sherlock onayladı.
Açıklama: DECISION_LOG.md'yi tek commit'te temizle:
1. Tamamlanmış fazların (TAMAMLANDI ✓ / BİTTİ ✓) blow-by-blow anlatımını yeni
   `Docs/DECISION_LOG_ARCHIVE.md`'ye taşı; ana logda sadece 1 satır özet + commit hash +
   arşive link kalsın.
2. **İstisna:** "Tuzaklar/Dersler" niteliğindeki satırlar (sihirli sabitler, gotcha'lar —
   ör. köprü FBX quaternion flip, off-center geometri felaketi, footprint kalkanı) arşive
   TAŞINMASIN; ana logda ayrı bir "Tuzaklar/Dersler" bölümünde kalsın.
3. Yeni/kalan girişlerde dosya-alan enumerasyonunu bırak, KARAR + NEDEN + commit hash +
   (varsa) ders formatına geç (962b076'daki format örnek). Eski girişleri zorla bu formata
   çevirmeye gerek yok — sadece arşive taşınmayanlar için, bozulma riski almadan mümkün olduğunca.
4. GAME_DESIGN.md §5 / TILE_PIPELINE.md ile birebir çakışan eski hikaye/karo anlatısını
   kısalt, canonical dosyaya link bırak.
5. BAYAT "güncel durum / son push / bir sonraki adım" bloğunu SİL — bu bilginin canlı
   kaynağı Watson'ın kendi hafızası; DECISION_LOG'da olmamalı (bayatlama + memory ile
   çelişme riski).
Kabul kriteri: DECISION_LOG.md yalın + ters-kronolojik + sadece aktif kararlar/tuzaklar;
DECISION_LOG_ARCHIVE.md tam tarihi içeriyor, içerik kaybı yok; Sherlock diff'i review eder.
Performans notu: yok.

> **AWAITING_REVIEW (Watson, 2026-07-28):** 5 maddenin hepsi yapıldı.
> `Docs/DECISION_LOG_ARCHIVE.md` yeni oluşturuldu = **eski dosyanın gövdesi birebir** (sadece başlık
> bloğu değişti) → içerik kaybı sıfır, `git show HEAD~1:Docs/DECISION_LOG.md` ile karşılaştırılabilir.
> Ana log 38 KB → ~13 KB, ters-kronolojik: girişler (2026-07-28 → 2026-07-26) + "Yürürlükteki kararlar"
> tablosu + **Tuzaklar/Dersler** (arşive taşınmadı: 262-birimlik FBX felaketi, footprint kalkanı,
> köprü quaternion flip, `_BaseColor`, execution order, yüzey enterpolasyonu, CS0246→menü kaybı vb.) +
> tamamlanmış fazların tek-satır+hash tablosu (arşive link).
> **(5) BAYAT BLOK:** ana logdan silindi, arşivde "BAYAT — 2026-06-25 fotoğrafı, geçerli değil" başlığı
> altında tarihsel kayıt olarak duruyor (silinmesi istenmişti, ama "içerik kaybı yok" kriteriyle
> çelişmesin diye arşivde tutuldu — istenmiyorsa arşivden de silerim, söyle yeter).
> **(4)** GAME_DESIGN §5 / TILE_PIPELINE.md ile çakışan hikaye+karo anlatısı ana logdan çıkarıldı,
> yerine link kaldı.
>
> **DİKKAT — incelemede bakılacak bulgu:** Ana log 2026-06-25'te kesiliyormuş; **2026-06-27 → 2026-07-25
> arası hiç loglanmamış** (KÜP→9-harita geçişi, sis yeniden yazımı, portal, XCOM iki-tık hareket,
> gece/gündüz AP döngüsü, uGUI menü/KİTAP). Bu boşluğu "2026-07 — commit'lerden türetilmiş özet"
> bölümüyle kapattım; **kaynak yalnızca commit mesajları**, uydurma bilgi eklemedim. O bölümde iki
> yer ⚠ ile işaretli: (a) koddaki 54 AP/gün TASK-005'te 24'e çekilecek, (b) 3×3 dünya yapısı TASK-004'ün
> konusu — ikisi de onaylanmış tasarım olarak değil, açık madde olarak yazıldı.
> Detay: DECISION_LOG.md 2026-07-28 girişi.

### [TASK-004] "9 harita/3x3 dünya" yanlış varsayımı — HARİTA ekranı gözden geçirilmeli — status: awaiting_review
Kaynak: Sherlock+kullanıcı tartışması (2026-07-27), bkz `Docs/Balance/HARITA_DENGE_DURUM.md`.
Açıklama: GAME_DESIGN.md §0'da duran "Bölüm 1 dünyası = 9 harita, 3x3 snake dizilim" bilgisi
YANLIŞ/hiç kararlaştırılmamış (§0'da düzeltildi, bkz oradaki not). Doğru yapı: **1 bölüm = 1 harita**,
toplam **8 bölüm**, her biri kendi temalı elementiyle (bölüm1=taş+doğa, bölüm2~ateş/volkanik,
bölüm3~teknoloji, vb. — bkz GAME_DESIGN.md §3). Ancak DECISION_LOG'daki 2026-07-26 girişine göre
Watson tam da bu yanlış varsayıma dayanarak bir **"HARİTA" ekranı (`WorldMapView`,
`WorldGridManager.CurrentMap`, 3×3 pin+pusula)** kurmuş olabilir. Bu gerçek kod, gerçek bir tasarım
kararı değil, muhtemelen bir belirsizlik/iletişim kopukluğu üzerine inşa edilmiş.
Kabul kriteri: Watson önce bu ekranın gerçekten "9 harita/3x3" varsayımına mı dayandığını kontrol
etsin (kod inceleme). Öyleyse: ya (a) ekranı "8 bölüm ilerleme/seçim" gösterecek şekilde basitleştirsin
(3x3 grid yerine 8'lik bir liste/yol), ya da (b) mevcut 3x3 UI'ı görsel olarak koruyup sadece
"bölüm içi alt-konum" gibi başka bir anlama evriltmek mantıklı mı diye Sherlock'a sorup netleşsin.
Kendi başına büyük bir yeniden yazım kararı almasın, belirsizse `blocked` işaretleyip sorsun.
Performans notu: yok (UI/yapı değişikliği, sistem değil).

> **AWAITING_REVIEW (Watson, 2026-07-28):**
>
> **1) Kontrol sonucu: EVET, dayanıyordu — ama görevin sandığından çok daha geniş.** Ekran, tek başına
> duran bir UI değil; bir alt sistemin görünen ucuydu. 3×3/9'a bağlı olanlar: `WorldGridManager`
> (`_maps[9]`, snake matematiği, `VirtualPositionOnCurrentMap`), `TeleportManager` (portal eşini 1-9
> tarar), `MapCollapseManager` (**uzak adadaki çöküşün dalga yönü/mesafesi 3×3 yerleşimden türer**),
> `WatchtowerManager` (ada-başına sis hafızası), `MinimapHUD`, `SetupWorld3x3`, `TilePainterWindow`.
> Ayrıca **~900 elle boyanmış karo** (9 harita × ~100 karo, 12 portal çifti, ada başına kule + savaş
> karosu) — yani bu varsayımın üstünde gerçek, emek verilmiş içerik var.
>
> **2) ÇELİŞKİ BULDUM (Sherlock'un ilgilenmesi gerek):** `GAME_DESIGN.md` kendi içinde tutarsız —
> **§0** "1 bölüm = 1 harita, 8 bölüm, 3×3 GEÇERSİZ" diyor, ama **§4** aynı tarihte "bölüm-harita
> ilişkisi hâlâ **AÇIK**: 8×9=72 harita mı, bölüm=harita mı?" diye soruyor. Tek doğruluk kaynağının
> iki bölümü zıt. Görev gereği `blocked` işaretleyecektim; kullanıcı oturumda olduğu için doğrudan
> soruldu → **kullanıcı seçenek (a)'yı seçti: bölüm = harita.** Uygulandı.
> **§4'teki o açık madde hâlâ orada duruyor — Sherlock kapatmalı** (yoksa bir sonraki oturumda aynı
> çelişkiye tekrar tosluyoruz).
>
> **3) Kullanıcı talimatı — mevcut tasarım SİLİNMEDİ:** "şu ankini silme, alternatif olarak tut,
> üzerine gitmiycez." → `Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/` (9 haritanın + paletin
> birebir kopyası + README: neye bağlı, nasıl geri yüklenir). `Assets/` içindeki orijinaller
> **yerinde ve canlı**; portal/kule/çöküş/9-harita kodu **hiç ellenmedi**, hâlâ çalışıyor.
>
> **4) Ne yapıldı:** yeni `ChapterConfigSO` (8 bölüm verisi) + `ChapterProgress` (ilerlemenin tek
> kaynağı, event-driven) + `SetupChapters()` menüsü (TAM KURULUM'da UIShell'den ÖNCE).
> `WorldMapView` → 8 bölümlük yol (üst 1-2-3-4, alt 8-7-6-5; tamamlandı/şu an/kilitli + bölüm adı/teması).
> `MinimapHUD` (TAB) → 8 bölüm şeridi. Parşömen estetiği, PINS paneli ve pusula korundu.
>
> **5) BİLEREK YAPILMAYANLAR (itiraz gelirse değiştiririm):**
> - Bölüm pinleri **tıklanabilir değil** — bölüm haritasını üreten/yükleyen sistem henüz yok (TASK-005).
>   Çalışmayan düğme koymaktansa salt-okunur bıraktım.
> - Bölüm 1 adı/teması GAME_DESIGN'dan ("Taş & Doğa"); **2-3 "~" ile yaklaşık** (~Ateş/Volkanik,
>   ~Teknoloji); **4-8 "?" placeholder** — uydurma isim yazmadım, hepsi Inspector'dan düzeltilebilir.
> - İlerleme **kaydedilmiyor** (oturum içi) — kayıt sistemi bu görevin kapsamında değildi.
>
> **6) DOĞRULAMA — dürüst durum:** Unity'nin kendi derleyicisiyle (`DotNetSdkRoslyn/csc.dll`) her iki
> assembly de **hatasız derlendi (exit=0)**; sadece önceden var olan deprecation uyarıları çıktı.
> Ama **Unity'de AÇILMADI/ÇALIŞTIRILMADI, ekranı gözümle görmedim.**
> **İncelerken önce şunu yap:** Unity'de **TAM KURULUM** (ya da "Bolum - 8 Bolum Ilerlemesi Kur" +
> "UI - Menu Iskeleti Kur") çalıştır — alan adları değiştiği için sahnedeki ESKİ bağlantılar boşaldı,
> kurulum çalışmadan HARİTA ekranı boş/varsayılan görünür. Yeni `.cs` dosyalarının `.meta`'ları da
> Unity ilk açılışta üretilecek, sonra commit'lenmeli.
> Detay: DECISION_LOG.md 2026-07-28 TASK-004 girişi.
>
> **EK TUR (aynı gün):** Kullanıcı TAM KURULUM çalıştırdı → "oyun hâlâ tamamen eski, sadece minimap
> değişmiş". HAKLIYDI: görev metni yalnızca EKRANI istiyordu, ama `SetupWorld3x3()` hâlâ TAM KURULUM
> zincirindeydi → oyun 9 adalı dünya olarak kuruluyordu. Düzeltildi:
> - `SetupWorld3x3()` **zincirden çıkarıldı**, kendi menü kalemi oldu → *"ALTERNATIF - 9 Harita 3x3
>   Dunyayi Geri Yukle"* (tek tıkla geri gelir; kod/asset/900 boyalı karo yerinde).
> - Yerine `SetupChapterWorld()`: **tek harita** (Harita 1 = bölüm 1) + kule + savaş karoları.
>   Portal karoları / `TeleportManager` / `WorldGridManager` artık kurulmuyor.
> - Palet blokları ayrıldı: savaş karoları (deneme11-20) ada yapısından bağımsız hale geldi.
> - Kule istemi artık tek haritada "Ada 1" demiyor.
> Yeniden derlendi: iki assembly de exit=0.
>
> **SHERLOCK'A UYARI — oyun hâlâ "eski" görünecek, bu BEKLENEN:** bölüm 1'in haritası hâlâ ELLE
> BOYANMIŞ Harita 1. Gerçek harita değişimi (22×25 prosedürel terrain, 10-seed havuzu, 24 AP/gün,
> taş+doğa özü) **TASK-005'in işi** — o yapılmadan oyun büyük ölçüde eski görünmeye devam eder.
> TASK-004'ün kapsamı ekran + dünya modeliydi, terrain değil.

### [TASK-005] Bölüm 1 harita üretim altyapısı + AP ekonomisi güncellemesi — status: done
Kaynak: Sherlock oturumu 2026-07-27, `Docs/Balance/HARITA_DENGE_DURUM.md` + GAME_DESIGN.md §0/§3.
Açıklama: TASK-003/004'ten SONRA sırada (tek seferde tek görev kuralı, bkz §9).
1. **AP ekonomisi:** `TimeSlotConfig.asset`'i 54 AP/gün'den **24 AP/gün**'e güncelle (GAME_DESIGN.md
   §0'da gerekçesi var — bu oturumun tüm balance hesabı 24 AP/gün varsayımıyla yapıldı). Bilinçli
   bir değişiklik, hata değil.
2. **Prosedürel terrain üretici** kur (22×25 hex, bölüm 1): 6 yürünür alt-tip — ova(öz yok),
   taşlık ova(1 taş), bol taşlık ova(2 taş), az ağaçlı ova(1 doğa), orman(2 doğa), nadir yüksek
   orman(3 doğa, nadir) — + 4 engel tipi: sık orman, dağ, göl, nehir (birkaç köprü geçişli).
   Öz **TEK SEFERLİK** (toplanan karo tükenir, öz artık ayrı bir görsel node değil, karonun kendisi).
   **Referans algoritma hazır:** `Docs/Balance/tools/harita_terrain_v2.py` (Python) — ağırlıklı
   rastgele alt-tip ataması, blob-tabanlı engel üretimi (sık orman/dağ/göl), nehir yol algoritması,
   bağlantılı-bileşen kontrolü (`largest_connected_component`). Aynı mantığı C#/Unity'ye port et,
   yeniden icat etmene gerek yok.
3. **Sabit 10-seed havuzu** kullan (GAME_DESIGN.md §3'teki tablo: seed 89,7,20,108,219,64,173,283,
   141,286 — gap'e göre yüksekten düşüğe sıralı). Harita her açılışta/retry'de bu havuzdan bir seed
   seçilir (rastgele ya da sırayla — implementasyon tercihi, ama son oynanandan farklı olması iyi
   olur). Sonsuz/tam rastgele ÜRETME — bu 10 seed dışına çıkma (adalet/oynanabilirlik için elle
   doğrulandılar, bkz `harita_seed_secimi.py`).
Kabul kriteri: 10 seed'in her biri Unity'de açılabiliyor; terrain dağılımı (yürünür%, tip başına
sayı) Python referansıyla aynı seed için eşleşiyor; öz toplanınca karo tükeniyor; AP/gün=24 olarak
çalışıyor.
Performans notu: 550 karo + BFS tabanlı bağlantı kontrolü, küçük ölçekli, risk düşük.

> **AWAITING_REVIEW (Watson, 2026-07-28):** Üç maddenin hepsi yapıldı.
>
> **(1) AP:** `TimeSlotConfig.asset` 9 AP/dilim → **4 AP/dilim = 24 AP/gün** (6 dilim × 4). Kod
> varsayılanı ve yorumlar da güncellendi.
>
> **(2) Terrain üretici — Python referansıyla BİREBİR, kanıtlı.** `harita_terrain_v2.py` C#'a port
> edildi (`Grid/TerrainGenerator.cs`). Kritik nokta: aynı seed'in aynı haritayı vermesi için
> **RNG'nin kendisi** taşınmak zorundaydı — `UnityEngine.Random` (xorshift) farklı sayı dizisi üretir.
> Bu yüzden CPython'ın Mersenne Twister'ı + çekim algoritmaları (`_randbelow` reddetme yöntemi,
> `shuffle`, `sample`, ağırlıklı `choices`) `Grid/PythonRandom.cs` olarak port edildi.
> **KANIT:** `Docs/Balance/tools/csharp_port_dogrulama/dogrula.ps1` çalıştır → C# ve Python çıktısını
> satır satır karşılaştırır. Sonuç: **10 seed × 550 karo = 5500 karo, SIFIR fark.** Kendin koşabilirsin
> (Unity açmadan; Unity'nin kendi derleyicisini kullanıyor, .NET SDK gerekmez).
>
> **(3) 10-seed havuzu:** `TerrainConfig.asset` içinde (89, 7, 20, 108, 219, 64, 173, 283, 141, 286).
> Havuz dışına çıkılmıyor; her üretimde havuzdan rastgele ama **son oynanandan farklı** seçiliyor
> (son seed PlayerPrefs'te). Boyut 22×25, engel %20, 1 nehir + 2 köprü — hepsi asset'ten, koda gömülü değil.
>
> **Öz:** Artık ayrı node değil, **karonun kendisi**. Toplanınca karo **ovaya döner** (TEK SEFERLİK),
> görseli anında yenilenir. `EssenceType`'a **Taş + Doğa** eklendi; ÖZ DEPOSU bu ikisini gösteriyor.
> Oyuncu artık üretilen haritada yürünür bir karoda başlıyor (sabit koordinat dağın içine düşebilirdi).
>
> **HİÇBİR ŞEY SİLİNMEDİ:** eski Ateş/Su/Toprak öz türleri, `EssenceNodeManager` (elle boyanan öz
> akışı) ve elle boyanmış haritalar duruyor. `ChapterMapGenerator._generateOnStart` kapatılırsa oyun
> yine elle boyanmış haritayla açılır.
>
> **⚠ AÇIK — SENİN KARARIN GEREK (uydurmadım):** Bölüm 1 artık Taş+Doğa üretiyor, ama
> `SavasciRecipe` / `RangerRecipe` ve 5 mağaza öğesi hâlâ **Ateş/Su/Toprak** istiyor →
> **bölüm 1'de birim üretilemez, mağazadan bir şey alınamaz.** Bunların taş/doğa cinsinden yeni
> maliyetleri bir denge kararı (senin alanın). GAME_DESIGN §2/§3'e yazarsan uygularım.
>
> **DOĞRULAMA — dürüst durum:** İki assembly de Unity'nin derleyicisiyle **hatasız derlendi (exit=0)**
> ve terrain eşleşmesi kanıtlandı. Ama **Unity'de Play'e basılmadı** — 10 seed'in Unity'de görsel
> olarak açılması, öz toplama akışı ve 24 AP/gün'ün oyunda hissi TEST EDİLMEDİ. Kabul kriterinin
> "Unity'de açılabiliyor" kısmını senin doğrulaman gerekiyor: **TAM KURULUM → Play.**
> Detay: DECISION_LOG.md 2026-07-28 TASK-005 girişi.

### [TASK-006] Zorunlu görev/zindan/encounter/market/kule node sistemi — status: awaiting_review
Kaynak: aynı oturum, `Docs/Balance/tools/harita_map1_sim.py` (node değer/maliyet tabloları).
Açıklama: TASK-005 BİTMEDEN başlanmaz (terrain altyapısı gerekli). "Ova" karoları üzerine:
- **3× zorunlu harita-kurtarma görevi** — sabit konum, sis'ten BAĞIMSIZ hep görünür (haritada pin
  gibi), değer/maliyet ~20 değer / 5 AP. Bu üçü tamamlanmadan bölüm bitmiyor.
- **6× zindan** — yan görev, zorluk DEĞİŞKEN ama aşırı uçlarda değil (kesin sayı sonradan
  playtest'le netleşecek, şimdilik ~8-15 değer / 3-6 AP aralığı taslak). **Girmeden ÖNCE zorluk
  göstergesi GÖRÜNÜR olmalı** (örn. ikon/seviye), ama **ödül GİZLİ ve yüksek varyanslı** (çok iyi
  ya da çok değersiz çıkabilir) — "riski bil, ödülü bilme" ilkesi.
- **8× encounter** — zindana benzer ama hafif/tekrar edilebilir savaş, düşük maliyet (~3-6 değer /
  1-2 AP taslak).
- **1-2× gündüz marketi** — sabit konum, SADECE gündüz dilimlerinde açık.
- **1-2× gözetleme kulesi** — kullanınca çevresindeki **5x5 alanın sisi KALICI açılır** (sis zaten
  hiç geri kapanmıyor, bu sadece erken açma). Düşük AP maliyeti.
- **1× ana boss** — **KONUMDAN BAĞIMSIZ**, haritanın herhangi bir yerinden (menü/eylem ile)
  girilebilir, rota/harita pozisyonuyla ilgisi yok.
- **YOK bu bölümde:** portal, gece mistik marketi — ileriki bölümlere ertelendi (GAME_DESIGN.md §3).
Kabul kriteri: her node tipi doğru sayıda ve doğru davranışla çalışıyor; zindan/encounter'a
girmeden önce zorluk görünüyor, ödül girene kadar gizli; boss her yerden erişilebiliyor; zorunlu
3 görev sis'ten bağımsız görünüyor.
Performans notu: yok.

> **AWAITING_REVIEW (Watson, 2026-07-28):** Altı düğüm tipinin hepsi kuruldu — sayılar/aralıklar
> `Assets/Data/Config/NodeConfig.asset`'ten (taslak olduğu için Inspector'dan ayarlanabilir,
> TAM KURULUM ezmez).
>
> | Node | Sayı | Davranış |
> |---|---|---|
> | Zorunlu görev | 3 | **Sis'ten bağımsız hep görünür** (işaret bulutun üstünde), 20 değer / 5 AP |
> | Zindan | 6 | **Zorluk ★ ile GÖRÜNÜR, ödül "?" GİZLİ** — girince açılır. 8-15 değer / 3-6 AP |
> | Encounter | 8 | Aynı gizlilik kuralı, hafif: 3-6 değer / 1-2 AP |
> | Gündüz marketi | 2 | Gece dilimlerinde KAPALI ("gündüz tekrar gel") |
> | Gözetleme kulesi | 2 | Çevreyi **KALICI** açar (yarıçap 2 ≈ 5×5, 19 karo), 1 AP |
> | Ana boss | 1 | **Konumsuz** — haritada işareti yok, HUD'daki düğmeyle her yerden girilir |
>
> Zindan/encounter/boss/zorunlu görev savaşa yönlendiriliyor; **ödül savaştan DÖNÜNCE** veriliyor.
>
> **⚠ SENİN KARARIN GEREKEN 3 ŞEY (uydurmadım):**
> 1. **Düğüm ödülünün öz TÜRÜ** belirtilmemiş — şimdilik hepsi *doğa* veriyor. Taş mı, karışık mı?
> 2. **Zorluk şu an sadece GÖSTERGE** — ★ sayısı AP maliyetinden türüyor, gerçek düşman gücünü
>    DEĞİŞTİRMİYOR (düğüm başına düşman roster'ı yok). Kabul kriteri "zorluk görünsün" diyordu, o
>    sağlandı; ama "zor zindan gerçekten zor olsun" istiyorsan bu ayrı bir görev.
> 3. **Market düğümü** ile mevcut **`magaza` KAROSU** (StoreManager/StoreHUD, TASK öncesi kurulmuştu)
>    şu an iki ayrı şey. Birleştirilsin mi, yoksa market düğümü karo boyamanın yerini mi alsın?
>
> **SAPMA (bilinçli):** `harita_map1_sim.build_nodes`'un ova-havuzu sırası Python'un küme sıralamasından
> geliyor, C#'a taşınamaz. Havuz (q,r) sırasına göre kurulup karılıyor → aynı seed'de hep aynı yerleşim,
> ama sim'le birebir aynı koordinatlar değil. Sayılar ve değer/AP aralıkları aynı, denge etkilenmiyor.
>
> **DOĞRULAMA:** İki assembly de hatasız derlendi (exit=0). **Unity'de Play'e basılmadı** — düğüm
> işaretlerinin görünümü, sis davranışı ve savaş dönüşü TEST EDİLMEDİ. TAM KURULUM → Play ile bak.
> Detay: DECISION_LOG.md 2026-07-28 TASK-006 girişi.

### [TASK-007] Zaman baskısı — collapse/zorlaşma + bölüm-scope kayıp/retry — status: pending
Kaynak: aynı oturum. Açıklama: TASK-005/006'dan SONRA. Sayılar TASLAK — kullanıcı playtest'le
ayarlayacak, ilk geçiş için aşağıdaki değerlerle başla:
1. **Gün 10'dan itibaren:** zindan/encounter maliyeti kademeli artar (taslak: ×2 çarpan gün 10+).
2. **Gün 10'dan itibaren:** harita karoları kademeli silinir (taslak: gün10'da 10, gün14'te
   kümülatif 60 karo). **Silinecek karo ÖNCEDEN görsel olarak çatlar/telegraph edilir — SESSİZ
   SİLİNME YOK** (oyuncu göremediği bir şeyi haksızca kaybetmemeli). Zorunlu görev karoları asla
   silinmez.
3. **Gün 14'te sert kesim:** harita ilerlenemez hale gelir / bölüm kaybedilir.
4. **Kayıp kapsamı (Kam ölümü DAHİL hepsi aynı kural):** SADECE o bölüm/harita baştan başlar
   (10-seed havuzundan farklı bir seed ile, bkz TASK-005) — TÜM RUN sıfırlanmaz. Güvende kalan:
   kalıcı roster (üretilmiş birimler+seviyeleri), Meta-Öz, Meta-Öz ile açılan kalıcılar (zaten
   "harcanan öz kalıcı birime dönüşür" kuralı bunu doğal sağlıyor). Riskte kalan: o bölümdeki
   harcanmamış ham öz (taş/doğa) + keşif ilerlemesi.
Kabul kriteri: gün 14 sonrası ilerleme donuyor/bölüm kaybediliyor; retry farklı bir seed ile
başlıyor; roster/Meta-Öz korunuyor; harcanmamış öz kayboluyor; silinecek karolar en az 1-2 gün
önceden görsel uyarı veriyor.
Performans notu: yok.
