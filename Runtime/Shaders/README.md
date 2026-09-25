# Shader

[← RiseOn.SpriteOutline](../../README.md)

| Shader | Dùng cho |
|---|---|
| `OutlineMask.shader` (`Hidden/RiseOn/SpriteOutline/Mask`) | Chụp silhouette vào mask, chỉ được vẽ qua CommandBuffer |
| `Outline.shader` (`RiseOn/SpriteOutline/Outline`) | Material của quad viền, viết cho URP (tag `UniversalPipeline`) |

Shader mask là việc nội bộ nên `Hidden` và không có ô nào để gán. Package nạp nó theo cách URP
nạp shader nội bộ của chính URP: `SpriteOutlineResources` là một `IRenderPipelineResources`, ô
shader mang `[ResourcePath]` tính từ gốc package. Khi nạp script, Editor tự thêm mục còn thiếu
vào URP Global Settings của project và điền shader theo đường dẫn đó; lúc chạy,
`GraphicsSettings.GetRenderPipelineSettings` trả nó ra. Graphics Settings tham chiếu file URP
Global Settings nên file này luôn vào bản build, kéo theo shader mask; `isAvailableInPlayerBuild`
giữ mục này lại qua bước strip settings của URP.

Vì thế cài package xong, `UniversalRenderPipelineGlobalSettings.asset` của project có thêm mục
`SpriteOutlineResources`, cạnh các mục URP tự thêm cho chính nó: commit cùng project. Diff của
file này hay kèm theo việc `m_RuntimeSettings` bị làm trống; URP xoá danh sách đó mỗi lần Editor
lưu file và chỉ điền lại lúc build, nên không sao.

Shader viền là ô tham chiếu trên component, được *SetupEditor* điền sẵn khi thêm component
trong Editor; chính tham chiếu đó đưa nó vào bản build. Thêm `SpriteOutline` bằng `AddComponent`
lúc chạy thì ô này trống: dùng prefab có sẵn component, hoặc cho shader viền vào *Always
Included Shaders*.

Không được gán shader mask cho renderer nào trong scene: nó không có tag pipeline và không có
nghĩa ở đó.

Những điều dưới đây không suy ra được từ việc đọc từng file.

## Shader mask chỉ đọc alpha của texture

`DrawRenderer` đưa vào texture của sprite và hình học đã lật sẵn theo `flipX` / `flipY`
(đã đo: trùng khít với việc tự dựng mesh từ `sprite.vertices`, cả khi lật), nên shader không
xử lý lật. Shader cũng cố ý bỏ màu và alpha của renderer: silhouette chỉ theo hình vẽ của
sprite. Mỗi fragment ghi 1 nếu alpha lớn hơn ngưỡng; `BlendOp Max` gộp các sprite thành một
silhouette bất kể thứ tự vẽ.

## Viền là một vành từ mép silhouette

Field lưu khoảng cách từ tâm texel tới tâm texel silhouette gần nhất, nên mép thật của
silhouette nằm ở 0.5 và mọi texel bên trong silhouette đều là 0. Vành chạy từ 0.5 tới độ dày
cộng 0.5. Mép ngoài luôn mềm đúng khoảng một pixel màn hình dù zoom bao nhiêu. Mép trong mềm
nhiều nhất một texel: bên trong không có khoảng cách nào cho biết gần hay xa mép, nên nếu mép
trong cũng mềm cả một pixel thì khi zoom xa (một pixel phủ hơn một texel) cả lòng silhouette
bị phủ một lớp màu viền mờ. Nhờ vậy vành không bao giờ phủ lên silhouette và quad vẽ đè lên
sprite được; cái giá là khi zoom xa, mép trong sắc hơn mép ngoài.

Độ rộng khử răng cưa lấy từ đạo hàm của UV (số texel trên một pixel màn hình), **không** lấy
`fwidth` của khoảng cách. Ở mép quad, GPU tính đạo hàm theo khối 2×2 pixel, có pixel phụ nằm
ngoài quad và đọc phải texel rác ngoài khung chụp; `fwidth(d)` khi đó phình to và mép quad
hiện một hàng chấm mờ (đã gặp trên cả D3D12 lẫn OpenGL ES). Khoảng cách không thể đổi nhanh
hơn một texel trên một texel, nên texel trên pixel là độ rộng đúng.

## Độ dày tính ngay trong vertex shader

Vertex shader đọc `unity_OrthoParams.y` (ortho size của camera đang vẽ), nội suy độ dày world
theo `_OrthoRange` / `_WidthRange`, đổi sang texel bằng độ dài trục X của ma trận object và
`_TexelSize`, rồi kẹp ở `_MaxRadius` để vành không chạy ra khỏi vùng đã chụp. Không script
nào phải cập nhật material theo camera.
