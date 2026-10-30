# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.outline2d` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [2.0.0] - 2026-10-30

### Đổi (phá tương thích)

- `OutlineSprite` đổi tên thành `OutlineRenderer`, `IOutlineSprite` thành `IOutlineRenderer`;
  `SetTargets` nhận `IEnumerable<Renderer>` thay vì `IEnumerable<SpriteRenderer>`. Shader viền đổi
  tên thành `RiseOn/Outline2D/OutlineRenderer`. GUID của script và shader giữ nguyên nên prefab và
  scene đã lưu vẫn trỏ đúng; code gọi theo tên cũ phải đổi.
- `OutlineImage` đổi tên thành `OutlineGraphic`, `IOutlineImage` thành `IOutlineGraphic`;
  `SetTargets` nhận `IEnumerable<Graphic>` thay vì `IEnumerable<Image>`. Shader viền đổi tên thành
  `RiseOn/Outline2D/OutlineGraphic`. GUID giữ nguyên như trên.
- Field `resolution` được thay bằng `performance` (`OutlinePerformanceProfile`), mục gập
  *Optimizations* thành *Performance*. Giá trị `resolution` đã lưu không được chuyển sang: prefab
  hay scene có `resolution` khác 256 phải đặt lại `Resolution` trong profile.

### Thêm

- `OutlineRenderer` viền được mọi `Renderer`, không chỉ sprite: renderer khác (như `MeshRenderer`
  của skeleton Spine) được vẽ vào mask từng submesh với texture chính của material ở submesh đó.
- `OutlineGraphic` viền được mọi `Graphic` của uGUI, không chỉ `Image`.
- `OutlinePerformanceProfile` và `IOutline.Performance` (cùng property trên hai component): profile
  serialize được để bên ngoài giữ sẵn cho từng loại nhóm rồi gán vào. Gồm:
  - `Resolution` (64–1024, như `resolution` cũ);
  - `Frequency`: `OnDemand` (chỉ chụp khi đổi nhóm hay setting), `FrameInterval` (chụp lại mỗi
    `Frames` frame, 1 là mỗi frame), `TimeInterval` (chụp lại mỗi `Seconds` giây thực);
  - `Quality`: `Exact` (khoảng cách Euclid chính xác và blur Gaussian trên worker thread, như
    trước) hoặc `Fast` (jump flood trên GPU, không đọc về; bóng tính từ khoảng cách thay vì blur).
    Máy không render được định dạng field hay định dạng toạ độ seed, hoặc không chép được render
    target sang texture, thì `Fast` dựng như `Exact`;
  - `Latency`: `Async` (rải việc qua vài frame, như trước) hoặc `Immediate` (viền hiện trong frame
    chụp; `Exact` thì main thread chờ đọc về và các job).

  Mọi tổ hợp đều chạy; xem bảng đo trên máy thật trong README của Performance.
- Bóng kiểu `Spread` (`OutlineShadowType`, `ShadowType` trên hai component và interface): tản đều
  mọi phía từ mép ngoài viền, đậm nhất sát viền và nhạt dần tới hết khoảng tản (`ShadowSpreadRange`
  theo zoom với `OutlineRenderer`, `ShadowSpread` theo đơn vị root canvas với `OutlineGraphic`). Vẽ
  thẳng từ khoảng cách nên không nướng blur nào: field một kênh; `Exact` và `Fast` vẽ cùng một
  cách, chỉ khác ở sai số khoảng cách của jump flood. `Directional` là bóng đổ một hướng như trước,
  và là mặc định.
- Shader nội bộ `Hidden/RiseOn/Outline2D/JumpFlood`, nạp qua `Outline2DResources` như shader mask:
  URP Global Settings của project có thêm một ô shader trong mục `Outline2DResources`.

### Sửa

- Target đang tắt hay nằm trên GameObject inactive lúc chụp không còn bị vẽ vào silhouette.
  Trước đây `DrawRenderer` vẫn vẽ nó, nên một nhóm đổi hình bằng cách tắt bớt renderer (như đồ treo
  lên móc) có viền ôm cả phần đã tắt.

## [1.2.0] - 2026-10-30

### Thêm

- `IOutlineSprite.OrthoRange` và `IOutlineSprite.WidthRange` (cùng property trên `OutlineSprite`):
  khoảng orthographic và độ dày viền theo world ở hai đầu của nó, đọc và đặt được từ code như các
  thông số bóng. Đặt lại thì chụp mới, vì độ dày là lề của lần chụp và độ mềm của bóng được nướng
  theo khoảng zoom.
