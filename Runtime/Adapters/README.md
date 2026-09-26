# Adapter

[← RiseOn.Outline2D](../../README.md)

Adapter cho các thư viện tween, chạy màu viền qua [`IOutline.Color`](../README.md): tween được
cả `OutlineSprite` lẫn `OutlineImage` mà không cần biết bên dưới vẽ bằng gì. Mỗi adapter có
đúng bộ hàm mà thư viện đó có sẵn cho màu của một SpriteRenderer, trả về đúng kiểu của thư viện.

## Tự bật theo thư viện

Mỗi adapter là một assembly riêng, chỉ biên dịch khi project có symbol của thư viện
(`defineConstraints`, cùng `ODIN_INSPECTOR` như assembly chính). Project thiếu thư viện thì
adapter không được biên dịch và không báo lỗi.

| Adapter | Bật khi | Symbol | Hàm |
|---|---|---|---|
| `RiseOn.Outline2D.LitMotion` | Có package `com.annulusgames.lit-motion` 2.0 trở lên; symbol sinh bằng `versionDefines` | `HAS_LITMOTION` | `builder.BindToColor(outline)`, `BindToColorR`, `BindToColorG`, `BindToColorB`, `BindToColorA` |
| `RiseOn.Outline2D.DOTween` | DOTween đã chạy *Setup*, bước này tự thêm symbol vào *Scripting Define Symbols* | `DOTWEEN` | `outline.DOColor`, `DOFade`, `DOGradientColor`, `DOBlendableColor` |

LitMotion không cài qua UPM (NuGet, `.unitypackage`) thì thêm tay `HAS_LITMOTION` vào *Player
Settings → Scripting Define Symbols*. DOTween là DLL trong Assets; adapter tham chiếu nó nhờ
*Auto Reference* mặc định của plugin, nên đừng tắt ô đó trên `DOTween.dll`.

Tween DOTween lấy outline làm target, nên `DOKill` trên component viền dừng được nó.

Đã đo trong Play mode: LitMotion trên `OutlineSprite` và `OutlineImage`, DOTween trên
`OutlineImage` (adapter chỉ chạm `IOutline.Color` của lớp cha, nên hai component như nhau).
Màu và alpha tới đúng giá trị cuối, hai `DOBlendableColor` cộng dồn, `DOGradientColor` dừng ở
màu cuối, `DOKill` trên component dừng tween đang chạy.

## Ví dụ

```csharp
using LitMotion;
using RiseOn.Outline2D.LitMotion;

LMotion.Create(0f, 1f, .2f).BindToColorA(outline); // hiện dần
```

```csharp
using DG.Tweening;
using RiseOn.Outline2D.DOTween;

outline.DOFade(0, .2f); // mờ dần
```

## Lưu ý

- Trong LitMotion, "adapter" còn là `IMotionAdapter`, bộ nội suy giá trị. Adapter ở đây không
  liên quan tới cái đó: chỉ là extension method nối thư viện với `IOutline`.
- Bên trong namespace của một adapter, đoạn cuối của tên che mất tên cùng chữ của thư viện:
  trong `RiseOn.Outline2D.DOTween`, chữ `DOTween` là namespace đó, nên gọi class của thư viện
  phải viết đủ `DG.Tweening.DOTween`.
