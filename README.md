# RiseOn.Outline2D

Viền quanh silhouette gộp của một nhóm renderer (sprite, hay mesh như skeleton Spine) hoặc một nhóm
UI Graphic, nở đều như *Select → Modify → Expand* của Photoshop: mọi điểm cách silhouette không quá
độ dày đều thuộc viền, đúng ở mọi độ dày, góc lồi bo tròn thật. Dưới viền có thể có thêm bóng:
bóng đổ mềm của cả silhouette lẫn viền, như một tấm sticker được nhấc khỏi mặt cảnh, hoặc quầng
tản đều quanh viền. Nhóm chỉ cần biết lúc gọi, không phải setup gì trên từng renderer hay từng
Graphic. Chi phí và tần suất chụp chỉnh bằng một profile hiệu năng, từ chụp một lần khi chọn tới
chụp lại mỗi frame cho nhóm tự đổi hình (skeleton, sprite có animation).

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
| `com.riseon.utils` 1.0.12 | Tự cài theo `package.json` | `DirectionExt`, hướng của bóng đổ |
| `com.unity.burst` 1.8.29 | Tự cài theo `package.json` | Distance transform và blur của bóng trên worker thread (chất lượng `Exact`) |
| `com.unity.mathematics` 1.3.3 | Tự cài theo `package.json` | Đổi float sang half trong job |
| `com.unity.render-pipelines.universal` 17.3.0 | Tự cài theo `package.json` | Shader viền của `OutlineRenderer` (URP), URP Global Settings giữ shader nội bộ |
| `com.unity.ugui` 2.0.0 | Tự cài theo `package.json` | `OutlineGraphic` vẽ bằng một Graphic của uGUI |
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
    "com.riseon.outline2d": "2.0.0"
  }
}
```

**Git URL**: *Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Outline2D.git#v2.0.0
```

**Thư mục local**: `"com.riseon.outline2d": "file:D:/path/to/Outline2D"`.

## Tổng quan

| Phần | Việc |
|---|---|
| **Outline** | `IOutline` (màu viền, profile hiệu năng) và lớp cha `Outline`: setting chung, vòng chụp, con ẩn vẽ viền |
| **Performance** | `OutlinePerformanceProfile`: độ phân giải, tần suất, chất lượng và độ trễ của các lần chụp |
| **OutlineRenderer** | Component viền một nhóm renderer (sprite, mesh), vẽ bằng một SpriteRenderer tự sinh |
| **OutlineGraphic** | Component viền một nhóm UI Graphic, vẽ bằng một Graphic tự sinh |
| **Field** (nội bộ) | Chụp silhouette trên GPU; distance transform và blur của bóng trên CPU, hoặc jump flood trên GPU; texture field; dùng chung cho cả hai component |
| **Shader** | Shader chụp silhouette và shader jump flood; shader viền nằm cạnh component của nó |
| **Adapter** | Tween màu viền bằng LitMotion hoặc DOTween, tự bật khi project có thư viện |

Mỗi viền là đúng một component. Thứ vẽ ra viền được sinh lúc chạy trên một GameObject con ẩn,
không lưu vào scene, và code bên ngoài không biết tới nó. Code bên ngoài chỉ chạm
`IOutlineRenderer` / `IOutlineGraphic` (`SetTargets` và setting vẽ của loại đó) hoặc `IOutline`
(màu và profile hiệu năng, chung cho cả hai). Mọi thứ còn lại là `internal`.

Việc nặng chỉ xảy ra lúc chụp: vẽ các target vào một mask rồi tính khoảng cách tới silhouette cho
từng texel và, khi có bóng, độ phủ của bóng. Mặc định chỉ chụp khi đổi nhóm; trong lúc giữ nhóm,
mỗi frame chỉ là một quad, vành và bóng vẽ chung. Nhóm di chuyển, camera di chuyển, zoom hay nhóm
nảy (scale) đều không phải chụp lại. Nhóm tự đổi hình dạng thì profile cho chụp lại mỗi một số
frame hay một số giây.

## Hướng dẫn nhanh

**Renderer (sprite, mesh)**

1. Tạo một GameObject cho viền, thêm `OutlineRenderer`. Màu, *Sorting Layer*, *Order* và *Mask
   Interaction* của viền chỉnh ngay trên component.
2. Cho GameObject đó bám theo nhóm renderer: làm con của nhóm, hoặc dùng Position / Rotation /
   Scale Constraint. Viền được chụp trong không gian local của nó nên phải cứng với nhóm.
