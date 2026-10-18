# Yeni yön — çalışma taslağı, 18 Ekim 2026

Bu dosya ana GAME_DESIGN veya DECISION_LOG yerine geçmez. İstek, öneri ve çalışan prototipi ayırır. Eski oyundaki mekanikler ana sahneden kaldırılmadı; yeni yön ayrı savaş laboratuvarında deneniyor.

## Kullanıcının söylediği / onayladığı

- Haritada öz toplama ve savaş içinde karakter yaratma kaldırılacak; necromancer kimliği kullanılmayacak.
- Kendi dünyasının bölgelerinden, kendi dünyasının paralel karşılıklarına açılan trajik yolculuk tercih ediliyor. Bağımsız evren parodileri ana omurga olmayacak.
- Ölümde mabede dönüş ve geri alınabilir kayıp yönü onaylandı. Oranlar ve ikinci ölüm davranışı henüz onaylanmadı.
- **18 Ekim kullanıcı A/B geri bildirimi: B (gerçek zamanlı Kam + destek ekibi) kesin olarak tercih edildi.** A karşılaştırma referansı olarak korunabilir; yeni savaş geliştirmesi B üzerinden tasarlanacak. Bu, ana kampanyanın dönüştürüldüğü anlamına gelmez.
- Biriktirmeye faiz veren ekonomi isteniyor. Savaş öncesi satın alma ve savaş sonrası augment seçimi tasarlanacak.
- Hades benzeri farklı silah/büyü aileleri ve kombinasyonlar isteniyor. Augmentin kalıcı silahı geliştirmesi veya yeni seçenek açması önerildi; kalıcılık kuralları açık.
- Hikâye balonlarla ve sürekli uzun konuşmalarla anlatılmayacak. Yapılabilir yakın plan sahneler, çevresel anlatım ve oyuncuyu sıkmayan sıklık isteniyor.
- Haritaların yerel mekanikleri ve ekonomileri; ana ekonomiyle takas seçenekleri isteniyor.
- Eski sürüm ve geliştirme kayıtları ayrı korunacak. Modelciye görselli, küçük ve açık görevler verilecek.

## 18 Ekim son uygulama — B seferi oynanabilir

Efe büyük engebeli arena, savaş içi seçim ve kombinasyonların savaşlar arasında korunmasını onayladı. Bu kapsam **ayrı prototipte kodlandı**: üç bağlı radius-7 alan, gerçek rampalı yükseklik/siper/su/çamur, yedi iki seviyeli güç, tohumlu teklifler/düşman bileşimleri, mabet molası ve ayrı disk kaydı. Başlatma: `Tools/SavasPrototipleri/B_SEFER_BASLAT.cmd`. Tam çalışan kurallar ve sınırlar: `B_EXPEDITION.md`.

Aşağıdaki önceki tasarım ölçüleri tarihsel öneriler olarak korunuyor; “uygulanmadı” ifadesiyle çelişki olduğunda güncel uygulama kaynağı yukarıdaki dosyadır. Yeni silah aileleri, satın alma/faiz, zırh/pot, hikâye ve tam demo hâlâ uygulanmış sayılmaz. Savaşlar arasında korunma artık bu sefer için kesin; yeni seferler, meta gelişim ve tüm sekiz harita arasındaki kalıcılık henüz kesin değil.

## Öneri: silah kimliği kalıcı, kombinasyonlar değişken

### B yönü: savaş içinde büyüyen kombinasyon + anlamlı arazi

Efe B'de anlık geliştirme seçimleri, daha büyük savaş haritası, engebeler ve avantaj/dezavantaj karoları önerdi. Ardından Vampire Survivors'daki gelişim hissinin bu yaklaşıma daha uygun olduğunu söyledi. Bunlar yönlendirmedir; aşağıdaki ölçüler, ödül ritmi ve örnek yetenekler tasarım önerisidir, uygulanmadı.

**Hedef deneyim:** sade bir silahla başla → savaşta ilerledikçe seçenek kazan → seçtiğin davranışı yeni etkilerle birleştir → baskı artarken kendi kurduğun güçlü kombinasyonla alanı yönet. Hades beğenisindeki farklı başlangıç silahı kimliği ile savaş içinde büyüyen kombinasyon birbirini tamamlayabilir; başka oyunun bütün içerik sistemini kopyalamak hedef değil.

İlk B devam prototipi için tek silah, üç gelişim kolu, 4–6 dakikalık bir savaş ve üç belirgin seçim anı öneriliyor. Savaş süresi henüz bütün oyunun standardı değil; her küçük karşılaşmayı uzun hayatta kalma seansına çevirmek harita/hikâye temposunu bozar. Savaş ilerlemesi ilk testte doğrudan ilerleme sayacına yazılsın; ana haritada öz toplama geri gelmeyecek, savaşta yere dökülen XP toplama da ayrıca karar verilmeden eklenmeyecek.

