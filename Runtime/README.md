# Lớp nền và phần chung của hai viền

[← RiseOn.Outline2D](../README.md)

`IOutline` là thứ chung của mọi viền: màu. Mỗi loại viền có interface riêng cho `SetTargets`
và setting vẽ của nó (`IOutlineSprite`, `IOutlineImage`), và một component cụ thể trong
`Concretes/`:

| Component | Viền | Vẽ bằng | Chi tiết |
|---|---|---|---|
| `OutlineSprite` | Nhóm `SpriteRenderer` | `SpriteRenderer` tự sinh | [OutlineSprite](Concretes/OutlineSprite/README.md) |
| `OutlineImage` | Nhóm UI `Image` | Graphic tự sinh (`OutlineGraphic`, nội bộ) | [OutlineImage](Concretes/OutlineImage/README.md) |

Lớp cha `Outline` giữ phần hai component giống nhau: setting mà lần chụp nào cũng đọc (shader,
màu, ngưỡng alpha, `resolution`), material viền, vòng chụp ([Field](Field/README.md)), con ẩn
vẽ viền, làm sẵn shader và việc chỉnh setting trong Play mode. Thời điểm chụp, cách hiện field
và con ẩn chứa gì là của từng component. Shader viền và nguồn vẽ của mỗi component nằm trong
thư mục của nó.

Những điều dưới đây không suy ra được từ việc đọc từng file.

## Con ẩn vẽ viền

- `Awake` sinh một GameObject con tên `Outline` với `HideAndDontSave`: không hiện trên
  Hierarchy, không lưu vào scene hay prefab, không sửa được trên Inspector. Nó nằm đúng gốc toạ
  độ của component nên dùng chung không gian local, và theo transform như mọi con.
- Con lấy layer của GameObject lúc sinh; đổi layer sau đó thì con không đổi theo.
- Mọi setting của thứ vẽ viền đặt trên component rồi được đẩy xuống con (màu ngay khi gán
  `Color`, phần còn lại ngay khi gán property hoặc chỉnh trên Inspector). Code bên ngoài không
  nên tìm tới con: nó là chi tiết cài đặt và có thể đổi.
- `Instantiate` một viền đã `Awake` thì Unity chép cả con ẩn (đã đo). Bản chép vẫn trỏ vào
  field của viền gốc, nên `Awake` của viền mới tắt và huỷ nó trước khi sinh con của mình.
- Tắt component thì con thôi vẽ; huỷ component thì con, material, texture và sprite (nếu có)
  bị huỷ theo. Đã đo: thoát Play mode không để sót object nào.

## Làm sẵn trong Awake

`Awake` cấp phát bộ đệm theo `resolution` rồi vẽ thử một lần bằng shader mask và shader viền
(`OutlineImage` vẽ thêm biến thể mà Canvas dùng dưới RectMask2D). Trên OpenGL ES, lần vẽ đầu
tiên với một shader bắt main thread chờ render thread dựng chương trình GPU của shader đó: đo
trên OPPO CPH2121 (PowerVR GM9446) mất khoảng 6 ms mỗi shader, một lần ở frame chụp và một lần
ở frame hiện viền. Làm sẵn thì hai khoản này rơi vào lúc nạp scene thay vì lần chọn đầu tiên.
Shader viền được làm sẵn ở biến thể ứng với bóng lúc đó; bật bóng lúc chạy thì các biến thể có
bóng (cả biến thể dưới RectMask2D của `OutlineImage`) được vẽ thử ngay lúc bật.

Vì thế GameObject của viền phải đang bật khi scene nạp. Script tắt GameObject đó trước khi
`Awake` của nó chạy, chẳng hạn trong `Awake` của một component đứng trước, sẽ dời cả việc làm
sẵn tới lần bật đầu tiên. Không cần tắt để giấu viền: viền tự ẩn cho tới lần `SetTargets`
đầu tiên.

## Shader viền

Shader viền là ô tham chiếu `shader` trên component, tự điền khi thêm component trong Editor
(`Reset`); nút *Default* cạnh ô đặt lại shader viền của package. Chính tham chiếu đó đưa shader
vào bản build. Thêm component bằng
`AddComponent` lúc chạy thì ô này trống và `Awake` báo lỗi vì không có shader để tạo material,
nên hãy dùng prefab có sẵn component.

