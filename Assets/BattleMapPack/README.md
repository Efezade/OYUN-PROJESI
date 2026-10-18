# Savaş haritası — devam eden sahne

Aktif sahne: `Assets/BattleMapPack/Scenes/BattleMap_WorkInProgress.unity`.

Bu sahne boşluksuz altıgen zeminin üç seviyeli örneğinden başlatıldı. Zeynep'in gönderdiği sarmaşıklı taş kemer ilk üst yapı olarak girişe eklendi. Sonraki assetler bu sahneye eklenir. Düz zemin ve yükselti test sahneleri ayrı durur.

## Giriş kapısı

- Prefab: `EntranceGate/Prefabs/EntranceGate.prefab`.
- Yaklaşık ölçüler: 3,95 m genişlik × 4,20 m yükseklik × 1,28 m derinlik; alt-orta pivot.
- 500.000 üçgenlik kaynak, 48.000 / 18.000 / 6.000 üçgen LOD'lara ayrıldı. Unity uzaklığa göre seçer.
- Sarmaşık ve yapraklar modelin gerçek geometrisidir. Kaynağın shaded dokusu URP/Lit malzemesine bağlıdır.
- Giriş konumu Q=0, R=-3 merkezinde: `(-2.598076, 0.30, -4.50)`, Y açısı 180°.
- Ayaklar alçak platforma oturur. Dört ek karo giriş yaklaşımını destekler; toplam 41 zemin karosu vardır.
- Taş kısımlar için yedi basit collider kullanılır. Orta geçit boş bırakıldı; 1,80 m boy ve 0,60 m genişlik zarfı merkez boyunca 41 noktada çarpışmasız doğrulandı. Bu, tam karakter hareketi/navmesh testi değildir.
- `OutsideSpawn` ve `InsideArrival` boş bağlantı noktaları sonraki oynanış kurulumunda kullanılabilir.

`Savas Karosu > Savas Haritasini Ac` bu sahneyi açar. Kapı düzenlenebilir prefabdır; konumu/ölçeği Inspector'dan değiştirilebilir. Sonraki assetlerde mevcut sahne üzerinde devam edin. Tam zemin kurulumunu tekrar çalıştırmak bu harita dosyasını yeniden oluşturmaz.

Aktarım paketi `MODELLER/Savas-Assetleri/Giris-Kapisi/BattleMap_WorkInProgress.unitypackage`; ana oyuna hazır sahne/prefab olarak aktarılabilir. Henüz ana oyunun haritasına/oynanışına bağlanmadı.

Blender kaynak: `MODELLER/Savas-Assetleri/Giris-Kapisi/Aktarim/GirisKapisi.blend`. Özgün ZIP içeriği `Kaynak/` altında korunur. Modelle ilgili karar ve devam durumu yanındaki `DEVAM.md` dosyasındadır.
