# Shader

[← RiseOn.Outline2D](../../README.md)

| Shader | Dùng cho |
|---|---|
| `OutlineMask.shader` (`Hidden/RiseOn/Outline2D/Mask`) | Chụp silhouette vào mask, chỉ được vẽ qua CommandBuffer |
| `OutlineSprite.shader` (`RiseOn/Outline2D/OutlineSprite`) | Material của quad viền `OutlineSprite`, viết cho URP (tag `UniversalPipeline`) |
| `OutlineImage.shader` (`RiseOn/Outline2D/OutlineImage`) | Material của `OutlineImage`, shader UI theo cấu trúc của `UI/Default` |

Shader mask là việc nội bộ nên `Hidden` và không có ô nào để gán. Package nạp nó theo cách URP
nạp shader nội bộ của chính URP: `Outline2DResources` là một `IRenderPipelineResources`, ô
shader mang `[ResourcePath]` tính từ gốc package. Khi nạp script, Editor tự thêm mục còn thiếu
vào URP Global Settings của project và điền shader theo đường dẫn đó; lúc chạy,
`GraphicsSettings.GetRenderPipelineSettings` trả nó ra. Graphics Settings tham chiếu file URP
Global Settings nên file này luôn vào bản build, kéo theo shader mask; `isAvailableInPlayerBuild`
giữ mục này lại qua bước strip settings của URP. `[HideInInspector]` trên class là để cửa sổ
Graphics settings không liệt kê mục này, giống cách URP đánh dấu resource nội bộ của nó (vd
`UniversalRenderPipelineRuntimeShaders`); Rider báo attribute này thừa vì nó chỉ biết tác dụng
trên field.

Vì thế cài package xong, `UniversalRenderPipelineGlobalSettings.asset` của project có thêm mục
`Outline2DResources`, cạnh các mục URP tự thêm cho chính nó: commit cùng project. Diff của
file này hay kèm theo việc `m_RuntimeSettings` bị làm trống; URP xoá danh sách đó mỗi lần Editor
lưu file và chỉ điền lại lúc build, nên không sao.

Shader viền là ô tham chiếu trên component, được *SetupEditor* điền sẵn khi thêm component
trong Editor; chính tham chiếu đó đưa nó vào bản build. Thêm component bằng `AddComponent` lúc
chạy thì ô này trống, nên hãy dùng prefab có sẵn component.

Không được gán shader mask cho renderer nào trong scene: nó không có tag pipeline và không có
nghĩa ở đó.

Những điều dưới đây không suy ra được từ việc đọc từng file.

## Shader mask chỉ đọc alpha của texture

Sprite đi qua `DrawRenderer`: Unity đưa vào texture của sprite và hình học đã lật sẵn theo
`flipX` / `flipY` (đã đo: trùng khít với việc tự dựng mesh từ `sprite.vertices`, cả khi lật),
nên shader không xử lý lật. Image đi qua `DrawMesh` với mesh uGUI đã dựng cho nó, texture chính
của Image đặt vào `_MainTex` qua MaterialPropertyBlock. Shader cố ý bỏ màu và alpha của target:
silhouette chỉ theo hình vẽ. Mỗi fragment ghi 1 nếu alpha lớn hơn ngưỡng; `BlendOp Max` gộp các
target thành một silhouette bất kể thứ tự vẽ.

## Viền là một vành từ mép silhouette

Cả hai shader viền dùng cùng một vành. Field lưu khoảng cách từ tâm texel tới tâm texel
silhouette gần nhất, nên mép thật của silhouette nằm ở 0.5 và mọi texel bên trong silhouette
đều là 0. Vành chạy từ 0.5 tới độ dày cộng 0.5. Mép ngoài luôn mềm đúng khoảng một pixel màn
hình dù zoom bao nhiêu. Mép trong mềm nhiều nhất một texel: bên trong không có khoảng cách nào
cho biết gần hay xa mép, nên nếu mép trong cũng mềm cả một pixel thì khi zoom xa (một pixel phủ
hơn một texel) cả lòng silhouette bị phủ một lớp màu viền mờ. Nhờ vậy vành không bao giờ phủ
lên silhouette và quad vẽ đè lên target được; cái giá là khi zoom xa, mép trong sắc hơn mép
ngoài.

Độ rộng khử răng cưa lấy từ đạo hàm của UV (số texel trên một pixel màn hình), **không** lấy
`fwidth` của khoảng cách. Ở mép quad, GPU tính đạo hàm theo khối 2×2 pixel, có pixel phụ nằm
ngoài quad và đọc phải texel rác ngoài khung chụp; `fwidth(d)` khi đó phình to và mép quad
hiện một hàng chấm mờ (đã gặp trên cả D3D12 lẫn OpenGL ES). Khoảng cách không thể đổi nhanh
hơn một texel trên một texel, nên texel trên pixel là độ rộng đúng.

## Độ dày của OutlineSprite tính ngay trong vertex shader

Vertex shader đọc `unity_OrthoParams.y` (ortho size của camera đang vẽ), nội suy độ dày world
theo `_OrthoRange` / `_WidthRange`, đổi sang texel bằng độ dài trục X của ma trận object và
`_TexelSize`, rồi kẹp ở `_MaxRadius` để vành không chạy ra khỏi vùng đã chụp. Không script
nào phải cập nhật material theo camera.

## Shader UI của OutlineImage

- Theo cấu trúc của `UI/Default` (đối chiếu với template Canvas của Shader Graph trong Unity 6):
  stencil và `ColorMask` cho Mask, `ZTest [unity_GUIZTestMode]`, clip rect có độ mềm cho
  RectMask2D, `UNITY_UI_ALPHACLIP`, `_UIVertexColorAlwaysGammaSpace`, alpha làm tròn theo bước
  1/255 và blend premultiplied.
- Canvas gộp mesh của mọi Graphic trong không gian canvas, nên vertex shader không biết scale
  riêng của `OutlineImage`. Bán kính vành (tính bằng texel field, đã kẹp) được tính trên CPU và
  đi trong `uv0.z`, kích thước field trong `uv0.w`; Canvas giữ đủ bốn thành phần của `uv0`
  (TextMesh Pro cũng đọc `uv0.w`). Nhờ vậy material không đổi theo instance, và Mask chép
  material một lần là đủ.
- Field được đọc bằng `sampler2D_float`: trên OpenGL ES, `sampler2D` mặc định chỉ có độ chính
  xác thấp, không giữ nổi khoảng cách tính bằng texel.
