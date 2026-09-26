# Editor

[← RiseOn.Outline2D](../README.md)

Hai assembly chỉ có trong Editor.

## Inspector

Assembly `RiseOn.Outline2D.Editor`, phụ thuộc Odin Inspector. `PlainFoldoutGroupDrawer` vẽ
`PlainFoldoutGroupAttribute` (ở `Runtime/Inspector`): một mục gập chỉ có mũi tên và nhãn, các field
thụt vào dưới nó, không khung, như *Additional Settings* trong Inspector của SpriteRenderer.
`FoldoutGroup` của Odin luôn vẽ khung và không có tuỳ chọn bỏ khung, nên package có drawer riêng.
Mục gập đóng cho tới khi mở; trạng thái mở được Odin nhớ theo từng người dùng.

Attribute là `internal`; assembly Runtime cho assembly này thấy nó qua `InternalsVisibleTo`.

## Requirements

Assembly `RiseOn.Outline2D.Requirements`, không tham chiếu gì.

Odin Inspector đến từ Asset Store nên `package.json` không kéo nó về được. Các assembly dùng Odin
có `defineConstraints: ODIN_INSPECTOR`, nên project thiếu Odin (hoặc thiếu define đó ở nền tảng
đang chọn) thì chúng bị bỏ qua thay vì báo hàng loạt lỗi biên dịch. Assembly này không phụ
thuộc Odin nên vẫn biên dịch, và báo đúng một lỗi nói rõ lý do.
