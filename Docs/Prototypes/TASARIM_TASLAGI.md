# Yeni yön — çalışma taslağı, 18 Ekim 2026

Bu dosya ana GAME_DESIGN veya DECISION_LOG yerine geçmez. İstek, öneri ve çalışan prototipi ayırır. Eski oyundaki mekanikler ana sahneden kaldırılmadı; yeni yön ayrı savaş laboratuvarında deneniyor.

## Kullanıcının söylediği / onayladığı

- Haritada öz toplama ve savaş içinde karakter yaratma kaldırılacak; necromancer kimliği kullanılmayacak.
- Kendi dünyasının bölgelerinden, kendi dünyasının paralel karşılıklarına açılan trajik yolculuk tercih ediliyor. Bağımsız evren parodileri ana omurga olmayacak.
- Ölümde mabede dönüş ve geri alınabilir kayıp yönü onaylandı. Oranlar ve ikinci ölüm davranışı henüz onaylanmadı.
- İki küçük savaş yaklaşımı oynanarak karşılaştırılacak; henüz kazanan seçilmedi.
- Biriktirmeye faiz veren ekonomi isteniyor. Savaş öncesi satın alma ve savaş sonrası augment seçimi tasarlanacak.
- Hades benzeri farklı silah/büyü aileleri ve kombinasyonlar isteniyor. Augmentin kalıcı silahı geliştirmesi veya yeni seçenek açması önerildi; kalıcılık kuralları açık.
- Hikâye balonlarla ve sürekli uzun konuşmalarla anlatılmayacak. Yapılabilir yakın plan sahneler, çevresel anlatım ve oyuncuyu sıkmayan sıklık isteniyor.
- Haritaların yerel mekanikleri ve ekonomileri; ana ekonomiyle takas seçenekleri isteniyor.
- Eski sürüm ve geliştirme kayıtları ayrı korunacak. Modelciye görselli, küçük ve açık görevler verilecek.

## Öneri: silah kimliği kalıcı, kombinasyonlar değişken

Kalıcı açılan şey silah ailesi ve alternatif kullanım biçimi olsun; bütün güçlü augmentleri sürekli biriktirmek sınırsız güç ve denge sorunu yaratır. Aktif yolculuğun augmentleri mabede dönüşte korunabilir, yeni yolculuk başladığında yeniden seçilebilir. Bu, her ölümün bütün build'i silmemesini sağlar. **Henüz karar değil.**

Demo için önce iki aday: kısa menzilde yay çizen **Mühür Kılıcı** ve uzağa hat saldırısı/işaret bırakan **Yankı Asası**. Bunlar isim ve davranış taslağı; mevcut prototipte uygulanmadı. İkisinde de aynı işaret/çekim/patlatma dili kullanılabilir, fakat menzil ve konum kararları farklı olmalı.

Örnek geliştirme üçlüsü: işaretin ikinci hedefe sıçraması / isabet sonrası konum değiştiren saldırı / riskli yakın patlama. Yalnız +%5 hasar kartlarıyla çeşitlilik iddia etmeyelim. İlk seçimin sonraki kartlarla en az iki anlamlı birleşimi olmalı; rastgele üç alakasız kart yerine bir uyumlu, bir yön değiştiren, bir savunma seçeneği denenebilir.

**Maliyet sınırı:** savaş tipi seçilmeden altı silah ve her silaha ayrı animasyon paketi üretme. İlk seçilen savaş tipinde iki silah ailesi ve altı davranış değiştirici seçenek hedefi ayrıca değerlendirilecek.

## Eşya, zırh ve anlık pot — son kullanıcı isteği

Efe eşya, zırh ve anlık kullanılabilen potlar eklemek istiyor; modelci kapsamının bunu bilmesini istedi. Aşağıdaki yuva ve adetler öneridir; yeni prototipte uygulanmadı. Eski oyundaki envanter/giydirme kodunun varlığı yeni savaş sistemiyle uyumlu olduğu anlamına gelmez.

