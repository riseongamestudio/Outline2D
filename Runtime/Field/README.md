# Field

[← RiseOn.Outline2D](../../README.md)

Phần nội bộ biến một nhóm target thành distance field, dùng chung cho `OutlineSprite` và
`OutlineImage`. `OutlineCapture` điều phối các lần chụp của một component: `OutlineFrame` chọn
khung chụp, `OutlineMask` chụp silhouette trên GPU và đọc về, `OutlineField` tính khoảng cách
trên worker thread và upload thành texture. Nguồn vẽ (`IMaskSource`) cho biết nhóm trông ra sao;
phần giữ danh sách và tính bounds chung ở `MaskSource<T>`, còn `SpriteSource` và `ImageSource`
nằm cạnh component dùng chúng. `Outline2DResources` giữ shader mask cho `OutlineMask` (xem
[Shader](../Shaders/README.md)). Tất cả là `internal`.

Những điều dưới đây không suy ra được từ việc đọc từng file.

## Một lần chụp mỗi lúc

Mỗi component có một `OutlineCapture`, đi qua các bước: chờ chụp, chờ mask về, đang
transform, đang hiện. `SetTargets` bắt đầu lại từ bước chờ chụp và tăng số phiên bản, nên kết
quả của lần chụp cũ về sau đó bị bỏ. Đổi setting trong Inspector chỉ đánh dấu kết quả là cũ:
lần chụp mới chỉ chạy khi lần đang chạy đã hiện, nên viền cũ vẫn hiện trong lúc chờ.

Khung của field đang hiện và khung của lần chụp đang chạy được giữ riêng: component dựng quad
theo khung đã upload, không bao giờ theo khung mà texture chưa có.

## Khung chụp theo ngân sách texel

Khung là bounds của các target trong không gian local của component viền, cộng vùng đệm (độ
dày lớn nhất, đổi sang local theo scale lúc chụp), cộng thêm 2 texel mỗi phía cho mép khử răng
cưa. Texel luôn vuông. Cạnh texel được giải thẳng từ phương trình
`(rộng·k + 4)(cao·k + 4) = resolution²`, rồi nới ra nếu một cạnh vượt `2·resolution`. Vì
thế chi phí mọi khâu phía sau chỉ theo `resolution`: nhóm to thì mỗi texel phủ nhiều đơn vị hơn,
không phải tốn thêm texel.

Mask và field được cấp phát một lần, vuông, cạnh `2·resolution`; mỗi lần chụp chỉ dùng góc
dưới trái rộng × cao. Chọn nhóm không bao giờ cấp phát bộ nhớ GPU; chỉ đổi `resolution` mới cấp
phát lại, ngay trước lần chụp kế tiếp.

## Nguồn vẽ

Mỗi loại target có nguồn vẽ riêng, ghi ở component dùng nó:
[OutlineSprite](../Concretes/OutlineSprite/README.md#nguồn-vẽ) (mỗi renderer một
`DrawRenderer`), [OutlineImage](../Concretes/OutlineImage/README.md#nguồn-vẽ) (bản chép mesh
uGUI của từng Image). Phần chung ở `MaskSource<T>`: giữ danh sách target đọc từ `SetTargets`
và tính bounds của cả nhóm trong không gian local của component viền.

## Chụp và đọc về

Một CommandBuffer chạy ngoài vòng render của camera: viewport là góc dưới trái, view là
`worldToLocalMatrix` của component viền, projection là ortho theo quy ước Unity (không qua
`GL.GetGPUProjectionMatrix`, nếu không D3D lật Y hai lần, bài học của Wipe2D), rồi mỗi target
một lần vẽ với shader mask. Không cần Renderer Feature, không phụ thuộc camera nào.

Đọc về bằng `RequestAsyncReadback` đúng góc đó. Trên D3D11 dữ liệu trả về hàng 0 là đáy
ảnh, trùng với `ReadPixels`, nên field được ghi thẳng không cần lật. Máy không có readback
bất đồng bộ thì rơi về `ReadPixels`, khựng một lần mỗi lần chọn.

