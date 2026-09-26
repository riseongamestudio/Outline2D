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
| `com.unity.ugui` 2.0.0 | Tự cài theo `package.json` | `OutlineImage` vẽ bằng một Graphic của uGUI |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Inspector của component |
| LitMotion 2.0 trở lên, DOTween | Tuỳ chọn | Tween màu viền, xem [Adapter](Runtime/Adapters/README.md) |

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
    "com.riseon.outline2d": "1.0.3"
  }
}
```

**Git URL**: *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Outline2D.git#v1.0.3
```

**Thư mục local**: `"com.riseon.outline2d": "file:D:/path/to/Outline2D"`.

## Tổng quan

| Phần | Việc |
|---|---|
| **Outline** | `IOutline` (màu viền) và lớp cha `Outline`: setting chung, vòng chụp, con ẩn vẽ viền |
| **OutlineSprite** | Component viền một nhóm sprite, vẽ bằng một SpriteRenderer tự sinh |
| **OutlineImage** | Component viền một nhóm UI Image, vẽ bằng một Graphic tự sinh |
| **Field** (nội bộ) | Chụp silhouette trên GPU, distance transform trên CPU, texture khoảng cách; dùng chung cho cả hai component |
| **Shader** | Shader chụp silhouette; shader viền nằm cạnh component của nó |
| **Adapter** | Tween màu viền bằng LitMotion hoặc DOTween, tự bật khi project có thư viện |

Mỗi viền là đúng một component. Thứ vẽ ra viền được sinh lúc chạy trên một GameObject con ẩn,
không lưu vào scene, và code bên ngoài không biết tới nó. Code bên ngoài chỉ chạm
`IOutlineSprite` / `IOutlineImage` (`SetTargets` và setting vẽ của loại đó) hoặc `IOutline`
(màu, chung cho cả hai). Mọi thứ còn lại là `internal`.

Việc nặng chỉ xảy ra lúc đổi nhóm: vẽ các target vào một mask, đọc mask về, tính khoảng
cách tới silhouette cho từng texel. Trong lúc giữ nhóm, mỗi frame chỉ là một quad. Nhóm di
chuyển, camera di chuyển, zoom hay nhóm nảy (scale) đều không phải chụp lại.

## Hướng dẫn nhanh

**Sprite**

1. Tạo một GameObject cho viền, thêm `OutlineSprite`. Màu, *Sorting Layer*, *Order* và *Mask
   Interaction* của viền chỉnh ngay trên component.
2. Cho GameObject đó bám theo nhóm sprite: làm con của nhóm, hoặc dùng Position / Rotation /
   Scale Constraint. Viền được chụp trong không gian local của nó nên phải cứng với nhóm.
3. Chỉnh độ dày theo zoom và độ phân giải chụp, rồi gọi `SetTargets`.

**UI Image**

1. Tạo một GameObject trong Canvas, thêm `OutlineImage`. Viền vẽ ngay sau GameObject đó theo
   hierarchy: đặt nó sau các Image cần viền. Nằm dưới cùng Mask / RectMask2D với các Image đó
   thì viền cũng bị cắt như chúng.
2. Cho nó cứng với nhóm, thường là làm con của nhóm hoặc của cùng content đang cuộn.
3. Chỉnh màu, độ dày `width` (đơn vị của root canvas) và *Maskable*, rồi gọi `SetTargets`.

```csharp
outline.SetTargets(targets); // chọn nhóm mới
outline.SetTargets(null);    // bỏ chọn
outline.Color = Color.cyan;  // đổi màu ngay, không chụp lại
```

Viền cũ tắt ngay lúc gọi, viền mới hiện sau vài frame. Nhóm đổi hình dạng giữa chừng (bật,
tắt, đổi sprite) thì gọi lại `SetTargets` với tập mới.

Thêm component trong Editor thì ô shader viền tự điền (`Reset`); nút *Default* cạnh ô đặt lại
shader viền của package. Shader chụp silhouette là `Hidden` và không có ô để gán: Editor tự thêm nó
vào URP Global Settings của project như resource nội bộ của URP, nên file settings đó có thêm
một mục cần commit (xem [Shader](Runtime/Shaders/README.md)).

## Mở rộng

Không lớp public nào `sealed`. Lớp cha `Outline` phải public vì hai component public kế thừa
nó, nhưng các điểm móc của nó là `private protected`, nên ngoài package không kế thừa thẳng
được. Muốn mở rộng thì kế thừa `OutlineSprite` hoặc `OutlineImage`: `SetTargets`,
`LateUpdate`, `OnEnable`, `OnDisable` và các lifecycle của lớp cha (`Awake`, `OnDestroy`,
`OnValidate`, `Reset`) là `virtual`, các field setting là `protected`.

## Thành phần

| Thành phần | Việc | Chi tiết |
|---|---|---|
| Lớp nền | `IOutline`, lớp cha `Outline`, con ẩn vẽ viền, làm sẵn shader, vành viền và màu viền | [Runtime](Runtime/README.md) |
| OutlineSprite | Hợp đồng của `SetTargets`, thời điểm chụp, vẽ bằng SpriteRenderer, setting, chọn `resolution`, độ dày theo zoom, shader viền sprite | [Runtime/Concretes/OutlineSprite](Runtime/Concretes/OutlineSprite/README.md) |
| OutlineImage | Hợp đồng của `SetTargets`, thời điểm chụp trong Canvas, Graphic tự sinh, setting, Mask và RectMask2D, shader viền UI | [Runtime/Concretes/OutlineImage](Runtime/Concretes/OutlineImage/README.md) |
| Field | Vòng chụp, khung chụp và ngân sách texel, readback, distance transform | [Runtime/Field](Runtime/Field/README.md) |
| Shader | Shader chụp silhouette, cách nó vào bản build | [Runtime/Shaders](Runtime/Shaders/README.md) |
| Adapter | Tween màu viền bằng LitMotion, DOTween | [Runtime/Adapters](Runtime/Adapters/README.md) |
| Editor | Báo thiếu Odin Inspector | [Editor](Editor/README.md) |

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).
