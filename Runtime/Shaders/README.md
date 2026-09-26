# Shader

[← RiseOn.Outline2D](../../README.md)

| Shader | Dùng cho |
|---|---|
| `OutlineMask.shader` (`Hidden/RiseOn/Outline2D/Mask`) | Chụp silhouette vào mask, chỉ được vẽ qua CommandBuffer; dùng chung cho cả hai viền |

Shader viền của mỗi component nằm cạnh component đó:
[OutlineSprite](../Concretes/OutlineSprite/README.md#shader),
[OutlineImage](../Concretes/OutlineImage/README.md#shader); phần vành chung của hai shader ở
[Lớp nền](../README.md#vành-viền).

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
