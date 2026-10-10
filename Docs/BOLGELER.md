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
| Yırtık Koru | **Gözcü Kuzgun:** görüş alanına girersen av birlikleri peşine düşer | öneri |
| Halka Köyü | Güvenli bölge: karşılaşma/zindan yok, dinlenme + tarif öğrenme | öneri |
| Fısıltı Bataklığı | **Alazlar** her gün yer değiştirir (hazine ya da tuzak); Mandragora sökülünce çığlık atar ve yakındaki düşmanları uyandırır | öneri |
| Uyuyanlar Vadisi | **Uyanış sayacı:** uyuyan dev heykellerinin yanından geçtikçe dolar | öneri |
| Oyuk Tepeler | **Kovuk tünelleri:** bir kovuktan girip diğerinden çık · tapınak = karo üstü bulmaca | öneri (`agac_kovugu` karosu hazır) |
| Bayterek'in Kalbi | Kökler her tur yeni hexleri kaplar | öneri (savaş arenası) |

## 3. Kara Aşı çürümesi: map çapında baskı

Efe: "evet yap". Kod: `CorruptionManager` + `CorruptionConfigSO`.

- **Kaynaklar:** Zar Yırtığı (Yırtık Koru, kalıcı) · 3 Kara Aşı (aşı bölgeleri, arınabilir) ·
  Bayterek'in Kalbi (kalıcı).
- **Her gün başı:** etkin kaynak yeni karolara yayılır (aşı 3/gün, kalıcı 2/gün, her gün +0,25 artar,
  2. günden başlar). Çürük karo %35 olasılıkla derinleşir: 1 damar · 2 çürük · 3 kararmış.
- **Görsel:** karo mora çalar (üst renk katmanı), sisli karonun bulutu mor pusa kayar. Oyuncu çürümenin
  nereye yayıldığını uzaktan görür ama karonun ne olduğunu görmez. HARİTA sekmesinde de görünüyor.
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

- Önerilen bölge kurallarından hangileri, hangi sırayla? (Kuzgun, Alaz, uyanış sayacı, kovuk tüneli)
- Çürümenin savaşa etkisi: kararmış karodan girilen savaşta düşman Kara Öz bonusu almalı mı?
- Kirli öz / temiz öz (hikâye §3): çürük karodan toplanan öz güçlü ama yan etkili olmalı mı?
- Savaş arenası bölgeye göre (bataklık arenası, kök arenası): arena zemini şu an tek tip.
- Köksüzler kampı: haritanın kenarında her gün yer değiştiren gezici hex (Taro).
- Sınır: orman evreninde kıta dışı deniz mi kalsın, "Zar sisi" mi olsun?
