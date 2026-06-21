# Hex Karo Tasarım Kılavuzu

Merhaba! Bu kılavuz, oyunumuz için **altıgen (hexagon) zemin karoları** tasarlaman için.
Aşağıdaki ölçülere dikkat edersen karolar oyunda birbirine kusursuz oturur.
Acele etme, adım adım gidelim. 🙂

---

## 1. Altıgenin Şekli ve Ölçüleri (önce şuna bak)

Karomuz **sivri ucu yukarı/aşağı bakan** bir altıgen. Yani üstte ve altta birer
sivri köşe var, sağ ve sol tarafta düz kenarlar var. Şöyle görünüyor:

```
                  ▲   <- üst sivri uç
                ╱   ╲
              ╱       ╲
            ╱           ╲          ┬
          │               │        │
          │               │        │
   sol    │       ●       │  sağ   │   1.90 m   (yukarıdan aşağıya,
   düz    │     merkez    │  düz   │           sivri uçtan sivri uca)
   kenar  │               │  kenar │        │
          │               │        │
            ╲           ╱          │
              ╲       ╱            │
                ╲   ╱              ┴
                  ▼   <- alt sivri uç

          ├───────────────┤
               1.645 m       (soldan sağa, düz kenardan düz kenara)
```

Yandan baktığımızda karonun bir de **kalınlığı** var (bir pasta dilimi gibi):

```
   ┌─────────────────────┐   ┬
   │                     │   │  0.30 m  (kalınlık / yükseklik)
   └─────────────────────┘   ┴
   ▲ alt yüz (zemine oturur)
```

### Tek bakışta tüm ölçüler

| Ne | Ölçü |
|---|---|
| Yukarıdan aşağıya (sivri uçtan sivri uca) | **1.90 m** |
| Soldan sağa (düz kenardan düz kenara) | **1.645 m** |
| Bir kenarın uzunluğu | **0.95 m** |
| Kalınlık (yükseklik) | **0.30 m** |

> Birim olarak **metre** kullanıyoruz. Birazdan Blender'ı metreye ayarlayacağız;
> sonra bu sayıları olduğu gibi gireceksin. Karmaşık matematik yok. 👍

---

## 2. Blender'da Sıfırdan Altıgen Yapımı (adım adım)

İlk karoyu birlikte yapalım. Sadece şu adımları sırayla uygula:

### Adım 0 — Blender'ı metreye ayarla (bir kez)
1. Sağdaki menülerden **Scene Properties** (koni+top+kamera ikonu) sekmesine git.
2. **Units** bölümünü aç:
   - **Unit System:** Metric
   - **Length:** Meters
   - **Unit Scale:** 1.0
3. Bu kadar. Artık 1 birim = 1 metre.

### Adım 1 — Başlangıç küpünü sil
- Sahnede hazır gelen küpe sol tıkla, sonra **X** tuşuna bas → **Delete**. Sahne boş olsun.

### Adım 2 — Altıgeni ekle (silindir aracıyla)
1. Üstten **Add → Mesh → Cylinder** seç.
2. Ekranın **sol alt köşesinde** küçük bir kutu açılır ("Add Cylinder"). Tıklayıp genişlet.
3. Şu değerleri gir:
   - **Vertices:** `6`  ← altıgen için 6 köşe
   - **Radius:** `0.95`  ← bu sayede karo doğru boyda olur
   - **Depth:** `0.30`  ← kalınlık
   - **Cap Fill Type:** `N-Gon`

Artık 6 kenarlı, 0.30 m kalınlığında bir altıgen elde ettin. 🎉

### Adım 3 — Sivri ucu doğru yöne çevir
- Şu an altıgenin **düz kenarı** öne bakıyor olabilir. Bizim **sivri uç** öne/arkaya baksın.
- Klavyeden sırayla bas: **R**, sonra **Z**, sonra **90** yaz, **Enter**.
  (Bu, altıgeni dik eksende 90 derece döndürür.)
