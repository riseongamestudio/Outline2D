# Editor

[← RiseOn.Outline2D](../README.md)

Một assembly chỉ có trong Editor. Inspector của component vẽ bằng `[SectionGroup]` của
`com.riseon.utils`, nên package không có drawer riêng.

## Requirements

Assembly `RiseOn.Outline2D.Requirements`, không tham chiếu gì.

Odin Inspector đến từ Asset Store nên `package.json` không kéo nó về được. Các assembly dùng Odin
có `defineConstraints: ODIN_INSPECTOR`, nên project thiếu Odin (hoặc thiếu define đó ở nền tảng
đang chọn) thì chúng bị bỏ qua thay vì báo hàng loạt lỗi biên dịch. Assembly này không phụ
thuộc Odin nên vẫn biên dịch, và báo đúng một lỗi nói rõ lý do.
