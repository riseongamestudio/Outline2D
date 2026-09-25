# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.outline2d` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

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