- `IOutlineImage.Width` (cùng property trên `OutlineImage`): độ dày viền theo đơn vị root canvas;
  đặt lại thì chụp mới.

## [1.1.0] - 2026-10-29

### Thêm

- Bóng đổ mềm của cả silhouette lẫn viền (silhouette nở thêm độ dày viền), cho cả `OutlineSprite` và
  `OutlineImage`, trong mục gập *Visual/Shadow*: ô đánh dấu trên tiêu đề để bật (mặc định tắt; tắt
  thì mục không mở được), màu (mặc định đen, alpha 0.5), hướng (`DirectionExt` của RiseOn Utils),
  khoảng cách và độ mềm. Với `OutlineSprite`, khoảng cách và độ mềm là range theo world, cùng
  `orthoRange` với độ dày; với `OutlineImage` là một giá trị theo đơn vị root canvas. Có cả property
  và mặt trong `IOutlineSprite` / `IOutlineImage`.
- Silhouette nở theo độ dày viền rồi blur Gaussian, nướng lúc chụp ở nửa độ phân giải, trên
  worker thread sau distance transform, vào kênh G, B, A của field (`OutlineSprite` ba mức theo
  zoom, `OutlineImage` một mức). Khi bóng bật, field thành `R16G16B16A16_SFloat` và bộ nhớ tăng
  (ở `resolution` 256: khoảng 2.3 MB GPU và 5.8 MB CPU, so với 768 KB và 2.1 MB); bật hay tắt bóng
  cấp phát lại field một lần. Ba mức của `OutlineSprite` được pha theo vị trí mép (logit của độ
  phủ), nên mép bóng không rộng ra hẹp lại khi zoom giữa hai mức.
- Bóng tắt thì chi phí như viền không bóng: field một kênh, không blur, và shader dùng biến thể
  không có bóng (keyword `_SHADOW`, `multi_compile_local`).
- Bóng có độ mềm không quá một texel field ở mức zoom đang vẽ được vẽ từ khoảng cách trong R, như
  mép ngoài của vành, nên sắc ở mọi mức zoom; từ ba texel trở lên là blur đã nướng, ở giữa thì pha.
- Bóng của `OutlineImage` tự thêm `TexCoord1` và `TexCoord2` vào *Additional Shader Channels*
  của root canvas.
- Phụ thuộc `com.riseon.utils` 1.0.12 (cho `DirectionExt`).

### Đổi

- `OutlineImage` ẩn viền khi field được cấp phát lại (đổi `resolution`, bật hay tắt bóng) cho tới
  khi lần chụp mới về, thay vì hiện texture chưa có gì trên khung cũ; texture field mới được
  upload rỗng ngay lúc cấp phát.
- Inspector: *Alpha Cutoff* đứng đầu *Visual*; `color` và độ dày (`widthRange` / `width`) vào mục
  gập *Visual/Outline*; `orthoRange` của `OutlineSprite` đứng ngay sau *Alpha Cutoff* vì độ dày và
  bóng dùng chung. Dữ liệu đã lưu không đổi.

## [1.0.5] - 2026-09-28

### Đổi

- Symbol bật adapter LitMotion đổi từ `HAS_LITMOTION` thành `OUTLINE2D_SUPPORT_LITMOTION`, mang
  tiền tố tên pack để không trùng với symbol project tự đặt. Cài LitMotion qua UPM thì không phải
  sửa gì. Cài LitMotion bằng NuGet hoặc `.unitypackage` và đã tự thêm `HAS_LITMOTION` vào
  *Scripting Define Symbols* thì đổi sang tên mới, nếu không adapter `RiseOn.Outline2D.LitMotion`
  sẽ bị bỏ qua. `DOTWEEN` giữ nguyên vì là symbol của DOTween.

## [1.0.4] - 2026-09-26

### Đổi

- Mục *Advanced* trong *Visual* gập mở chỉ bằng mũi tên và nhãn, không khung, như mục con trong
  Inspector của Unity; drawer nằm trong assembly Editor `RiseOn.Outline2D.Editor`.

## [1.0.3] - 2026-09-26

### Đổi

- Setting sorting và mask (`sortingLayerID`, `sortingOrder`, `maskInteraction` của `OutlineSprite`,
  `maskable` của `OutlineImage`) chuyển từ nhóm *Rendering* vào nhóm gập *Visual/Advanced* trên
  Inspector. Dữ liệu đã lưu không đổi.
