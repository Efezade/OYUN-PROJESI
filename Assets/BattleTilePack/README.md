# Boşluksuz altıgen savaş zemini — V2

## Kullanılacak varlıklar

- `Prefabs/SeamlessHexGround.prefab`: 2,000 m sivri uçlar arası, 1,7320508 m düz kenarlar arası, 0,30 m kalınlık; pivot alt-orta.
- `Prefabs/SeamlessHexGround_090cm.prefab`: 0,90 m toplam yükseklik.
- `Prefabs/SeamlessHexGround_150cm.prefab`: 1,50 m toplam yükseklik.
- Önceki `BasicBattleTile` ve `BasicBattleTile_FullFit_2m` prefabları da aynı doğru geometriye güncellendi.
- `Scenes/SeamlessGround_Flat.unity`: 19 karolu, düz birleşim denemesi.
- `Scenes/SeamlessGround_HeightStudy.unity`: 37 karolu, üç seviyeli zemin örneği.
- Önceki `BattleTile_Example.unity` artık düzeltilmiş düz sahneyi gösterir.

## Yan yana yerleştirme

Prefabı sahneye ekleyin. Inspector > Hex Ground Placement içindeki Q ve R tam sayı koordinatlarını değiştirin. Elevation 0/1/2, üst yüzeyi 0,30/0,90/1,50 metreye getirir. Elevation Step 0,60 m'dir ve değiştirilebilir. Gövde tabana kadar uzar; üst yüzey yalnız yukarı taşınır, kaplama gerilmez. Merkez ve altı kenar bağlantı noktası yeni yüzey yüksekliğini takip eder.

Elle sürükledikten sonra bileşen menüsünden **Snap current position to nearest hex** kullanın. Ayrı karolara farklı ölçek vermeyin. Izgara formülü:

`X = sqrt(3) * (Q + R/2)`
`Z = 1.5 * R`

Örneğin sağ komşu `(1.7320508, 0, 0)`, çapraz komşu `(0.8660254, 0, 1.5)` merkezindedir. 2 m köşe çapı, 2 m merkez aralığı demek değildir.

## Doku devamlılığı

Üst yüzeyler ortak **SeamlessGround_Top** materyalini kullanır. Shader, UV'yi karo başına sıfırlamaz: `world.xz / 4 + 0.5`. Böylece her taş ve bitki deseni komşu karoda aynı noktadan devam eder. Doku sınırlarında Mirror örnekleme kullanılır; geniş alanlarda desen tekrar eder. Materyal ölçeğini tüm karolarda ortak değiştirin.

Taş/bitki ayrıntıları bu zemin sürümünde albedo ve mikro kabartı gölgelendirmesidir. Oynanabilir yüzey düz tutulur; toplam 24 üçgenlik kapalı zemin gövdesi ve MeshCollider vardır. Ayrı üç boyutlu bitki, merdiven, kapı ve engeller sonraki aşamadır. Yükseltilerin yanları ortak dünya konumundan kaya dokusu alır.

## Ana oyuna aktarım

Unity 6000.2.14f1 / URP 17.2.0 ile doğrulandı. `SeamlessBattleGround.unitypackage` dosyasını **Assets > Import Package > Custom Package** ile alın. **Hazır prefabı kullanın.** Ana oyunun genel FBX tarayıcısı modelleri 1,90 m'ye küçültebildiğinden bu zemin için genel tarama işlemini kullanmayın. Ana oyunun TAM KURULUM ızgarası R=1 m'dir; bu prefab ona uygundur.

FBX `Use File Scale` açık, Scale Factor 1 olmalıdır. Üst ve yan mesh ayrıdır. Unity prefabı malzemeleri, çarpışmayı, yerleşim bileşenini ve bağlantıları içerir. Ana oyunun harita/veri dosyaları bu pakete dahil değildir.

Blender kaynak: `MODELLER/Savas-Karolari/BasicKaro/SeamlessV2/SeamlessHexGround.blend`. Modelin yanında üç sahne bulunur: tek karo, düz alan ve yükselti çalışması. Özgün model `Aktarim/BasicBattleHex.blend` altında korunur.
