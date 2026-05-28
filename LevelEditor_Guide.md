# PixelShoot — Level Editor Kullanım Kılavuzu

## 🎯 Genel Bakış

Level Editor, Unity Editor içinde bir pencere olarak açılır. Tek bir dosyada (`LevelData` ScriptableObject) tüm level verisini saklar: grid hücreleri, palet renkleri, shooter columnları ve kapasiteler.

Editör **hiçbir oyun sahnesini değiştirmez**; sadece `LevelData` asset'lerini düzenler ve **isteğe bağlı olarak** açık olan `LevelEditor.unity` sahnesinde **canlı önizleme** sunar.

---

## 🔧 Bir Defalık Sahne Hazırlığı

Önizleme yapabilmek için **LevelEditor.unity** sahnesinin içinde şunlar olmalı:

| GameObject | Component | Inspector'da Doldurulması Gereken Alanlar |
|---|---|---|
| **GameRoot** (örnek) | `LevelLoader` | `grid`, `conveyor`, `reserve`, `gameController`, `shooterPrefab`, `columnPrefab`, `columnsRoot` |
| | `GridController` | `boxPrefab`, `gridRoot`, (opsiyonel) `lockedBoxMaterial`, `unhitBoxMaterial` |
| | `ConveyorController` | `pathRoot` (waypoint zinciri) |
| | `ReserveController` | `slotTransforms[]` |
| | `PlayOnReserveController` | (opsiyonel) |
| | `GameController` | — |
| **GridRoot** (boş) | Transform | — |
| **ColumnsRoot** (boş) | Transform | — |

> ⚠️ Hangi alanın eksik olduğunu Level Editor pencere'sinin **"Scene preview"** bölümü kırmızı kutuda otomatik söyler. "Select offending GameObject" butonuyla hatalı GO'ya direkt zıplarsın.

---

## 🚀 Editör'ü Açma

Unity menüsünden: **`PixelShoot ▶ Open Level Editor Wizard`**

Pencere ilk açıldığında soldan sağa şu bölümler vardır:

1. **Asset** (en üst)
2. **Scene preview**
3. **Import**
4. **Grid**
5. **Columns & capacities**

---

## 1️⃣ Asset (Level Adı, Save/Load/Clear)

```
Level name: [Level_01     ]
[ Save ] [ Load ] [ Clear window ]
Bound asset: [LevelData object field]
```

- **Level name** → İsim yaz (`Level_05` gibi).
- **Save** →
  - `Assets/_Game/Levels/{levelName}.asset` **varsa** → üzerine yazar.
  - **Yoksa** → o isimle yeni asset yaratır.
  - Disk'e gerçekten yazılır (`AssetDatabase.SaveAssets`).
- **Load** →
  - Aynı isimde asset varsa → yükler.
  - Yoksa → "asset bulunamadı" uyarısı.
- **Clear window** →
  - Wizard state'ini temizler (cells, palette, columns).
  - Sahnedeki gridRoot ve columnsRoot child'larını siler.
  - **Asset diskte aynen kalır.** Asset binding kaldırılır — `Load`'a basarak geri yükleyebilirsin.

> ⚠️ **Önemli**: Sadece **Save** butonu disk'e yazar. Boyama / import / column üretimi otomatik kaydetmez — terminate etmeden önce **Save**'e basmalısın.

---

## 🎬 Scene Preview

```
[ Refresh preview in scene ] [ Show final state / Exit final state ]
```

- **Refresh preview in scene** → Bağlı LevelData'yı sahnedeki LevelLoader'a verir, gridRoot ve columnsRoot'u temizler, runtime'daki gerçek scriptlerle (`GridController.Build`, `LevelLoader.SpawnColumns`) yeniden inşa eder. Yani oyundaki haliyle birebir görürsün.
- **Show final state** (toggle) → Her hücre, ColorData'sındaki vivid renge geçer (oyunda tüm kutular yıkılmış halde nasıl görünür). Tekrar bas → **Exit final state** → normal görünüm.

> 🟢 **Auto-refresh**: Wizard'da herhangi bir değişiklik yaptığında (boya, slider'ı çevir, capacity değiştir, import et) sahne **otomatik 150ms içinde yenilenir**. Manuel refresh atmana gerek yok.

> ⚠️ Auto-refresh **diske yazmaz** — sadece sahneyi günceller. Diske yazmak için Save'e basman lazım.

