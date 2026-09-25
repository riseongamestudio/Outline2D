# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.spriteoutline` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [1.0.0] - 2026-09-25

### Thêm

- `SpriteOutline`: viền quanh silhouette gộp của một nhóm `SpriteRenderer`, nở chính xác theo khoảng cách Euclid, chụp một lần mỗi lần đổi nhóm.
- Setting trên component: *Visual* (ngưỡng alpha, màu, độ dày theo world ứng với hai mức ortho) và *Optimizations* (ngân sách pixel, kéo từ 16K tới 256K).
