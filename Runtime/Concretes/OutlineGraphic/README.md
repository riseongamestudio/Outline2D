# OutlineGraphic

[← RiseOn.Outline2D](../../../README.md)

`OutlineGraphic` viền một nhóm `Graphic` của uGUI (`Image`, `RawImage`...), hợp đồng là
`IOutlineGraphic`. Viền được vẽ bằng một Graphic tự sinh trên con ẩn ([Lớp
nền](../../README.md#con-ẩn-vẽ-viền)): `OutlineCanvasGraphic`, một `MaskableGraphic` nội bộ, vẽ qua
`CanvasRenderer` như mọi Graphic của uGUI; bóng đổ tuỳ chọn ([Lớp nền](../../README.md#bóng-đổ)) vẽ
chung với viền. Màu, bóng và *Maskable* đặt trên component. Thư mục này còn chứa shader viền UI và
nguồn vẽ `GraphicSource`; phần chụp và distance field dùng chung với `OutlineRenderer`
([Field](../../Field/README.md)).

- [Hợp đồng của SetTargets](#hợp-đồng-của-settargets)
- [Hệ toạ độ và thời điểm chụp](#hệ-toạ-độ-và-thời-điểm-chụp)
- [Graphic tự sinh](#graphic-tự-sinh)
- [Setting](#setting)
- [Trong Canvas](#trong-canvas)
- [Nguồn vẽ](#nguồn-vẽ)
- [Shader](#shader)
- [Giới hạn](#giới-hạn)

## Hợp đồng của SetTargets

- Chuỗi Graphic được đọc hết ngay trong lúc gọi rồi bỏ; Graphic null bị bỏ qua. Truyền `null`
  hoặc chuỗi rỗng là tắt viền.
- Viền cũ tắt ngay lúc gọi, vì nó thuộc về nhóm cũ. Viền mới hiện ngay trong frame đó với độ trễ
  `Immediate`, sau vài frame với `Async` (xem [Hiệu năng](../../Performance/README.md)).
- Gọi nhiều lần trong một frame thì chỉ lần cuối được chụp. Kết quả của một lần chụp về
  sau khi đã có lần gọi mới thì bị bỏ.
- Với tần suất `OnDemand`, silhouette chụp một lần: nhóm đổi hình dạng giữa chừng (bật, tắt, đổi
  sprite, đổi kích thước) thì phải gọi lại `SetTargets`. Nhóm tự đổi hình dạng thì dùng
  `FrameInterval` hoặc `TimeInterval` để chụp lại định kỳ.

## Hệ toạ độ và thời điểm chụp

Silhouette được chụp trong không gian local của `OutlineGraphic`, nên nhóm phải cứng so với nó từ
lúc chụp tới lần gọi sau. Cách bám thường gặp là làm `OutlineGraphic` thành con của nhóm, hoặc
của cùng content đang cuộn.

Chụp chạy ở bước cuối của lượt cập nhật Canvas trong frame gọi (`GraphicUpdateComplete` của
Graphic tự sinh): layout đã chạy xong và mọi Graphic được đánh dấu trong frame đã giao mesh mới
cho CanvasRenderer của nó. Vì thế Graphic vừa bật, vừa đổi kích thước hay vừa đổi sprite ngay
trong frame gọi vẫn được chụp đúng hình mới. Canvas chỉ gọi bước đó cho Graphic đang chờ dựng
lại, nên mỗi khi có lần chụp đến hạn (`SetTargets`, đổi setting trong Play mode, hết một khoảng
của tần suất), `LateUpdate` đánh dấu Graphic tự sinh.

Lần chụp `Immediate` đã xong ngay tại bước đó. Canvas lúc đó từ chối mọi đánh dấu mới nhưng chưa
gộp batch, nên Graphic tự sinh dựng lại quad ngay (gọi thẳng `UpdateGeometry`) và viền hiện trong
frame chụp.

## Graphic tự sinh

- Con ẩn có `RectTransform` cỡ 0 neo đúng pivot của `OutlineGraphic`, nên không gian local của
  Graphic trùng với của `OutlineGraphic` dù RectTransform của nó to nhỏ thế nào hay bị layout đổi
  kích thước.
- Con đứng đầu danh sách con, nên viền vẽ ngay sau GameObject của `OutlineGraphic` và trước các
  con khác của nó, như khi chính GameObject đó là một Graphic. Hệ quả: `GetChild(0)` của
  GameObject này trả về con ẩn.
- Graphic là `ILayoutIgnorer`: LayoutGroup đặt trên GameObject của `OutlineGraphic` bỏ qua con
  ẩn, không xếp nó thành một ô (đã đo với HorizontalLayoutGroup).
- Viền không bao giờ nhận input: *Raycast Target* của Graphic tắt.
- Đổi `Maskable` lúc chạy có tác dụng ngay với cả RectMask2D. Setter `maskable` của uGUI chỉ
  tính lại stencil; RectMask2D chỉ nhận thay đổi ở lần Graphic bật lại hay đổi cha (đã đo), nên
  component gọi thêm `RecalculateClipping` khi giá trị đổi.

## Setting

Chỉnh trong Play mode: xem [Lớp nền](../../README.md#chỉnh-setting-trong-play-mode). Độ dày
cũng đổi ngay, trong phạm vi vùng đệm của lần chụp trước, rồi kèm một lần chụp lại vì độ dày là
vùng đệm (và phần nở của bóng `Directional`).

**Visual**

| Field | Ý nghĩa |
|---|---|
| `alphaCutoff` | Texel của texture chính của Graphic có alpha lớn hơn mức này thì thuộc silhouette |

**Visual/Outline**

| Field | Ý nghĩa |
|---|---|
| `color` | Màu viền, cũng là `IOutline.Color`; là Color của Graphic tự sinh nên nhân alpha của CanvasGroup như mọi Graphic |
| `width` | Độ dày theo đơn vị root canvas, xem [Trong Canvas](#trong-canvas); là vùng đệm của lần chụp khi không có bóng, có bóng thì cộng thêm khoảng cách và độ mềm (`Directional`) hoặc khoảng tản (`Spread`) |

**Visual/Shadow**

| Field | Ý nghĩa |
|---|---|
| `shadowEnabled` | Ô đánh dấu trên tiêu đề mục: bật bóng. Tắt thì mục không mở được. Mặc định tắt |
| `shadowColor` | Màu bóng, alpha là độ đục. Mặc định đen, alpha 0.5 |
| `shadowType` | `Directional` (đổ một hướng) hoặc `Spread` (tản đều mọi phía); setting của kiểu kia bị ẩn. Mặc định `Directional` |
| `shadowDirection` | `Directional`: hướng bóng đổ theo trục của root canvas (`DirectionExt`). Mặc định xuống |
| `shadowDistance` | `Directional`: khoảng cách bóng đổ theo đơn vị root canvas. Mặc định 8 |
| `shadowSoftness` | `Directional`: độ mềm theo đơn vị root canvas, quãng mép bóng mờ dần. Nướng vào lần chụp, một mức blur. Mặc định 10 |
| `shadowSpread` | `Spread`: khoảng tản theo đơn vị root canvas ra ngoài mép viền. Mặc định 16 |

Bóng `Directional` là của silhouette nở thêm `width`, nên khi nó bật, vùng đệm của lần chụp là
`width + shadowDistance + shadowSoftness`. Với `Spread`, vùng đệm là `width + shadowSpread`. Vì
`width` luôn nằm trong vùng đệm, đổi nó luôn phải chụp lại.

**Visual/Advanced**

| Field | Ý nghĩa |
|---|---|
| `maskable` | Bật thì viền bị Mask / RectMask2D phía trên cắt như mọi Graphic |

**Performance**

| Field | Ý nghĩa |
|---|---|
| `performance` | Profile hiệu năng, như của `OutlineRenderer` (xem [Hiệu năng](../../Performance/README.md)). `Resolution`: khoảng `Resolution²` pixel mỗi lần chụp, kéo từ 64 tới 1024, mặc định 256; bảng đo và cách chọn ở [OutlineRenderer](../OutlineRenderer/README.md#chọn-resolution) |

Màu và material của các Graphic trong nhóm không ảnh hưởng silhouette: Graphic đang mờ đi vẫn
được viền theo đúng hình của nó.

## Trong Canvas

- Viền sort theo hierarchy như mọi Graphic (đặt `OutlineGraphic` sau các Graphic cần viền), nhân
  alpha của CanvasGroup, bị Mask và RectMask2D phía trên cắt khi `maskable` bật.
- Silhouette lấy từ đúng mesh uGUI đã giao cho CanvasRenderer của từng Graphic, nên mọi *Image
  Type* (Simple, Sliced, Tiled, Filled), *Preserve Aspect* và *Use Sprite Mesh* đều theo đúng
  hình Image vẽ ra. Image không có sprite được viền theo hình chữ nhật của nó.
- `width` tính bằng đơn vị của root canvas (với Canvas Scaler là đơn vị của reference
  resolution), bất kể scale nằm giữa root canvas và `OutlineGraphic`. Scale đổi sau lúc chụp,
  chẳng hạn nhóm nảy, thì viền giữ nguyên độ dày đó trong phạm vi vùng đệm đã chụp: chỉ dựng
  lại quad, không chụp lại.
- Khoảng cách và độ mềm của bóng cũng theo đơn vị root canvas; hướng tính theo trục của root
  canvas rồi đổi sang không gian của `OutlineGraphic`, nên xoay nhóm thì bóng vẫn đổ về cùng một
  phía. Canvas không zoom nên mỗi lần chụp chỉ blur một mức. Scale đổi sau lúc chụp thì khoảng
  cách và mép sắc vẫn giữ đơn vị (tính lại khi dựng quad), còn phần blur đã nướng nên co giãn
  theo nhóm.
- Canvas bỏ `uv1` và `uv2` của đỉnh trừ khi root canvas bật kênh tương ứng. Khi bóng bật,
  `OutlineGraphic` tự thêm `TexCoord1` và `TexCoord2` vào *Additional Shader Channels* của root
  canvas, như TextMesh Pro tự thêm kênh nó dùng; bóng tắt thì không đụng tới canvas.
- Quad của viền nằm quanh nhóm chứ không nằm trên RectTransform nào, nên RectTransform của
  `OutlineGraphic` không cần khớp gì. Viền chỉ bị cull khi RectMask2D phía trên không còn vùng
  nhìn thấy; còn lại RectMask2D cắt từng pixel như với Image.
- Đã đo trong Play mode trên cả tám nhóm của scene review (Simple, Sliced, Filled, Preserve
  Aspect, trong RectMask2D đang cuộn, trong Mask hình tròn...): quad ôm nhóm, lề mỗi cạnh bằng
  `width` cộng phần đệm khử răng cưa; trong RectMask2D thì có rect clipping, trong Mask thì có
  stencil.

## Nguồn vẽ

`GraphicSource` vẽ mỗi Graphic bằng mesh uGUI đã giao cho CanvasRenderer của nó
(`CanvasRenderer.GetMesh()`), với texture chính của Graphic (`mainTexture`) đặt vào `_MainTex` qua
MaterialPropertyBlock. Vẽ thẳng mesh đó thì mask trống (đo trong Editor trên D3D12: 0 texel,
còn bản chép của cùng mesh ra 12652 texel), nên mỗi Graphic được chép sang một mesh riêng bằng
`Mesh.CombineMeshes` rồi mới vẽ. Các mesh chép được giữ lại cho lần chụp sau và huỷ cùng
component. Graphic tắt hoặc chưa có mesh thì bị bỏ qua.

## Shader

`OutlineGraphic.shader` (`RiseOn/Outline2D/OutlineGraphic`) là material của Graphic tự sinh. Vành
viền giống shader của `OutlineRenderer` ([Lớp nền](../../README.md#vành-viền)); phần riêng:

- Theo cấu trúc của `UI/Default` (đối chiếu với template Canvas của Shader Graph trong Unity 6):
  stencil và `ColorMask` cho Mask, `ZTest [unity_GUIZTestMode]`, clip rect có độ mềm cho
  RectMask2D, `UNITY_UI_ALPHACLIP`, `_UIVertexColorAlwaysGammaSpace`, alpha làm tròn theo bước
  1/255 và blend premultiplied.
- Màu: `Graphic.color` được ghi vào màu đỉnh lúc dựng mesh như mọi Graphic, Canvas lo alpha của
  CanvasGroup và việc đổi gamma.
- Canvas gộp mesh của mọi Graphic trong không gian canvas, nên vertex shader không biết scale
  riêng của `OutlineGraphic`. Bán kính vành (tính bằng texel field, đã kẹp) được tính trên CPU và
  đi trong `uv0.z`, kích thước field trong `uv0.w`; Canvas giữ đủ bốn thành phần của `uv0`
  (TextMesh Pro cũng đọc `uv0.w`). Nhờ vậy material không đổi theo instance, và Mask chép
  material một lần là đủ. Chỉ bật, tắt bóng hay đổi kiểu bóng mới đổi material: component chuyển
  sang một material mới ở biến thể kia (keyword `_SHADOW` hoặc `_SHADOW_SPREAD`) chứ không đổi
  keyword trên material cũ, vì Mask giữ bản chép stencil chừng nào material gốc vẫn là cùng một
  object.
- Bóng đi cùng đường đó: độ dịch (texel field, đã đổi sang không gian của `OutlineGraphic`) trong
  `uv1.xy`, độ mềm (texel field) trong `uv1.z` (bóng `Spread` để khoảng tản, tính bằng texel field
  và đã kẹp, ở đó; `uv1.xy` khi đó bằng 0), kích thước khung trong `uv1.w` dưới dạng `rộng +
  cao·4096` (cạnh khung không quá 2048 nên tổng dưới 2²⁴, float giữ chính xác), màu bóng trong
  `uv2`. Màu bóng được đổi sang linear trên CPU khi project dùng linear color space, vì Canvas chỉ
  đổi màu đỉnh chứ không đổi `uv2`. Fragment đọc R và G tại điểm đã dịch, pha mép sắc từ khoảng cách
  với mức blur theo độ mềm như [Lớp nền](../../README.md#bóng-đổ) tả, bỏ những điểm nằm ngoài khung,
  rồi vẽ vành đè lên bóng.
- Đổi màu hay hướng của bóng chỉ dựng lại mesh. Khoảng cách, độ mềm, khoảng tản, `width`, bật hay
  tắt bóng và đổi kiểu bóng thì chụp lại; bật, tắt bóng `Directional` hay đổi kiểu bóng còn cấp phát
  lại field, và viền ẩn cho tới khi field mới về thay vì hiện texture chưa có gì trên khung cũ.
- Field được đọc bằng `sampler2D_float`: trên OpenGL ES, `sampler2D` mặc định chỉ có độ chính
  xác thấp, không giữ nổi khoảng cách tính bằng texel.

## Giới hạn

- Phần Graphic bị Mask / RectMask2D che vẫn góp cả hình vào silhouette; viền nằm cùng Mask thì bị
  cắt cùng chỗ.
- Silhouette chỉ theo alpha của texture chính của Graphic: material riêng của Graphic và texture
  alpha tách rời (ETC1 split alpha) không được tính.
