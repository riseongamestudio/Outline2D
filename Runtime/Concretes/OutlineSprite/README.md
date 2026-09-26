# OutlineSprite

[← RiseOn.Outline2D](../../../README.md)

`OutlineSprite` viền một nhóm `SpriteRenderer`, hợp đồng là `IOutlineSprite`. Viền được vẽ bằng
một `SpriteRenderer` tự sinh trên con ẩn ([Lớp nền](../../README.md#con-ẩn-vẽ-viền)); màu,
sorting và mask interaction đặt trên component. Thư mục này còn chứa shader viền sprite và nguồn
vẽ `SpriteSource`; phần chụp và distance field dùng chung với `OutlineImage`
([Field](../../Field/README.md)).

- [Hợp đồng của SetTargets](#hợp-đồng-của-settargets)
- [Hệ toạ độ và thời điểm chụp](#hệ-toạ-độ-và-thời-điểm-chụp)
- [Vẽ bằng SpriteRenderer](#vẽ-bằng-spriterenderer)
- [Setting](#setting)
- [Chọn resolution](#chọn-resolution)
- [Độ dày theo zoom](#độ-dày-theo-zoom)
- [Nguồn vẽ](#nguồn-vẽ)
- [Shader](#shader)
- [Giới hạn](#giới-hạn)

## Hợp đồng của SetTargets

- Chuỗi renderer được đọc hết ngay trong lúc gọi rồi bỏ; renderer null bị bỏ qua. Truyền `null`
  hoặc chuỗi rỗng là tắt viền.
- Viền cũ tắt ngay lúc gọi, vì nó thuộc về nhóm cũ. Viền mới hiện sau vài frame: chụp mask,
  đọc mask về (bất đồng bộ), distance transform trên worker thread, upload, rồi mới hiện.
- Gọi nhiều lần trong một frame thì chỉ lần cuối được chụp. Kết quả của một lần chụp về
  sau khi đã có lần gọi mới thì bị bỏ.
- Silhouette chụp một lần. Nhóm đổi hình dạng giữa chừng (bật, tắt, đổi sprite) thì phải gọi
  lại `SetTargets`.

## Hệ toạ độ và thời điểm chụp

Silhouette được chụp trong không gian local của `OutlineSprite`, không phải của camera. Vì vậy
nhóm di chuyển, camera di chuyển hay zoom đều không cần chụp lại, với điều kiện nhóm cứng so
với `OutlineSprite` từ lúc chụp tới lần gọi sau: làm nó thành con của nhóm, hoặc cho nó bám
nhóm bằng Position / Rotation / Scale Constraint.

Chụp chạy trong `LateUpdate` của frame gọi, không chạy ngay trong `SetTargets`. Trong
`PreLateUpdate` của Unity, `ConstraintManagerUpdate` đứng trước `ScriptRunBehaviourLateUpdate`
(đã đo trên Unity 6000.3), nên lúc chụp thì constraint vừa đổi nguồn đã kịp đặt `OutlineSprite`
vào đúng nhóm mới. Nếu bám bằng script tự viết trong `LateUpdate` thì thứ tự giữa hai script
không được bảo đảm; khi đó hãy cập nhật vị trí trong `Update`.

## Vẽ bằng SpriteRenderer

- Mỗi lần chụp tạo một Sprite mới trên phần field của khung chụp: một pixel của sprite là một
  texel, pivot đặt đúng gốc toạ độ của transform, nên không gian local của sprite trùng với của
  `OutlineSprite`. Pivot và pixels per unit chỉ đặt được lúc tạo sprite, nên sprite cũ bị huỷ
  và thay bằng sprite mới.
- Rect của sprite chạy từ tâm texel đầu tới tâm texel cuối của khung, nên bốn góc nằm đúng tâm
  texel và lọc tuyến tính không với ra ngoài khung chụp. `Sprite.Create` giữ nguyên rect lẻ nửa
  texel (đã đo) và tốn vài µs; đặt góc bằng `Sprite.OverrideGeometry` thì tốn khoảng 190 µs mỗi
  lần chụp (đo trong Editor), nên không dùng.
- *Sorting Layer*, *Order* và SortingGroup phía trên có tác dụng như với mọi sprite.
- *Mask Interaction* được giao cho `SpriteRenderer` của con, và SpriteMask cắt viền như cắt một
  sprite. Đã đo với shader viền: không có mask thì *Visible Inside Mask* không vẽ gì; mask phủ
  nửa trái thì phần trong mask cộng phần ngoài mask đúng bằng cả viền.
- `SpriteRenderer` của con được sinh mới nên luôn ở *Draw Mode* Simple và không lật; không ai
  khác chạm tới nó.
- Đã đo trong Editor: đỉnh và UV của sprite trùng với quad tính từ khung chụp (lệch cỡ 1e-7);
  màu viền đổi theo `Color`; nhóm phóng to gấp đôi thì viền vẫn dày đúng bằng số pixel cũ, tức
  là không batching nào làm mất scale của object mà shader cần.

## Setting

Chỉnh trong Play mode: xem [Lớp nền](../../README.md#chỉnh-setting-trong-play-mode).

**Visual**

| Field | Ý nghĩa |
|---|---|
| `color` | Màu viền, alpha là độ đục; cũng là `IOutline.Color` |
| `alphaCutoff` | Texel của sprite có alpha lớn hơn mức này thì thuộc silhouette |
| `orthoRange`, `widthRange` | Độ dày theo world ở hai mức ortho, xem [Độ dày theo zoom](#độ-dày-theo-zoom). `max(widthRange)` còn là vùng đệm của lần chụp |

**Visual/Advanced**

| Field | Ý nghĩa |
|---|---|
| `sortingLayerID` | Sorting Layer của viền, chọn trong danh sách layer của project. Lưu unique ID của layer như renderer lưu `m_SortingLayerID`, nên đổi tên hay đổi thứ tự layer vẫn đúng; ID không còn hợp lệ thì renderer về Default (đã đo) |
| `sortingOrder` | Order trong layer. Renderer lưu order bằng 16 bit: Inspector kẹp trong −32768…32767 như Inspector của Unity; property `SortingOrder` thì như `Renderer.sortingOrder`, giá trị ngoài khoảng bị cuộn (đã đo: 40000 thành −25536) |
| `maskInteraction` | Tác dụng của SpriteMask lên viền, như của một sprite |

**Optimizations**

| Field | Ý nghĩa |
|---|---|
| `resolution` | Độ phân giải chụp: một lần chụp có khoảng `resolution²` pixel (rộng × cao), trải theo tỉ lệ của nhóm. Kéo từ 64 tới 1024 (4K tới 1M pixel), mặc định 256. Là núm chỉnh chi phí duy nhất: cùng giá trị thì cùng chi phí, bất kể nhóm to, nhỏ hay dẹt |

Mỗi cạnh của khung chụp tối đa `2·resolution` (256 là 512): nhóm dẹt tới 4:1 vẫn dùng hết
`resolution²` pixel, dẹt hơn thì chụp ít pixel hơn chứ không tốn thêm. Bộ đệm giữ sẵn theo
đúng cạnh đó: khoảng `12·resolution²` byte trên GPU (mask R8, field half-float) và
`33·resolution²` byte trên CPU; 256 là 768 KB và 2.1 MB, 1024 là 12 MB và 33 MB.

Màu và alpha của các sprite trong nhóm không ảnh hưởng silhouette: sprite đang mờ đi vẫn được
viền theo đúng hình của nó.

## Chọn resolution

Đo trên một nhóm cỡ cái áo (3.5 × 3 world, vùng đệm 0.2) trong OrganiCat, render 1080 × 1920.
Lệch mép là độ dịch trung bình của mép viền so với bản 1024, tính bằng pixel màn hình;
distance transform và upload đo trong Editor (Ryzen 9, Burst).

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
  [Field](../../Field/README.md#đo-trên-máy-thật)).

## Độ dày theo zoom

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

## Nguồn vẽ

`SpriteSource` vẽ mỗi renderer bằng một `DrawRenderer` vào mask. Unity đưa vào texture của
sprite và hình học đã lật sẵn theo `flipX` / `flipY` (đã đo: trùng khít với việc tự dựng mesh từ
`sprite.vertices`, cả khi lật), nên shader mask không xử lý lật.

## Shader

`OutlineSprite.shader` (`RiseOn/Outline2D/OutlineSprite`), viết cho URP (tag
`UniversalPipeline`), là material của `SpriteRenderer` trên con ẩn. Vành viền giống shader của
`OutlineImage` ([Lớp nền](../../README.md#vành-viền)); phần riêng:

- Field là texture của sprite, `SpriteRenderer` đưa vào `_MainTex`. Kích thước field nằm trong
  `_FieldSize` trên material riêng của component, không trông vào `_MainTex_TexelSize`.
- Màu: `SpriteRenderer.color` tới shader qua `unity_SpriteColor` và màu đỉnh, shader nhân cả
  hai như shader sprite của URP (`Sprite-Unlit-Default`). Blend thường, alpha không nhân trước,
  như mọi shader sprite.
- Shader không khai báo stencil: stencil của SpriteMask do `SpriteRenderer` đặt theo *Mask
  Interaction*, như với shader sprite của URP.
- *Flip X* / *Flip Y* không được áp: quad đã nằm đúng không gian của object, lật nó sẽ đẩy viền
  lệch khỏi nhóm.
- Độ dày tính ngay trong vertex shader: đọc `unity_OrthoParams.y` (ortho size của camera đang
  vẽ), nội suy độ dày world theo `_OrthoRange` / `_WidthRange`, đổi sang texel bằng độ dài trục
  X của ma trận object và `_TexelSize`, rồi kẹp ở `_MaxRadius` để vành không chạy ra khỏi vùng
  đã chụp. Không script nào phải cập nhật material theo camera.

## Giới hạn

- Chỉ có nghĩa với camera orthographic.
- Scale không đồng nhất chỉ được xấp xỉ theo trục X khi đổi độ dày world sang local.
- SpriteMask trên các sprite trong nhóm không được tính khi chụp: phần sprite bị mask che vẫn
  nằm trong silhouette. Riêng viền thì bị SpriteMask cắt theo `maskInteraction`.
- Silhouette chỉ theo alpha của texture chính: texture alpha tách rời (ETC1 split alpha) không
  được tính.
- Sprite trong nhóm ở *Draw Mode* Sliced / Tiled chưa được kiểm.
