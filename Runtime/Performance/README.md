# Hiệu năng

[← RiseOn.Outline2D](../../README.md)

`OutlinePerformanceProfile` là một struct serialize được, gom mọi lựa chọn về chi phí của các lần
chụp. Mỗi viền giữ một profile (field `performance`, mục *Performance* trên Inspector) và đọc nó qua
`IOutline.Performance`. Bên dùng thường serialize sẵn một profile cho mỗi loại nhóm rồi gán vào
trước `SetTargets`, chẳng hạn một profile cho nhóm sprite đứng yên và một cho nhóm có skeleton:

```csharp
outline.Performance = HasSkeleton(targets) ? animatedProfile : stillProfile;
outline.SetTargets(targets);
```

| Field | Giá trị | Ý nghĩa |
|---|---|---|
| `Resolution` | 64–1024, mặc định 256 | Một lần chụp có khoảng `Resolution²` texel, trải theo tỉ lệ của nhóm. Cách chọn ở [OutlineRenderer](../Concretes/OutlineRenderer/README.md#chọn-resolution) |
| `Frequency` | `OnDemand` (mặc định), `FrameInterval`, `TimeInterval` | Khi nào chụp lại ngoài lúc đổi nhóm hay đổi setting |
| `Frames` | từ 1, mặc định 1 | Số frame giữa hai lần bắt đầu chụp, với `FrameInterval`; 1 là mỗi frame |
| `Seconds` | từ 0, mặc định 0.1 | Số giây thực (không theo `Time.timeScale`) giữa hai lần bắt đầu chụp, với `TimeInterval` |
| `Quality` | `Exact` (mặc định), `Fast` | Cách dựng field: chính xác trên CPU hay jump flood trên GPU |
| `Latency` | `Async` (mặc định), `Immediate` | Rải việc của một lần chụp qua vài frame, hay làm hết trong frame chụp |

`OutlinePerformanceProfile.Default` là 256, `OnDemand`, `Exact`, `Async`: cấu hình cho nhóm đứng
yên. Struct tạo bằng `new` có `Resolution` 0 (được nâng lên mức thấp nhất, 32² texel) và `Frames` 0
(tính như 1), nên hãy bắt đầu từ `Default`.

Đổi `Resolution` hay `Quality` thì chụp lại (kết quả khác); đổi `Frequency` hay `Latency` thì
không, chúng áp dụng từ lần chụp kế tiếp.

Những điều dưới đây không suy ra được từ việc đọc từng file.

## Mọi tổ hợp đều chạy

Ba trục độc lập với nhau; tổ hợp nào cũng ra viền đúng, chỉ khác chi phí và độ trễ.

- **Tần suất** quyết định có bao nhiêu lần chụp. Khoảng tính từ lúc lần trước bắt đầu, và mỗi lúc
  chỉ có một lần chụp, nên khoảng ngắn hơn độ trễ thì các lần chụp chạy nối nhau (xem
  [Field](../Field/README.md#một-lần-chụp-mỗi-lúc)).
- **Chất lượng** quyết định việc của mỗi lần chụp nằm ở đâu. `Exact`: vẽ mask trên GPU, đọc về
  CPU, distance transform và blur bằng Burst trên worker thread, upload. `Fast`: vẽ mask rồi
  khoảng 12 lượt vẽ quad trên GPU (seed, các bước jump, resolve; ở `Resolution` 256), không đọc gì
  về CPU. Khác biệt về hình ở [Field](../Field/README.md#fast-jump-flood-trên-gpu): khoảng cách gần
  như trùng, bóng `Directional` không lấp các khe hẹp hơn độ mềm.
- **Độ trễ** quyết định việc đó trải qua mấy frame. `Exact` + `Async`: đọc về bất đồng bộ, job
  chạy song song với game, viền hiện sau vài frame. `Exact` + `Immediate`: main thread chờ GPU vẽ
  xong mask, đọc về, rồi chờ các job. `Fast` + `Async`: các lượt GPU chia đều cho ba frame.
  `Fast` + `Immediate`: mọi lượt trong frame chụp. `Fast` không phải chờ GPU hay job, nhưng ghi và
  chạy CommandBuffer chứa mọi lượt vẫn có lúc tốn vài ms trên main thread (xem bảng); `Async` chia
  nhỏ cả phần đó lẫn thời gian GPU của một lần chụp, có ích nhất khi GPU là chỗ nghẽn.

## Đo trên máy thật

Samsung Galaxy A30s (Exynos 7904, Mali-G71 MP2, OpenGL ES 3), màn 720 × 1560, 60 fps có vsync,
Development Build. Nhóm là skeleton Spine con mèo ở level 4 của OrganiCat, cỡ khoảng 2 × 1.3 world,
viền có bóng (ba mức). Mỗi dòng là 240 frame sau 40 frame làm nóng; với `OnDemand`, `SetTargets`
được gọi lại mỗi 30 frame để có lần chụp mà đo.

- *Main* và *GPU* lấy từ `FrameTimingManager` (`cpuMainThreadFrameTime`, `gpuFrameTime`), trung
  bình / p95, ms. Trên máy này `gpuFrameTime` gồm cả thời gian chờ vsync (frame không có gì vẫn
  khoảng 16 ms), nên chỉ phần chênh mới là chi phí của viền. Mức nền là dòng `OnDemand`, `Exact`,
  `Async` (8.9 / 15.9 ms trung bình): nó chụp mỗi 30 frame và gần như không tốn gì.
- *Chụp* là marker `Outline2D.Capture` (main thread), trung bình mỗi frame / lớn nhất, ms. Với
  `Fast` + `Immediate` nó gồm cả việc ghi mọi lượt GPU; với `Fast` + `Async` chỉ gồm phần ở frame
  đầu, hai phần sau nằm dưới marker `Outline2D.Flood`, không có trong bảng.
- Cái giỏ đan (một nhóm sprite) cho số gần như trùng, nên không chép lại.

**`Resolution` 256**

| Tần suất | Chất lượng | Độ trễ | Main | GPU | Chụp |
|---|---|---|---|---|---|
| `OnDemand` | `Exact` | `Async` | 8.9 / 12.7 | 15.9 / 17.7 | 0.02 / 0.7 |
| `OnDemand` | `Exact` | `Immediate` | 9.8 / 12.0 | 16.8 / 19.2 | 0.9 / 36.8 |
| `OnDemand` | `Fast` | `Async` | 9.1 / 12.4 | 16.0 / 20.5 | 0.02 / 0.9 |
| `OnDemand` | `Fast` | `Immediate` | 10.1 / 22.2 | 16.6 / 26.2 | 0.07 / 11.3 |
| `FrameInterval` 1 | `Exact` | `Async` | 9.6 / 15.9 | 15.9 / 20.1 | 0.09 / 2.0 |
| `FrameInterval` 1 | `Exact` | `Immediate` | 35.0 / 39.2 | 43.2 / 46.0 | 25.3 / 32.2 |
| `FrameInterval` 1 | `Fast` | `Async` | 9.2 / 11.3 | 18.2 / 22.6 | 0.18 / 0.9 |
| `FrameInterval` 1 | `Fast` | `Immediate` | 11.3 / 14.6 | 20.5 / 29.9 | 0.66 / 6.9 |
| `FrameInterval` 3 | `Exact` | `Async` | 8.8 / 14.9 | 16.2 / 19.2 | 0.09 / 0.6 |
| `FrameInterval` 3 | `Exact` | `Immediate` | 16.5 / 39.8 | 23.3 / 37.8 | 8.2 / 32.4 |
| `FrameInterval` 3 | `Fast` | `Async` | 9.5 / 12.0 | 18.3 / 24.3 | 0.19 / 1.0 |
| `FrameInterval` 3 | `Fast` | `Immediate` | 9.3 / 13.6 | 19.0 / 30.3 | 0.22 / 1.1 |
| `TimeInterval` 0.1 | `Exact` | `Async` | 8.9 / 16.0 | 16.2 / 20.5 | 0.06 / 0.7 |
| `TimeInterval` 0.1 | `Exact` | `Immediate` | 12.5 / 40.8 | 19.7 / 43.2 | 4.2 / 36.0 |
| `TimeInterval` 0.1 | `Fast` | `Async` | 8.9 / 12.3 | 17.0 / 26.4 | 0.09 / 0.9 |
| `TimeInterval` 0.1 | `Fast` | `Immediate` | 9.5 / 15.2 | 17.5 / 31.2 | 0.13 / 3.2 |

**`Resolution` 128**, chụp mỗi frame

| Chất lượng | Độ trễ | Main | GPU | Chụp |
|---|---|---|---|---|
| `Exact` | `Async` | 8.9 / 12.8 | 16.5 / 20.1 | 0.11 / 1.0 |
| `Exact` | `Immediate` | 25.0 / 29.5 | 31.7 / 35.6 | 14.4 / 36.7 |
| `Fast` | `Async` | 9.4 / 12.0 | 19.3 / 22.0 | 0.18 / 1.2 |
| `Fast` | `Immediate` | 9.4 / 12.8 | 19.6 / 25.1 | 0.59 / 2.9 |

Đọc bảng:

- **`Exact` + `Immediate` khựng thật.** Main thread chờ GPU vẽ xong rồi đọc về đồng bộ: khoảng
  25 ms mỗi lần chụp ở 256, 14 ms ở 128. Với `OnDemand` đó là một lần khựng 30–50 ms mỗi lần
  chọn; chụp mỗi frame thì game tụt còn khoảng 23 fps ở 256, 30 fps ở 128.
- **`Exact` + `Async` gần như miễn phí** trên main thread và GPU, kể cả "mỗi frame": việc nằm ở
  worker thread. Cái giá là nhịp: một lần chụp mất khoảng 5 frame từ đầu tới lúc hiện, nên "mỗi
  frame" thực ra là khoảng 12 lần một giây, viền theo sau hình khoảng 80 ms.
- **`Fast` chụp mỗi frame tốn GPU vừa phải**: thêm khoảng 2.3 ms trung bình với `Async` (một lần
  chụp mỗi 3 frame, tức 20 lần một giây) và 4.6 ms với `Immediate` (60 lần một giây), main thread
  thêm 0.3 và 2.4 ms.
- **`Async` làm nhẹ đỉnh GPU của `Fast`** như dự tính: p95 22.6 so với 29.9 ms ở 256, 22.0 so với
  25.1 ms ở 128.
- **`Fast` + `Immediate` có lúc khựng main thread**: trung bình dưới 1 ms mỗi lần chụp nhưng lớn
  nhất tới 11 ms ở 256 (dưới 3 ms ở 128), ngay lúc chạy CommandBuffer chứa mọi lượt GPU. `Fast` +
  `Async` chạy cùng các lượt đó, chia ba frame: phần ở frame đầu không quá 1.2 ms, mỗi phần sau
  (marker `Outline2D.Flood`, ghi riêng trong cùng lần chạy) lớn nhất 2.0 ms. Lần chụp `Fast` đầu
  tiên (cấp phát render target, lần dùng đầu của shader jump flood) rơi vào một dòng `Async` và
  không thấy khựng.
- Giảm `Resolution` từ 256 xuống 128 chỉ làm nhẹ rõ `Exact` + `Immediate` (và đỉnh main thread của
  `Fast` + `Immediate`); GPU của `Fast` gần như không đổi.
