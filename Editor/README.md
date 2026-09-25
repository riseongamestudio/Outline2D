# Editor

[← RiseOn.Outline2D](../README.md)

Assembly `RiseOn.Outline2D.Editor`, chỉ chạy trong Editor, phụ thuộc Odin Inspector.

`OutlineImage` kế thừa `MaskableGraphic` của uGUI, nên mang theo mọi field serialize của Graphic.
Unity giấu bớt các field đó bằng editor riêng cho Image, RawImage; `OutlineImage` được Odin vẽ nên
sẽ hiện hết. `OutlineImageAttributeProcessor` là một `OdinAttributeProcessor`, cách Odin cho gắn
attribute lên field không thuộc về mình (cùng cách `com.riseon.utils` dùng cho
`ForwardAttributeProcessor`). Processor này:

- Ẩn *Material* (viền luôn vẽ bằng material riêng tạo từ shader viền), *Raycast Target*,
  *Raycast Padding* (viền không nhận input, `Awake` còn tắt hẳn *Raycast Target*) và *On Cull
  State Changed*.
- Đưa *Color* và *Maskable* vào nhóm *Visual*, xếp thành *Alpha Cutoff*, *Color*, *Width*,
  *Maskable* cho giống `OutlineSprite`.
- Đặt nhóm *References* lên đầu. Field của lớp cha được khai báo trước, nên nếu không, nhóm
  *Visual* sẽ mở ra trên cùng.

Các field bị ẩn vẫn được serialize như với mọi Graphic; processor chỉ đổi cách Inspector vẽ.
Processor là generic theo `OutlineImage` nên lớp con cũng được áp.