## Chỉnh setting trong Play mode

Màu và mọi setting chỉ ảnh hưởng lúc vẽ đổi ngay, kể cả hướng và khoảng cách của bóng. Ngưỡng
alpha, `resolution`, vùng đệm của lần chụp (độ dày lớn nhất, cộng khoảng cách và độ mềm lớn
nhất của bóng khi có bóng), bật hay tắt bóng, độ mềm của bóng và, khi có bóng, độ dày thì phải
chụp lại. Gán các setting này qua property lúc chạy cũng đi đường đó. Mỗi lúc chỉ có một lần
chụp: kéo slider liên tục thì lần đang chạy vẫn chạy xong và hiện ra, lần sau lấy giá trị mới
nhất, nên viền đổi theo tay kéo mà không chụp dồn mỗi frame.

Inspector có *Shader* (kèm nút *Default*), *Visual* rồi *Optimizations*. Trong *Visual*, ngưỡng
alpha đứng đầu vì cả vành lẫn bóng đều theo silhouette (với `OutlineSprite`, `orthoRange` đứng ngay
sau vì vành và bóng dùng chung), rồi tới ba mục gập *Outline*, *Shadow* và *Advanced* (sorting và
mask). Mục gập chỉ có mũi tên và nhãn, không khung, như *Additional Settings* của SpriteRenderer,
riêng *Shadow* có thêm ô đánh dấu bật bóng giữa mũi tên và nhãn (xem [Editor](../Editor/README.md));
nhãn trong mục bỏ tiền tố của mục (*Color*, không phải *Shadow Color*). Inspector vẽ field của lớp
cha trước, nên *Optimizations* được đặt thứ tự để luôn đứng cuối, kể cả khi lớp con thêm nhóm.

## Vành viền

Hai shader viền vẽ cùng một vành. Field lưu khoảng cách từ tâm texel tới tâm texel silhouette
gần nhất, nên mép thật của silhouette nằm ở 0.5 và mọi texel bên trong silhouette đều là 0.
Vành chạy từ 0.5 tới độ dày cộng 0.5. Mép ngoài luôn mềm đúng khoảng một pixel màn hình dù
zoom bao nhiêu. Mép trong mềm nhiều nhất một texel: bên trong không có khoảng cách nào cho biết
gần hay xa mép, nên nếu mép trong cũng mềm cả một pixel thì khi zoom xa (một pixel phủ hơn một
texel) cả lòng silhouette bị phủ một lớp màu viền mờ. Nhờ vậy vành không bao giờ phủ lên
silhouette và quad vẽ đè lên target được; cái giá là khi zoom xa, mép trong sắc hơn mép ngoài.

Độ rộng khử răng cưa lấy từ đạo hàm của UV (số texel trên một pixel màn hình), **không** lấy
`fwidth` của khoảng cách. Ở mép quad, GPU tính đạo hàm theo khối 2×2 pixel, có pixel phụ nằm
ngoài quad và đọc phải texel rác ngoài khung chụp; `fwidth(d)` khi đó phình to và mép quad
hiện một hàng chấm mờ (đã gặp trên cả D3D12 lẫn OpenGL ES). Khoảng cách không thể đổi nhanh
hơn một texel trên một texel, nên texel trên pixel là độ rộng đúng.

## Bóng đổ

Bóng là bóng của cả tấm sticker: silhouette nở thêm đúng độ dày viền (đọc từ khoảng cách trong
field, mép nở mềm một texel), blur Gaussian, rồi dịch theo một hướng và một
khoảng cách. Bóng vẽ chung quad với vành: vành đè lên bóng, và giống vành, bóng nhân với độ phủ
"ngoài silhouette" nên không bao giờ phủ lên chính các target. Quad vì thế vẫn vẽ đè lên target
được, và bóng chỉ hiện quanh nhóm.

