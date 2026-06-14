Taktiksel RPG Projesi - Geliştirme Yol Haritası (28 Günlük Vertical Slice)
Bu doküman, "For The King" ve "XCOM" tarzı, hex-grid tabanlı taktiksel RPG projesinin geliştirme adımlarını içerir.
Geliştirme Felsefesi (Whiteboxing): Kodlamalar sırasında (Claude tarafından) kesinlikle nihai UI veya karmaşık shader/tasarım kullanılmayacaktır. Tüm görsel objeler Unity'nin varsayılan 3D nesneleri (küp, silindir) olacak ve her referans [SerializeField] ile Inspector'a bırakılacaktır. Tasarım entegrasyonu manuel yapılacaktır.

HAFTA 1: Makro Harita ve Keşif Altyapısı
Amaç: Ekranda çalışan bir harita, hareket eden bir karakter ve işleyen bir zaman motoru görmek.

Gün 1-2 (Faz 1.1 & 1.2): Hex Grid matematik algoritmasının kurulması ve Savaş Sisi (Fog of War) sisteminin entegre edilmesi.

Gün 3-4 (Faz 1.3): Tıklanan karoya A* (Pathfinding) ile yürüme mekaniği. Haritadaki Kule (Watchtower) yapısına girildiğinde bölgenin sisinin kalıcı olarak kaldırılması.

Gün 5-6 (Faz 1.4): AP (Aksiyon Puanı) ve Zaman Motoru. Her hareket = 1 AP. Her 3 AP = 1 Zaman Dilimi. 6 Dilim = 1 Gün. (Zaman sayacının kodlanması).

Gün 7 (Faz 1.5): Harita Çöküşü (Kıyamet Sayacı). 3. günün sonuna gelindiğinde (4. güne geçiş) haritadaki rastgele karoların silinmesi ve ilerlemenin ölümcül hale gelmesi.

HAFTA 2: Veri Mimarisi, Ekonomi ve Evrim Sistemi
Amaç: Savaş ve meta-gelişim için gerekli olan "Öz" (Essence) ve Karakter statlarının veritabanını inşa etmek.

Gün 8-9 (Faz 2.1): Envanter sistemi. Toplanan kaynakları (Ateş, Su, Mavi Öz, Pot vb.) tutan veri yapısı.

Gün 10-11 (Faz 2.2): Birlik ScriptableObject şablonlarının oluşturulması. Askerlerin (Rogue, Mage vb.) statlarını ve öz harcanarak açılan 1., 2. ve 3. seviye evrim/pasif özelliklerini tutan sistem.

Gün 12-14 (Faz 2.3): Kam (Ana Karakter) Skill Tree ve Mana Sistemi. Ana karakterin 10'luk savaş mana havuzu ve özlerle kilitleri açılan büyülerin (Alev Topu vb.) veri altyapısı.

HAFTA 3: Taktiksel Savaş ve Kurulum (Deployment)
Amaç: Makro haritadan izole edilmiş mikro grid savaş alanına geçiş ve sıra tabanlı çarpışma.

Gün 15-16 (Faz 3.1): Savaş Alanı (Battlefield) grid sisteminin ve engellerinin (Obstacles) kurulması. Oyuncunun öz harcayarak seçtiği askerleri başlangıç karolarına yerleştirmesi (Spawn mekaniği).

Gün 17-18 (Faz 4.1): İnisiyatif Kuyruğu (Turn Manager). Sahadaki karakterlerin hızlarına göre sıraya girmesi.

Gün 19-21 (Faz 4.2): Savaş mekanikleri. Karakter hareketi, menzil kontrolü, Kam'ın mana harcayarak yetenek kullanması, hasar hesaplama ve HP'si biten karakterin sahadan silinmesi.

HAFTA 4: Ganimet, Döngü ve Meta-İlerleme
Amaç: Savaş sonu ödüllerinin alınması, makro haritaya dönüş ve genel "Game Loop" yapısının tamamlanması.

Gün 22-23 (Faz 5.1): Savaş Sonu Drafting Sistemi. Kazanılan savaşın ardından rastgele 3 ödülün (Öz, Pot, Eşya) oyuncuya seçtirilmesi.

Gün 24-25 (Faz 5.2): Durum Kaydı (State Saving). Savaştan makro haritaya dönüldüğünde zamanın, AP'nin, sisin ve karakterin koordinatının sıfırlanmadan kaldığı yerden devam etmesi.

Gün 26-27 (Faz 5.3): Ana Menü Meta-İlerleme. Ölen/kaybeden oyuncunun kazandığı kalıcı (Meta) özlerin ana menüye taşınması ve bu özlerle yeni karakter sınıflarının kilitlerinin kalıcı olarak açılması (PlayerPrefs/JSON kaydı).

Gün 28: Dikey Kesit (Vertical Slice) Testi, hata ayıklama (Bug fixing) ve oyun hissini (Game Feel) cilalama.
