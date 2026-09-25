# RiseOn.Outline2D

Viền quanh silhouette gộp của một nhóm sprite hoặc một nhóm UI Image, nở đều như
*Select → Modify → Expand* của Photoshop: mọi điểm cách silhouette không quá độ dày đều
thuộc viền, đúng ở mọi độ dày, góc lồi bo tròn thật. Nhóm chỉ cần biết lúc gọi, không phải
setup gì trên từng sprite hay từng Image.

Package `com.riseon.outline2d`, namespace `RiseOn.Outline2D`.

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
| `com.unity.render-pipelines.universal` 17.3.0 | Tự cài theo `package.json` | Shader viền sprite (URP), URP Global Settings giữ shader mask |
| `com.unity.ugui` 2.0.0 | Tự cài theo `package.json` | `OutlineImage` là một Graphic của uGUI |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Inspector của component |

Odin không có trên UPM nên phải cài vào project trước; thiếu Odin thì project
báo đúng một lỗi từ `RiseOn.Outline2D.Requirements`.

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
    "com.riseon.outline2d": "1.0.1"
  }
}
```

**Git URL**: *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Outline2D.git#v1.0.1
```

**Thư mục local**: `"com.riseon.outline2d": "file:D:/path/to/Outline2D"`.

## Tổng quan

| Phần | Việc |
|---|---|
| **Outline** (`OutlineSprite`, `OutlineImage`) | Component nhận nhóm, giữ setting, vẽ viền: `OutlineSprite` bằng MeshRenderer của chính nó, `OutlineImage` như một Graphic trong Canvas |
| **Field** (nội bộ) | Chụp silhouette trên GPU, distance transform trên CPU, texture khoảng cách; dùng chung cho cả hai component |
| **Shader** | Shader chụp silhouette, shader viền sprite, shader viền UI |

Code bên ngoài chỉ chạm `IOutline<T>.SetTargets(IEnumerable<T>)`: `OutlineSprite` là
`IOutline<SpriteRenderer>`, `OutlineImage` là `IOutline<Image>`. Setting nằm trên component.
Mọi thứ còn lại là `internal`.

Việc nặng chỉ xảy ra lúc đổi nhóm: vẽ các target vào một mask, đọc mask về, tính khoảng
cách tới silhouette cho từng texel. Trong lúc giữ nhóm, mỗi frame chỉ là một quad. Nhóm di
chuyển, camera di chuyển, zoom hay nhóm nảy (scale) đều không phải chụp lại.

## Hướng dẫn nhanh

**Sprite**

1. Tạo một GameObject cho viền, thêm `OutlineSprite` (tự kèm MeshFilter và MeshRenderer). Chỉnh
   *Sorting Layer* / *Order in Layer* trên MeshRenderer để viền vẽ đè lên những gì cần.
2. Cho GameObject đó bám theo nhóm sprite: làm con của nhóm, hoặc dùng Position / Rotation /
   Scale Constraint. Viền được chụp trong không gian local của nó nên phải cứng với nhóm.
3. Chỉnh setting ngay trên component (màu, độ dày theo zoom, độ phân giải chụp), rồi gọi
   `SetTargets`.

**UI Image**

1. Tạo một GameObject trong Canvas, thêm `OutlineImage` (tự kèm CanvasRenderer). Viền là một
   Graphic nên sort theo hierarchy: đặt nó sau các Image cần viền. Nằm dưới cùng Mask /
   RectMask2D với các Image đó thì viền cũng bị cắt như chúng.
2. Cho nó cứng với nhóm, thường là làm con của nhóm hoặc của cùng content đang cuộn.
3. Màu viền là *Color* của Graphic, độ dày `width` tính bằng đơn vị của root canvas; rồi gọi
   `SetTargets`.

```csharp
outline.SetTargets(targets); // chọn nhóm mới
outline.SetTargets(null);    // bỏ chọn
```

Viền cũ tắt ngay lúc gọi, viền mới hiện sau vài frame. Nhóm đổi hình dạng giữa chừng (bật,
tắt, đổi sprite) thì gọi lại `SetTargets` với tập mới.

Cả hai component có nút *SetupEditor* và tự chạy nó lúc `Reset`, nên kéo component vào là ô
shader viền (và MeshFilter, MeshRenderer của `OutlineSprite`) tự điền. Shader chụp silhouette
là `Hidden` và không có ô để gán: Editor tự thêm nó vào URP Global Settings của project như
resource nội bộ của URP, nên file settings đó có thêm một mục cần commit (xem
[Shader](Runtime/Shaders/README.md)).

## Mở rộng

Không lớp public nào `sealed`. Các điểm móc để `virtual`: `SetTargets`, `LateUpdate` và
`SetupEditor` của cả hai; `Awake`, `OnDestroy`, `OnValidate`, `Reset` của `OutlineSprite`.
`OutlineImage` là Graphic nên `Awake`, `OnDestroy`, `OnPopulateMesh`, `GraphicUpdateComplete`,
`Cull`, `OnValidate`, `Reset` là override của uGUI và vẫn override tiếp được. Các field setting
là `protected` để lớp con đọc và đổi được.

## Thành phần

| Thành phần | Việc | Chi tiết |
|---|---|---|
| Outline | Hợp đồng của `SetTargets`, hệ toạ độ, thời điểm chụp, setting, độ dày | [Runtime/Outline](Runtime/Outline/README.md) |
| Field | Vòng chụp, khung chụp và ngân sách texel, nguồn vẽ, readback, distance transform | [Runtime/Field](Runtime/Field/README.md) |
| Shader | Shader chụp silhouette, shader viền sprite và UI | [Runtime/Shaders](Runtime/Shaders/README.md) |
| Editor | Inspector của `OutlineImage` | [Editor](Editor/README.md) |

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).
