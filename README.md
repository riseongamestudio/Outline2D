# RiseOn.SpriteOutline

Viền quanh silhouette gộp của một nhóm sprite, nở đều như *Select → Modify → Expand*
của Photoshop: mọi điểm cách silhouette không quá độ dày đều thuộc viền, đúng ở mọi độ
dày, góc lồi bo tròn thật. Nhóm sprite chỉ cần biết lúc gọi, không phải setup gì trên
từng sprite.

Package `com.riseon.spriteoutline`, namespace `RiseOn.SpriteOutline`.

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Tổng quan](#tổng-quan)
- [Hướng dẫn nhanh](#hướng-dẫn-nhanh)
- [Mở rộng](#mở-rộng)
- [Thành phần](#thành-phần)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| `com.unity.burst` 1.8.29 | Tự cài theo `package.json` | Distance transform trên worker thread |
| `com.unity.mathematics` 1.3.3 | Tự cài theo `package.json` | Đổi float sang half trong job |
| `com.unity.render-pipelines.universal` 17.3.0 | Tự cài theo `package.json` | Shader vẽ viền (URP), URP Global Settings giữ shader mask |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Inspector của component |

Odin không có trên UPM nên phải cài vào project trước.

## Cài đặt

**OpenUPM** (khuyên dùng): thêm registry OpenUPM với scope `com.riseon` vào
`Packages/manifest.json`, rồi thêm package:

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.riseon"]
    }
  ],
  "dependencies": {
    "com.riseon.spriteoutline": "1.0.0"
  }
}
```

**Git URL**: *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/SpriteOutline.git#v1.0.0
```

**Thư mục local**: `"com.riseon.spriteoutline": "file:D:/path/to/SpriteOutline"`.

## Tổng quan

| Phần | Việc |
|---|---|
| **SpriteOutline** (`SpriteOutline`) | Component nhận nhóm sprite, giữ setting, vẽ viền bằng MeshRenderer của chính nó |
| **Field** (nội bộ) | Chụp silhouette trên GPU, distance transform trên CPU, texture khoảng cách |
| **Shader** | Shader chụp silhouette và shader vẽ viền |

Code bên ngoài chỉ chạm `ISpriteOutline.SetTargets(IEnumerable<SpriteRenderer>)`; setting nằm trên
component. Mọi thứ còn lại là `internal`.

Việc nặng chỉ xảy ra lúc đổi nhóm: vẽ các sprite vào một mask, đọc mask về, tính khoảng
cách tới silhouette cho từng texel. Trong lúc giữ nhóm, mỗi frame chỉ là một quad. Nhóm di
chuyển, camera di chuyển hay zoom đều không phải chụp lại.

## Hướng dẫn nhanh

1. Tạo một GameObject cho viền, thêm `SpriteOutline` (tự kèm MeshFilter và MeshRenderer). Chỉnh
   *Sorting Layer* / *Order in Layer* trên MeshRenderer để viền vẽ đè lên những gì cần.
2. Cho GameObject đó bám theo nhóm sprite: làm con của nhóm, hoặc dùng Position / Rotation /
   Scale Constraint. Viền được chụp trong không gian local của nó nên phải cứng với nhóm.
3. Chỉnh setting ngay trên component (màu, độ dày theo zoom, ngân sách pixel), rồi gọi:

   ```csharp
   outliner.SetTargets(renderers); // chọn nhóm mới
   outliner.SetTargets(null);      // bỏ chọn
   ```

Viền cũ tắt ngay lúc gọi, viền mới hiện sau vài frame. Nhóm đổi hình dạng giữa chừng (bật,
tắt, đổi sprite) thì gọi lại `SetTargets` với tập mới.

`SpriteOutline` có nút *SetupEditor* và tự chạy nó lúc `Reset`, nên kéo component vào là các ô
tham chiếu (MeshFilter, MeshRenderer, shader viền) tự điền. Shader chụp silhouette là `Hidden`
và không có ô để gán: Editor tự thêm nó vào URP Global Settings của project như resource nội bộ
của URP, nên file settings đó có thêm một mục cần commit (xem [Shader](Runtime/Shaders/README.md)).

## Mở rộng

Không lớp public nào `sealed`. Các điểm móc để `virtual`: `SpriteOutline.SetTargets`, `Awake`,
`LateUpdate`, `OnDestroy`, `OnValidate`, `Reset`, `SetupEditor`; các field setting là
`protected` để lớp con đọc và đổi được.

## Thành phần

| Thành phần | Việc | Chi tiết |
|---|---|---|
| SpriteOutline | Hợp đồng của `SetTargets`, hệ toạ độ, thời điểm chụp, độ dày theo zoom | [Runtime/SpriteOutline](Runtime/SpriteOutline/README.md) |
| Field | Khung chụp và ngân sách texel, readback, distance transform, texture | [Runtime/Field](Runtime/Field/README.md) |
| Shader | Shader chụp silhouette và shader vẽ viền | [Runtime/Shaders](Runtime/Shaders/README.md) |

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).