3. Chỉnh độ dày theo zoom, bóng (bật ở ô đánh dấu trên tiêu đề *Shadow*; màu và kiểu; với
   `Directional` thì hướng, khoảng cách và độ mềm, với `Spread` thì khoảng tản; các độ dài theo
   zoom, chung một `orthoRange` với độ dày) và profile hiệu năng, rồi gọi `SetTargets`.

**UI Graphic**

1. Tạo một GameObject trong Canvas, thêm `OutlineGraphic`. Viền vẽ ngay sau GameObject đó theo
   hierarchy: đặt nó sau các Graphic cần viền. Nằm dưới cùng Mask / RectMask2D với các Graphic đó
   thì viền cũng bị cắt như chúng.
2. Cho nó cứng với nhóm, thường là làm con của nhóm hoặc của cùng content đang cuộn.
3. Chỉnh màu, độ dày `width`, bóng (bật ở ô đánh dấu trên tiêu đề *Shadow*; màu và kiểu; với
   `Directional` thì hướng, khoảng cách, độ mềm, với `Spread` thì khoảng tản) và *Maskable*, rồi
   gọi `SetTargets`. Độ dài đều tính bằng đơn vị của root canvas.

```csharp
outline.Performance = profile; // profile serialize sẵn cho loại nhóm này
outline.SetTargets(targets);   // chọn nhóm mới
outline.SetTargets(null);      // bỏ chọn
outline.Color = Color.cyan;    // đổi màu ngay, không chụp lại
```

Viền cũ tắt ngay lúc gọi; viền mới hiện cùng frame hay sau vài frame tuỳ độ trễ của profile. Với
tần suất mặc định `OnDemand`, nhóm đổi hình dạng giữa chừng (bật, tắt, đổi sprite) thì gọi lại
`SetTargets` với tập mới.

Thêm component trong Editor thì ô shader viền tự điền (`Reset`); nút *Default* cạnh ô đặt lại
shader viền của package. Shader chụp silhouette và shader jump flood là `Hidden` và không có ô để
gán: Editor tự thêm chúng vào URP Global Settings của project như resource nội bộ của URP, nên file
settings đó có thêm một mục cần commit (xem [Shader](Runtime/Shaders/README.md)).

## Mở rộng

Không lớp public nào `sealed`. Lớp cha `Outline` phải public vì hai component public kế thừa
nó, nhưng các điểm móc của nó là `private protected`, nên ngoài package không kế thừa thẳng
được. Muốn mở rộng thì kế thừa `OutlineRenderer` hoặc `OutlineGraphic`: `SetTargets`,
`LateUpdate`, `OnEnable`, `OnDisable` và các lifecycle của lớp cha (`Awake`, `OnDestroy`,
`OnValidate`, `Reset`) là `virtual`, các field setting (trừ ô `shader`) là `protected`.

## Thành phần

| Thành phần | Việc | Chi tiết |
|---|---|---|
| Lớp nền | `IOutline`, lớp cha `Outline`, con ẩn vẽ viền, làm sẵn shader, vành viền, bóng đổ và màu viền | [Runtime](Runtime/README.md) |
| Hiệu năng | `OutlinePerformanceProfile`: ý nghĩa từng field, các tổ hợp, số đo trên máy thật | [Runtime/Performance](Runtime/Performance/README.md) |
| OutlineRenderer | Hợp đồng của `SetTargets`, thời điểm chụp, vẽ bằng SpriteRenderer, setting, chọn `Resolution`, độ dày và bóng theo zoom, shader viền | [Runtime/Concretes/OutlineRenderer](Runtime/Concretes/OutlineRenderer/README.md) |
| OutlineGraphic | Hợp đồng của `SetTargets`, thời điểm chụp trong Canvas, Graphic tự sinh, setting, Mask và RectMask2D, bóng trong Canvas, shader viền UI | [Runtime/Concretes/OutlineGraphic](Runtime/Concretes/OutlineGraphic/README.md) |
| Field | Vòng chụp, khung chụp và ngân sách texel, readback, distance transform, jump flood, blur của bóng | [Runtime/Field](Runtime/Field/README.md) |
| Shader | Shader chụp silhouette và jump flood, cách chúng vào bản build | [Runtime/Shaders](Runtime/Shaders/README.md) |
| Adapter | Tween màu viền bằng LitMotion, DOTween | [Runtime/Adapters](Runtime/Adapters/README.md) |
| Editor | Báo thiếu Odin Inspector, vẽ mục gập không khung của Inspector | [Editor](Editor/README.md) |

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).
