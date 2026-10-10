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

## 5. Açık sorular / sıradaki adaylar

- Dört bölge kuralı kodda; Play'de ayarlanacak sayılar: kuzgun halkası/av hızı, Alaz hazine oranı, uyanış eşiği, tünel bedeli.
- Pusu düşmanları geçici (Goblin/Yamyam): hikâyedeki Av Geyiği, Diken Kurdu, Ulu Kayın modelleri gelince değişecek.
- Kirli öz / temiz öz (hikâye §3): çürük karodan toplanan öz güçlü ama yan etkili olmalı mı?
- Savaş arenası bölgeye göre (bataklık arenası, kök arenası): arena zemini şu an tek tip.
- Köksüzler kampı: haritanın kenarında her gün yer değiştiren gezici hex (Taro).
- Sınır: orman evreninde kıta dışı deniz mi kalsın, "Zar sisi" mi olsun?