Aşağıda son refresh'in durum kutusu görünür:
- 🟦 "Built 339 boxes, 8 columns from Level_01."
- 🟧 "Build ran but no boxes appeared. Data has 200 cells. Likely causes: …"
- 🟥 "Build threw: NullReferenceException — …" (console'da stack)

---

## 2️⃣ Import (Palette + RLE)

```
Paste the full encoder export (PALETTE and/or RLE_ROWS):
[                                                ]
[                                                ]
[ Import all (auto-detect) ]
```

**Ne yapıştırırsın?** Pixel-art-encoder HTML aracından **PALETTE** bloğu ve **RLE_ROWS** dizisini içeren tam metni. Editör hepsini bir kerede algılar:

### Otomatik algılanan parçalar

| Parça | Format |
|---|---|
| **PALETTE** | `"#RRGGBB"` formatındaki tüm hex renkler — sırayla palette'e eklenir, her renk için `Assets/_Game/Colors/{HEX}/Color_{HEX}.asset` ScriptableObject + materyaller oluşturulur (yoksa) |
| **Grid size** | `// Grid: NxN` yorumu veya satır verisinden otomatik hesaplanır |
| **RLE_ROWS** | `[-1, count, colorIdx, count, …]` formatında satır satır hücreler |

> Sonuç: tek tıklamayla palette + grid boyutu + tüm hücreler dolar.

**Status mesajı**: `"Imported: 8 colors, 30×30 grid, 200 filled cells. (Press Save to persist to disk.)"`

---

## 3️⃣ Grid (Boyama + Görselleştirme)

```
Grid size: [30   ] [ New (clear) ]
Grid root position: ( X, Y, Z )
Grid root scale:    [─────────●──] (slider 0.05 — 5)
[ Paint ] [ Erase ] [ Initial preview ]
Palette: [0][1][2][3][4][5][6][7]
┌─────────────────────────┐
│                         │
│   [Tıklanabilir grid]   │
│                         │
└─────────────────────────┘
Cells filled: 339 / 900
```

### Grid ayarları
- **Grid size**: NxN boyut. Değiştirince hücreler korunur, sınır dışı kalanlar düşer.
- **New (clear)**: Onay sorduktan sonra grid'i sıfırlar.
- **Grid root position / scale**: Sahnedeki `GridRoot` transformunun konumu ve ölçeği. Slider ile uniform scale (her eksene aynı değer). Burayı değiştirince oyundaki grid'in pozisyonu/büyüklüğü değişir. Auto-refresh anında uygular.

### Tool toggle'ları (mutually exclusive)
- **Paint** açık → tıklama, seçili palette rengiyle hücre ekler.
- **Erase** açık → tıklama, hücre siler.
- **İkisi de kapalı** (default) → tıklama hiçbir şey yapmaz (yanlışlıkla sürüklemeye karşı koruma).
- **Initial preview** açık → Edit kapanır, sahnedeki silüet/iç doluluk simülasyonu görünür.

### Palette swatch'leri
- Renk butonuna tıklayınca o renk seçilir **ve Paint modu otomatik açılır**.
- Seçili renk `[3]` şeklinde işaretlenir.

### Boyama (2D grid'in içinde)
- **LMB tıkla** veya **sürükle**: Paint açıksa ekle, Erase açıksa sil.
- Y-ekseni alt-üst flip edilir (görünen z=0 alt sırada, oyunla aynı).
- Initial preview açıkken **read-only**, tıklama çalışmaz.

> 🟢 **Auto-refresh**: Her boyamadan sonra 150ms içinde sahne güncellenir — anlık görsel feedback alırsın.

---

## 4️⃣ Columns & Capacities

```
Max shots / shooter: [5 ]
[ Auto-generate columns from grid ]
Conveyor capacity: [5 ]
Reserve capacity:  [5 ]
Columns: 8, shooters: 24, total shots: 339
```

- **Max shots / shooter**: Bir shooter en fazla kaç kutu vurabilir (yani kaç mermisi var).
- **Auto-generate columns from grid**:
  - Grid'deki her **gameplay rengi** için 1 column üretir (tone variant'ler aynı renkle birleşir).
  - Her gameplay renginin toplam kutu sayısını `maxShotsPerShooter`'a göre shooter'a böler.
  - Sonuçta her column = aynı gameplay renginden N shooter.
  - Genel kural: **toplam shots ≈ toplam kutu sayısı** olur.