- Her eşikte savaş tamamen durur; üç anlaşılır seçenekten biri seçilir, sonra devam eder. Seçim beklerken hasar veya düşman hareketi sürmez. Sık menü kesintisi yaratmamak için eşikler çatışmanın ritmine bağlanır.
- Örnek kol: tek hedef yıldırım → ikinci hedefe sıçrama → işaretlileri bağlayan zincir → ıslak zemindeki düşmanlara yayılma. Alternatifler aynı anda hasar yarışı değil: dar koridorda delme, açık alanda çevresel koruma, uzakta sıçrama.
- Seçimlerin tamamen rastgele ilgisiz gelmesini önlemek için bir mevcut yönü destekleyen, bir yön değiştiren ve bir savunma/konum seçeneği denenebilir. Seçimlerin yanında gerçekten değişen davranış açıkça gösterilir.
- Kalıcılık önerisi (son tempo geri bildirimiyle güncellendi): seçilen ana silah dalı ve kombinasyonlar bağlantılı mücadelelerden oluşan sefer boyunca korunur; yalnız kısa süreli alan etkileri/kalkan gibi açıkça geçici bonuslar biter. Her kısa arenada bütün build sıfırlanmaz. Ölüm, mabet, sefer ve haritalar arası korunma ayrıca kararlaştırılacak; sınırsız üst üste güç birikimi kabul edilmiş değil.

**Arena deneyi:** mevcut arenanın yaklaşık iki katı yürünebilir alanıyla başla; çapı ikiye katlamak yaklaşık dört kat alan üretir, ilk adım için gereksiz olabilir. Kamera uzaklaştırılarak karakterler küçültülmesin; Kam'ı takip eden görünüş, yaklaşan düşmanları haber veren işaretler ve okunabilir saldırı hazırlıkları denensin. Harita üç bağlı mücadele alanı içersin, her alanın en az iki kaçış rotası olsun. Büyük boş yürüyüş alanı eklemek amaç değil. Düşman yoğunluğu ve oyuncunun anlamlı bir eylem yapmadan geçirdiği süre ölçülsün.

İlk arazi seti dört okunur işlevle sınırlı:

| Arazi | Oyuncuya fırsat | Bedel / karşı oyun |
|---|---|---|
| Kırık sütun/duvar | Mermiden saklanma, görüş kesme | Geri kaçışı daraltır; düşman da aynı görüş kuralından yararlanır. |
| Dar geçit | Düşmanları delici/alan saldırısına hizalama | Arkadan gelen baskıda sıkışma; ikinci çıkış şart. |
| Sığ ıslak zemin | Elektrik birleşimini güçlendirme | Tehlikeli elektrik etkisi Kam'ı da tehdit edebilir; etki seçilen kuralla açıkça gösterilmeli. |
| Rampalı yükselti | Menzilli silah için görüş/konum fırsatı | Ulaşma ve geri çekilme bedeli; görünmez mutlak güvenli nokta olmamalı. |

Her karoya ayrı gizli yüzde bonusu koyma. Zemin şekli, efekt ve kısa açıklama aynı işlevi anlatmalı. İlk aşamada gerçek zıplama/tırmanma yok; yükseltiler rampayla erişilir. Yüksekliğin görüş/mermi/yol bulma kuralları tutarlı değilse önce düz zeminde engel ve geçitleri doğrula. Ekip tehlikede takılmamalı veya Kam'dan kopmamalı; takip/toplanma ve tehlikeden kaçış davranışı arena büyütmenin parçasıdır.

**Kabul:** üç seçim sonrası başlangıç saldırısından gözle görülür farklı davranış; en az iki işe yarar kombinasyon; bir bölgede yararlı seçimin başka bölgede belirgin bedeli; sonsuz çember çizerek kaçmanın ve rampada dokunulmaz kalmanın baskın çözüm olmaması; çevrenin savaş alanını kapatmaması. Bunlar test ölçütleri, başarılı sonuç iddiası değil.

**Modelciye yeni sıra önerisi:** mevcut setten kırık sütun/duvar, iki çıkışlı geçit, sığ zemin varyantı ve modüler rampa/yükselti için kaba hacim örnekleri. Kam ölçeği ve gerçek oyun kamerasında okunurluk görünsün. Final doku, büyük harita ve efekt üretimi bu B arena denemesinden sonra. Eski v001 görsel föyündeki "savaş yönü seçimi bekleniyor" notu artık tarihsel; silah/rig ve arazi kuralları yine henüz kesinleşmiş değil.

### Tempo ve bölüm yapısı — açık tasarım sorusu