- Bỏ nút *SetupEditor* cùng method `SetupEditor`: ô shader có nút *Default* đặt lại shader viền của
  package, và thêm component vẫn tự điền ô này.

## [1.0.2] - 2026-09-26

### Đổi

- Mỗi viền chỉ còn đúng một component. Thứ vẽ viền được sinh lúc `Awake` trên một GameObject con
  ẩn, không lưu vào scene hay prefab: `OutlineSprite` sinh một `SpriteRenderer` thay cho
  `MeshFilter` + `MeshRenderer`, `OutlineImage` sinh một Graphic. Code bên ngoài không còn chạm
  tới renderer.
- `OutlineImage` không còn kế thừa `MaskableGraphic` mà là một MonoBehaviour; *Color* và
  *Maskable* của Graphic thành field `color`, `maskable` của nó.
- `OutlineSprite` có nhóm setting *Rendering*: `sortingLayerID`, `sortingOrder` và
  `maskInteraction` (SpriteMask cắt viền như cắt sprite).
- `IOutline<T>` tách thành `IOutline` (màu, chung cho cả hai), `IOutlineSprite` (sorting layer,
  order, mask interaction, `SetTargets(IEnumerable<SpriteRenderer>)`) và `IOutlineImage`
  (maskable, `SetTargets(IEnumerable<Image>)`).
- Lớp cha chung `Outline` giữ setting chung, material, vòng chụp và con ẩn.
- Shader viền vẽ vành trắng, renderer nhân màu vào như với sprite và Graphic thường; shader viền
  sprite bỏ thuộc tính `_Color`.
- Field `outlineShader` đổi tên thành `shader`, đứng đầu Inspector ngoài mọi nhóm; ô script ẩn
  (`HideMonoScript`).
- Thư mục: `IOutline` và `Outline` ở gốc `Runtime/`, hai component trong `Runtime/Concretes/`,
  mỗi thư mục chứa interface, shader viền và nguồn vẽ riêng của component đó.

Project đang dùng 1.0.x phải tự chuyển dữ liệu:

- GameObject có `OutlineSprite`: bỏ `MeshFilter` và `MeshRenderer`, chép *Sorting Layer* và
  *Order in Layer* của `MeshRenderer` sang `sortingLayerID` và `sortingOrder`, gán lại `shader`.
- GameObject có `OutlineImage`: bỏ `CanvasRenderer`, chép *Color* và *Maskable* sang `color` và
  `maskable`, gán lại `shader`.
- Code: `IOutline<SpriteRenderer>` thành `IOutlineSprite`, `IOutline<Image>` thành `IOutlineImage`.

### Thêm

- Adapter tween màu viền qua `IOutline`: LitMotion (`BindToColor`, `BindToColorR/G/B/A`) và
  DOTween (`DOColor`, `DOFade`, `DOGradientColor`, `DOBlendableColor`). Mỗi adapter là một assembly
  tự bật khi project có thư viện.

### Bỏ

- Assembly `RiseOn.Outline2D.Editor`: processor Odin giấu các field của Graphic trên
  `OutlineImage`, nay không còn field nào như thế.

### Sửa

- `OutlineImage`: đổi *Maskable* lúc chạy có tác dụng ngay với RectMask2D, không phải chờ lần
  bật lại hay đổi cha như setter `maskable` của uGUI.

## [1.0.1] - 2026-09-25

### Sửa

- Thiếu Odin Inspector, hoặc thiếu define `ODIN_INSPECTOR` ở nền tảng đang chọn, thì project
  chỉ báo một lỗi rõ ràng từ assembly `RiseOn.Outline2D.Requirements` thay vì hàng loạt lỗi biên dịch.
  Các assembly dùng Odin có thêm `defineConstraints: ODIN_INSPECTOR`.

## [1.0.0] - 2026-09-25

### Thêm

- `OutlineSprite`: viền quanh silhouette gộp của một nhóm `SpriteRenderer`, nở chính xác theo khoảng cách Euclid, chụp một lần mỗi lần đổi nhóm.
- `OutlineImage`: viền quanh silhouette gộp của một nhóm UI `Image`, là một Graphic trong Canvas, theo đúng mọi Image Type, bị Mask và RectMask2D cắt như Image. Độ dày cố định theo đơn vị root canvas, màu là Color của Graphic.
- Setting trên component: *Visual* (ngưỡng alpha, màu, độ dày: theo world ứng với hai mức ortho cho sprite, theo đơn vị canvas cho Image) và *Optimizations* (`resolution`: độ phân giải chụp, kéo từ 64 tới 1024, tức 4K tới 1M pixel).
