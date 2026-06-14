Taktiksel RPG Projesi - Geliştirme Yol Haritası (28 Günlük Vertical Slice)
Bu doküman, hex-grid tabanlı taktiksel RPG projesinin geliştirme adımlarını içerir.
İş Dağılımı: Claude (Backend/Lojik) ve Efe (UI/Görsel Entegrasyon) şeklinde paralel ilerleyecektir.

HAFTA 1: Makro Harita, Keşif ve Zaman UI Entegrasyonu
Amaç: Ekranda çalışan bir harita, hareket eden bir karakter ve UI üzerinde dönen bir zaman çarkı görmek.

Gün 1-2 (Faz 1.1 & 1.2):

Backend: Hex Grid algoritması ve Savaş Sisi (Fog of War) veri yapısı.

UI: Geçici hex'lerin yerine 3D harita modellerinin konması ve karanlık/aydınlık materyallerin ayarlanması.

Gün 3-4 (Faz 1.3):

Backend: Tıklanan karoya A* ile yürüme ve Kule (Watchtower) sis kaldırma mekaniği.

UI: Kule 3D modellerinin haritaya yerleştirilmesi ve karakter yürüme animasyonlarının bağlanması.

Gün 5-6 (Faz 1.4):

Backend: AP (Aksiyon Puanı) ve Zaman Motoru (Her 3 AP = 1 Dilim, 6 Dilim = 1 Gün).

UI: Zaman Çarkı UI Entegrasyonu: Sol üstteki gün/saat çarkı tasarımının canvas'a eklenmesi ve koddaki zaman değişkenine göre bu çarkın UI üzerinde dönmesinin/güncellenmesinin sağlanması.

Gün 7 (Faz 1.5):

Backend: Harita Çöküşü (Kıyamet Sayacı) - 4. güne geçişte karoların silinmesi.

UI: Silinen karoların düşme animasyonları ve ekranın kızarması (Post-processing) gibi gerilim efektlerinin eklenmesi.

HAFTA 2: Veri Mimarisi, Envanter ve Menü UI Tasarımları
Amaç: Savaş veritabanını kurmak ve kitap şeklindeki envanter/karakter menülerini fonksiyonel hale getirmek.

Gün 8-9 (Faz 2.1):

Backend: Toplanan özlerin tutulduğu Envanter sistemi.

UI: Kitap Envanter Menüsü: M harfine veya butona basıldığında animasyonlu şekilde açılan Parşömen/Kitap menü UI'ının yapılması, öz miktarlarının ekrana yazdırılması.

Gün 10-11 (Faz 2.2):

Backend: Birlik (ScriptableObject) statları ve 1., 2., 3. seviye evrim/pasif özellikleri.

UI: Karakter Kartları UI: Kitap menüsündeki savaşçı kartlarının tasarımlarının UI'a eklenmesi ve seviye (1,2,3) ikonlarının kilit açılma durumlarının gösterilmesi.

Gün 12-14 (Faz 2.3):

Backend: Kam (Ana Karakter) savaş manası ve büyü açma/harcama altyapısı.

UI: Yetenek Ağacı UI: Kitabın skill tree sayfasının UI üzerinde oluşturulması, kilitli/açık yetenek butonlarının tasarımları ve tıklanabilirliklerinin koda bağlanması.

HAFTA 3: Taktiksel Savaş ve Çarpışma Arayüzleri
Amaç: Savaş alanına geçiş ve sıra tabanlı çarpışmanın arayüzlerini tamamlamak.

Gün 15-16 (Faz 3.1):

Backend: Savaş Alanı grid sistemi ve başlangıç karolarına asker (Spawn) çıkarma.

UI: Taktiksel savaş alanı modelleri, yerleştirme aşamasında beliren "Mavi/Kırmızı" onaylama grid görselleri ve "Savaşı Başlat" UI butonu.

Gün 17-18 (Faz 4.1):

Backend: İnisiyatif Kuyruğu (Turn Manager) ve hız hesaplaması.

UI: Sıra Kuyruğu UI: Ekranın üst kısmında savaşacak karakterlerin yüz portrelerinin sırayla dizilmesi ve sırası geçenin sağa kayması (Timeline UI).

Gün 19-21 (Faz 4.2):

Backend: Hareket, büyü kullanımı, mana düşüşü ve hasar hesaplama.

UI: Düşman can barları (HP Bar), Kam'ın Mana Barı, yetenek ikonları UI paneli ve hasar yiyen karakterin üzerinde beliren kırmızı sayı (Floating Text) efektleri.

HAFTA 4: Ganimet Ekranı ve Ana Menü Döngüsü
Amaç: Draft seçim arayüzü ve kalıcı ana menü UI'ının tamamlanması.

Gün 22-23 (Faz 5.1):

Backend: Savaş Sonu Draft (Ödül Seçimi) algoritması.

UI: Ganimet Seçim Ekranı: Savaş bitince ekrana gelen "3 Karttan Birini Seç" (Draft) UI tasarımının yapılması ve buton hover/click animasyonları.

Gün 24-25 (Faz 5.2):

Backend: Durum Kaydı (State Saving) ve haritaya geri dönüş.

UI: Savaş sahnesinden harita sahnesine geçerken kullanılan Yükleme Ekranı (Loading Screen) veya Fade In/Out siyah ekran geçiş animasyonları.

Gün 26-27 (Faz 5.3):

Backend: Kalıcı Meta-Öz verileri ve yeni karakter kilidi açma.

UI: Ana Menü UI: Oyunun ilk açıldığı Ana Menü tasarımı, kalıcı özlerin gösterildiği "Öz Deposu" paneli ve kilitli sınıfların (Mage, Rogue vb.) zincirli görsellerinin koda bağlanması.

Gün 28: Testler, arayüz cilalamaları, buton ses efektlerinin (UI Audio) eklenmesi ve Vertical Slice tamamlanması.
