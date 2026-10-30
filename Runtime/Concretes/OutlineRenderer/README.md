# OutlineRenderer

[← RiseOn.Outline2D](../../../README.md)

`OutlineRenderer` viền một nhóm `Renderer`, hợp đồng là `IOutlineRenderer`: sprite, hay mesh như
`MeshRenderer` của skeleton Spine, chung một silhouette. Viền, cùng bóng đổ
tuỳ chọn ([Lớp nền](../../README.md#bóng-đổ)), được vẽ bằng một `SpriteRenderer` tự sinh trên con
ẩn ([Lớp nền](../../README.md#con-ẩn-vẽ-viền)); màu, bóng, sorting và mask interaction đặt trên
component. Thư mục này còn chứa shader viền và nguồn vẽ `RendererSource`; phần chụp và distance
field dùng chung với `OutlineGraphic` ([Field](../../Field/README.md)).

- [Hợp đồng của SetTargets](#hợp-đồng-của-settargets)
- [Hệ toạ độ và thời điểm chụp](#hệ-toạ-độ-và-thời-điểm-chụp)
- [Vẽ bằng SpriteRenderer](#vẽ-bằng-spriterenderer)
- [Setting](#setting)
- [Chọn Resolution](#chọn-resolution)
- [Độ dày và bóng theo zoom](#độ-dày-và-bóng-theo-zoom)
- [Nguồn vẽ](#nguồn-vẽ)
- [Shader](#shader)
- [Giới hạn](#giới-hạn)

## Hợp đồng của SetTargets

- Chuỗi renderer được đọc hết ngay trong lúc gọi rồi bỏ; renderer null bị bỏ qua. Truyền `null`
  hoặc chuỗi rỗng là tắt viền.
- Viền cũ tắt ngay lúc gọi, vì nó thuộc về nhóm cũ. Viền mới hiện ngay trong frame đó với độ trễ
  `Immediate`, sau vài frame với `Async` (xem [Hiệu năng](../../Performance/README.md)).
- Gọi nhiều lần trong một frame thì chỉ lần cuối được chụp. Kết quả của một lần chụp về
  sau khi đã có lần gọi mới thì bị bỏ.
- Với tần suất `OnDemand`, silhouette chụp một lần: nhóm đổi hình dạng giữa chừng (bật, tắt, đổi
  sprite) thì phải gọi lại `SetTargets`. Nhóm tự đổi hình dạng (skeleton, sprite có animation) thì
  dùng `FrameInterval` hoặc `TimeInterval` để chụp lại định kỳ. Renderer bị tắt, nằm trên
  GameObject inactive hay có sprite rỗng lúc chụp thì không có trong silhouette lần đó.

## Hệ toạ độ và thời điểm chụp

Silhouette được chụp trong không gian local của `OutlineRenderer`, không phải của camera. Vì vậy
nhóm di chuyển, camera di chuyển hay zoom đều không cần chụp lại, với điều kiện nhóm cứng so
với `OutlineRenderer` từ lúc chụp tới lần gọi sau: làm nó thành con của nhóm, hoặc cho nó bám
nhóm bằng Position / Rotation / Scale Constraint.

Chụp chạy trong `LateUpdate` của frame gọi, không chạy ngay trong `SetTargets`. Trong
`PreLateUpdate` của Unity, `ConstraintManagerUpdate` đứng trước `ScriptRunBehaviourLateUpdate`
(đã đo trên Unity 6000.3), nên lúc chụp thì constraint vừa đổi nguồn đã kịp đặt `OutlineRenderer`
vào đúng nhóm mới. Nếu bám bằng script tự viết trong `LateUpdate` thì thứ tự giữa hai script
không được bảo đảm; khi đó hãy cập nhật vị trí trong `Update`.

## Vẽ bằng SpriteRenderer

- Sprite của viền phủ phần field của khung chụp: một pixel của sprite là một texel, pivot đặt
  đúng gốc toạ độ của transform, nên không gian local của sprite trùng với của `OutlineRenderer`.
  Pivot và pixels per unit chỉ đặt được lúc tạo sprite, nên lần chụp có khung mới (gốc, cỡ texel
  hay số texel khác đi) tạo sprite mới và huỷ sprite cũ; lần chụp có đúng khung cũ, như nhóm đứng
  yên được chụp lại định kỳ, dùng lại sprite cũ vì chỉ nội dung texture đổi.
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
| `alphaCutoff` | Texel của texture target (sprite, hay texture chính của material ở từng submesh) có alpha lớn hơn mức này thì thuộc silhouette |
| `orthoRange` | Hai mức ortho mà mọi range bên dưới lấy `.x` và `.y`, xem [Độ dày và bóng theo zoom](#độ-dày-và-bóng-theo-zoom) |

**Visual/Outline**

| Field | Ý nghĩa |
|---|---|
| `color` | Màu viền, alpha là độ đục; cũng là `IOutline.Color` |
| `widthRange` | Độ dày theo world ở hai mức ortho. `max(widthRange)` là vùng đệm của lần chụp khi không có bóng; có bóng thì cộng thêm khoảng cách và độ mềm (`Directional`) hoặc khoảng tản (`Spread`) |

**Visual/Shadow**

| Field | Ý nghĩa |
|---|---|
| `shadowEnabled` | Ô đánh dấu trên tiêu đề mục: bật bóng. Tắt thì mục không mở được. Mặc định tắt |
| `shadowColor` | Màu bóng, alpha là độ đục. Mặc định đen, alpha 0.5 |
| `shadowType` | `Directional` (đổ một hướng) hoặc `Spread` (tản đều mọi phía); setting của kiểu kia bị ẩn. Mặc định `Directional` |
| `shadowDirection` | `Directional`: hướng bóng đổ theo trục world (`DirectionExt`: trái, phải, lên, xuống, hoặc góc tự chọn). Mặc định xuống |
| `shadowDistanceRange` | `Directional`: khoảng cách bóng đổ theo world ở hai mức ortho. Mặc định (0.12, 0.48) |
| `shadowSoftnessRange` | `Directional`: độ mềm theo world ở hai mức ortho, quãng mép bóng mờ dần. Nướng vào lần chụp. Mặc định (0.12, 0.48) |
| `shadowSpreadRange` | `Spread`: khoảng tản theo world ra ngoài mép viền, ở hai mức ortho. Mặc định (0.15, 0.6) |

Bóng `Directional` là của silhouette nở thêm độ dày viền, nên khi nó bật, vùng đệm của lần chụp là
`max(widthRange) + max(shadowDistanceRange) + max(shadowSoftnessRange)`, và đổi `widthRange` cũng
phải chụp lại. Với `Spread`, vùng đệm là `max(widthRange) + max(shadowSpreadRange)`.

**Visual/Advanced**

| Field | Ý nghĩa |
|---|---|
| `sortingLayerID` | Sorting Layer của viền, chọn trong danh sách layer của project. Lưu unique ID của layer như renderer lưu `m_SortingLayerID`, nên đổi tên hay đổi thứ tự layer vẫn đúng; ID không còn hợp lệ thì renderer về Default (đã đo) |
| `sortingOrder` | Order trong layer. Renderer lưu order bằng 16 bit: Inspector kẹp trong −32768…32767 như Inspector của Unity; property `SortingOrder` thì như `Renderer.sortingOrder`, giá trị ngoài khoảng bị cuộn (đã đo: 40000 thành −25536) |
| `maskInteraction` | Tác dụng của SpriteMask lên viền, như của một sprite |

**Performance**

| Field | Ý nghĩa |
|---|---|
| `performance` | Profile hiệu năng, cũng là `IOutline.Performance`: `Resolution`, `Frequency` (kèm `Frames` hoặc `Seconds`), `Quality`, `Latency`. Mặc định 256, `OnDemand`, `Exact`, `Async`. Xem [Hiệu năng](../../Performance/README.md) |

`Resolution` là độ phân giải chụp: một lần chụp có khoảng `Resolution²` pixel (rộng × cao), trải
theo tỉ lệ của nhóm, kéo từ 64 tới 1024 (4K tới 1M pixel). Cùng giá trị thì cùng chi phí mỗi lần
chụp, bất kể nhóm to, nhỏ hay dẹt.

Mỗi cạnh của khung chụp tối đa `2·Resolution` (256 là 512): nhóm dẹt tới 4:1 vẫn dùng hết
`Resolution²` pixel, dẹt hơn thì chụp ít pixel hơn chứ không tốn thêm. Bộ đệm giữ sẵn theo
đúng cạnh đó: khoảng `12·Resolution²` byte trên GPU (mask R8, field half-float) và
`33·Resolution²` byte trên CPU; 256 là 768 KB và 2.1 MB, 1024 là 12 MB và 33 MB. Khi bóng
`Directional` bật, field có bốn kênh half-float (cả mảng job lẫn bản CPU của texture) và thêm ba
mặt phẳng blur nửa độ phân giải: khoảng `36·Resolution²` byte trên GPU và `93·Resolution²` byte
trên CPU; 256 là 2.3 MB và 5.8 MB, 1024 là 36 MB và 93 MB. `Fast` cần thêm ba render target,
`72·Resolution²` byte GPU (`96·Resolution²` khi có bóng `Directional`), tức 4.5 MB (6 MB) ở 256
(xem [Field](../../Field/README.md#khung-chụp-theo-ngân-sách-texel)).

Màu và alpha của các renderer trong nhóm không ảnh hưởng silhouette: sprite đang mờ đi vẫn được
viền theo đúng hình của nó.

## Chọn Resolution

Đo trên một nhóm cỡ cái áo (3.5 × 3 world, vùng đệm 0.2) trong OrganiCat, render 1080 × 1920.
Lệch mép là độ dịch trung bình của mép viền so với bản 1024, tính bằng pixel màn hình;
distance transform và upload đo trong Editor (Ryzen 9, Burst).

| `Resolution` | Texel (world) | Lệch mép, ortho 5.5 | Lệch mép, ortho 25 | Distance transform, 1 luồng / 2 worker | Upload |
|---|---|---|---|---|---|
| 64 | 0.060 | 2.2 px | 0.6 px | 0.07 / 0.06 ms | < 0.01 ms |
| 128 | 0.029 | 1.1 px | 0.4 px | 0.27 / 0.14 ms | 0.02 ms |
| 256 | 0.014 | 0.5 px | 0.1 px | 0.95 / 0.59 ms | 0.04 ms |
| 512 | 0.0071 | 0.3 px | 0.05 px | 3.8 / 2.2 ms | 0.13 ms |
| 1024 | 0.0036 | mốc | mốc | 16.4 / 8.5 ms | 0.74 ms |

- Lệch mép giảm một nửa mỗi lần `Resolution` gấp đôi, và bằng khoảng 1/5 kích thước một texel
  trên màn hình. Trong ảnh so sánh, lệch cỡ nửa pixel trở xuống thì mép viền trông mịn; từ 1 px
  trở lên thì thấy gợn sóng dọc các cạnh chéo (128 và 64 ở zoom gần).
- Texel tỉ lệ với cỡ nhóm: nhóm to gấp đôi thì cần `Resolution` gấp đôi để giữ cùng độ mịn.
- Vùng đệm cũng ăn vào ngân sách: bóng xa và mềm làm khung rộng ra, mỗi texel phủ nhiều world hơn,
  nên có thể cần nâng `Resolution` để giữ mép viền mịn như khi không có bóng.
- Chi phí tăng theo `Resolution²`. Trên OPPO CPH2121, distance transform một luồng đo được
  3.1 ms ở 256 và 12.6 ms ở 512, khoảng 3.3 lần số Editor ở trên (bảng đầy đủ ở
  [Field](../../Field/README.md#đo-trên-máy-thật)).

## Độ dày và bóng theo zoom

`widthRange.x` là độ dày (đơn vị world) khi camera có orthographic size bằng
`orthoRange.x`, `widthRange.y` ứng với `orthoRange.y`. Ở giữa thì nội suy tuyến tính, ra ngoài
khoảng thì giữ ở đầu gần nhất. Hai đầu cùng tỉ lệ với nhau là độ dày không đổi theo pixel màn
hình; hai đầu bằng nhau là độ dày không đổi theo world.

`shadowDistanceRange` và `shadowSoftnessRange` theo đúng cách đó, cùng một `orthoRange`, nên
vành và bóng giữ nguyên tỉ lệ với nhau ở mọi mức zoom. Khoảng cách chỉ là chỗ shader đọc field,
nên đổi liên tục; độ mềm ở mức zoom đang vẽ cũng vào shader để chọn giữa mép sắc từ khoảng cách
và blur (xem [Lớp nền](../../README.md#bóng-đổ)). Phần blur thì đã nướng: mỗi lần chụp làm ba
mức, mỗi mức là một mức zoom (hai đầu của `orthoRange` và một mức ở giữa), nở theo độ dày và blur
theo độ mềm của chính mức zoom đó; shader pha hai mức kề nhau theo zoom đang vẽ.

Shader pha vị trí mép chứ không pha độ phủ. Hai mức nở theo hai độ dày khác nhau có mép lệch
nhau; pha độ phủ thì thấy cả hai mép, thành một dốc lúc rộng lúc hẹp khi zoom. Mép đã blur gần
với đường cong logistic, nên logit của độ phủ là khoảng cách tới mép chia cho độ rộng blur; shader
pha các logit (nhân độ rộng blur của từng mức, tính cả phần mềm do nửa độ phân giải) rồi đổi
ngược ra độ phủ. Trên mép thẳng, kết quả gần như một lần blur ở độ dày và độ mềm nằm giữa hai
mức (mép blur là đường Gauss, chỉ gần logistic); ở góc và chỗ mảnh thì sai khác lớn hơn, nhưng ở
đâu kết quả cũng nằm giữa độ phủ của hai mức.

Mức giữa đặt ở chỗ độ mềm bằng trung bình nhân của hai đầu (đầu sắc hơn được nâng lên ít nhất
1/16 đầu mềm hơn trước khi lấy trung bình nhân, nên một đầu bằng 0 vẫn có mức ở giữa; hai đầu
bằng nhau thì mức giữa ở chính giữa). Với dải độ mềm tới 4:1, hai mức kề nhau cách nhau không quá
2 lần, nên ở góc và chỗ mảnh hai mức vẫn gần nhau.

Camera chỉ tham gia lúc vẽ: shader đọc ortho size của chính camera đang vẽ nên nhiều camera vẫn
đúng, và zoom không bao giờ phải chụp lại. Khâu chụp chỉ cần một hằng số làm vùng đệm quanh
silhouette: độ dày lớn nhất, `max(widthRange)`, cộng tầm với lớn nhất của bóng khi có bóng
(`max(shadowDistanceRange) + max(shadowSoftnessRange)` với `Directional`, `max(shadowSpreadRange)`
với `Spread`). Vì vậy range rộng thì vùng đệm to và mỗi texel phủ nhiều world hơn: đó là cái giá của
việc cho độ dày và bóng chạy theo zoom.

Độ dày theo world được đổi sang không gian local bằng scale hiện tại của `OutlineRenderer`, nên
nhóm nảy (scale) sau lúc chụp thì viền vẫn giữ đúng độ dày world. Viền bị kẹp để không vượt
vùng đệm đã chụp. Khoảng cách và mép sắc của bóng cũng được tính lại như vậy; phần blur thì đã
nướng theo scale lúc chụp nên co giãn theo nhóm.

## Nguồn vẽ

`RendererSource` vẽ mỗi renderer bằng `DrawRenderer` vào mask, với hình học hiện tại của chính
renderer đó. Với sprite, Unity đưa vào texture của sprite và hình học đã lật sẵn theo `flipX` /
`flipY` (đã đo: trùng khít với việc tự dựng mesh từ `sprite.vertices`, cả khi lật), nên shader mask
không xử lý lật. Renderer khác vẽ từng submesh, mỗi submesh với texture chính (`mainTexture`) của
material ở submesh đó; submesh mà material không có texture chính thì bị bỏ qua (xem
[Shader](../../Shaders/README.md#shader-mask-chỉ-đọc-alpha-của-texture)). Bounds của nhóm lấy từ
`localBounds` của từng renderer.

## Shader

`OutlineRenderer.shader` (`RiseOn/Outline2D/OutlineRenderer`), viết cho URP (tag
`UniversalPipeline`), là material của `SpriteRenderer` trên con ẩn. Vành viền giống shader của
`OutlineGraphic` ([Lớp nền](../../README.md#vành-viền)); phần riêng:

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
- Bóng cũng tính trong vertex shader với cùng hệ số zoom: khoảng cách nội suy từ
  `_ShadowDistanceRange`, nhân hướng `_ShadowDirection` (trục world), đổi sang không gian object
  bằng ma trận world-to-object rồi sang texel; hệ số zoom đổi thành trọng số của hai mức kề nhau (G
  là đầu gần, B là mức giữa ở `_ShadowLevels.x`, A là đầu xa), nhân độ rộng blur của từng mức để
  fragment pha theo logit (xem [Độ dày và bóng theo zoom](#độ-dày-và-bóng-theo-zoom)). Độ mềm nội
  suy từ `_ShadowSoftnessRange`, đổi sang texel như độ dày, thành độ rộng ramp của mép sắc và tỉ lệ
  pha giữa mép sắc và blur. Fragment đọc R, G, B, A tại điểm đã dịch, bỏ những điểm nằm ngoài khung
  (`_FrameSize`), rồi vẽ vành đè lên bóng.
- `_ShadowLevels.w` cho biết field đang hiện có các mức bóng hay không. Field chụp không có bóng
  thì trọng số và phần mép sắc bằng 0: texture một kênh đọc ra A bằng 1, nên nếu không vậy thì
  bật bóng đúng lúc một lần chụp không bóng đang chạy sẽ phủ bóng kín khung. Độ mềm bằng 0 thì
  vẫn là bóng sắc, không phải tắt.
- `_ShadowLevels` được đặt cùng lúc field mới hiện, không phải lúc bắt đầu chụp, nên trong lúc
  lần chụp sau đang chạy, shader vẫn pha đúng các mức của field đang hiện.
- Bóng `Spread` (biến thể `_SHADOW_SPREAD`) chỉ cần khoảng tản: vertex shader nội suy nó từ
  `_ShadowSpreadRange` theo zoom, đổi sang texel như độ dày, rồi kẹp ở `_MaxRadius` trừ bán kính
  vành; fragment tính bóng từ chính khoảng cách đã đọc cho vành (xem
  [Lớp nền](../../README.md#bóng-tản)).

## Giới hạn

- Chỉ có nghĩa với camera orthographic.
- Scale không đồng nhất chỉ được xấp xỉ theo trục X khi đổi độ dày world sang local.
- SpriteMask trên các sprite trong nhóm không được tính khi chụp: phần sprite bị mask che vẫn
  nằm trong silhouette. Riêng viền thì bị SpriteMask cắt theo `maskInteraction`.
- Silhouette chỉ theo alpha của texture chính: texture alpha tách rời (ETC1 split alpha) không
  được tính. Với mesh, đó là `mainTexture` của material ở từng submesh, nên shader nào đọc alpha
  từ texture khác thì silhouette không theo được.
- Sprite trong nhóm ở *Draw Mode* Sliced / Tiled chưa được kiểm.
- Bóng vẽ ở sorting của viền, nên đổ lên những gì nằm dưới order đó và bị những gì nằm trên che;
  SpriteMask cắt bóng như cắt viền.
