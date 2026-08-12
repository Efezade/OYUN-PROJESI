# Karar Günlüğü (DECISION_LOG)

> **Ne için:** Geriye dönük **NEDEN** kaydı — hangi karar verildi, neden, hangi commit'te, varsa ne
> öğrendik. Ters-kronolojik (en yeni en üstte).
> **Sahiplik:** Watson (Unity/kod tarafı) yazar; Sherlock yalnız okur.
> **Ne YOK burada:** güncel durum / "son push" / "nerede kaldık" blokları (bayatlıyor — canlı kaynağı
> Watson'ın hafızası), dosya-alan enumerasyonu, tamamlanmış fazların blow-by-blow anlatımı.
>
> **Kardeş dosyalar:** `ROADMAP.md` = ileriye dönük plan · `Docs/GAME_DESIGN.md` = canonical
> tasarım/denge sayıları (tek doğruluk kaynağı) · `Docs/DECISION_LOG_ARCHIVE.md` = tam tarihsel arşiv ·
> `Docs/TILE_PIPELINE.md` = karo import hattı · `Docs/UI_LAYOUT.md` = ekran taslakları ·
> `Docs/INBOX_TASKS.md` = görev kuyruğu. Aynı şeyi ikisinde anlatma.
>
> **Yeni giriş formatı:** `KARAR / NEDEN / COMMIT / (varsa) DERS`. Dosya listesi yazma — `git show <hash>`
> zaten söylüyor. Bir gotcha bulduysan aşağıdaki **Tuzaklar/Dersler** bölümüne de bir satır ekle.

---

## 2026-08-13 — Kam'ın büyüleri (5 aktif yetenek) + animasyon "zıplama" hatası

**KARAR 1 — Davul draftına GARANTİ büyü.** Draft 3 karo + **1 büyü** = 4 kart oldu. Büyü kendi
yuvasında durur, karo yuvası YEMEZ (kırpma sırasında da korunur) — yoksa "her draftta en az bir
büyü" sözü kart sayısı düşünce sessizce bozulurdu. Karo ile büyü arasındaki gerilim bilinçli:
karo yalnız Kam'ın 3 karo çevresine konur (kalıcı, Kam'ı riske sokar), büyü haritanın her yerine
atılır (tek kullanımlık, risksiz). Her vuruşta "tahtayı mı kurayım, şimdi mi patlatayım" sorusu.

**KARAR 2 — Beş büyü, hepsi alan hedefli:** Gök Ateşi (gökten alev topu, 5 karo çapı, hasar) ·
Umay'ın Şifası (11 karo çapı, İKİ TARAFI da iyileştirir → bilerek yeşil değil, ak-altın) ·
Yel Ata (içten dışa sert rüzgâr, alan dışına + 2 karo savurur) · Taş Kesilme (alan taşlaşır,
sarmaşıklar çıkar, herkes 1 tur sersemler) · Kara Kasırga (herkesi merkeze toplar).
Yarıçaplar kullanıcının verdiği ölçüler: "5 karo çapı" = yarıçap 2; şifa için "5 karo yarıçapı"
denildiği için yarıçap 5 — iki tarafı da iyileştirdiğinden geniş olması onu güçlü değil RİSKLİ
yapar, o yüzden aynen uygulandı.

**KARAR 3 — Hedefleme LoL kalıbı.** Kart seçilir → kamera uzaklaşır (`CameraZoomSettings`
sinematik çarpanı) → şeffaf etki alanı FARE ile birlikte hareket eder → aynı karoya ÇİFT TIK
ile atılır, Escape iptal eder (kart geri gelir, vuruş yanmaz). Gösterge ışını karo collider'ına
değil, karo yüzeyi hizasındaki SONSUZ DÜZLEME atar: imleç tahtadan çıkınca alan donmasın.
Kamerayı sahiplenen sınıf `CameraZoomSettings` olduğu için sinematik çarpan da orada — büyü
sistemi `orthographicSize`'a doğrudan dokunsaydı ayarlar menüsünün ölçeğiyle çakışırdı.

**KARAR 4 — Gösteri ile kural ayrı.** `KamSkillVfx` yalnız gösterir ve efektin VURDUĞU anda geri
çağırır; hasar/can/itme tam o karede uygulanır (meteor havadayken değil). İtme/çekmede hedefler
ÖNCE hesaplanıp yer rezerve edilir, animasyon SONRA başlar — aksi halde iki birim aynı karoya
kayardı. İtmede en uzaktaki, çekmede en yakındaki önce hareket eder.

**KARAR 5 — TEK KAROLUK ETKİ KALDIRILDI; taban ölçü YARIÇAP 1 (3 karo çapı).**
Kullanıcı kuralı: "tek karoluk olmasın". Mekanik gerekçe: tek hexi etkileyen karo, işe yaraması
için düşmanın TAM O KAROYA basmasını gerektiriyordu — hex tahtada bu neredeyse hiç olmuyor, kart
ya boşa gidiyor ya da oyun "düşman oraya bassın" bekleme oyununa dönüyordu. Yeni iki kademe:
**r1 = 7 hex / 3 karo çapı = TABAN** (19 kart) — 80 karoluk arenada %8.5 yer kaplar, ~10 birimlik
sahada alana ortalama 1 birim düşer: kesin bir şey yapar ama her şeyi kapsamaz; eni (3) düşmanın
tur hareketinden (3-4) küçük olduğu için hâlâ kaçınılabilir, yani konum kararı olmaya devam eder.
**r2 = 19 hex / 5 karo çapı** (2 kart) yalnız gecikmeli/zayıf etkilerde (Ruh Bombası fitilli,
Kutsal Zemin tur başı). Arazi kartları yarıçapla değil kapladıkları karoyla ölçülür; Boşluk da
1 karodan **3 karoya** çıktı. Kural artık `AugmentCatalog.Validate()` ile makine tarafından
denetleniyor — yeni kart yarıçapsız eklenirse kurulum bağırır (yorumda duran kural er geç delinir).
**Denge notu:** 7 kartın alanı 1→7 hexe çıktı ama BÜYÜKLÜKLERİNE dokunulmadı (CLAUDE.md §9: denge
durduruldu). En çok güçlenen ikisi Öfke Taşı (+2 hasar) ve Tuzak Taşı (sersemletme).

**COMMIT:** (bu değişiklik)

**DERS — "düşmanlar sürekli zıplıyor" hatasının sebebi LOOP POSE'du.** İlk şüphem yanlış çıktı:
batch raporu (`CharacterAnimationImporter.RebuildAndReportBatch`) controller bağlarının ZATEN
doğru olduğunu gösterdi (Idle=Idle, Walk=Walk, Attack=Punch, Death=Death). Asıl sebep import
ayarındaydı: araç döngülü kliplerde `loopTime` ile birlikte `loopPose`'u da açıyordu. İkisi aynı
şey değil — `loopTime` "baştan başla", `loopPose` ise "son kareyi ilk kareye ZORLA uydur" demek
ve Unity bu düzeltmeyi klibin tamamına uygular. Zaten temiz döngülenen Quaternius take'lerinde
bu, kökü her döngüde kaydırıyor → yerinde duran düşman saniyede bir sekiyordu. Artık `loopPose`
asla zorlanmıyor (14 klipte `loopBlend: 0`). Ek sertleştirme: `Jump_Idle`/`Jump_Land` gibi
klipler ("idle" içeriyorlar!) artık süzgeçten geçemiyor ve klip seçimi "ilk eşleşen" yerine
PUANLAMA yapıyor (tam ad eşleşmesi kazanır) — asset yükleme sırası garanti olmadığı için eski
kural makineye göre farklı sonuç verebiliyordu.

**Bilinen sınır:** Kam/Savasci/Ranger'ın FBX'inde idle klibi YOK — controller yürüyüş klibinin
ilk karesini donduruyor (`speed = 0`). Gerçek idle istenirse klasörlerine `model@Idle.fbx`
atılıp "Karakter - Tum Karakterleri Kur" koşturulması yeterli.

## 2026-08-12 (4) — Haritanın dışı dolduruldu (üç katman) + savaş haritasında sis kaldırıldı

**Sorun:** Tahta dikdörtgen, kıta organik. Kıtanın dışındaki koordinatlarda hücre ÜRETİLMİYOR
(bilinçli: kare siluet olmasın diye) → kıyının ötesi HİÇLİK. Kamera azıcık kayınca haritanın
nerede bittiği görünüyor. Savaş arenasında daha beter: tahta 10×8, çevresi tamamen boş.

**ARAŞTIRMA — tür oyunları sınırı nasıl gizliyor:** üç teknik var ve hepsi BİRLİKTE kullanılıyor;
biri eksik olursa bir kamera açısında mutlaka açık kalıyor.
1. *Diyejetik geçilemez arazi:* Civ VI haritayı kutupta buz kalkanı, kenarda okyanusla bitirir —
   sınır bir duvar değil, coğrafya. 2. *Sonsuz yüzey:* oynanan alanın altına/ötesine giden tek
   büyük düzlem (okyanus). 3. *Kenar maskeleme:* hex haritalarda kenarı dağ/kaya/ağaç siluetiyle
   kapatmak (cliff kenarları her şeye uyar, "kasıtlı" görünür) ya da sis bandı + görünmez collider.
   Mesh tarafında buna "skirt" deniyor: yüzey kenarına eklenen etek, iki yüzey arasındaki çatlağı
   kapatır.

