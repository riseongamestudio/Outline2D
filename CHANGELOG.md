# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.outline2d` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

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
