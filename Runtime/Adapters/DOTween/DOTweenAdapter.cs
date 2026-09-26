using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace RiseOn.Outline2D.DOTween {
    /// <summary>
    /// DOTween shortcuts for <see cref="IOutline.Color"/>, the same set DOTween has for a SpriteRenderer's colour. Each<br/>
    /// tween takes the outline as its target, so DOKill and the other filtered operations reach it.
    /// </summary>
    public static class DOTweenAdapter {
        /// <summary>Tweens the outline's colour to the given value.</summary>
        public static TweenerCore<Color, Color, ColorOptions> DOColor(this IOutline target, Color endValue, float duration) {
            var tween = DG.Tweening.DOTween.To(() => target.Color, x => target.Color = x, endValue, duration);

            tween.SetTarget(target);

            return tween;
        }

        /// <summary>Tweens the alpha of the outline's colour, which is its opacity, to the given value.</summary>
        public static TweenerCore<Color, Color, ColorOptions> DOFade(this IOutline target, float endValue, float duration) {
            var tween = DG.Tweening.DOTween.ToAlpha(() => target.Color, x => target.Color = x, endValue, duration);

            tween.SetTarget(target);

            return tween;
        }

        /// <summary>
        /// Tweens the outline's colour through the colours of the gradient, not its alphas. A Sequence, not a Tweener.
        /// </summary>
        public static Sequence DOGradientColor(this IOutline target, Gradient gradient, float duration) {
            var sequence = DG.Tweening.DOTween.Sequence();
            var keys = gradient.colorKeys;

            for (var i = 0; i < keys.Length; ++i) {
                var key = keys[i];

                if (i == 0 && key.time <= 0) {
                    target.Color = key.color;
                    continue;
                }

                // The last step takes whatever time is left, so the whole lasts exactly the duration.
                var step = i == keys.Length - 1
                    ? duration - sequence.Duration(false)
                    : duration * (i == 0 ? key.time : key.time - keys[i - 1].time);

                sequence.Append(target.DOColor(key.color, step).SetEase(Ease.Linear));
            }

            sequence.SetTarget(target);

            return sequence;
        }

        /// <summary>
        /// Tweens the outline's colour by its difference to the given value, so that several of these on the same<br/>
        /// outline add up instead of fighting as DOColor tweens would.
        /// </summary>
        public static Tweener DOBlendableColor(this IOutline target, Color endValue, float duration) {
            var delta = endValue - target.Color;
            var applied = new Color(0, 0, 0, 0);

            return DG.Tweening.DOTween.To(() => applied, x => {
                    target.Color += x - applied;
                    applied = x;
                }, delta, duration)
                .Blendable()
                .SetTarget(target);
        }
    }
}