Trên OpenGL ES, riêng việc yêu cầu đọc về tốn 1–2 ms trên render thread
(`Gfx.RequestAsyncReadbackData`, đo trên PowerVR GM9446), không chạm main thread. Đã thử đợi
một frame sau khi vẽ mask mới yêu cầu: chi phí không giảm mà viền hiện chậm thêm một frame,
nên đọc về vẫn nằm chung CommandBuffer với lúc vẽ.

## Distance transform trên CPU, không trên GPU

Felzenszwalb & Huttenlocher, chính xác theo khoảng cách Euclid: mỗi hàng quét hai chiều ra
bình phương khoảng cách trong hàng, rồi mỗi cột lấy bao dưới của các parabola. Chi phí tuyến
tính theo số texel, không phụ thuộc độ dày viền, nên viền dày bao nhiêu cũng không thiếu mẫu
hay méo góc.

Chạy bằng Burst trên worker thread thay vì compute shader vì:

- Máy chỉ có OpenGL ES 3.0 không có compute shader; readback bất đồng bộ thì có từ Unity
  2021.2. Một đường duy nhất chạy trên mọi máy.
- Thuật toán tuần tự trong từng hàng, từng cột: trên GPU chỉ được vài trăm thread, song song
  kém; trên CPU là đúng dạng của nó.
- Không cộng thêm thời gian GPU vào frame, trong khi máy yếu thường nghẽn ở GPU.

Cái giá là viền hiện trễ vài frame sau khi chọn.

## Đo trên máy thật

OPPO CPH2121 (Helio P90, PowerVR GM9446, OpenGL ES 3), Development Build, game khoá
30 fps, đang quay màn hình để máy chậm thêm. Unity chỉ cấp 2 job worker trên 8 nhân. Mỗi lần
chọn một nhóm sprite cỡ cái áo (khoảng 3.5 × 3 world) bằng `OutlineSprite`:

| | `resolution` 256 (64K pixel) | `resolution` 512 (256K pixel) |
|---|---|---|
| Chụp, main thread | 0.4–0.5 ms | 0.3–0.4 ms |
| Chụp và yêu cầu đọc về, render thread | 1.9–2.3 ms | 2.3–2.8 ms |
| Distance transform, 2 worker (thời gian chờ) | 3.5–4.7 ms | 4.5–5.4 ms |
| Distance transform, tổng CPU | 3.5–4.7 ms | 6.5–6.8 ms |
| Upload, main thread | 0.2–0.3 ms | 0.2–0.3 ms |
| Từ lúc chọn tới lúc viền hiện | 4–5 frame | 5 frame |

Frame main thread giữ nguyên 32.5 ms ở cả hai mức, tức là chọn không làm khựng. Distance
transform đo riêng trên cùng máy, một luồng: 64K pixel 3.1 ms, 128K 6.1 ms, 256K 12.6 ms. Viền hiện
chậm chủ yếu vì chờ GPU trả mask về (2–4 frame), không vì distance transform.

`OutlineImage` đi cùng đường từ lúc có mask trở đi; phần riêng của nó là chép mesh của từng
Image trước khi vẽ. Chưa đo trên máy thật.

## Độ chính xác và định dạng

- Bình phương khoảng cách được tính bằng float trong pass cột; chúng chính xác tới 2²⁴, tức
  cạnh tối đa 2048. Cạnh bị kẹp ở đó; `resolution` lớn nhất 1024 dùng đúng 2048.
- Field là `R16_SFloat`, khoảng cách tính bằng texel. Không dùng `R32F` vì OpenGL ES 3.0
  không bắt buộc lọc tuyến tính cho nó; không dùng `R16` unorm vì đó chỉ là extension.
- Khoảng cách không bị kẹp (lớn nhất khoảng 2900 texel, half-float vẫn giữ được). Mép ngoài
  của vành mềm trong nửa pixel màn hình, tức nhiều texel khi một pixel phủ nhiều texel (zoom
  xa, `resolution` cao), nên khoảng cách phải tiếp tục tăng tới tận mép quad. Bản đầu kẹp ở
  bán kính lớn nhất cộng 2 texel và cả quad bị ám màu viền: đo ở `resolution` 1024, ortho 25.
- Mỗi lần upload cả texture field. Texel ngoài khung chụp là rác của lần trước và
  không bao giờ được lấy mẫu: góc quad đặt ở tâm texel viền nên lọc tuyến tính không với ra
  ngoài.
