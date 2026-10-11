# Bölgeler ve Kara Aşı çürümesi (Bölüm 1 — Kök Ahdi Ormanı)

> Tasarım belgesi (2026-10-10, Efe + Claude). Hikâye kaynağı: `../hikaye/Dokuz Hücre - Evren ve Hikâye Kitabı.md` §4-5.
> Karo listesi ve modelleme önceliği: `../MODELLER/Karolar/KARO_LISTESI.md`. Neden kayıtları: `DECISION_LOG.md`.
> **Sayılar örnektir**: denge hesapları çekirdek mekanikler bitince yapılacak.

## 1. Harita iskeleti: yarı sabit, Minecraft biyomu gibi

Efe'nin kararı: bölgelerin yeri her girişte değişebilir. Bölgeler Minecraft biyomları gibi olacak:
her bölgenin kendine özgü karoları var ve bunlar yan yana, bitişik bir alan halinde çıkıyor.

| Rol | Bölge | Yerleşim kuralı |
|---|---|---|
| Giriş | Yırtık Koru | Kıyıya yakın (kıyıdan 2-3 karo), rastgele yön |
| Merkez | Halka Köyü | Giriş ile finale eşit uzaklıkta, iç kesimde |
| Aşı ×3 | Fısıltı Bataklığı · Uyuyanlar Vadisi · Oyuk Tepeler | Kalan boşluklara "en uzak nokta" ile dağılır |
| Final | Bayterek'in Kalbi | Girişten yürüme mesafesi en uzak %12'lik dilim |
| Ara doku | Kök Ahdi Koruları (2 parça) | Boşlukları doldurur |

- Arazi seed'i (30'luk havuz) değişmedi. Bölge yerleşiminin **ayrı seed'i** var ve her koşuda rastgele seçiliyor.
  Aynı kıtada bölgeler başka yere düşüyor; havuzun oran ve bağlantı filtreleri geçerli kalıyor.
- Bölgenin yükseklik/nem bükmesi dağ, göl ve nehir yerleşiminden ÖNCE uygulanıyor. Bataklık bu sayede
  kendiliğinden alçak ve göllü, tepeler de sırtlı çıkıyor. Dağ/göl SAYILARI değişmiyor, yalnız yerleri kayıyor.
- Oyuncu Yırtık Koru'da doğuyor. Doğuş noktası giriş çekirdeği ile köy arasında, köye yakın taraf (%55).
  Ölçülen ortalama: köye 8 karo.
- Ölçüm (240 harita): erişilebilirlik bölgesiz üretime göre −0,07 puan. 240 haritanın hepsinde oyuncu giriş
  bölgesinde doğdu, 5 çürüme kaynağı ve 3 tekil karo kondu.

## 2. Bölge kuralları (oynanış)

Bölgeler yalnız görsel ve düşman olarak değil, **oynanış kuralıyla** da farklı. Kural `RegionSO`'da duruyor,
`RegionPresence` oyuncunun bölgesini izleyip uyguluyor.

| Bölge | Kural | Durum |
|---|---|---|
| Fısıltı Bataklığı | **Sis:** görüş −1 | ✅ kodda (`RegionSO._visionDelta`) |
| Hepsi | Bölgeye girince adı ve tek satırlık atmosferi ekranın üstünde görünür | ✅ kodda |
| Yırtık Koru | **Gözcü Kuzgun / Av birliği** (`RavenWatch`): 3 sabit mor merkezde savaş goblini hazır bekler. Mor sınırda aynı goblin fark eder; Kam'ın rotasını kesmez. 1,2 sn tepki/kaçış süresinden sonra merkezden takip başlar. Takipte kırmızı yakalanma (0,6 karo), sarı yakın takip (3 karo), yeşil iz kaybetme (6 karo) halkaları hareket eder; mor merkez sabittir. Takip hızı 2,275 m/sn, gerçek AP başına 0,8125 karo (temel 1,25 × 0,65); bonus/yenileme ilerletmez. Kaçış/köy/gün dönüşünde aynı merkeze normal 3,5 m/sn hızla yürür; dönüşte yakalamaz. İç halkada tepki süresi bittikten sonra Kam'a yaklaşır, gerçek Attack klibi, kamera yakınlaşması ve kararma ardından pusu açılır. Karşılaşma bitiş/iptali hareket/seyahat kilidini temizler. Savaş açan nöbet noktası ertesi güne kadar dinlenir. | ✅ kodda |
| Halka Köyü | Av birliği köye giremez (güvenli bölge). Karşılaşma/zindan yasağı ve tarif öğrenme: öneri | kısmen |
| Fısıltı Bataklığı | **Alaz ışıkları** (`WispLights`): 4 ışık, her gün göçer, sisin içinden de görünür. Üstüne basınca %55 hazine (bölüm özünden 2-4), %45 tuzak (3-5 karo öteye savrulma + 2 AP). İkisi önceden ayırt edilemez. Mandragora çığlığı: öneri | ✅ kodda |
| Uyuyanlar Vadisi | **Uyanış sayacı** (`SleeperWatch`): dev heykelinin dibinden geçen adım +2, iki karo öteden +1; her gün −3. 8'de en yakın dev uyanır, pusu savaşı (`Pusu_UyananDev`); o dev bir daha uyanmaz. Gözler sayaçla kızarır, vadide alt ortada sayaç görünür. Heykel sayısı 2'den azsa kural ekler. | ✅ kodda |
| Oyuk Tepeler | **Kovuk tünelleri** (`HollowTunnels`): kovuğun üstünde duran oyuncuya sağda panel açılır. 1 AP ile başka bir kovuğa anında çıkar; görmediği kovuk "bilinmeyen kovuk" diye listelenir. En az 3 kovuk olur. `agac_kovugu` artık Landmark (öz yok). Öz toplanınca kovuk kaybolmasın diye. Tapınak bulmacası: öneri | ✅ kodda |
| Bayterek'in Kalbi | Kökler her tur yeni hexleri kaplar | öneri (savaş arenası) |

