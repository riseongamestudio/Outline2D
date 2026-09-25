# SpriteOutline

[← RiseOn.SpriteOutline](../../README.md)

`SpriteOutline` là component duy nhất: nhận nhóm sprite qua `SetTargets`, giữ setting trong
Inspector, vẽ viền bằng MeshRenderer trên chính GameObject của nó.

- [Hợp đồng của SetTargets](#hợp-đồng-của-settargets)
- [Hệ toạ độ và thời điểm chụp](#hệ-toạ-độ-và-thời-điểm-chụp)
- [Làm sẵn trong Awake](#làm-sẵn-trong-awake)
- [Setting](#setting)
- [Độ dày theo zoom](#độ-dày-theo-zoom)
- [Giới hạn](#giới-hạn)

## Hợp đồng của SetTargets

- Chuỗi renderer được đọc hết ngay trong lúc gọi rồi bỏ; renderer null bị bỏ qua. Truyền
  `null` hoặc chuỗi rỗng là tắt viền.
- Viền cũ tắt ngay lúc gọi, vì nó thuộc về nhóm cũ. Viền mới hiện sau vài frame: chụp ở
  `LateUpdate` của frame gọi, đọc mask về (bất đồng bộ), distance transform trên worker
  thread, upload, rồi mới hiện.
- Gọi nhiều lần trong một frame thì chỉ lần cuối được chụp. Kết quả của một lần chụp về
  sau khi đã có lần gọi mới thì bị bỏ.
- Silhouette chụp một lần. Nhóm đổi hình dạng giữa chừng (bật, tắt, đổi sprite) thì phải gọi
  lại `SetTargets`.

## Hệ toạ độ và thời điểm chụp

Silhouette được chụp trong không gian local của `SpriteOutline`, không phải của camera. Vì vậy
nhóm di chuyển, camera di chuyển hay zoom đều không cần chụp lại, với điều kiện nhóm cứng
so với `SpriteOutline` từ lúc chụp tới lần gọi sau: làm `SpriteOutline` thành con của nhóm, hoặc cho
nó bám nhóm bằng Position / Rotation / Scale Constraint.

Chụp chạy trong `LateUpdate`, không chạy ngay trong `SetTargets`. Trong `PreLateUpdate` của
Unity, `ConstraintManagerUpdate` đứng trước `ScriptRunBehaviourLateUpdate` (đã đo trên
Unity 6000.3), nên lúc chụp thì constraint vừa đổi nguồn đã kịp đặt `SpriteOutline` vào đúng
nhóm mới. Nếu bám bằng script tự viết trong `LateUpdate` thì thứ tự giữa hai script không
được bảo đảm; khi đó hãy cập nhật vị trí trong `Update`.

## Làm sẵn trong Awake

`Awake` cấp phát bộ đệm theo `pixelBudget` rồi vẽ thử một lần bằng shader mask và shader viền.
Trên OpenGL ES, lần vẽ đầu tiên với một shader bắt main thread chờ render thread dựng chương
trình GPU của shader đó: đo trên OPPO CPH2121 (PowerVR GM9446) mất khoảng 6 ms mỗi shader,
một lần ở frame chụp và một lần ở frame hiện viền. Làm sẵn thì hai khoản này rơi vào lúc nạp
scene thay vì lần chọn đầu tiên.

Vì thế GameObject của `SpriteOutline` phải đang bật khi scene nạp. Script tắt GameObject đó
trước khi `Awake` của `SpriteOutline` chạy, chẳng hạn trong `Awake` của một component đứng
trước nó, sẽ dời cả việc làm sẵn tới lần bật đầu tiên. Không cần tắt để giấu viền: viền tự ẩn
cho tới lần `SetTargets` đầu tiên.

## Setting

Hai nhóm trên Inspector. Chỉnh trong Play mode thì màu và độ dày đổi ngay; ngưỡng alpha, ngân
sách và vùng đệm của độ dày thì chụp lại. Mỗi lúc chỉ có một lần chụp: kéo slider liên tục thì
lần đang chạy vẫn chạy xong và hiện ra, lần sau lấy giá trị mới nhất, nên viền đổi theo tay kéo
mà không chụp dồn mỗi frame.

**Visual**

| Field | Ý nghĩa |
|---|---|
| `alphaCutoff` | Texel của sprite có alpha lớn hơn mức này thì thuộc silhouette |
| `color` | Màu viền; alpha của màu là độ đục của viền |
| `orthoRange`, `widthRange` | Độ dày theo world ở hai mức ortho, xem mục dưới. `max(widthRange)` còn là vùng đệm của lần chụp |

**Optimizations**

| Field | Ý nghĩa |
|---|---|
| `pixelBudget` | Tổng số pixel của một lần chụp (rộng × cao), kéo từ 16K tới 256K, mặc định 64K. Là núm chỉnh chi phí duy nhất: cùng ngân sách thì cùng chi phí, bất kể nhóm to, nhỏ hay dẹt |

Mỗi cạnh của khung chụp tối đa `2·√pixelBudget` (64K là 512): nhóm dẹt tới 4:1 vẫn dùng hết
ngân sách, dẹt hơn thì chụp ít pixel hơn chứ không tốn thêm. Bộ đệm giữ sẵn theo đúng cạnh đó,
khoảng `4 × pixelBudget × 3` byte trên GPU (mask R8, field half-float) và gấp ba số đó trên CPU;
với 64K là 768 KB và 2.3 MB.

Màu và alpha của renderer không ảnh hưởng silhouette: sprite đang mờ đi vẫn được viền theo
đúng hình của nó.

## Độ dày theo zoom

`widthRange.x` là độ dày (đơn vị world) khi camera có orthographic size bằng
`orthoRange.x`, `widthRange.y` ứng với `orthoRange.y`. Ở giữa thì nội suy tuyến tính, ra ngoài
khoảng thì giữ ở đầu gần nhất. Hai đầu cùng tỉ lệ với nhau là độ dày không đổi theo pixel màn
hình; hai đầu bằng nhau là độ dày không đổi theo world.

Camera chỉ tham gia lúc vẽ: shader đọc ortho size của chính camera đang vẽ nên nhiều camera
vẫn đúng, và zoom không bao giờ phải chụp lại. Khâu chụp chỉ cần một hằng số là độ dày lớn
nhất, `max(widthRange)`, làm vùng đệm quanh silhouette. Vì vậy `widthRange` rộng thì vùng
đệm to và mỗi texel phủ nhiều world hơn: đó là cái giá của việc cho độ dày chạy theo zoom.

Độ dày theo world được đổi sang không gian local bằng scale hiện tại của `SpriteOutline`, nên
nhóm nảy (scale) sau lúc chụp thì viền vẫn giữ đúng độ dày world. Viền bị kẹp để không vượt
vùng đệm đã chụp.

## Giới hạn

- Chỉ có nghĩa với camera orthographic.
- Scale không đồng nhất chỉ được xấp xỉ theo trục X khi đổi độ dày world sang local.
- `SpriteMask` không được tính: phần sprite bị mask che vẫn nằm trong silhouette.
- Sprite ở *Draw Mode* Sliced / Tiled chưa được kiểm.