**KARAR — üç katmanlı `MapSurroundBuilder` + profil SO'ları.**
   1. **DÜZLEM** — ufka kadar tek yüzey (overworld ~700×690 birim). Garantiyi bu verir: kamera
      nereye bakarsa baksın altta bir şey vardır.
   2. **BANT** — tahtanın dışına 6-7 halka GERÇEK hex karosu, halka başına TEK birleştirilmiş mesh.
      Dışa gittikçe rengi düzleme yaklaşır ve yüksekliği düzleme *iner* (skirt) — yoksa bandın son
      halkasıyla düzlem arasında karo kalınlığı (0.3) kadar basamak kalır, yani tam da gizlemeye
      çalıştığımız çizgi geri gelirdi.
   3. **SÜSLEME** — bandın ötesine serpilmiş ağaç/kaya/sis, yine tek mesh. Düz renk tek başına
      "arka plan" gibi durur; "devam eden dünya" hissini bu katman verir.
   Overworld profili **sonsuz okyanus** (seyrek kaya/sis öbeği), arena profili **sonsuz orman**
   (sık kanopi → "ormandaki açıklık" okunur). Hiçbiri collider taşımaz ve hiçbiri hücre olarak
   kaydedilmez → tıklanamaz, yürünemez, sise girmez. Sınır zaten doğal: `TerrainGenerator`'ın
   margin kuralı kara karosunun tahta kenarına değmesini engelliyor.
   Ölçüm (batch, Play'siz): 873 hücrelik kıta → 6 halka / 963 bant karosu + 257 süsleme.

**KARAR — savaş haritasında sis TAMAMEN kapalı (`FogOfWarManager.SetFogEnabled`).**
Eskiden savaşta `RevealAll()` çağrılıyordu: bulutlar önce KURULUYOR, sonra sönüyordu → savaşın ilk
yarım saniyesi sisli açılıyordu. Artık savaşta sis sistemi hiç çalışmıyor (bulut üretilmez,
karartma uygulanmaz). Overworld sisine dokunulmadı — dönüşte birebir eskisi gibi kuruluyor.

**COMMIT:** (bu değişiklik)

**DERS — "boşluktaki sis" bulutların önbelleğinden geliyordu.** Bulutlar koordinata göre
önbelleklenip asla yok edilmiyor (pop olmasın diye). Overworld 36×34 iken arena 10×8 olduğu için
savaşa girince overworld'ün ~1100 bulutu, karşılığı olmayan koordinatlarda HAVADA ASILI kalıyordu.
`HideOrphanCaps` bunu kapatıyor: harita değişince hücresi olmayan bulut gizlenir.

**Kaynaklar:** [Unity Discussions — Creative ways to hide the edge of the game
world](https://discussions.unity.com/t/creative-ways-to-hide-the-edge-of-the-game-world/704297) ·
[GameDev.net — hex grid terrains like Civ 5/6 or AoW](https://gamedev.net/forums/topic/705413-some-doubts-regarding-hex-grid-terrains-like-civ56-or-aow/) ·
[Civ VI harita yapısı (kutup buzulları / okyanus sınırı)](https://civilization.fandom.com/wiki/Map_(Civ6))

## 2026-08-12 (3) — Davul karoları GERÇEKTEN çalışıyor: etki motoru + kendi modelleri + geri bildirim

**Sorun:** Kam davulda karo koyuyordu, karo boyanıyordu, orada bitiyordu. "Sersemletir",
"2 can yeniler", "patlar" — hiçbirini çözümleyen kod yoktu. Kullanıcı haklı olarak
"savaş alanında karolar çalışmıyor" dedi.

**KARAR 1 — Etkinin ZAMANI veriye yazıldı.** `AugmentCatalog.Entry`'ye `Trigger` alanı geldi
(`Aura / TurnStart / OnEnter / OnDamaged / Fuse / Terrain`). Bu alan olmadan çözümleyici "Stun"
gördüğünde bunun sürekli bir aura mı yoksa girişte bir kez mi olduğunu bilemezdi — etkilerin hiç
yazılmamış olmasının asıl sebebi buydu, kartlar ne yaptığını söylüyordu ama NE ZAMAN yaptığını
söylemiyordu.

**KARAR 2 — `AugmentTileManager` tek çözümleyici.** Her tetikleyicinin TEK kancası var
(`OnUnitTurnBegan`, `Unit.OnEnteredCell`, `Unit.OnDamaged`, `OnRoundStarted`, tur değişimi).
Stat farkı birimde BİRİKMEZ: `RefreshAuras` her seferinde sıfırdan kurar (`Unit.TileBonus`) —
birikmeli bir sistemde "karodan çıkarken çıkarmayı unutma" hatası kaçınılmazdı. SO verisi
değişmiyor (CLAUDE.md §2), bonus ayrı katman.

**KARAR 3 — Açıklama = sözleşme.** Kartta yazan ile kodun yaptığı ayrışırsa METİN düzeltilir.
Çığ Taşı ("kırılınca moloz yayar" — kırılma sistemi yok) → "yerleştiği karoyu ve 2 komşusunu
molozla kapatır" oldu; Ley Damarı/Kutsal Zemin/Gölge Yarığı da uygulanan davranışa göre yeniden
yazıldı. Korku Sisi hâlâ draft dışı: isabet YÜZDESİ için vuruş zarı gerekiyor, saldırılar şu an
hep isabet ediyor. Nişan Kayası ise artık çalışıyor (menzil bonusu `Unit.AttackRange`'e giriyor).

**KARAR 4 — Görüş hattı (`LineOfSight`).** "Duvar keser, siper kesmez". Taş Duvar/Boşluk/Çığ ve
arena `CombatRole.Wall` sırtları menzilli atışı keser; `Cover` kesmez — taktik fark tam olarak bu.
Tek sınıfta, çünkü aynı soruyu üç yer soruyor (saldırı doğrulaması, highlight, düşman AI); ayrı ayrı
hesaplasalardı HUD "vurabilirsin" der, tıklayınca vurmazdı. AI hedef seçimi de hattı puanlıyor,
yoksa Taş Duvar düşmanı kilitleyip savaşı tıkardı.

**KARAR 5 — Karoların KENDİ modelleri, tek renk teması.** 24 karo `TileCatalog`'a kendi
`TileFamily.Augment` girdileriyle eklendi; modelleri `TileVisualFactory.Augments.cs` üretiyor.
Tema: kara bazalt taban + ruh teali (0.30,0.85,0.78) emisyonlu oyma — 24 karoda AYNI renk.
**Ayrım biçimden gelir:** ocak bir ateş çukuru, tuzak dişli bir halka, kapı bir kemer, fıçı bir
fıçı. Eskiden arazi görsellerini ödünç alıyorlardı (dikilitaş, bataklık, sıcak kaynak) — ne
tanınıyor ne de bir takım gibi duruyorlardı.

**KARAR 6 — Geri bildirim mekaniğin parçasıdır.** Karo yerden yükselerek oturuyor (overshoot +
dönüş), oymalar nabız atıyor, tetiklenince çakıyor; etki alanı halka olarak açılıyor ve aura
karolarında kalıcı halka duruyor; birimin üstünde yükselen yazı ("SERSEMLEDI", "+2 CAN",
"-3 CAN"); savaş panelinde "KARO: +2 hasar" satırı tur boyunca duruyor. Bir mekanik ancak
GÖRÜLDÜĞÜ kadar vardır — şikâyetin yarısı buydu.

**COMMIT:** (bu değişiklik)

**DERSLER:**
- **Patlama zinciri kuyrukla çözülür.** Patlama hasar verir, hasar başka bir fıçıyı tetikler.
  Doğrudan özyineleme geçici listeleri iç içe ezip karoyu iki kez patlatıyor/hiç patlatmıyordu →
  `QueueBlast` + `_resolvingBlasts` bayrağı.
- **Etki uygularken listenin üstünde gezme.** Hasar → ölüm → `_placed`/`UnitManager` değişimi.
  Önce eşleşenleri topla, sonra uygula.
- **Emisyon keyword'ü materyalde açık olmalı.** `MaterialPropertyBlock` `_EmissionColor` yazsa da
  `_EMISSION` kapalıysa hiçbir şey parlamaz — nabız sessizce çalışmaz görünürdü.

## 2026-08-12 (2) — Savaş ayrıştırıldı: arena üreticisi + hasar formülü + davul temposu

**KARAR 1 — Savaş haritası overworld'den TAMAMEN ayrıldı.** `CombatArenaGenerator` (Unity'siz,
taranabilir) düğüm tipine göre 4 kademede arena üretiyor: encounter 10×8 (~70 karo) · zindan 11×9
(~79) · zorunlu 12×10 (~97) · boss 13×11 (~109). Eskiden savaş, overworld ile aynı 22×25'lik
`CombatTileMap`'i kullanıyordu: **550 karo düz "kaya", sıfır engel**, iki tarafın buluşması 7 tur.
400 seed × 4 kademe ölçüldü: engel %8-10, etkileşimli %10-12, **kilitli arena 0/1600**.
Tahta boyutu artık `TileMapSO.GridSize`'da (ikisi aynı `HexGridManager`'ı paylaşıyor).

**KARAR 2 — Hasar formülü çarpansal.** `max(0, ATK−DEF)` küçük sayılarda çöküyordu: Goblin (ATK 3) →
Savaşçı (DEF 3) = **0 hasar**, seviye 1 Savaşçı goblinlere karşı ölümsüzdü; "+1 hasar" veren bir karo
bazı eşleşmelerde %20, bazılarında sonsuz iyileştirme demekti (0→1) — davul karolarının değerleri o
zeminde hesaplanamazdı. Yeni: `ATK × 100 / (100 + DEF × 15)`, taban 1. `CombatMath` (Unity'siz) +
`CombatFormulaSO`.

**KARAR 3 — Davul temposu + karo draftı.** Tur 2,4,6,8,10'da davul çalar, 3 kart sunulur
(1 Kut + 1 Kargış + 1 Nötr/Patlayıcı, %15 nadir sınıfsal), seçilen karo **yalnız Kam'ın 3 karo
çevresine** konur (boss 4). Yarıçap türetmesi: düşman tehdit menzili 4-5, temas hattı deploy'dan 5-6
karo → R=3 ile Kam temas hattına karo koymak için tehdit menziline GİRMEK zorunda. R=5-6 olsaydı
güvenli mesafeden koyar, risk sıfırlanırdı. 24 kartlık havuz (`AugmentCatalog`), yarıçap kuralı
"güç × alan ≈ sabit": r0 sert (sersemletme/patlama), r1 orta, r2 hafif-geniş.

**Denge sayıları:** saha tavanı Kam+4; düşman GE bütçesi encounter 0.8× → boss 1.5×; deploy 3 öz/birim
(50 birim-indirme × 3 = 150, bütçe 151). Karar/savaş = 20-28 (Into the Breach 15, XCOM ~40) —
144 savaşlık bir run'da tekrar yorgunluğunu önlemek için bilinçli.

**COMMIT:** (bu değişiklik)

**DERSLER (hepsi gerçek hata olarak çıktı):**
- **Execution order ezmesi:** `ChapterMapGenerator` (-90) oyuncuyu doğru karoya koyuyordu, sonra
  `PlayerController.Start()` (0) SERİLEŞMİŞ eski koordinatla EZİYORDU → oyuncu hep Hex(3,4)'te,
  organik haritada denizde/cepte doğuyordu. Bir bileşen başkasını Start'ta kuruyorsa, kurulan
  tarafta "zaten kuruldum" bayrağı ŞART.
- **IMGUI her zaman Canvas'ın ÜSTÜNE çizer.** uGUI panelin `sortingOrder`'ı bunu değiştirmez;
  tek çözüm IMGUI'yi susturmak (`MenuState.HudsHidden`).
- **`HudScale.UiScale = 1.5` yüzünden sanal ekran 1080 DEĞİL** (~720). IMGUI'de sabit yükseklik
  yazmak paneli ekran dışına taşırır — `HudScale.Height`'tan türetilmeli.
- **HorizontalLayoutGroup çocuk boyutunu preferred size'dan okur;** `sizeDelta` ona hiçbir şey
  söylemez → `LayoutElement`'siz kartlar sıfır genişliğe çöküp üst üste biner.
- **`CharacterClassData._unitModelEuler` varsayılanı (90,0,0) idi** → koddan üretilen dik modeller
  yatıyordu VE auto-scale yatık bounds'u ölçüp devasa büyütüyordu. Varsayılan sıfırlandı.
- **Başlangıç karosu seçimi üç şart ister:** en büyük YÜRÜNÜR bileşen + çevresi açık (2 hex'te ≥13
  yürünür) + o bileşenin KENDİ ağırlık merkezine yakın. Kıtanın ağırlık merkezi hilal şekillerde
  körfeze düşüyor. "Başlangıç kapalı" filtresi tek başına 12.000 adayın 642'sini eledi.

---

## 2026-08-12 — Kare tahta bırakıldı: ORGANİK KITA üreticisi + 30 seed havuzu + 74 karo tipi

**KARAR:** Bölüm haritası artık dolu bir 22×25 dikdörtgen değil; 36×34'lük bir tahtanın içine oturan,
kıyısı gürültüyle şekillenmiş **organik bir kıta** (~550 kara karo) + kıyının dışında düzensiz
genişlikte **sis/deniz bandı**. Tahtanın dışı gerçekten boş: `TileCatalog.Void` karosunda
`HexGridManager` hücre ÜRETMEZ.

**NEDEN:** Efe (2026-08-12): "haritayı kare olarak görünce otomatik sınırı olduğunu anlamak oyunu
sıkıcılaştırıyor" — For The King tarzı, sınırı belirsiz, sonsuz hissi veren harita isteniyor. Kare
siluet keşif gerilimini ilk bakışta öldürüyordu.

**Karo dağılımı Efe'nin kendi araştırmasından** (KITAYA oranla, sınır dekoru sayılmaz):
yürünür %78.4 · nehir %4.9 · dağ/kaya %7.5 · sık orman+göl %8.9 · köprü/geçit %0.4.
Üretici bu oranları hedefleyerek çalışıyor; 30-seed havuzunun ortalaması %78.8 / %4.9 / %7.4 / %8.9.

**Köprü kuralı:** köprü artık nehir yolundan rastgele örneklenmiyor. Aday = iki KARŞIT yanında
yürünür kara olan nehir karosu; aralarından "köprü olmasa dolaşmak en uzun sürecek" olanlar seçiliyor.
Ayrıca kopuk kalan büyük cepler için dağ geçidi / sığ geçit açılıyor (harita bitirilebilir kalsın).

**Boru hattı (gerçek coğrafya taklidi):** domain-warp'lı fBm + harmonik burun/koy terimi → kıta
maskesi · SIRT gürültüsü → çizgisel dağ silsileleri · yokuş-aşağı akış → nehirler denize/göle ·
çukurlarda göl, nemli alçakta sık orman · (yükseklik, nem, sıcaklık) → ~30 yürünür alt tipe biyom
dağıtımı. İklim ekseni seed'e göre DÖNER (her harita hep "kuzeyi karlı" olmasın).

**Seed havuzu 10 → 30.** Seçim artık elle değil: `Docs/Balance/tools/seed_taramasi/tara.ps1`
OYUNDA ÇALIŞAN C# kodunun aynısını Unity açmadan derleyip 12.000 aday üretiyor, oran/bağlantı/
kıyı-organikliği/14-günlük AP baskısı filtrelerinden geçiriyor (6358 geçti) ve METRİK UZAYINDA
BİRBİRİNDEN UZAK 30 tanesini seçiyor (30 harita birbirinin kopyası olmasın diye).

**Karo çeşitliliği 16 → 74 tip**, hepsi renkli + prosedürel dokulu. Tek doğruluk kaynağı
`TileCatalog`; görselleri `TileVisualFactory` üretiyor (12 gri tonlu yüzey dokusu × karo rengi +
~30 süsleme reçetesi). Bir haritada tipik olarak 55-59 farklı karo tipi görünüyor.

**COMMIT:** (bu değişiklik) · eski Python-portu üretici git'te: `git show 70ac734:Assets/Scripts/Grid/TerrainGenerator.cs`

**DERS:** Boyut tek bir yerde (HexGridManager Inspector) durduğu için overworld tahtası büyüyünce
SAVAŞ arenası da büyüyordu (aynı grid paylaşılıyor). Tahta boyutu artık `TileMapSO.GridSize` ile
haritanın kendi verisi.

---

## 2026-08-05 — "Harita düzgün oluşmuyor"un GERÇEK sebebi: koordinat uzayı uyuşmazlığı + eski oyun SİLİNDİ

**Kullanıcı emri:** görevleri git'ten denetle, eksik kalanı tamamla, **eski oyunun öğelerini SİL**
(assetler kalsın — tekrar kullanılabilir).

### 1) KÖK SEBEP — üretilen harita tahtaya KAYIK oturuyordu

`TerrainGenerator` (sütun, satır) indisli bir dizi üretiyordu ve `ChapterMapGenerator` bunu
`new HexCoordinate(q, r)` diye **doğrudan** TileMap'e yazıyordu. Ama `HexGridManager.GenerateGrid`
hücreleri **odd-r offset**'ten türetiyor: `Q = col - (row >> 1)`. İki uzay aynı değil.

Ölçülen zarar (üretilmiş `Bolum1_Uretilen.asset` doğrudan okunarak):
- **550 hücrenin 144'ü (%26) hiç atama almıyordu** → `defaultTileId` = düz ova. Sol altta satır
  indikçe büyüyen boş bir kama. Aynı anda üretimin sağ tarafı tahta dışına düşüp **çöpe gidiyordu**.
- `ChapterNodeManager` de ham indis kullanıyordu → alt satırlara düşen düğümler var olmayan
  hücrelere denk gelip `RegenerateCellVisual`'da sessizce eleniyordu (**zorunlu görev/market dahil**).

**İkinci, daha derin katman:** düzeltme sırasında görüldü ki üretici **düz axial komşuluk**
kullanıyordu (`{1,0},{1,-1},{0,-1},{-1,0},{-1,1},{0,1}`) — yani diziyi axial bir *eşkenar dörtgen*
sanıyordu. Tahta ise *dikdörtgen*. Shear dönüşümü komşuluğu KORUMAZ (çift satırlarda "sağ-üst"
komşu tahtada 2 karo uzağa düşüyor) → nehir tahtada kopuk kopuk, bloblar delikli, ve
`largest_connected_component`'in "bağlantılı" dediği alan tahtada bağlantısız olabiliyordu.

**KARAR:** üretici ve tahta AYNI uzayda buluşturuldu — **odd-r offset**:
- `HexCoordinate.FromOffset/ToOffset` eklendi; `HexGridManager` ve `ChapterMapGenerator` ikisi de bunu kullanıyor.
- `TerrainGenerator`'ın komşuluk tablosu satır **paritesine** bağlı hale getirildi (DirsEven/DirsOdd),
  eski axial sırayla birebir eşleşen sırada.
- Python referansı (`harita_terrain_v2.py`) da aynı şekilde düzeltildi (`harita_sim.neighbors`
  importu yerine yerel offset komşuluğu).
- **PARİTE KORUNDU:** `dogrula.ps1` yeniden koşuldu → *"BIREBIR AYNI — 10 seed × 550 karo = 5500 karo,
  sıfır fark"*. TASK-005'in "Python referansıyla eşleşiyor" kriteri hâlâ geçerli.
- Doğrulandı: tahtanın 550 hücresinin **550'si** atama alıyor, 0 boşluk, 0 taşma.

**⚠ SHERLOCK'A:** 10 seed'in "elle doğrulanmış adalet/oynanabilirlik" onayı **BAYAT** — o doğrulama
bozuk komşulukla yapılmıştı. Yeni komşulukta seed **20** (82 karo) ve seed **286** (129 karo, yürünür
alanın %29'u) ana bölgeden KOPUK cepler içeriyor. Oyun artık bunlarla da oynanabilir (düğümler
erişilebilir bölgeye kısıtlandı, aşağı bkz) ama öz arzı hesabı değişti. Havuz `GAME_DESIGN §3`'te
canonical olduğu için DEĞİŞTİRMEDİM.

### 2) TASK-006 port eksiği: düğümler erişilemeyen ceplere düşebiliyordu

Python referansı (`harita_map1_sim.build_nodes`) havuzu **`walkable_comp`**'tan (ana bağlantılı
bileşen) kuruyor; C# portu ise TÜM `ova` karolarından kuruyordu. Dağ ardındaki bir cebe düşen
zorunlu görev bölümü **bitirilemez** yapardı. `ChapterMapGenerator.IsReachable()` eklendi, havuz
ona kısıtlandı. (Seed 89: 244 ova → 241'i erişilebilir; 22 düğüm için fazlasıyla yeterli.)

### 3) TASK-007 kabul kriteri tamamlandı: uyarı süresi 1 → **2 gün**

Kriter "en az 1-2 gün önceden görsel uyarı" diyordu, sistem tam 1 gündü. `CollapseConfig`'e
`_telegraphDays` (varsayılan 2) eklendi; işaretli karolar artık *silinecekleri günü* taşıyor
(`_doomed: (coord, removeDay)`), her gün sınırında vadesi gelenler siliniyor ve
`GetRemovalCount(gün + uyarıSüresi)` kadar yenisi işaretleniyor → **günlük silme takvimi aynı kaldı**
(gün10=10 … gün14=14, kümülatif 60), sadece uyarı iki gün önce başlıyor.
Ayrıca `ResetCollapse()` eklendi ve `ChapterRunManager.RestartChapter` onu çağırıyor — retry'de
eski haritanın silinmiş/işaretli karoları yeni haritaya taşınmıyordu.

### 4) ESKİ OYUN SİLİNDİ (kullanıcı emri) — assetlere DOKUNULMADI

**Silinen kod:** `WorldGridManager` · `TeleportManager` · `WatchtowerManager` ·
`EssenceNodeManager` · `EssencePainterWindow` · `SceneSetupTool.SetupWorld3x3()` +
`EnsurePortalPaletteEntries()` + `LoadOrCreateFaceAsset()` + `GetOrCreateEssenceMap()` ·
`HexGridManager._watchtowerPositions` · `MapCollapseManager`'ın ada-başına durum sözlüğü
(tek haritaya indirildi) · `MapInputHandler._worldGrid` · `StoreManager._worldGrid` ·
`OverworldEssenceHUD._nodes`. "Arsiv" menüsü de gitti.

**Neden önemliydi (kozmetik değil):** `EssenceNodeManager` CANLI sahnedeydi ve `Start()`'ta
`EssenceMap.asset`'teki **eski elle boyanmış** koordinatlardan (negatif Q'lar dahil) öz küreleri
sahneye serpiyordu — prosedürel haritada anlamsız yerlerde duran görsel çöp.

**Silinmeyen (emir gereği):** hiçbir `.asset`/`.fbx`/`.prefab`. `TileMap.asset` + `Face_2..9.asset`
(eski 9 harita), `EssenceMap.asset`, paletteki `portal*`/`deneme*`/`agac*` girişleri, tüm modeller
yerinde. `EssenceMapSO.cs` de duruyor — `EssenceMap.asset`'in şeması olduğu için (silinseydi asset
bozuk script'e düşerdi).

**Geri dönüş:** artık tek yol git — `git show 48a8b49:Assets/Scripts/Core/WorldGridManager.cs` vb.

**DOĞRULAMA:** İki assembly de Unity'nin kendi Roslyn'iyle **hatasız derlendi (exit=0)**;
C#↔Python paritesi **kanıtlandı**; `Bolum1_Uretilen.asset` doğru koordinatlarla (seed 89) yeniden
üretildi ve tahtayı tam kapladığı doğrulandı. **UNITY'DE AÇILMADI/OYNANMADI.**

**COMMIT:** (bu giriş)
**DERS:** İki ayrı yerde "(q, r)" yazması aynı koordinat sistemi oldukları anlamına gelmez. Bir
üretici ile onu tüketen tahta arasında dönüşüm YOKSA, bu bir varsayımdır — ve sessizce yanlış
olabilir. Belirti "harita biraz tuhaf" gibi görünür; asıl kanıt veri: kaç hücre atama ALMIYOR?

---

## 2026-08-04 — Eski 3×3 tasarımı editörden GİZLENDİ (silinmedi) + "harita düzgün oluşmuyor" teşhisi

> **SÜPERSEDE (2026-08-05):** Kullanıcı eski tasarımın saklanmasına gerek olmadığını, silinebileceğini
> söyledi. Buradaki "gizleme" işi yerini SİLMEYE bıraktı (bkz üstteki giriş). Tile Painter'daki
> arşiv-karo filtresi duruyor (palet asset'i korunduğu için hâlâ işe yarıyor).

**ŞİKÂYET (kullanıcı):** "harita düzgün oluşmuyor gibi hissediyorum" + "eski harita tasarımını
sadece sakla, gösterme, görmek de istemiyorum."

**BULGU 1 — üretilen haritanın VERİSİ sağlam.** `Bolum1_Uretilen.asset` doğrudan okundu:
550 karo (22×25), 431 yürünür ve **hepsi tek bağlantılı bileşende** (BFS ile doğrulandı — kopuk
ada yok), 1 nehir + 2 köprü, 3 kule, 3 zorunlu görev, 6 mağara, 8 kamp, 2 mağaza. Sahne bağlantısı
da doğru: `HexGridManager._tileMap` → `Bolum1_Uretilen`, 22×25, palet atanmış. Yani üretici çalışıyor.

**BULGU 2 — asıl şüpheli Tile Painter'ın 9-harita seçicisiydi.** Pencerenin en üstünde duran
"Harita (3×3 snake)" ızgarasındaki bir düğmeye basmak `SetTileMap(TileMap/Face_N)` çağırıyor →
sahnedeki **üretilen harita sessizce eski elle boyanmış haritayla değişiyordu**. Kullanıcı bunu
"harita düzgün oluşmadı" diye görür; aslında başka bir harita yüklenmiştir.

**KARAR — eski tasarım göz önünden alındı, HİÇBİR ŞEY SİLİNMEDİ:**
1. Tile Painter'daki 9-harita/3×3 yüz seçicisi **kaldırıldı**; yerine yalnız düzenlenen haritanın
   adı + "yeni harita nasıl üretilir" ipucu. (Bu aynı zamanda yukarıdaki tuzağı ortadan kaldırıyor.)
2. Palette eski dünyanın karoları (`default`, `agac1/2/3`, `cicek`, `mantar`, `su`, `kum`, `lav`,
   `portal*`, `deneme*`) **varsayılan olarak gösterilmiyor** — "Eski (arşiv) karoları da göster"
   kutusuyla geri gelir (tercih EditorPrefs'te). **Palet asset'i değiştirilmedi**, bu sadece görüntü
   filtresi → boyalı alternatif haritalar etkilenmez.
3. Menü kalemi `ALTERNATIF - 9 Harita 3x3 Dunyayi Geri Yukle` → **`TacticalRPG/Arsiv/`** altına taşındı.
4. TAM KURULUM ve harita-üretme diyaloglarındaki "9 adalı dünya / eski harita silinmedi" cümleleri
   çıkarıldı (kullanıcı o metinleri görmek istemiyor).

Geri yükleme yolu ve tam liste: `Docs/Alternatif_Tasarimlar/3x3_Dunya_Haritasi/README.md` → "Gizleme".

**AÇIK — kullanıcıya soruldu, cevap bekleniyor:** Harita verisi doğruysa "düzgün değil" hissi
büyük olasılıkla **görsel**: alt tipler (ova/taşlık/ağaçlı/orman) hücre hücre bağımsız ağırlıkla
seçiliyor, kümelenme YOK → orman/taşlık bölge yerine tuz-biber serpiştirme. Python referansı da
böyle (denge simülasyonu için yazılmıştı, görsel için değil). Kümeleme istenirse ayrı görev.

**DOĞRULAMA:** İki assembly de Unity'nin kendi Roslyn'iyle **hatasız derlendi (exit=0)**.
**Unity'de açılmadı/oynanmadı** — Tile Painter'ın yeni hâli gözle görülmedi.

**COMMIT:** (bu giriş)
**DERS:** Bir editör aracının "sadece görünüm" sandığın bir parçası, canlı veriyi yazan bir düğme
olabilir. Eski tasarımı saklarken onu KURCALANABİLİR bırakmak, silmekten daha tehlikeli.

---

## 2026-08-04 — "Yürünmez yaptım ama hâlâ yürünüyorum": 3 ayrı sebep + palet temizliği

**ŞİKÂYET (kullanıcı):** Tile Painter'da karolar "Yürünmez ✗" yapılıyor ama oyunda hâlâ üstünden
geçiliyor.

**BULGU — hareket tarafı SAĞLAMDI.** `HexPathfinder`, `MapInputHandler` ve `TurnManager` üçü de
`IsWalkable`'ı doğru kontrol ediyor; `HexGridManager.SpawnVisual` da hücreyi palete göre senkronluyor.
Kırık olan **paletteki değerin kendisiydi** — üç ayrı sebepten:

1. **`TileFolderImporter.UpsertEntry` mevcut girişleri koşulsuz eziyordu.** "Klasörü Tara → Palete Ekle"
   her basıldığında `isWalkable` (+ ad, renk, yüzey yüksekliği) sabit `Overrides` tablosundan geri
   yazılıyordu; tabloda `su`/`lav` dışında her şey `walkable = true`. Karo hattı zaten
   "FBX at → Klasörü Tara → boya" olduğu için bu düğmeye sık basılıyor. Ölçülen zarar: kullanıcının
   `agac1/2/3` **ve `kule`** ayarları her taramada sıfırlanıyordu — `kule` CANLI Bölüm 1 haritasında var,
   yani bu sessizce oynanışı bozuyordu.
   **KARAR:** `Overrides`/`ResolveDef` artık **yalnız ilk oluşturma** varsayılanı. Mevcut girişte sadece
   `prefab` tazeleniyor; ad/renk/yürünürlük/yükseklik **tasarımcınındır** ve korunuyor.
2. **Anahtar diske yazmıyordu.** Toggle yalnız `EditorUtility.SetDirty` çağırıyordu →
   kaydetmeden çıkışta/reimport'ta ayar kayboluyordu. `AssetDatabase.SaveAssets()` eklendi.
3. **Palet 58 karo gösteriyordu ama canlı harita bunların yalnız 16'sını kullanıyor.** Kullanıcı
   haritadaki ağacı yürünmez yapmak isteyip "Ağaç 1/2/3"ü kapatıyor — oysa üretilen haritadaki ağaçlar
   `orman` / `nadir_yuksek_orman` / `sik_orman`. `agac*` eski 3×3 dünyanın karosu, Bölüm 1'de hiç yok.
   **KARAR:** Tile Painter her satırda artık "**haritada N**" / "**bu haritada YOK**" (turuncu) rozeti
   gösteriyor → etkisiz bir toggle görünür oldu.

**PALET TEMİZLİĞİ (kullanıcı isteği, kapsam kullanıcıya sorulup onaylandı):** 58 → **45** giriş.
Silinen 13: `barbarkarakteri` (kırık referans — bir karakter FBX'i yanlışlıkla karo olarak taranmış,
işaret ettiği asset zaten yoktu), `deneme1-10` (prefabsız placeholder), `kum`, `lav`.
Ayrıca 3 yetim prefab: `Tile_kum`, `Tile_lav`, `Tile_KopruKaro` (sonuncusu hiçbir yerden referanssızdı;
güncel köprü `Tile_kopru.prefab`).

**SİLİNMEYENLER — bilinçli:** `default` (Face_2..9 + TileMap'in `defaultTileId`'si), `agac1/2/3`,
`cicek`, `mantar`, `su`, `portal1-12`, `deneme11-20`. Hepsi ALTERNATİF 3×3 dünyanın haritalarında
fiilen kullanılıyor — silmek korunan alternatifi bozardı.

**Silmenin KALICI olması için iki kod değişikliği gerekti** (yoksa geri gelirlerdi):
`SceneSetupTool.EnsureCombatTestTiles` döngüsü `1..20` → **`11..20`** (TAM KURULUM `deneme1-10`'u geri
ekliyordu); `kum_karo.fbx` + `lav_karo.fbx` **taranan klasörün dışına** alındı
(`Assets/Art/Models/Tiles_Arsiv/`, README ile) — o klasörde kalsalardı ilk taramada palete dönerlerdi.
FBX'ler silinmedi, arşivlendi (geri dönülebilir).

**DOĞRULAMA:** Kalan 45 girişin, tüm haritalarda geçen her `tileId` ve `defaultTileId`'yi karşıladığı
script'le doğrulandı. **UNITY'DE OYNANARAK TEST EDİLMEDİ** — derleme ve görsel doğrulama bekliyor.

**AÇIK BULGU (bu işin parçası değil, dokunulmadı):** `CombatTileMap.asset`'in `defaultTileId`'si
`kaya`, ama palette `kaya` diye bir karo YOK → savaş arenasının her hücresi `entry == null`'a düşüyor,
`_hexCellPrefab` ile çiziliyor ve `IsWalkable` varsayılan `true` kalıyor. Arena düz/yürünür olduğu için
şu an görünür bir zarar yok, ama arenaya engel konulmak istenirse önce bu bağ kurulmalı.

**COMMIT:** (bu giriş)
**DERS:** Bir ayar "tutmuyor" diyorsa, ayarı OKUYAN tarafı suçlamadan önce ayarı YAZAN tarafları say.
Burada okuma zinciri baştan sona doğruydu; üç ayrı yazıcı (importer, eksik kaydetme, yanlış karo seçimi)
aynı semptomu üretiyordu.

---

## 2026-08-04 — Canlı sahne artık `Assets/Scenes/xd.unity` (SampleScene DEĞİL)

**KARAR:** TASK-005/006/007 ile kurulan 22×25 prosedürel harita, node'lar (`Node_Mandatory_*`,
`NodeRing_*`, `Path_*`, `State_*`) ve yeni UI (Panel_Map/Bag/Book, pusula) **`Assets/Scenes/xd.unity`**
içinde. Eski `Assets/Scenes/SampleScene.unity` 10×10 grid'li ESKİ hâliyle duruyor —
silinmedi, alternatif/referans olarak kalıyor.

**NEDEN:** Unity oturumu boyunca çalışılan sahne kaydedilmemiş (Untitled) bir sahneydi; kapatırken
`xd` adıyla `Assets/Scenes/` altına kaydedildi. SampleScene'in üzerine yazılmadı → eski whitebox
düzeni referans olarak korunuyor.

**DİKKAT (bir sonraki oturum):** Unity'i açınca varsayılan olarak SampleScene açılabilir; güncel
harita için `Assets/Scenes/xd.unity`'e çift tıkla. `ProjectSettings/EditorBuildSettings.asset`
sahne listesi BOŞ — build alınacaksa File → Build Settings → Add Open Scenes gerekli.
Sahnenin adı `xd` geçici; Unity Project penceresinden `Bolum1` gibi bir isme değiştirilebilir
(GUID korunur, Explorer'dan değil Unity içinden yeniden adlandır).

**COMMIT:** bu giriş ile aynı commit.

---

## 2026-07-28 — Kamera karakteri takip ediyor (kullanıcı isteği)

**SORUN:** Kamera sabitti (Faz 0'da 10×10 grid merkezine göre elle konumlanmış). Harita TASK-005 ile
**22×25**'e büyüyünce karakter haritanın üst kısmında ekran dışına çıkıyordu.
**KARAR:** Yeni `CameraFollow` (Assets/Scripts/Input/) — Faz 0'da kameraya eklenir, Faz 1'de oyuncuya
bağlanır. Konum `hedef − forward × mesafe` ile hesaplanıyor: **kamera açısına hiç dokunulmaz**, hedef
her rotasyonda ekranın tam ortasında kalır, izometrik görünüm (30°/45°, ortografik) korunur.
`LateUpdate` + `SmoothDamp` (oyuncu hareket ettikten SONRA otur → titreme yok). Savaşta oyuncu
GameObject'i gizlendiği için hedef pasifken kamera olduğu yerde durur.

**TUZAK (bu bölümü okumadan `Input/` altına sınıf ekleme):** Sınıfı önce `namespace TacticalRPG.Input`
içine koydum → `TacticalRPG.*` altındaki HER dosyada `Input.GetKeyDown(...)` bu namespace'e çözüldü,
`UnityEngine.Input` gölgelendi ve **9 dosya birden derlenmedi** (AbilityCaster, MapInputHandler,
MenuNavigator, MinimapHUD…). Çözüm: dosya `Input/` klasöründe kalsın, ama namespace
`TacticalRPG.Core` olsun — projedeki diğer sahne bileşenleriyle aynı (bkz `MapInputHandler`).

**COMMIT:** (bu giriş)

---

## 2026-07-28 — Kule karosu gerçek modeliyle + sis kalıcı açılıyor (kullanıcı isteği)

**İSTEK:** (1) kule karolarında kule asset'i görünsün, (2) karakterin geçtiği her yerin sisi kalıcı kalksın.

**BULGU (1):** Kule modeli zaten vardı — `Tile_kule.prefab` palette'teki `kule` girişine bağlıydı.
Eksik olan şuydu: **prosedürel harita hiç `kule` karosu üretmiyordu**, gözetleme kulesi düğümleri beyaz
küre işareti olarak çiziliyordu.
**KARAR:** Kule düğümünün karosu yerleşimden sonra gerçek `kule` karosuna çevriliyor
(`ChapterMapGenerator.SetTile`) → palette'teki model render ediliyor, ayrı işaret nesnesi kaldırıldı.
`kule` karosu YÜRÜNEMEZ olduğu için kule artık **komşu karodan** kullanılıyor
(`ChapterNodeManager.NodeForPlayer`) — eski `WatchtowerManager` de böyle çalışıyordu.

**ÇAKIŞMA ÇÖZÜLDÜ:** Eski `WatchtowerManager` de `kule` karolarını yakalıyor ve **TÜM haritanın**
sisini açıyor. Prosedürel harita artık kule karosu ürettiği için iki sistem aynı karoda çakışacaktı ve
eskisi yeni 5×5 kuralını anlamsız kılardı. → Eski bileşen **bölüm kurulumundan çıkarıldı**; SİLİNMEDİ,
ALTERNATİF 9 adalı dünyada (`SetupWorld3x3`) kurulmaya devam ediyor — orada ada-başına açma anlamlı.

**KARAR (2):** `FogOfWarManager`'a `_permanentExploration` (varsayılan AÇIK): bir kez tam görünür olan
karo `_permanentReveals`'a girer ve bir daha sislenmez.
**NEDEN:** Sis bu projede dinamikti (arkanda kapanıyordu), ama **GAME_DESIGN §3 zaten tersini
varsayıyor**: kule tanımında "sis zaten hiç geri kapanmıyor, bu sadece erken açma" yazıyor. Yani bu
değişiklik kodu canonical tasarımla hizalıyor, ona aykırı değil.
**YAN ETKİ (bilinçli):** Gece görüşü daralması artık keşfedilmiş alanı karartmıyor — keşfedilen yer
gece de açık kalır. İstenmezse `_permanentExploration` Inspector'dan kapatılır, eski davranış döner.

**COMMIT:** (bu giriş)
**DERS:** "X yok" şikâyetinin sebebi asset'in eksikliği olmayabilir — burada model ve palet bağı
hazırdı, üretici o karoyu hiç yerleştirmiyordu. Önce zincirin hangi halkasının kopuk olduğuna bak.

---

## 2026-07-28 — Tarifler + mağaza fiyatları taş/doğa'ya çevrildi (görev dışı, kullanıcı talimatı)

**SORUN:** TASK-005 bölüm 1 özlerini taş+doğa yapmıştı, ama üretim tarifleri ve 5 mağaza öğesi hâlâ
Ateş/Su/Toprak istiyordu → **oyun fiilen kırıktı**: bölüm 1'de birim üretilemiyor, alışveriş
yapılamıyordu (başlangıç 4/4/4 stok bitince kazanma yolu yok). İki kez INBOX'ta bayraklandı,
gelen kutusunda karşılık gelen bir görev açılmadı.

**KARAR (kullanıcı, sorulunca):** "Taş değerli, doğa yakıt". **NEDEN:** GAME_DESIGN §3'teki
erişilebilir arz taş 79 / doğa 216 (≈1:2.7) — doğayı akan para, taşı kıt "kapı" yapmak hem bu orana
uyuyor hem de taşlık bölgelere gitmeyi anlamlı kılıyor.

| | Taş | Doğa |
|---|---|---|
| Savaşçı | 2 | 3 |
| Ranger | 1 | 4 |
| Yel Ayağı / Kartal Gözü (pot) | — | 3 |
| Zaman Kumu (pot) | 2 | — |
| Sağlam Çizme / Kâhin Pusulası (kalıcı) | 4 | 3 |

Hem kurulum kodunda hem de 7 asset dosyasında güncellendi (TAM KURULUM beklemeden çalışsın diye).

**SAYILAR TASLAK.** Denge Sherlock'un alanı — bunlar "oyun çalışsın" diye konmuş, playtest'le
ayarlanacak makul başlangıç değerleri. **Sherlock bunları GAME_DESIGN §2/§3'e canonical olarak
yazmalı**, yoksa bir sonraki oturumda kaynak belirsiz kalır.

**COMMIT:** (bu giriş)

---

## 2026-07-28 — TASK-007: zaman baskısı + bölüm-kapsamlı kayıp/retry

**KARAR:** Çöküş sistemi SIFIRDAN yazılmadı — `MapCollapseManager` zaten 1-gün-önceden işaretleme +
kırmızı çerçeve + dalga + yıldırım telegraph'ına sahipti; yalnız **yeniden ayarlandı** (gün 10 başlar,
10 + günde +1 → gün 14'te kümülatif tam 60) ve **zorunlu görev karoları muaf** edildi
(`SetProtectedTiles`). **NEDEN:** çalışan ve cilalı bir sistemi yeniden yazmak saf risk; görev
"gün 10'dan itibaren" diyordu, mekanik değil parametre farkıydı.

**KARAR:** Bölüm kaybı tek bir yerde toplandı (`ChapterRunManager`) — süre dolması, Kam'ın ölümü ve
elle çağrı AYNI yola girer. **NEDEN:** görev metni "Kam ölümü DAHİL hepsi aynı kural" diyor; üç ayrı
yerde ayrı ceza mantığı olsaydı ilerideki değişiklikte biri unutulurdu.

**DÜZELTME (kendi hatam, uygulamadan önce yakalandı):** Sert kesimi önce `ActionPointManager.SetFrozen(true)`
ile yapmıştım — ama dondurulan motor AP HARCAMAZ, yani hareketi engellemek yerine **bedava** yapıyordu,
istenenin tam tersi. Gerçek engel `MapInputHandler`'a kondu: `ChapterLost` iken harita tıklaması işlenmez.
**DERS:** "dondur" demek her sistemde "durdur" demek değil — AP motoru için donmak "sayacı işletme"
demekti, "oyuncuyu durdur" değil.

**COMMIT:** (bu giriş)

**AÇIK — Sherlock'a:**
1. **Meta-Öz kodda HİÇ YOK** (projede hiç yazılmamış). "Meta-Öz korunuyor" kriteri bu yüzden boşlukta
   doğru — korunacak bir şey yok. Gerçekten istenen bir sistemse ayrı görev gerekiyor.
2. Uyarı süresi **tam 1 gün** (kriter "1-2 gün" diyordu). Mevcut tasarım işaretle→ertesi gün sil.
   2 güne çıkarmak isteniyorsa işaretleme ile silme arasına bir gün daha eklenmeli.

---

## 2026-07-28 — TASK-006: harita düğümleri (zorunlu görev / zindan / encounter / market / kule / boss)

**KARAR:** Düğümler ayrı bir katman (`Core/ChapterNodeManager` + `Data/NodeConfigSO` + `UI/ChapterNodeHUD`),
terrain üreticisinden bağımsız — harita üretilince (`OnMapGenerated`) "ova" karolarına yerleşir.
Sayılar/aralıklar SO'dan gelir (taslak: 3 zorunlu · 6 zindan · 8 encounter · 2 market · 2 kule · 1 boss).

**"Riski bil, ödülü bilme" nasıl uygulandı:** düğümün `Value`'su var ama `RewardKnown` bayrağı
zindan/encounter'da **false** başlar → HUD ödül yerine "? (girmeden bilinmez)" yazar; zorluk ise
AP maliyetinden türetilip ★ ile GÖSTERİLİR. Girildikten sonra `RewardKnown` true olur.
Ödülü "gizli" tutmak için değeri sonradan üretmedim — **üretim anında belirlenip saklanıyor**, böylece
kaydetme/yükleme geldiğinde de tutarlı kalır.

**KARAR (sis'ten bağımsız zorunlu görev):** Sis bu projede DİNAMİK (her adımda oyuncu mesafesinden
yeniden hesaplanıyor), o yüzden "hep görünür" bir işaret sis sisteminden muaf tutulmalıydı. Zorunlu
görev işaretleri bulut yüksekliğinin ÜSTÜNE konuluyor ve görünürlük kontrolünden muaf; diğerleri
`FogOfWarManager.IsKnown` ile açılıyor.

**KARAR (kule 5×5):** `FogOfWarManager`'a `RevealAreaPermanent(center, radius)` + kalıcı açıklık kümesi
eklendi; `UpdateFogAround` bu karoları muaf tutuyor. Hex'te "5×5 alan" ≈ yarıçap 2 (19 karo) olarak
yorumlandı, yarıçap SO'dan ayarlanabilir.

**KARAR (boss konumsuz):** Boss havuzdan karo ALMAZ, haritada işareti YOKTUR; HUD'da her zaman duran
bir düğmeyle girilir — rota/konumla ilişkisi yok (görev metni birebir bunu istiyordu).

**SAPMA (bilinçli, Python sim'inden):** `harita_map1_sim.build_nodes`'un "ova havuzu" sırası Python'un
KÜME sıralamasından geliyor — dile bağlı, C#'a taşınamaz. Havuz burada (q, r) sırasına göre kurulup
karılıyor: aynı seed'de hep aynı yerleşim, ama sim'le birebir aynı koordinatlar değil. Sayılar ve
değer/maliyet aralıkları aynı olduğu için denge etkilenmiyor.

**AÇIK SORULAR — CEVAPLANDI (kullanıcı, aynı gün):**
(1) Düğüm ödülü **doğa kalsın** → kod zaten öyleydi, değişiklik yok.
(2) Zorluk **şimdilik sadece gösterge kalsın** → değişiklik yok; "zor zindan gerçekten zor olsun"
ileriye bırakıldı (ayrı görev).
(3) **Market düğümü ile boyalı `magaza` karosu BİRLEŞTİ.** `StoreManager.SetNodeStores` ile düğüm
karoları da "dükkân yakını" sayılıyor → market düğümüne yaklaşınca mevcut 5 eşyalı dükkân açılıyor;
gece kapalı (`OnTimeAdvanced` ile güncelleniyor). Boyama yolu bozulmadan duruyor — iki yol da geçerli.
**Kurulum tuzağı:** `SetupStore` zincirde `SetupChapters`'tan SONRA çalıştığı için düğüm→dükkân
bağı SetupStore'un sonunda GERİ-BAĞLANIYOR; ileri yönde bağlamak null bırakırdı.

**COMMIT:** (bu giriş)
**DERS:** "Hep görünür olsun" gibi bir istek, sistemin dinamik mi statik mi olduğuna bakmadan
karşılanamaz — buradaki sis her karede yeniden hesaplandığı için "görünür yap" değil "hesaptan muaf
tut" gerekiyordu.

---

## 2026-07-28 — TASK-005: prosedürel terrain + 24 AP/gün + 10-seed havuzu

**KARAR (RNG):** Python'ın `random`'ı (Mersenne Twister + CPython'ın çekim algoritmaları) C#'a
**birebir port edildi** → `Grid/PythonRandom.cs`.
**NEDEN:** Kabul kriteri "aynı seed → Python referansıyla AYNI terrain". `UnityEngine.Random`
(xorshift) ya da `System.Random` bunu asla veremez — farklı sayı dizisi, farklı harita. 10 seed denge
tarafında elle doğrulandığı için (adalet/oynanabilirlik) haritaların birebir aynı çıkması şart.
Port kapsamı: `random()`, `getrandbits`, `_randbelow` (modulo değil, **reddetme** — dizi ilerlemesi
buna bağlı), `randint`, `randrange`, `choice`, `shuffle`, `sample` (iki dallı), `choices` (kümülatif
ağırlık + `bisect_right`).

**KARAR (üretici):** `Grid/TerrainGenerator.cs` = `harita_terrain_v2.py`'nin birebir portu — nehir
(köprü geçitli) → sık orman/dağ/göl blobları → kalan alana ağırlıklı alt-tip dağıtımı. İkisi de
**UnityEngine'e bağımsız** yazıldı ki Unity'siz doğrulanabilsinler.

**DOĞRULAMA (kanıtlı):** `Docs/Balance/tools/csharp_port_dogrulama/dogrula.ps1` — Unity'nin kendi
Roslyn'i ile portu konsol programına derler, 10 seed × 22×25 üretir, aynı seti Python'dan üretir,
satır satır karşılaştırır. Sonuç: **5500 karo, SIFIR fark.** Tekrar çalıştırılabilir.

**KARAR (AP):** `TimeSlotConfig` 9 AP/dilim → **4 AP/dilim = 24 AP/gün** (asset + kod varsayılanı).
**NEDEN:** Bölüm 1'in TÜM denge simülasyonu 24 AP/gün varsayımıyla yapıldı (GAME_DESIGN §0).

**KARAR (öz):** Öz artık ayrı node değil, **karonun kendisi**; toplanınca karo ovaya döner (TEK
SEFERLİK). `EssenceType`'a **Taş + Doğa** eklendi — eski Ateş/Su/Toprak **silinmedi** (mevcut
tarifler/mağaza fiyatları onları kullanıyor, kaldırmak onları bozardı).

**AÇIK — Sherlock'a:** Bölüm 1 artık Taş+Doğa üretiyor ama `SavasciRecipe`/`RangerRecipe` ve 5
mağaza öğesi hâlâ Ateş/Su/Toprak istiyor → **bölüm 1'de birim üretilemez / mağazadan alınamaz.**
Yeni maliyetler denge kararı (Sherlock'un alanı), uydurmadım. GAME_DESIGN §2/§3'te taş/doğa
cinsinden tarif+fiyat verilmesi gerekiyor.

**COMMIT:** (bu giriş)
**DERS:** "Referans algoritmayla aynı sonucu ver" denince asıl iş algoritma değil **RNG'nin
kendisi**; ağırlıkları `float` tutmak bile (0.55f ≠ 0.55) eşleşmeyi bozar — bu yüzden config'te
`double` kullanıldı.

---

## 2026-07-28 — TASK-004: HARİTA ekranı 3×3 dünyadan 8 bölümlük yola geçti

**BULGU (kod incelemesi):** Evet, HARİTA ekranı gerçekten "9 harita / 3×3 snake" varsayımına dayanıyordu —
ama görevin öngördüğünden **çok daha geniş** bir bağımlılık ağının görünen ucuydu: `WorldGridManager`
(`_maps[9]`, snake matematiği, `VirtualPositionOnCurrentMap`), `TeleportManager` (portal eşini 1-9 tarar),
`MapCollapseManager` (uzak-ada çöküş dalgasının yönü 3×3 yerleşimden türer), `WatchtowerManager`
(ada-başına sis hafızası), `MinimapHUD`, `SetupWorld3x3`, `TilePainterWindow` — **artı ~900 elle boyanmış
karo** (9 harita × ~100, 12 portal çifti, ada başına kule + savaş karosu).

**ÇELİŞKİ:** `GAME_DESIGN.md` kendi içinde tutarsızdı — §0 "1 bölüm = 1 harita, 3×3 GEÇERSİZ" diyor,
§4 aynı tarihte "bölüm-harita ilişkisi hâlâ AÇIK: 8×9=72 mi, bölüm=harita mı?" diye soruyor. Görev
"belirsizse blocked" diyordu; kullanıcı oturumda hazır olduğu için doğrudan soruldu.

**KARAR (kullanıcı, 2026-07-28): A — bölüm = harita.** HARİTA ekranı 8 bölümlük ilerleme yoluna
dönüştü. 3×3 dünya SİLİNMEDİ, **alternatif tasarım** olarak saklandı; üzerine gidilmeyecek.
**NEDEN:** §0'a uymak + kullanıcının açık talimatı ("şu ankinin üzerine gitmiycez, alternatif olarak tut").

**NE DEĞİŞTİ:** yeni `ChapterConfigSO` (8 bölüm verisi) + `ChapterProgress` (ilerlemenin tek kaynağı,
event-driven) + `SetupChapters()` (TAM KURULUM'da UIShell'den ÖNCE). `WorldMapView` 9 ada vurgusundan
8 bölümlük yol göstergesine (tamamlandı/şu an/kilitli), `MinimapHUD` 3×3 ada minimap'inden 8 bölüm
şeridine geçti. **Portal/kule/çöküş/9-harita kodu HİÇ ELLENMEDİ** — alternatif dünya çalışır durumda.

**NE DEĞİŞMEDİ (bilinçli):** bölüm pinleri tıklanabilir değil — bölüm haritasını üreten/yükleyen sistem
henüz yok (TASK-005). Çalışmayan düğme koymaktansa salt-okunur bırakıldı.

**EK TUR (aynı gün, kullanıcı geri bildirimi):** İlk teslimden sonra kullanıcı Unity'de TAM KURULUM
çalıştırıp "oyun hâlâ tamamen eski, sadece minimap değişmiş" dedi — **haklıydı.** Görev metni yalnızca
ekranı istiyordu, ben de ekranı yaptım; ama `SetupWorld3x3()` TAM KURULUM zincirinde durduğu için
oyunun kendisi hâlâ 9 adalı dünyaydı. Kullanıcı kuralı netleştirdi: *"görev dosyasına tamamen güven,
ne diyorsa yap; silinmesi gereken şeyi alternatif olarak stokla, oyun ekranından kaldırabilirsin ama
kodunu/asset'ini silme — sonra kolay geri getirebileyim."*
→ `SetupWorld3x3()` **zincirden çıkarıldı** (silinmedi: kendi menü kalemi oldu, tek tıkla geri gelir);
yerine `SetupChapterWorld()` geldi — tek harita + kule + savaş karoları, portal/ada YOK. Paletteki
portal ve savaş karosu blokları iki ayrı yardımcıya bölündü (`EnsurePortalPaletteEntries` /
`EnsureCombatTestTiles`) ki savaş karoları ada yapısına bağlı kalmasın.
**Bu güvenliydi çünkü** tüketicilerin hepsi (`MapInputHandler`, `StoreManager`, `WatchtowerManager`,
`MapCollapseManager`) `_world` alanını zaten OPSİYONEL/null-guard'lı tutuyordu; `TeleportManager`
guard'sız ama artık sahneye hiç eklenmiyor (Faz 0 `DestroyRoot` ile GameManager sıfırdan kuruluyor).

**COMMIT:** (bu giriş)
**DERS:** "Bir ekranı düzelt" görünen iş, dokunulmamış bir alt sistemin ucu olabilir — koda bakmadan
kapsam tahmin etme. Ve tek doğruluk kaynağı sayılan belge kendi içinde çelişebilir (§0 ↔ §4);
uygulamadan önce ikisini de oku.
**DERS 2 (daha önemli):** Bir UI'ı yeni tasarıma çevirmek, OYUNU yeni tasarıma çevirmez. Kurulum
zinciri (`FullSetup`) neyi kuruyorsa oyun odur — bir tasarım kararı uygulanırken **zincire de bak**,
yoksa "kod doğru ama oyun eski" durumu çıkar. Kullanıcı için ölçüt Play'de gördüğüdür, dosyalar değil.
**DOĞRULAMA:** Unity'nin kendi Roslyn'i (`DotNetSdkRoslyn/csc.dll`) ile iki assembly de derlendi
(exit=0). **Unity'de ÇALIŞTIRILMADI/görsel doğrulanmadı.**

---

## 2026-07-28 — TASK-003: DECISION_LOG temizliği

**KARAR:** Bu dosya yalın "aktif kararlar + tuzaklar" logu oldu; tüm blow-by-blow tarih
`Docs/DECISION_LOG_ARCHIVE.md`'ye taşındı (tek satır özet + commit hash + link geride kaldı).
Bayat "güncel durum / son push / DÖNÜŞTE SIRADAKİ İŞLER" bloğu ana logdan **silindi** — arşivde
"BAYAT" etiketiyle tarihsel kayıt olarak duruyor (içerik kaybı yok).
**NEDEN:** Dosya 38 KB'a şişmişti ve büyük kısmı ya tamamlanmış faz detayı ya da bayat durum notuydu;
Sherlock'un okuması gereken "hangi karar hâlâ yürürlükte" bilgisi arasında kayboluyordu. Ayrıca
"son push/sıradaki iş" bilgisi Watson'ın hafızasıyla çelişme riski taşıyordu.
**Ek bulgu (Sherlock'a):** Ana log 2026-06-25'te kesiliyordu — **2026-06-27 → 2026-07-25 arasındaki
işlerin hiçbiri (KÜP → 9-harita geçişi, sis yeniden yazımı, portal, XCOM iki-tık hareket, gece/gündüz
AP döngüsü, uGUI menü/KİTAP) loglanmamıştı**, yalnızca commit mesajlarında duruyordu. Bu boşluk
aşağıdaki "2026-07 — commit'lerden türetilmiş özet" bölümüyle kapatıldı. O bölüm commit mesajlarından
türetildi (o oturumların ayrıntılı notu hiç yazılmamış); yeni bilgi uydurulmadı.
**COMMIT:** (bu giriş)
**DERS:** Log ancak yazıldığı sürece log — oturum sonunda commit'lemek yetmiyor, kararı buraya da
düşmek gerekiyor.

---

## 2026-07-26 — Store.cs derleme hatası

**KARAR/DÜZELTME:** `Store.cs`'e eksik `using TacticalRPG.Grid` eklendi.
**NEDEN:** `HexGridManager` niteliksiz kullanılıyordu.
**COMMIT:** `d44b780`
**DERS:** Tek CS0246 **tüm editor assembly'sini** düşürüyor → `TacticalRPG` menüsünün tamamı kayboluyor.
Menü kaybolduğunda "menü bozuldu" diye aramaya başlama, önce Console'daki derleme hatasına bak.

---

## 2026-07-26 — TASK-002: SessionStart auto-pull hook

**KARAR:** Bu makineye (Watson) SessionStart hook'u kuruldu → oturum başında güvenli `git pull`.
**NEDEN:** İki-PC akışında Watson elle pull yapmayı unutabilir; INBOX/GAME_DESIGN güncelken çalışmak
lazım. Güvenli çünkü: tracked commit'siz değişiklik varsa pull ATLANIR (Unity sık sık sahne/.controller
kirletir), temizse `--ff-only` (asla merge/clobber yok), her zaman exit 0 (oturumu bloke etmez).
**NEREDE:** `C:\3D OYUN\.claude\settings.local.json` + `.claude/hooks/session-pull.sh` — **OYUN
repo'sunun DIŞINDA** (makineye özel; Sherlock etkilenmez).
**COMMIT:** `962b076`
**DERS:** SessionStart olayı çalışan tur içinde tetiklenemez → script'i iki dalda (temiz/kirli) pipe-test
ederek doğrula, canlı ateşleme ancak yeni oturumda görülür. `settings.local.json`'a hook eklerken
watcher mevcut oturumda görmeyebilir (yeni oturum ya da `/hooks` gerekir).

---

## 2026-07-26 — 4 ekran uGUI + Mağaza + TASK-001

**KARAR (UI):** AYARLAR / ÇANTA / HARİTA / KİTAP ekranları uGUI'ye ve ortak "parşömen UI kiti"ne
(`SceneSetupTool.UIKit.cs`) geçirildi; hepsi gerçek içerik + canlı veri gösteriyor (öz deposu,
Kam büyü kartları, sınıf kartları). "ÇANTA/HARİTA" başlık yazıları kaldırıldı — şekil kimliği taşıyor.
**NEDEN:** `game UI.pdf` mockup'larına yaklaşmak + IMGUI'nin cila aşamasına taşınamaması.
**KARAR (Mağaza):** Palete `magaza` karosu (`isStore`) + 5 `ShopItemSO` + `StoreManager` /
`PlayerBuffs` / `StoreHUD`. Öz karşılığı: anlık AP ya da geçici/kalıcı hız-menzil buff'ı (adımla sayılır).
Hook'lar: `ActionPointManager.GrantAP`, `PlayerController.SpeedMultiplier`, `MapInputHandler.BonusMoveRange`.
`SetupStore` TAM KURULUM zincirinde.
**KARAR (TASK-001):** Hafızadaki 3 referans notu repoya yedeklendi → `Docs/GAME_DESIGN.md §5` (canonical
§0-4 korunarak EKLENDİ, üzerine yazılmadı), `Docs/UI_LAYOUT.md`, `Docs/TILE_PIPELINE.md`.
**COMMIT:** `721c3bf` (UI+mağaza) · `72aea8d` (TASK-001)
**AÇIK:** Bu ekranlar **Unity'de görsel olarak henüz doğrulanmadı** (kod+push yapıldı, Play denenmedi).

---

## 2026-07 — commit'lerden türetilmiş özet (o dönem log tutulmamış)

> Aşağıdakiler ilgili commit mesajlarından çıkarıldı; ayrıntılı oturum notu yok. Sayılar o günkü hâl —
> canonical değer için `Docs/GAME_DESIGN.md`.

- **AP / zaman döngüsü** (`f080ab7` 23 Tem, `bc0aa71` 25 Tem): 1 karo = 1 AP, öz toplama = 1 AP,
  savaşa girme = 3 AP; savaş boyunca AP motoru **DONAR** (`SetFrozen`) → tüm savaş 3 AP sayılır.
  6 dilim = 1 gün, ilk 4 dilim gündüz / son 2 gece. Dilim başına AP 3 → **9** yükseltildi (54 AP/gün).
  `DayNightCycle` + `DayNightProfile` (keskin geçiş, lerp yok), gece görüşü yarıya iner,
  `TimeDialHUD` dairesel sayaç, `HudScale` ile tüm IMGUI 1920×1080 sanal ekrana çizilir.
  **⚠ AÇIK:** TASK-005 bunu **24 AP/gün**'e çekecek (GAME_DESIGN §0 gerekçeli) — koddaki 54 geçici.
- **Hareket (XCOM tarzı)** (`a350c9e` 16 Tem): iki-tık — ilk tık `PathPreview` (çizgi + hex çerçeveleri),
  aynı karoya ikinci tık yürütür. Menzil **sise bağlı**: sis varken 2 karo, kule ile kalıcı açılmış adada
  sınırsız.
- **Çöküş (kıyamet)** (`a350c9e`, `5e156da`): ada-bağımsız durum (uzak adadaki çöküş ışınlanma sayacını
  sıfırlamaz, dönüşte kalıcı uygulanır); silinen karo başına kırmızı su dalgası; silinecek karolar
  **1 gün önceden** kırmızı hex-çizgi + kalan-AP etiketi, dalga cephesi geçerken yıldırımla açılır.
  Portal karoları çöküşten muaf. `CollapseConfig` startDay 5 → 3.
- **Adalar arası geçiş = yalnız PORTAL** (`5e156da` 14 Tem, `1cb6679` 16 Tem): kenar-karosu geçişi
  güvenilmez çıktı → **tamamen kaldırıldı** (`WorldGridManager` 300 → 56 satır, `TransitionMarker` silindi).
  Portal çiftleri Tile Painter'dan boyanır; ışınlanmada "toz olma" efekti.
- **Sis (FogOfWar) iki katmanlı yeniden yazım** (`4af644c` 12 Tem, `72fbbc5` 13 Tem): parlaklık çarpanı
  `MaterialPropertyBlock` ile (boyalı karo bozulmaz) + görülmemiş karonun üstüne havuzlanmış bulut kapağı
  (collider'sız, tıklamayı engellemez). `WatchtowerManager`: boyalı "kule" karosu 1 karo yakınından ada
  sisini **KALICI** açar, açık adalar hatırlanır.
- **Karakter modelleri + savaş animasyonu** (`f267b03` 27 Haz, `0570f30`, `f080ab7`): placeholder kapsüller
  gerçek modellere (`CharacterModelBinder`, kalıcı bake); Quaternius CC0 sınıf/düşman modelleri;
  `CharacterAnimationImporter` (klasör başına karakter + `_SharedHumanoid` retarget);
  `TurnOrderBarHUD` (For The King tarzı initiative kuyruğu). Gerçekçi grafik preset'i (ACES + bloom +
  vignette) Faz 0'a girdi → her TAM KURULUM'da gelir.
- **Bölüm 1 dünya yapısı: KÜP → 9 harita 3×3 SNAKE** (`986c6cd`/`9b172c7` 28 Haz → `c415d27` 29 Haz):
  gezegen-küp denemesi (6 yüz, 90° döndürme) **TERK EDİLDİ**, yerine düz 9 harita 3×3 snake dünya +
  `WorldGridManager` + TAB minimap geldi. Küp kalıntıları `72fbbc5`'te silindi.
  **⚠ AÇIK:** "Bölüm 1 = 9 harita" varsayımının kendisi sorgulanıyor (doğrusu: 1 bölüm = 1 harita,
  8 bölüm) → **TASK-004** bunu inceleyecek. Bu satır o incelemenin girdisidir, onaylanmış tasarım değildir.

---

## Yürürlükteki kararlar (özet)

Tarihsel gerekçeleri arşivde; burada sadece **hâlâ geçerli olan** hüküm var.

| Karar | Durum |
|---|---|
| **Tek sahne, durum-tabanlı geçiş** (Overworld/ConfirmMission/Deployment/Combat). Combat'ta grid savaş TileMap'iyle yeniden üretilir; ayrı Unity scene YOK. | yürürlükte (KARAR 1) |
| Deployment **bedava**; öz **birim üretiminde** harcanır (üretim savaş öncesi yerleştirme ekranında). | yürürlükte — eski "öz harcayarak deploy" (KARAR 2) Faz D2'de **iptal** |
| Üretilen birimler roster'da **KALICI** (öz bir kez harcanır, sonraki savaşlarda hazır). | yürürlükte |
| **Kam zorunlu + ücretsiz komutan**; deploy zone alt-ortaya otomatik iner. **Yenilgi = Kam'ın ölümü.** | yürürlükte |
| Savaşta **permadeath**: ölen deploy birimi o savaş için silinir. | yürürlükte |
| Initiative **hıza göre** (XCOM/Banner Saga), eşitlikte oyuncu önce. | yürürlükte |
| Düşman **ana haritada yok** — sadece savaş haritasında spawn. | yürürlükte |
| Öz türleri: **Ateş / Su / Toprak** (Kırmızı/Mavi/Yeşil), toplama 1 AP. | yürürlükte — GAME_DESIGN §3'teki taş/doğa şemasıyla TASK-005'te uyumlanacak |
| Öz haritası **el yapımı** (Essence Painter), rastgele spawn kaldırıldı. | yürürlükte — TASK-005 prosedürel terrain'e geçirecek |
| Mana savaş başında **resetlenmiyor** (havuz overworld'de regen eder). | bilinçli erteleme |
| Min-max hasar + kritik vuruş yok (düz `Attack`). | bilinçli erteleme |
| Adalar arası tek geçiş yolu **portal**. | yürürlükte |
| Karo dokuları FBX'e gömülü değil → karolar şimdilik **renksiz**; kullanıcı kararı: ileride gelişmiş karolar. | bilinçli kabul |

---

## Tuzaklar / Dersler

> Sihirli sabitler ve tekrar etmemesi gereken hatalar. **Bu bölüm arşive taşınmaz.**

**Karo / FBX**
- **Ham tasarımcı FBX'i doğrudan grid karosu OLARAK KULLANILAMAZ.** v1 karoları ölçüldüğünde: her biri
  ~262 birim (1.9 m olmalı), geometri merkez-dışı ~450 birim, 13+ ayrı mesh parçası, 61 MB (gömülü dev
  texture). Toplu import bounding-box'ı 1.9 m'ye sığdırınca **asıl karo nokta kadar küçüldü** → harita kaos.
  Geri alma: `git checkout -- TilePalette.asset ...` (kullanıcının 126 karoluk boyaması korundu).
- **Çözüm checklist'i (Blender):** Join (tek mesh) + Mesh > Clean Up > **Delete Loose** + **Ctrl+A All
  Transforms** + FBX export **-Z Forward / Y Up** + texture'ı embed etme. Önce TEK karo doğrula, sonra kalanı.
  Ayrıntılı hat: `Docs/TILE_PIPELINE.md`.
- **Footprint kalkanı — SİLME.** `TileFolderImporter`: footprint **> 50 birim → palete EKLEMEZ**
  ("ATLANDI — Blender'da düzelt" raporu); > 5 birim veya > 20 mesh → ekler ama uyarır. Yukarıdaki
  262-birimlik felaketin bir daha olmamasının tek sebebi bu kalkan.
- **Köprü FBX quaternion flip:** FBX baş-aşağı geldi; prefab YAML'ında child rotasyonu elle
  `(0.5,-0.5,0.5,0.5)` + `pos.y ≈ 0` yapılarak düzeltildi (180° dikey flip). İlk hâli ayrıca 10× büyük +
  Z-up + pivot tepedeydi.
- Karo FBX'lerinin doku yolları `C:\Users\zeynep\Downloads\*.jpg`'ye referans veriyor → dokular projede
  yok. "Karolar neden renksiz" sorusunun cevabı bu, shader değil.
- **Importer'ın `Overrides` tablosu YALNIZ İLK OLUŞTURMA varsayılanıdır.** `UpsertEntry` mevcut girişte
  sadece `prefab`'ı tazeler. Oraya "ad/renk/yürünürlük de tazelensin" diye geri koyma: 2026-08-04'e kadar
  öyleydi ve her "Klasörü Tara" tasarımcının Yürünür/Yürünmez ayarlarını **sessizce** geri alıyordu.
- **Palet girişini silmek tek başına KALICI DEĞİL.** İki üretici geri ekler: taranan klasörde
  (`Assets/Art/Models/Tiles/`, özyinelemeli) duran FBX → "Klasörü Tara"da; `deneme*` ve `portal*` →
  TAM KURULUM'un `EnsureCombatTestTiles` / `EnsurePortalTiles` döngüleri. Silerken ikisini de kes.
- **Palet ≠ harita.** Palette bir karonun olması onun haritada KULLANILDIĞI anlamına gelmez: 45 karoluk
  palette Bölüm 1 haritası yalnız 16'sını kullanıyor, kalanı eski 3×3 dünyanın. Tile Painter'daki
  "haritada N / bu haritada YOK" rozeti bunun içindir — yürünürlük ayarlamadan önce ona bak.
  (2026-08-04'ten beri o eski karolar varsayılan olarak gizli.)
- **"Harita bozuk göründü" demeden önce hangi TileMapSO'nun yüklü olduğuna bak.** `SetTileMap`
  çağıran her yol (eskiden Tile Painter'ın 9-harita düğmeleri) sahnedeki üretilen haritayı sessizce
  başka bir asset'le değiştirebilir. Grid'in `_tileMap`'i `Bolum1_Uretilen` değilse üretici suçsuzdur.
- **Tahta koordinatı ≠ dizi indisi.** Tahta **odd-r offset**: `Q = col - (row >> 1)`
  (`HexCoordinate.FromOffset`). Harita üreten/okuyan her yeni kod bu dönüşümü YAPMAK ZORUNDA;
  ham `(q, r)` yazmak haritayı satır satır kaydırır ve %26'sını sessizce yutar (2026-08-05).
  Sınama basit: **tahtanın kaç hücresi atama almıyor?** 0 değilse dönüşüm eksiktir.
- **Komşuluk tablosu da offset'e bağlıdır** (satır paritesi: `DirsEven`/`DirsOdd`). Düz axial tablo
  kullanan bir algoritma dikdörtgen tahtada kopuk nehir/delikli blob üretir — üstelik kendi
  "bağlantılıdır" kontrolünü de geçer, çünkü yanlış komşulukla ölçer.
- **Düğüm yerleşimi ERİŞİLEBİLİR bölgeye kısıtlı olmalı** (`ChapterMapGenerator.IsReachable`).
  Aksi halde zorunlu görev dağ ardındaki cebe düşer ve bölüm bitirilemez.

**Render / sahne**
- URP shader property **`_BaseColor`** (`_Color` DEĞİL). Tint her zaman `MaterialPropertyBlock` ile →
  paylaşılan materyal bozulmaz.
- `DefaultExecutionOrder`: `HexGridManager(-100)` → `FogOfWarManager(-50)` → `PlayerController(0)`.
- Hex karo mesh'i 3B prizma, `TileHeight = 0.3f`.
- Kapsül placeholder'ın `_heightOffset = 0.8` = TileHeight(0.3) + kapsül yarısı(0.45) + boşluk(0.05).
  Gerçek model bake edilince `_heightOffset = TileHeight` (yüzeye sıfır clearance) oldu.
- **Birim Y'si sabit ofset değil:** iki karonun `SurfaceHeight` değeri enterpole edilir. Eski `RaycastAll`
  yöntemi terk edildi — köprüde ayak güverte yerine **kemere biniyordu**.
- Sis bulutu yüksekliği `_fogLift`: 0.45 izometrik kamerada komşu karoları örtüyordu → **0.18**.

**Editor / kurulum**
- **`namespace TacticalRPG.Input` YASAK.** Böyle bir namespace açılırsa `TacticalRPG.*` altındaki her
  dosyada `Input.GetKeyDown(...)` ona çözülür, `UnityEngine.Input` gölgelenir ve proje derlenmez
  (bir kez denendi: 9 dosya birden kırıldı). `Input/` klasörüne sınıf eklerken namespace
  `TacticalRPG.Core` olmalı.
- **Tek CS0246 tüm editor assembly'sini düşürür** → `TacticalRPG` menüsü komple kaybolur (`d44b780`).
  Menü yoksa önce Console'a bak.
- **TAM KURULUM kullanıcı verisini KORUR:** `EssenceConfig`, `EssenceMap`, palet girişleri ve boyalı
  karolar üzerine yazılmaz (upsert / varsa-koru). Yeni asset oluşturan bir faz yazarken bu kuralı bozma.
- **Yeni faz eklediğinde `FullSetup` zincirine de ekle** — kullanıcı her geliştirmede tek tıkla temiz
  kurulum bekliyor.
- **IMGUI ↔ uGUI çakışması:** menü açıkken IMGUI HUD'lar uGUI'nin üstüne çizer → `MenuState` statik
  bayrağıyla gizlenir; tık sızmasını `EventSystem.IsPointerOverGameObject` + `ImguiBlocker` engeller.

**Süreç / git**
- Unity, `.controller` / TMP fallback / `.mat` dosyalarını kendiliğinden sık sık yeniden serileştirir →
  working tree "kendiliğinden" kirlenir. Auto-pull hook'unun kirli tree'de pull'u atlamasının sebebi bu.
- SessionStart hook'u çalışan tur içinde tetiklenemez; script'i temiz/kirli iki dalda pipe-test et.

---

## Tamamlanan fazlar (tek satır özet + commit)

> Ayrıntılı anlatım, dosya listeleri ve test akışları: **`Docs/DECISION_LOG_ARCHIVE.md`**.

| Faz | Ne getirdi | Commit |
|---|---|---|
| Faz 0-1 | Kamera/sahne kurulumu, 10×10 hex grid, A* hareket, FogOfWar, AP + zaman motoru, MapCollapse, DebugHUD | `5b943ed` → `093f08e` |
| Faz 2 / 2.2 | `CharacterClassData` + `CharacterCard` + öz + Kam mana; `KamAbilityData` SO'ları (3 büyü asseti) | `487b7b3`, `8821d52` |
| Faz 2.5 | Tile Painter (palet tabanlı karo boyama, tint + yürünebilirlik senkronu) | `8da509c`, `60f6d45` |
| Faz 3 | Yetenek dikey dilimi (Unit + AbilityCaster + test HUD) — sandbox, Faz A bunu temizledi | `f46adf4` |
| Faz A | Overworld ↔ savaş durum makinesi + görev alanları (+ savaşa giriş 1 AP) | `239f662`, `5fb7991` |
| Karo hattı | Köprü FBX pipeline + karo yüzey yüksekliği + kontur takibi | `285dd0c` |
| Faz B | Deployment akışı — öz ile birim yerleştirme, `Unit` ↔ `CharacterCard` bağı | `88d9b6d`, `c030fb5` |
| Faz C (C1-C3) | Düşman spawn + hıza göre initiative + tur tabanlı muharebe + win/lose | `bbf507e` |
| Faz C4 | Kam komutan + savaş büyüsü + sınıf renkleri | `6040b9c`, `b6125f8` |
| Faz D | Çok-tipli öz + haritadan toplama + tarifle birim üretme | `14301c9` |
| Faz D2 | Üretim savaşa taşındı, el yapımı öz haritası (Essence Painter), yakınlık istemi | `9087edb` |
| Cila | Savaş nameplate'leri (isim + bölümlü can barı + hasar flaşı) | `9311e52` |
| Karo hattı v2 | Tile Painter "Klasörü Tara" + 11 karo prefab + importer düzeltmeleri | `de55ab6`, `ce7be02` |