- Phần nở và blur được nướng vào field lúc chụp (kênh G, B, A; xem
  [Field](Field/README.md#blur-của-bóng)), nên đổi độ mềm, hay độ dày khi có bóng, phải chụp lại.
  Hướng và khoảng cách chỉ là chỗ shader đọc field, nên hiện ngay trên màn hình; nhưng khoảng
  cách nằm trong vùng đệm, nên đổi khoảng cách (với `OutlineSprite`: đầu lớn hơn của range), kể
  cả giảm, còn kéo theo một lần chụp lại để vùng đệm khớp.
- Hướng là `DirectionExt` của RiseOn Utils, theo trục world (`OutlineImage`: trục của root
  canvas), tức là theo màn hình khi camera hay canvas không xoay. Hướng được đổi sang không gian
  local của viền lúc vẽ, nên nhóm xoay hay lật thì bóng vẫn đổ về cùng một phía.
- Blur nướng ở nửa độ phân giải nên mép của nó mềm cỡ hai texel field dù độ mềm bằng 0, và zoom
  sát vào thì thấy mờ. Vì vậy shader đọc luôn khoảng cách trong R tại điểm đã dịch (cùng một lần
  đọc texture với G, B, A) và vẽ mép bóng sắc đúng như mép ngoài của vành: silhouette nở theo độ
  dày đang vẽ, ramp theo độ mềm hoặc một pixel màn hình nếu lớn hơn. Độ mềm (tính bằng texel field
  ở mức zoom đang vẽ) không quá một texel thì dùng hẳn mép này, từ ba texel trở lên thì dùng hẳn
  blur, ở giữa thì pha.
- Blur được nướng theo scale lúc chụp, nên nhóm nảy (scale) sau đó thì phần blur co giãn theo
  nhóm, trong khi độ dày của vành, khoảng cách và mép sắc (độ dày và độ mềm của nó) được tính lại
  mỗi lần vẽ nên vẫn giữ đúng đơn vị.
- Điểm bóng cần đọc mà nằm ngoài khung chụp thì không có bóng: texel ngoài khung là rác của lần
  chụp trước. Vùng đệm của khung đã chứa độ dày, khoảng cách và độ mềm lớn nhất, nên điều này chỉ
  cắt đuôi bóng khi bóng với xa hơn vùng đệm của lần chụp đang hiện.
- Bóng bật bằng ô đánh dấu trên tiêu đề mục *Shadow* (`ShadowEnabled`); tắt thì mục gập không mở
  được. Bóng tắt thì chi phí như viền không bóng: không chạy blur, field chỉ có một kênh, và
  material dùng biến thể shader không có bóng (keyword `_SHADOW`; `multi_compile_local` vì material
  được tạo lúc chạy, `shader_feature` sẽ bị bỏ khỏi bản build). Alpha của màu bóng chỉ là độ đục,
  nên tween alpha về 0 không đổi field. Bật hay tắt thì cấp phát lại field một lần; texture mới được
  upload rỗng ngay lúc cấp phát (khoảng cách 0, tức trong silhouette, không vẽ gì) và chưa có gì cho
  tới khi lần chụp đó về, nên viền ẩn trong vài frame đó thay vì giữ viền cũ như khi chỉ đổi
  setting.
- Độ dài của bóng theo đơn vị của component: world và theo zoom với `OutlineSprite`, đơn vị root
  canvas với `OutlineImage`.

## Màu viền là màu của renderer

Shader chỉ vẽ ra một vành trắng, alpha là độ phủ của vành, như texture của một sprite hay một
Image. Màu đến từ thứ vẽ viền theo đúng đường chuẩn của nó: `Color` được gán vào
`SpriteRenderer.color` hay `Graphic.color` của con ẩn, rồi renderer nhân vào như với mọi sprite
hay Image; không script nào đẩy màu vào material. Vì vậy đổi màu, kể cả tween mỗi frame (xem
[Adapter](Adapters/README.md)), không bao giờ phải chụp lại.

Màu bóng không phải màu của renderer: `OutlineSprite` đặt nó vào material riêng của component,
`OutlineImage` ghi nó vào `uv2` của đỉnh. Alpha của bóng còn nhân với alpha của màu viền, nên
tween làm mờ viền thì bóng mờ theo.