- Silah saldırı biçimini; tek zırh/tılsım yuvası savunma tarzını; iki tüketilebilir yuvası acil müdahaleyi taşısın.
- Demo için 2 zırh davranışı ve 3 tüketilebilir aday yeterli başlangıç: koruma karşılığı hareket/kaçınma bedeli; kırılgan ama mana verimli alternatif; iyileştirme, geçici kalkan, mana potu.
- Potlar sınırlı stoklu olsun. Taktik modda eylem bedeli, gerçek zamanda ortak kullanım bekleme süresi denenebilir. Bunlar iki moda birebir eşit sayı kopyalamak yerine zamanına uygun bedellerdir; pot basmak her hasarın zorunlu cevabı olmamalı.
- Pot alışverişi faizle rekabet eder; ücretsiz mabet yenilemesi + satma/tekrar alma gibi sonsuz para açığı oluşmamalı. Ölümde tüketilmiş potun geri gelip gelmeyeceği henüz belirlenmedi.
- Baş/gövde/kol/bacak başına ayrı ekipman ve görünüş, ilk demo için önerilmiyor. Önce bir slotun gerçekten karar yarattığını gösterelim.
- Modelci ilk aşamada mevcut iskelete göre silah ölçeğini, ortak şişe gövdesini ve üç farklı tıpa/siluet işaretini önerebilir. Yalnız renk farkına güvenme. Pot içme hareketi bütün şişelerde aynı tutuş noktasını kullanabilsin. Anında oyun etkisi uzun içme animasyonu gerektirmez.
- Zırhın karakterde görünmesi kesinleşmeden final skinned mesh siparişi verme. Tılsım/istatistik eşyası ikonla temsil edilebilir; her eşya için ayrı dünya modeli gerekmez. Rig, ağırlık boyama ve zırh kesişmeleri savaş yönü seçilince ayrı teslim olacak.

## Öneri: hikâyeyi eylem taşısın

İlk haritada oyuncu önce birini/yerleşimi korusun; dünyanın bütün kozmolojisini dinleyerek başlamasın. Boltzmann Beyni kavramı açılışta zorunlu açıklama yerine ileride bulunabilen kozmolojik yorum olabilir. Merak, açık bir ilk amaçla birlikte verilmeli.

İlk bölüm için başlangıç denemesi: açılış 15–25 saniye, ortada sonuç gösteren 20–30 saniye, finalde 30–45 saniye. Bunlar üretim hedefidir, araştırmayla kanıtlanmış ideal süreler değildir. Oynanabilir ilk hareketi erken ver; konuşmayı atlama ve altyazı sağla. Yakın planlar için 2–3 sabit kamera, bakış yönü, 3–4 tekrar kullanılabilir beden hareketi; yüz rig’i/lip-sync şart değil. Diyaloğu dinlemeyen oyuncu da sıradaki hedefi anlayabilsin.

Çevresel örnek: önce boş yemek masaları, sonra isimleri kazınmış sığınak taşları, finalde aynı isimlerle hareket eden birkaç kurtulan. Oyuncu sonucu görür. Her küçük keşfe kamera kesmesi ekleme. Ana sonuç sahneleri zorunlu olabilir; ayrıntılı geçmiş isteğe bağlı inceleme ve kısa konuşmalarda kalır.

## Ağaç: yeniden kullanmak için hikâyeyi zorlamadan

**Öneri:** mevcut büyük ağaç ilk haritanın yön bulma simgesi ve son sığınağı olabilir. Gövdesindeki canlı halkalar dünyanın belleğini tutar; düşmanlar bu sürekliliği bozmuştur. Kam finalde bütün evreni değil, sığınağın yaşayanlarını ve belleğin küçük bir bölümünü kurtarır. Bu küçük zafer görünür olmalı: sığınak canlanır, ağaçta yeni bir dal belirir, kurtulanlar yer değiştirir.

Bulgu: kurtarılan kayıtlarda kendi dünyasına ait, burada hiç yaşanmamış bir felaket görünür. Önce ikinci haritadaki kendi dünyasının gözlemevine gidip bunun kaynağını doğrular; paralel dünyalara geçiş daha sonra açılır. Böylece ilk final hemen konuyla ilgisiz bir evrene fırlatmaz.

Eski zorunlu görev kök zinciri yeni anlatının mecburi temeli değildir. Görsel ağacı kullanmak, eski karakter/öz toplama/kök görev mekaniklerini kabul etmek anlamına gelmez. Ağacın ve kullanılacak modellerin kökeni Efe'nin yeniden kullanım sınırıyla ayrıca eşleştirilmeli.

## Sekiz harita için omurga önerisi

Bu tablo üretim taahhüdü veya onaylanmış hikâye değildir. İlk üç harita kendi dünya bölgeleri; sonraki dört harita aynı dünyanın alternatif sonuçları; final eve dönüş. Her yerde aynı felaketin başka sonucunu görürüz.