## 3. Kara Aşı çürümesi: map çapında baskı

Efe: "evet yap". Kod: `CorruptionManager` + `CorruptionConfigSO`.

- **Kaynaklar:** Zar Yırtığı (Yırtık Koru, kalıcı) · 3 Kara Aşı (aşı bölgeleri, arınabilir) ·
  Bayterek'in Kalbi (kalıcı).
- **Her gün başı:** etkin kaynak yeni karolara yayılır (aşı 3/gün, kalıcı 2/gün, her gün +0,25 artar,
  2. günden başlar). Çürük karo %35 olasılıkla derinleşir: 1 damar · 2 çürük · 3 kararmış.
- **Görsel** (`CorruptionVisuals`): keşfedilmiş çürük karonun üstüne yarı saydam **mor damar katmanı**
  (damar + obsidyen leke dokusu, kademeyle koyulaşır) + karo hafif kararır; sisli karonun bulutu mor pusa kayar.
  İlk sürümde yalnız renk ÇARPMASI vardı ve Efe hiç mor görmedi: yeşil doku × mor = koyu gri. İkinci testte de
  görünmedi (katman soluktu, çürüme hep sisin içindeydi) → katman güçlendirildi, bulut pusu %85, HARİTA sekmesinde
  keşfedilmemiş çürük karo da mor pus olarak görünüyor, her gün başı "Kara Aşı yayıldı / yaklaşıyor" bildirimi.
- **Savaş (Kara Öz):** çürük karodan (ya da yanındaki savaş karosundan) girilen savaşta düşmanlara kademe 1/2/3 için
  +0/+1/+2 seviye; güçlenen düşmanın üstünde soğuk mor ışık; savaş açılırken uyarı yazısı.
- **Kıyamet bağı:** çökecek karo seçilirken kademe başına ×1,5 ağırlık var; kararmış karo 5,5 kat önce düşer.
  Böylece "karo silinmesi bir sisteme bağlı" (Efe 2026-09-02). Yakın/uzak havuz payı korunuyor.
- **Arınma:** zorunlu görev = aşı noktası. Bir aşı bölgesindeki zorunlu görev bitince o bölgenin kaynağı
  arınıyor: yayılma duruyor, çürük her gün 1 kademe geri çekiliyor (ilk kademe hemen).
- Kaba hesap (hiç arındırılmazsa): 7. gün ~%22, 14. gün ~%50 kara çürük. Play'de ayarlanacak.

## 4. Zorunlu görev ↔ hikâye

Mekanik aynen kaldı: başta 2 görev var, zaman ve ekonomiyle artıyor. Görevlerin YERİ hikâyeye bağlandı:

- İlk görevler, arınabilir 3 aşı kaynağından rastgele ikisinin dibine (kaynağa en yakın düzlük, en az 2 karo) konuyor.
- Gökten düşen yeni görev, sahipsiz bir aşı noktası varsa onun başına düşüyor. Mesafe bandı (adalet kuralı)
  geçerli; bant dışındaysa eski seçim işliyor. 3 aşı da sahiplenince sonraki görevler eskisi gibi düşüyor.
- Hangi aşı noktasının önce düşeceğini oyuncunun nerede dolaştığı belirliyor. Hikâyedeki "sırayı oyuncu seçer"
  böylece kendiliğinden oluyor.

## 5. Biyom görselleri, büyük yapılar ve görev kökleri (2026-10-11)

Yedi bölgenin varyantları, tüm aktif arazi kataloğu, 24 savaş karosu ve altı etkileşim
karosu dahil 127 karo görünüşü ile iki büyük yapı var. Gerçek mesh/UV ve pişirilmiş
renk dokuları kullanılır. Kule, pazar, kamp, mağara, görev ve bitmiş görev de modellenmiştir.
Kişisel çayır modelleri korunur; yeni set `Assets/Art/Models/Tiles/Biomes` ve
`Assets/Prefabs/Grid/Biomes` altında. Kaynak: `MODELLER/Biyom-Onizleme/biyom_tam_set_v007.blend`.
Kullanıcının kendi nihai çizim/model seti geldiğinde paletten tek tek değiştirilebilir.