- **Conveyor capacity**: Aynı anda conveyor'da kaç shooter olabilir.
- **Reserve capacity**: Conveyor doluyken yedeğe geçen shooter'lar için kaç slot var.

> Aşağıda info kutusu sonucu özetler: `"Columns: 8, shooters: 24, total shots: 339"`. Total shots = boyalı hücre sayısına eşit olmalı; tutmazsa import / column üret tekrar dene.

---

## 🎨 Tipik İş Akışı

### A) Sıfırdan yeni bir level
1. Level Editor Wizard'ı aç (`PixelShoot ▶ Open Level Editor Wizard`).
2. **Level name** yaz → **Save** (boş asset oluşturuldu).
3. Pixel-art encoder HTML aracından PALETTE + RLE_ROWS kopyala → "Import all" alanına yapıştır → **Import all**.
4. Sahnede grid'i göreceksin. Konumu ve scale'i ayarla (Grid root sliders).
5. **Auto-generate columns from grid**'e bas → otomatik shooter dağıtımı oluşur.
6. **Conveyor / Reserve capacity** ayarla.
7. **Show final state** ile tüm kutuların yıkılmış halini önizle, sağlamasını yap.
8. **Save** → asset diske yazılır.

### B) Mevcut bir level'ı açma
1. Level adını yaz → **Load** → wizard ve sahne dolar.
2. İstediğin değişiklikleri yap.
3. **Save** → üzerine yazılır.

### C) Hızlı manuel düzenleme
- Birkaç hücre eklemek/çıkarmak için Paint/Erase toggle'larını aç, palette swatch seç, grid üzerine tıkla.
- Otomatik sahne refresh'i sayesinde anında görürsün.
- **Save** unutma.

### D) "Final state" ile kontrol
- Level'ı bitirip kontrol etmek istediğinde **Show final state**'e bas — tüm kutular oyundaki "yıkılmış" görünümüne geçer. Renk dağılımı doğru mu, level estetik mi gözle bak. **Exit final state**'le geri dön.

---

## 🛠 Otomatik Üretilen Klasörler

İlk import sırasında oluşur:

```
Assets/_Game/
├── Levels/
│   └── Level_01.asset
├── Colors/
│   └── E18134/
│       └── Color_E18134.asset
└── Materials/
    ├── Boxes/E18134/Hit.mat
    ├── Shooters/E18134/Shooter.mat
    └── Bullets/E18134/Bullet.mat
```

> Frontier (henüz vurulmamış) kutuların tek paylaşılan materyali `GridController.unhitBoxMaterial`'dan gelir — per-color hit/unhit ayrımı yok.

---

## ⚠️ Sık Karşılaşılan Sorunlar

| Belirti | Sebep | Çözüm |
|---|---|---|
| **"No LevelLoader found in the open scene"** | LevelEditor sahnesini açmadın veya LevelLoader yok | Doğru sahneyi aç |
| Kırmızı kutu: "Missing references on the scene" | LevelLoader/GridController inspector'ında bazı slotlar boş | "Select offending GameObject" → eksikleri doldur |
| **Build ran but no boxes appeared** | GridRoot zero-scale, kamera dışında, veya cell'lerin ColorData null | Grid root scale slider'ı kontrol et |
| Renkleri görmüyorum (Show final state) | ColorData'da `BoxHitMaterial` yok | Otomatik DisplayColor tint devreye girer; status kutusu sayıları söyler |
| Save'e bastım ama Load'da boş geliyor | Levelname yanlış | Disk'teki dosya adıyla aynı olmalı (`Level_01`) |
| "DOTween Max Tweens" uyarısı | Eski versiyon | `AppInit.cs` halletti, görmemen lazım |

---

## ⌨️ Kısa Yol Özeti

| Aksiyon | Yol |
|---|---|
| Editör'ü aç | `PixelShoot ▶ Open Level Editor Wizard` |
| Boya | Palette'ten renk seç (otomatik Paint açılır) + grid'e tıkla/sürükle |
| Sil | "Erase" toggle + grid'e tıkla/sürükle |
| Final state | "Show final state" (toggle) |
| Tek sahne build et | "Refresh preview in scene" |
| Asset oluştur/üst yaz | Level name yaz + "Save" |
| Asset yükle | Level name yaz + "Load" |
| Sahneyi/wizard'ı temizle | "Clear window" (asset diskte kalır) |