| Harita | Oynanan amaç / hikâye sonucu | Tek baskın yerel kural | Ekonomi farkı önerisi |
|---|---|---|---|
| 1 · Son Sığınak | Ağacın çevresindeki yaşayanları koru; çelişkili dünya kaydını bul. | Mabet, ekip ve temel saldırı dili. Görev başarısı seçenekleri etkiler; başta kötü stat attı diye reset teşvik etme. | Ana para, hazırlık mağazası; ilk faiz denemesi. |
| 2 · Kırık Gözlemevi | Kaydın başka bir olasılıktan geldiğini doğrula; geçidi onar. | Görüş/konum ve yer değiştiren güvenli hatlar. | Yerel erişim hakkı; altına çevrilen sınırsız çift yönlü para olmasın. |
| 3 · Sönmüş Kıyı | Evde kalanları savun; geçişin bedelini üstlen. | Gelgit, aynı arenanın açılan/kapanan bölgeleri. | Erzakla kısa süreli destek arasında seçim. |
| 4 · Cam Çölü | Kendi dünyasının kuruduğu bir sonucu gör; felaketi durduran düzenek parçasını bul. | Isı ve gölge; özgün yaratık ve toplum tasarımı. | Su hakkı ile hizmet takası; gezip yerde öz toplamak yok. |
| 5 · Kızıl Yaşam | Yok oluşu durdurmak için her şeyi canlı tutan dünyanın bedelini çöz. | Can harcayan güçler ve erişilebilir geri kazanım. | Kan sözleşmeleri; gerçek para/can dönüşüm oranı net ve sınırlı. |
| 6 · Donmuş An | Hiçbir kaybı kabul etmeyen bir dünyanın hareketsiz kaldığını gör. | Sınırlı durdurma/erteleyerek çözme. | Zaman kredisi; bekleyerek üretilemez. |
| 7 · Aşırı Büyüyen Dünya | Yaşamın denetimsiz biçimde dünyayı boğduğu sonucu çöz. | Çevreyi kullanan dönüşüm ve yer açma. | Hizmet/sözleşme tercihi; ikinci bir hasat oyunu kurma. |
| 8 · Eve Dönüş | Kurtardıkların ve aldığın bedellerle dünyayı onar; neyin korunacağını seç. | Önceki iki kuralın kontrollü birleşimi; son anda yeni sistem öğretme. | Birikimi final hazırlığına harca; sonsuz faiz bekleme noktası yok. |

Her mapte ayrı savaş motoru, silah ağacı ve para yönetim ekranı yapılmayacak. Ortak çekirdek + bir yerel kural + sınırlı eşya varyasyonlarıyla çeşitlilik aranacak. Bu bir kapsam önerisidir.

## Ekonominin açıkları ve ilk deney kuralı

Öneri: `faiz = min(5, floor(eldeki ana para / 10))`, yalnız yeni bir anlamlı karşılaşmanın ilk tamamlanışında. Kontrol sırası ve bakiyenin ne zaman sabitlendiği UI'da gösterilmeli. Yeniden doğan düşmanlar, beklemek, turn bitirmek veya mabede dönmek faiz üretmemeli.

Faiz her zaman kazandırırsa harcamak cezaya dönüşür. İlk testte 20 parası olan oyuncunun 8 paraya alacağı gücün yaklaşan mücadelede görünür etkisi olmalı. Tüm mağazalarda sırf 50'yi korumak en iyi karar çıkarsa faiz tavanı/ödül ritmi değiştirilmeli.

Yerel parayla ana para takasında alış-satış oranı açık, döngüde kâr oluşmayan ve bölüm başına kotası olan örnek kullanılabilir. İki yönde farklı ücretler, güncellenen kur ve faiz birleşince arbitraj doğabilir: ana→yerel→ana dönüşü ve faiz tetik sırası test edilmeden yayınlanmamalı. Henüz kodlanmadı.

## Sıradaki ortak kararlar

### Sis hakkında son geri bildirim

Efe sisin varlığını mantıklı buluyor ama mevcut animasyonunu beğenmiyor. Öneri: ana haritada keşif örtüsünü koru; keşfedilmemiş / yumuşak siluetli sınır / kalıcı keşfedilmiş üç durumu kullan. Görsel durağana yakın ve aydınlık okunabilir olsun. Yakınlaşınca sert asset belirmesi olmamalı; örtü ve nesne görünürlüğü birlikte geçiş yapmalı. Siluetler gizli karşılaşmanın bütün içeriğini uzaktan açıklamamalı; yürünmez sınırlar ve rota kararları haksız sürpriz üretmemeli.

Savaş arenasında genel sis önerilmiyor; düşman hazırlığı ve kaçış alanı görünür kalsın. İleride bir haritaya özgü kısa ve açıkça haber verilen görüş etkisi ayrı değerlendirilebilir. Bu bir öneri; mevcut ana oyun sis kodu değiştirilmedi. A/B laboratuvarında sis yok.

1. Savaş A/B karşılaştırmasını oynayıp temel kontrol yaklaşımını seçmek.
2. İlk harita finalinde ağacın sığınağı/belleği fikrinin kabulü ve eski varlıkların kökeni.
3. Silah/augment kalıcılığı; ölümde kayıp miktarı ve geri alma sınırı.
4. Modelcinin rig/animasyon kapasitesi; ilk küçük setin sanat onayı.
5. Steamworks/mağaza durumu. Tam ilk harita hedefi ancak gerçek üretim hızına göre sabitlenecek.

**Çalışma adı önerisi:** KAM: Son Yankı. Efe henüz onaylamadı; proje/ürün adı değiştirilmedi, isim uygunluğu araştırılmadı.