- **Yırtık Koru:** solgun kuru zemin, çıplak dallar ve kırık koru.
- **Halka Köyü:** sıcak ahşap halkaları, fener yolları, dal evler; merkezde birkaç karoyu
  kaplayan tek dev kütük tabanı. Çevresinde iki halkalık köy açıklığı ayrılır.
- **Fısıltı Bataklığı:** koyu nemli zemin, su gözleri, saz ve yüzey kökleri.
- **Uyuyanlar Vadisi:** serin yosun, eğreltiler, Kodama yuvaları ve yatan taş dev.
- **Oyuk Tepeler:** kızıl toprak, kök sırtları, oyuk gövde geçitleri ve ruh tapınağı.
- **Bayterek'in Kalbi:** yoğun kök ağı, kehribar; yaklaşık 12 m Bayterek tek karoya
  küçültülmez. Bir halkalık geçilmez kök yakası ve dışarıda yürünebilir açıklık vardır.
- **Kök Ahdi Koruları:** yeşil çayır, ağaç kümeleri ve meyve bahçesiyle ara doku.

Tahta 36×34 kalır; büyük yapılar için yerel açıklıklar üretim sırasında açılır. Bölge
sınırlarında iki bölgenin zemin/kimlik renklerini taşıyan ince, düzensiz ve kenarı yumuşak
şeritler vardır. Tek mesh kullanılır; yalnız iki tarafı da keşfedilmiş kenarlar çizilir.
Boyut ve renk ayarları `BiomeBoundaries.asset` içindedir. Bölge giriş yazısı korunur.

`BiomeTerrainStyle.asset`, komşu karoların zemin ve bitki tonlarını ortak, ölçülü bir
palete yaklaştırır. Mesh vertex renkleri zemin/yaprak/taşı ayrı işaretler; su, ahşap,
bez ve fenerlerin kendi dokuları korunur. Sınırdaki altı komşudan ağırlıklı renk alınır:
ortak kenarda iki biyom, köşede üç biyom yumuşakça karışır. Dünya koordinatlı hesap
haritanın rastgele yerleşiminde ve aynalanan varyantlarda da çalışır; sabit yarım-karo
envanteri gerektirmez. Kişisel çayır kaynaklarına yazılmaz, yalnız oyun sırasında
geçici materyal uygulanır. Minimap aynı bölge paletini izler. Savaş zemini kendi
ortak tonunu kullanır; bölge sınır şeritleri savaşta görünmez.

Zorunlu görev açıldığında Bayterek'ten görev karosunun kenarına doğru kök **karo karo
büyür**. Büyümeye başlayan karo yürünemez olur. Görev bitince dal **görevden ağaca doğru
karo karo çekilir**; her parça sıfırlanınca o karodaki geçici engel kalkar. Paylaşılan
kök karosu, kullanan son dal da çekilene kadar kapalıdır. Görev karosu, oyuncunun
bulunduğu/adım attığı karo ve önemli yapıların erişimi korunur. Güvenli rota bulunamayan
durumda büyüme ertelenir ve oyuncu hareketi/harita değişimiyle tekrar denenir.

Kök engeli arazi yürünebilirliğinden ayrıdır: çekilen kök çökmüş karoyu geri açmaz;
karoyu geri getirmek de hâlâ etkin olan kökü kaldırmaz. Savaş arenasına kök taşınmaz,
overworld'e dönünce dal ilerlemesi ve engeller geri uygulanır. Minimap ve rota gösterimi
engelleri izler. Süre/kalınlık/materyal `BayterekRoots.asset` üzerinden ayarlanır.
Bu animasyon, mor Kara Aşı katmanının **gün gün arınmasından ayrı** çalışır.

Kurulum `TAM KURULUM` zincirine bağlıdır; var olan kullanıcı prefab ve ayarlarını ezmez.
`BiomeWorldQA.Run` yalnız `.biome-qa-copy` işaretli ayrı proje kopyasında çalıştırılır.

## 6. Açık sorular / sıradaki adaylar

- Dört bölge kuralı kodda; Play'de ayarlanacak sayılar: kuzgun halkası/av hızı, Alaz hazine oranı, uyanış eşiği, tünel bedeli.
- Pusu düşmanları geçici (Goblin/Yamyam): hikâyedeki Av Geyiği, Diken Kurdu, Ulu Kayın modelleri gelince değişecek.
- Kirli öz / temiz öz (hikâye §3): çürük karodan toplanan öz güçlü ama yan etkili olmalı mı?
- Savaş arenası bölgeye göre (bataklık arenası, kök arenası): arena zemini şu an tek tip.
- Köksüzler kampı: haritanın kenarında her gün yer değiştiren gezici hex (Taro).
- Sınır: orman evreninde kıta dışı deniz mi kalsın, "Zar sisi" mi olsun?
