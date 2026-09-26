# OutlineImage

[← RiseOn.Outline2D](../../../README.md)

`OutlineImage` viền một nhóm `Image`, hợp đồng là `IOutlineImage`. Viền được vẽ bằng một Graphic
tự sinh trên con ẩn ([Lớp nền](../../README.md#con-ẩn-vẽ-viền)): `OutlineGraphic`, một
`MaskableGraphic` nội bộ, vẽ qua `CanvasRenderer` như mọi Graphic của uGUI. Màu và *Maskable*
đặt trên component. Thư mục này còn chứa shader viền UI và nguồn vẽ `ImageSource`; phần chụp và
distance field dùng chung với `OutlineSprite` ([Field](../../Field/README.md)).

- [Hợp đồng của SetTargets](#hợp-đồng-của-settargets)
- [Hệ toạ độ và thời điểm chụp](#hệ-toạ-độ-và-thời-điểm-chụp)
- [Graphic tự sinh](#graphic-tự-sinh)
- [Setting](#setting)
- [Trong Canvas](#trong-canvas)
- [Nguồn vẽ](#nguồn-vẽ)
- [Shader](#shader)
- [Giới hạn](#giới-hạn)

## Hợp đồng của SetTargets

- Chuỗi Image được đọc hết ngay trong lúc gọi rồi bỏ; Image null bị bỏ qua. Truyền `null` hoặc
  chuỗi rỗng là tắt viền.
- Viền cũ tắt ngay lúc gọi, vì nó thuộc về nhóm cũ. Viền mới hiện sau vài frame: chụp mask,
  đọc mask về (bất đồng bộ), distance transform trên worker thread, upload, rồi mới hiện.
- Gọi nhiều lần trong một frame thì chỉ lần cuối được chụp. Kết quả của một lần chụp về
  sau khi đã có lần gọi mới thì bị bỏ.
- Silhouette chụp một lần. Nhóm đổi hình dạng giữa chừng (bật, tắt, đổi sprite, đổi kích
  thước Image) thì phải gọi lại `SetTargets`.

## Hệ toạ độ và thời điểm chụp

Silhouette được chụp trong không gian local của `OutlineImage`, nên nhóm phải cứng so với nó từ
lúc chụp tới lần gọi sau. Cách bám thường gặp là làm `OutlineImage` thành con của nhóm, hoặc
của cùng content đang cuộn.

Chụp chạy ở bước cuối của lượt cập nhật Canvas trong frame gọi (`GraphicUpdateComplete` của
Graphic tự sinh): layout đã chạy xong và mọi Graphic được đánh dấu trong frame đã giao mesh mới
cho CanvasRenderer của nó. Vì thế Image vừa bật, vừa đổi kích thước hay vừa đổi sprite ngay
trong frame gọi vẫn được chụp đúng hình mới. Canvas chỉ gọi bước đó cho Graphic đang chờ dựng
lại, nên mỗi khi có lần chụp đến hạn (`SetTargets`, đổi setting trong Play mode), `LateUpdate`
đánh dấu Graphic tự sinh.

## Graphic tự sinh

- Con ẩn có `RectTransform` cỡ 0 neo đúng pivot của `OutlineImage`, nên không gian local của
  Graphic trùng với của `OutlineImage` dù RectTransform của nó to nhỏ thế nào hay bị layout đổi
  kích thước.
- Con đứng đầu danh sách con, nên viền vẽ ngay sau GameObject của `OutlineImage` và trước các
  con khác của nó, như khi chính GameObject đó là một Graphic. Hệ quả: `GetChild(0)` của
  GameObject này trả về con ẩn.
- Graphic là `ILayoutIgnorer`: LayoutGroup đặt trên GameObject của `OutlineImage` bỏ qua con
  ẩn, không xếp nó thành một ô (đã đo với HorizontalLayoutGroup).
- Viền không bao giờ nhận input: *Raycast Target* của Graphic tắt.
- Đổi `Maskable` lúc chạy có tác dụng ngay với cả RectMask2D. Setter `maskable` của uGUI chỉ
  tính lại stencil; RectMask2D chỉ nhận thay đổi ở lần Graphic bật lại hay đổi cha (đã đo), nên
  component gọi thêm `RecalculateClipping` khi giá trị đổi.

## Setting

Chỉnh trong Play mode: xem [Lớp nền](../../README.md#chỉnh-setting-trong-play-mode). Độ dày
cũng đổi ngay, trong phạm vi vùng đệm của lần chụp trước.

**Visual**

| Field | Ý nghĩa |
|---|---|
| `color` | Màu viền, cũng là `IOutline.Color`; là Color của Graphic tự sinh nên nhân alpha của CanvasGroup như mọi Graphic |
| `alphaCutoff` | Texel của Image có alpha lớn hơn mức này thì thuộc silhouette |
| `width` | Độ dày theo đơn vị root canvas, xem [Trong Canvas](#trong-canvas); cũng là vùng đệm của lần chụp |

**Rendering**

| Field | Ý nghĩa |
|---|---|
| `maskable` | Bật thì viền bị Mask / RectMask2D phía trên cắt như Image |

**Optimizations**

| Field | Ý nghĩa |
|---|---|
| `resolution` | Như của `OutlineSprite`: khoảng `resolution²` pixel mỗi lần chụp, kéo từ 64 tới 1024, mặc định 256. Bảng đo và cách chọn ở [OutlineSprite](../OutlineSprite/README.md#chọn-resolution) |

Màu và material của các Image trong nhóm không ảnh hưởng silhouette: Image đang mờ đi vẫn được
viền theo đúng hình của nó.

## Trong Canvas

- Viền sort theo hierarchy như Image (đặt `OutlineImage` sau các Image cần viền), nhân alpha
  của CanvasGroup, bị Mask và RectMask2D phía trên cắt khi `maskable` bật.
- Silhouette lấy từ đúng mesh uGUI đã giao cho CanvasRenderer của từng Image, nên mọi *Image
  Type* (Simple, Sliced, Tiled, Filled), *Preserve Aspect* và *Use Sprite Mesh* đều theo đúng
  hình Image vẽ ra. Image không có sprite được viền theo hình chữ nhật của nó.
- `width` tính bằng đơn vị của root canvas (với Canvas Scaler là đơn vị của reference
  resolution), bất kể scale nằm giữa root canvas và `OutlineImage`. Scale đổi sau lúc chụp,
  chẳng hạn nhóm nảy, thì viền giữ nguyên độ dày đó trong phạm vi vùng đệm đã chụp: chỉ dựng
  lại quad, không chụp lại.
- Quad của viền nằm quanh nhóm chứ không nằm trên RectTransform nào, nên RectTransform của
  `OutlineImage` không cần khớp gì. Viền chỉ bị cull khi RectMask2D phía trên không còn vùng
  nhìn thấy; còn lại RectMask2D cắt từng pixel như với Image.
- Đã đo trong Play mode trên cả tám nhóm của scene review (Simple, Sliced, Filled, Preserve
  Aspect, trong RectMask2D đang cuộn, trong Mask hình tròn...): quad ôm nhóm, lề mỗi cạnh bằng
  `width` cộng phần đệm khử răng cưa; trong RectMask2D thì có rect clipping, trong Mask thì có
  stencil.

## Nguồn vẽ

`ImageSource` vẽ mỗi Image bằng mesh uGUI đã giao cho CanvasRenderer của Image
(`CanvasRenderer.GetMesh()`), với texture chính của Image đặt vào `_MainTex` qua
MaterialPropertyBlock. Vẽ thẳng mesh đó thì mask trống (đo trong Editor trên D3D12: 0 texel,
còn bản chép của cùng mesh ra 12652 texel), nên mỗi Image được chép sang một mesh riêng bằng
`Mesh.CombineMeshes` rồi mới vẽ. Các mesh chép được giữ lại cho lần chụp sau và huỷ cùng
component. Image tắt hoặc chưa có mesh thì bị bỏ qua.

## Shader

`OutlineImage.shader` (`RiseOn/Outline2D/OutlineImage`) là material của Graphic tự sinh. Vành
viền giống shader của `OutlineSprite` ([Lớp nền](../../README.md#vành-viền)); phần riêng:

- Theo cấu trúc của `UI/Default` (đối chiếu với template Canvas của Shader Graph trong Unity 6):
  stencil và `ColorMask` cho Mask, `ZTest [unity_GUIZTestMode]`, clip rect có độ mềm cho
  RectMask2D, `UNITY_UI_ALPHACLIP`, `_UIVertexColorAlwaysGammaSpace`, alpha làm tròn theo bước
  1/255 và blend premultiplied.
- Màu: `Graphic.color` được ghi vào màu đỉnh lúc dựng mesh như mọi Graphic, Canvas lo alpha của
  CanvasGroup và việc đổi gamma.
- Canvas gộp mesh của mọi Graphic trong không gian canvas, nên vertex shader không biết scale
  riêng của `OutlineImage`. Bán kính vành (tính bằng texel field, đã kẹp) được tính trên CPU và
  đi trong `uv0.z`, kích thước field trong `uv0.w`; Canvas giữ đủ bốn thành phần của `uv0`
  (TextMesh Pro cũng đọc `uv0.w`). Nhờ vậy material không đổi theo instance, và Mask chép
  material một lần là đủ.
- Field được đọc bằng `sampler2D_float`: trên OpenGL ES, `sampler2D` mặc định chỉ có độ chính
  xác thấp, không giữ nổi khoảng cách tính bằng texel.

## Giới hạn

- Phần Image bị Mask / RectMask2D che vẫn góp cả hình vào silhouette; viền nằm cùng Mask thì bị
  cắt cùng chỗ.
- Silhouette chỉ theo alpha của texture chính của Image: material riêng của Image và texture
  alpha tách rời (ETC1 split alpha) không được tính.