Efe, Survivors/Megabonk benzeri büyümenin oyunu fazla hızlandırmasından ve bölüm bölüm tasarımla uyuşmamasından endişeli. Kesintisiz büyüyen bir kombinasyonu kısa savaşlar sonunda sıfırlamak bu kaygıyı artırır. Yukarıdaki 4–6 dakika yalnız dar prototip ölçüsüdür; ana oyunun her karşılaşması veya bölüm uzunluğu olarak onaylanmadı.

Önerilen sonraki akış deneyi: 20–30 dakikalık bir seferde üç bağlantılı mücadele alanı; aralarda kısa keşif/hazırlık ve güvenli mabet. Aynı kombinasyon taşınır, alanlar farklı hedef ve arazi ister, finale doğru güçlü bir kullanım anı oluşur. Süre deneme hedefidir, kesin tasarım değil. Sefer terimi yeni bir zorunlu UI katmanı değil; test akışını tanımlar. Mevcut demo hedefi en az 30 dakika ve kompakt ilk harita olarak kalır. Sekiz harita, sefer sayısı ve 15+ saat hedefi bundan otomatik çıkmaz; gerçek içerik/oynanış ölçümü gerekir.

Hız; daha fazla düşman ve parçacıkla sürekli artırılmak zorunda değil. Baskı → rahatlama → seçim → yeni tehdit ritmi; daha çok hedefe etki ederken düşman hazırlıklarını okunur tutmak; mağaza/hikâyeyi güvenli anlara koymak denenmeli. Kaydedip çıkabilmek ile ölüm cezası ayrı kurallar olarak ele alınmalı. Mabede dönüş kararı korunuyor; geri alınabilir kayıp ve build'in ölümde korunması henüz ayrıntılanmadı.

Kaynak karşılaştırması: geliştiricinin [Vampire Survivors Adventures açıklaması](https://poncle.games/adventures-faq), belirli kazanma koşulları olan bölümlü mini hikâye kampanyaları tarif ediyor. Bu, bölümlemenin türle bağdaşabildiğine örnektir; bizim oyun için aynı sıfırlama kurallarının doğru olduğunu kanıtlamaz. [Megabonk resmi Steam açıklaması](https://store.steampowered.com/app/3405340/Megabonk/) rastgele geliştirme seçimleriyle run içinde büyümeyi tarif ediyor. Her iki oyunun bütün modlarının süresiz olduğu varsayılmadı.

Kalıcı açılan şey silah ailesi ve alternatif kullanım biçimi olsun; bütün güçlü augmentleri sürekli biriktirmek sınırsız güç ve denge sorunu yaratır. Aktif yolculuğun augmentleri mabede dönüşte korunabilir, yeni yolculuk başladığında yeniden seçilebilir. Bu, her ölümün bütün build'i silmemesini sağlar. **Henüz karar değil.**

Demo için önce iki aday: kısa menzilde yay çizen **Mühür Kılıcı** ve uzağa hat saldırısı/işaret bırakan **Yankı Asası**. Bunlar isim ve davranış taslağı; mevcut prototipte uygulanmadı. İkisinde de aynı işaret/çekim/patlatma dili kullanılabilir, fakat menzil ve konum kararları farklı olmalı.

Örnek geliştirme üçlüsü: işaretin ikinci hedefe sıçraması / isabet sonrası konum değiştiren saldırı / riskli yakın patlama. Yalnız +%5 hasar kartlarıyla çeşitlilik iddia etmeyelim. İlk seçimin sonraki kartlarla en az iki anlamlı birleşimi olmalı; rastgele üç alakasız kart yerine bir uyumlu, bir yön değiştiren, bir savunma seçeneği denenebilir.

**Maliyet sınırı:** B seçildi; yine de altı silah ve her silaha ayrı animasyon paketi üretme. Önce B'de bir silahın dallanmasını kanıtla; demo için iki silah ailesi ve altı davranış değiştirici seçenek hedefi ayrıca değerlendirilecek.

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

1. **B seferi hazır.** Sonraki ortak adım: yeni prototipte güçlenme hissi, arazi kararı, ekip katkısı ve tempo için Efe'nin oynanış geri bildirimi. Savaşlar arası güç koruma uygulanıyor.
2. İlk harita finalinde ağacın sığınağı/belleği fikrinin kabulü ve eski varlıkların kökeni.
3. Silah/augment kalıcılığı; ölümde kayıp miktarı ve geri alma sınırı.
4. Modelcinin rig/animasyon kapasitesi; ilk küçük setin sanat onayı.
5. Steamworks/mağaza durumu. Tam ilk harita hedefi ancak gerçek üretim hızına göre sabitlenecek.

**Çalışma adı önerisi:** KAM: Son Yankı. Efe henüz onaylamadı; proje/ürün adı değiştirilmedi, isim uygunluğu araştırılmadı.
