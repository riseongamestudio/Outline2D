# Outline

[← RiseOn.Outline2D](../../README.md)

Hai component cùng hợp đồng `IOutline<T>`: `OutlineSprite` viền một nhóm `SpriteRenderer` và
vẽ bằng MeshRenderer trên chính GameObject của nó; `OutlineImage` viền một nhóm `Image` và tự
là một Graphic trong Canvas. Cả hai dùng chung phần chụp và distance field
([Field](../Field/README.md)).

- [Hợp đồng của SetTargets](#hợp-đồng-của-settargets)
- [Hệ toạ độ và thời điểm chụp](#hệ-toạ-độ-và-thời-điểm-chụp)
- [Làm sẵn trong Awake](#làm-sẵn-trong-awake)
- [Setting](#setting)
- [Độ dày theo zoom của OutlineSprite](#độ-dày-theo-zoom-của-outlinesprite)
- [OutlineImage trong Canvas](#outlineimage-trong-canvas)
- [Giới hạn](#giới-hạn)

## Hợp đồng của SetTargets

- Chuỗi target được đọc hết ngay trong lúc gọi rồi bỏ; target null bị bỏ qua. Truyền `null`
  hoặc chuỗi rỗng là tắt viền.
- Viền cũ tắt ngay lúc gọi, vì nó thuộc về nhóm cũ. Viền mới hiện sau vài frame: chụp mask,
  đọc mask về (bất đồng bộ), distance transform trên worker thread, upload, rồi mới hiện.
- Gọi nhiều lần trong một frame thì chỉ lần cuối được chụp. Kết quả của một lần chụp về
  sau khi đã có lần gọi mới thì bị bỏ.
- Silhouette chụp một lần. Nhóm đổi hình dạng giữa chừng (bật, tắt, đổi sprite, đổi kích
  thước Image) thì phải gọi lại `SetTargets`.

## Hệ toạ độ và thời điểm chụp

Silhouette được chụp trong không gian local của component viền, không phải của camera. Vì vậy
nhóm di chuyển, camera di chuyển hay zoom đều không cần chụp lại, với điều kiện nhóm cứng so
với component viền từ lúc chụp tới lần gọi sau.

**OutlineSprite** chụp trong `LateUpdate` của frame gọi, không chạy ngay trong `SetTargets`.
Hãy làm nó thành con của nhóm, hoặc cho nó bám nhóm bằng Position / Rotation / Scale
Constraint. Trong `PreLateUpdate` của Unity, `ConstraintManagerUpdate` đứng trước
`ScriptRunBehaviourLateUpdate` (đã đo trên Unity 6000.3), nên lúc chụp thì constraint vừa đổi
nguồn đã kịp đặt `OutlineSprite` vào đúng nhóm mới. Nếu bám bằng script tự viết trong
`LateUpdate` thì thứ tự giữa hai script không được bảo đảm; khi đó hãy cập nhật vị trí trong
`Update`.

**OutlineImage** chụp ở bước cuối của lượt cập nhật Canvas trong frame gọi
(`GraphicUpdateComplete`): layout đã chạy xong và mọi Graphic được đánh dấu trong frame đã giao
mesh mới cho CanvasRenderer của nó. Vì thế Image vừa bật, vừa đổi kích thước hay vừa đổi sprite
ngay trong frame gọi vẫn được chụp đúng hình mới. Cách bám thường gặp là làm `OutlineImage`
thành con của nhóm, hoặc của cùng content đang cuộn.

## Làm sẵn trong Awake

`Awake` cấp phát bộ đệm theo `resolution` rồi vẽ thử một lần bằng shader mask và shader viền;
`OutlineImage` vẽ thử thêm biến thể mà Canvas dùng khi viền nằm dưới RectMask2D. Trên OpenGL ES,
lần vẽ đầu tiên với một shader bắt main thread chờ render thread dựng chương trình GPU của
shader đó: đo trên OPPO CPH2121 (PowerVR GM9446) mất khoảng 6 ms mỗi shader với `OutlineSprite`,
một lần ở frame chụp và một lần ở frame hiện viền. Làm sẵn thì các khoản này rơi vào lúc nạp
scene thay vì lần chọn đầu tiên.

Vì thế GameObject của component viền phải đang bật khi scene nạp. Script tắt GameObject đó
trước khi `Awake` của nó chạy, chẳng hạn trong `Awake` của một component đứng trước, sẽ dời cả
việc làm sẵn tới lần bật đầu tiên. Không cần tắt để giấu viền: viền tự ẩn cho tới lần
`SetTargets` đầu tiên.

`OutlineImage` là Graphic nên chạy cả trong edit mode, nhưng chỉ cấp phát, làm sẵn và chụp khi
đang Play.

## Setting

Chỉnh trong Play mode thì màu và độ dày đổi ngay; ngưỡng alpha, `resolution` và vùng đệm của
độ dày thì chụp lại. Mỗi lúc chỉ có một lần chụp: kéo slider liên tục thì lần đang chạy vẫn chạy
xong và hiện ra, lần sau lấy giá trị mới nhất, nên viền đổi theo tay kéo mà không chụp dồn mỗi
frame.

**Visual**

| Field | Component | Ý nghĩa |
|---|---|---|
| `alphaCutoff` | cả hai | Texel của texture có alpha lớn hơn mức này thì thuộc silhouette |
| `color` | `OutlineSprite` | Màu viền; alpha của màu là độ đục của viền |
| *Color* | `OutlineImage` | Color của Graphic, dùng làm màu viền và nhân alpha của CanvasGroup |
| `orthoRange`, `widthRange` | `OutlineSprite` | Độ dày theo world ở hai mức ortho, xem mục dưới. `max(widthRange)` còn là vùng đệm của lần chụp |
| `width` | `OutlineImage` | Độ dày theo đơn vị root canvas, xem mục dưới; cũng là vùng đệm của lần chụp |
| *Maskable* | `OutlineImage` | Của Graphic: bật thì viền bị Mask / RectMask2D phía trên cắt như Image |

**Optimizations**

| Field | Ý nghĩa |
|---|---|
| `resolution` | Độ phân giải chụp: một lần chụp có khoảng `resolution²` pixel (rộng × cao), trải theo tỉ lệ của nhóm. Kéo từ 64 tới 1024 (4K tới 1M pixel), mặc định 256. Là núm chỉnh chi phí duy nhất: cùng giá trị thì cùng chi phí, bất kể nhóm to, nhỏ hay dẹt |

Mỗi cạnh của khung chụp tối đa `2·resolution` (256 là 512): nhóm dẹt tới 4:1 vẫn dùng hết
`resolution²` pixel, dẹt hơn thì chụp ít pixel hơn chứ không tốn thêm. Bộ đệm giữ sẵn theo
đúng cạnh đó: khoảng `12·resolution²` byte trên GPU (mask R8, field half-float) và
`33·resolution²` byte trên CPU; 256 là 768 KB và 2.1 MB, 1024 là 12 MB và 33 MB.

**Chọn resolution.** Đo trên một nhóm cỡ cái áo (3.5 × 3 world, vùng đệm 0.2) trong OrganiCat,
render 1080 × 1920. Lệch mép là độ dịch trung bình của mép viền so với bản 1024, tính bằng
pixel màn hình; distance transform và upload đo trong Editor (Ryzen 9, Burst).

| `resolution` | Texel (world) | Lệch mép, ortho 5.5 | Lệch mép, ortho 25 | Distance transform, 1 luồng / 2 worker | Upload |
|---|---|---|---|---|---|
| 64 | 0.060 | 2.2 px | 0.6 px | 0.07 / 0.06 ms | < 0.01 ms |
| 128 | 0.029 | 1.1 px | 0.4 px | 0.27 / 0.14 ms | 0.02 ms |
| 256 | 0.014 | 0.5 px | 0.1 px | 0.95 / 0.59 ms | 0.04 ms |
| 512 | 0.0071 | 0.3 px | 0.05 px | 3.8 / 2.2 ms | 0.13 ms |
| 1024 | 0.0036 | mốc | mốc | 16.4 / 8.5 ms | 0.74 ms |

- Lệch mép giảm một nửa mỗi lần `resolution` gấp đôi, và bằng khoảng 1/5 kích thước một texel
  trên màn hình. Trong ảnh so sánh, lệch cỡ nửa pixel trở xuống thì mép viền trông mịn; từ 1 px
  trở lên thì thấy gợn sóng dọc các cạnh chéo (128 và 64 ở zoom gần).
- Texel tỉ lệ với cỡ nhóm: nhóm to gấp đôi thì cần `resolution` gấp đôi để giữ cùng độ mịn.
- Chi phí tăng theo `resolution²`. Trên OPPO CPH2121, distance transform một luồng đo được
  3.1 ms ở 256 và 12.6 ms ở 512, khoảng 3.3 lần số Editor ở trên (bảng đầy đủ ở
  [Field](../Field/README.md#đo-trên-máy-thật)).

Màu và alpha của renderer hay của Image không ảnh hưởng silhouette: target đang mờ đi vẫn được
viền theo đúng hình của nó.

## Độ dày theo zoom của OutlineSprite

`widthRange.x` là độ dày (đơn vị world) khi camera có orthographic size bằng
`orthoRange.x`, `widthRange.y` ứng với `orthoRange.y`. Ở giữa thì nội suy tuyến tính, ra ngoài
khoảng thì giữ ở đầu gần nhất. Hai đầu cùng tỉ lệ với nhau là độ dày không đổi theo pixel màn
hình; hai đầu bằng nhau là độ dày không đổi theo world.

Camera chỉ tham gia lúc vẽ: shader đọc ortho size của chính camera đang vẽ nên nhiều camera
vẫn đúng, và zoom không bao giờ phải chụp lại. Khâu chụp chỉ cần một hằng số là độ dày lớn
nhất, `max(widthRange)`, làm vùng đệm quanh silhouette. Vì vậy `widthRange` rộng thì vùng
đệm to và mỗi texel phủ nhiều world hơn: đó là cái giá của việc cho độ dày chạy theo zoom.

Độ dày theo world được đổi sang không gian local bằng scale hiện tại của `OutlineSprite`, nên
nhóm nảy (scale) sau lúc chụp thì viền vẫn giữ đúng độ dày world. Viền bị kẹp để không vượt
vùng đệm đã chụp.

## OutlineImage trong Canvas

- Viền là một Graphic như Image: sort theo hierarchy, lấy màu từ *Color*, nhân alpha của
  CanvasGroup, bị Mask và RectMask2D phía trên cắt khi *Maskable* bật.
- Silhouette lấy từ đúng mesh uGUI đã giao cho CanvasRenderer của từng Image, nên mọi *Image
  Type* (Simple, Sliced, Tiled, Filled), *Preserve Aspect* và *Use Sprite Mesh* đều theo đúng
  hình Image vẽ ra. Image không có sprite được viền theo hình chữ nhật của nó.
- `width` tính bằng đơn vị của root canvas (với Canvas Scaler là đơn vị của reference
  resolution), bất kể scale nằm giữa root canvas và `OutlineImage`. Scale đổi sau lúc chụp,
  chẳng hạn nhóm nảy, thì viền giữ nguyên độ dày đó trong phạm vi vùng đệm đã chụp: chỉ dựng
  lại quad, không chụp lại.
- Quad của viền nằm quanh nhóm chứ không nằm trên RectTransform của `OutlineImage`, nên
  RectTransform đó không cần khớp gì. Viền chỉ bị cull khi RectMask2D phía trên không còn vùng
  nhìn thấy; còn lại RectMask2D cắt từng pixel như với Image.
- Viền không bao giờ nhận input: `Awake` tắt *Raycast Target*, ô này cũng không hiện trên
  Inspector (xem [Editor](../../Editor/README.md)).

## Giới hạn

- `OutlineSprite` chỉ có nghĩa với camera orthographic.
- Scale không đồng nhất chỉ được xấp xỉ theo trục X khi đổi độ dày sang không gian local.
- Phần target bị che không được tính: sprite dưới `SpriteMask`, Image dưới Mask / RectMask2D vẫn
  góp cả hình vào silhouette. Viền của `OutlineImage` nằm cùng Mask thì bị cắt cùng chỗ.
- Silhouette chỉ theo alpha của texture chính: material riêng của Image, texture alpha tách rời
  (ETC1 split alpha) không được tính.
- Sprite ở *Draw Mode* Sliced / Tiled chưa được kiểm.