- Üstten bak (klavyede **7** tuşu): sivri uçlar yukarı ve aşağı bakmalı.

### Adım 4 — Karoyu zemine oturt
- Şu an altıgenin yarısı zeminin altında. Yukarı kaldıralım:
- Sırayla bas: **G**, sonra **Z**, sonra **0.15** yaz, **Enter**.
  (Kalınlığın yarısı kadar yukarı kalkar; alt yüzü tam zemine oturur.)

### Adım 5 — Merkez noktasını (pivot) tabana al — ÖNEMLİ
Oyun karoyu bu merkez noktasından tutup yerleştirir. Doğru yerde olmalı:
1. **Shift + C** → imleç dünya merkezine gider (zeminin tam ortası).
2. Üst menü: **Object → Set Origin → Origin to 3D Cursor**.
- Artık karonun turuncu nokta (origin) **alt-orta** noktasında.

### Adım 6 — Ayarları sabitle
- Sırayla bas: **Ctrl + A** → açılan listeden **All Transforms** seç.
  (Döndürme/ölçek bilgilerini sıfırlar, oyuna temiz gelir.)

İlk karon hazır! Üstüne ağaç, taş, çimen gibi detaylar ekleyebilirsin (bkz. Bölüm 3).

---

## 3. Karonun Üstüne Süs/Detay Ekleme (ağaç, taş, dağ vb.)

- Süsler karonun **üst yüzeyine** otursun (üst yüz, tabandan 0.30 m yukarıda).
- Çok uzun yapma: **en fazla ~1.5 m** boy. Kamera yandan-üstten baktığı için
  çok uzun objeler arkadaki karoları kapatır.
- Süs, altıgenin kenarlarından **dışarı taşmasın**; yoksa yan karoya girer.

---

## 4. Renk / Kaplama (basit tutalım)

- Oyun stili **For The King** gibi: sade, el-çizimi hissi, foto-gerçekçi DEĞİL.
- Her karo için **tek renk veya tek kaplama** yeterli (oyun daha hızlı çalışır).
- Kaplama (texture) kullanırsan **1024 px** ideal. Sadece düz renk de olur.
- Çok detaylı/yüksek poligon yapma; düşük poligon (low-poly) tercih edilir.

---

## 5. Bittiğinde Bana Nasıl Gönderirsin (FBX export)

Kaydetmeden önce: Adım 6'daki **Ctrl+A → All Transforms** yapıldığından emin ol.

Sonra: **File → Export → FBX (.fbx)** ve sağdaki ayarları şöyle yap:

| Ayar | Seçim |
|---|---|
| Selected Objects | ✔ (sadece karoyu seç) |
| Scale | 1.0 |
| Forward | **-Z Forward** |
| Up | **Y Up** |
| Apply Unit | ✔ |
| Apply Transform | ✔ |
| Triangulate Faces | ✔ |

- Dosyayı karonun türüne göre adlandır: `karo_cimen.fbx`, `karo_su.fbx`, `karo_kaya.fbx` ...
- Bana hangi karonun ne olduğunu yaz (çimen / su / kaya / dağ vb.).

---

## 6. Göndermeden Önce Kontrol Listesi

- [ ] Altıgen, sivri uçları yukarı/aşağı bakacak şekilde.
- [ ] Ölçüler doğru: yukarı-aşağı **1.90 m**, sağ-sol **1.645 m**, kalınlık **0.30 m**.
- [ ] Alt yüz zemine oturuyor (yarısı zeminin altında değil).
- [ ] Merkez nokta (origin) alt-orta noktada.
- [ ] **Ctrl+A → All Transforms** yapıldı.
- [ ] Süsler kenardan taşmıyor, çok uzun değil.

---

Bir yerde takılırsan ekran görüntüsü at, birlikte çözelim. Kolay gelsin! ✨
