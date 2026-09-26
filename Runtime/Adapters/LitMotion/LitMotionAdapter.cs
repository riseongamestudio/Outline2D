using System;
using LitMotion;
using UnityEngine;

namespace RiseOn.Outline2D.LitMotion {
    /// <summary>
    /// LitMotion bindings for <see cref="IOutline.Color"/>, the same set LitMotion has for a SpriteRenderer's colour.
    /// </summary>
    public static class LitMotionAdapter {
        /// <summary>Create a motion and bind it to the outline's colour.</summary>
        public static MotionHandle BindToColor<TOptions, TAdapter>(this MotionBuilder<Color, TOptions, TAdapter> builder, IOutline outline)
            where TOptions : unmanaged, IMotionOptions
            where TAdapter : unmanaged, IMotionAdapter<Color, TOptions> {
            if (outline == null) throw new ArgumentNullException(nameof(outline));

            return builder.Bind(outline, static (x, o) => o.Color = x);
        }

        /// <summary>Create a motion and bind it to the red channel of the outline's colour.</summary>
        public static MotionHandle BindToColorR<TOptions, TAdapter>(this MotionBuilder<float, TOptions, TAdapter> builder, IOutline outline)
            where TOptions : unmanaged, IMotionOptions
            where TAdapter : unmanaged, IMotionAdapter<float, TOptions> {
            if (outline == null) throw new ArgumentNullException(nameof(outline));

            return builder.Bind(outline, static (x, o) => {
                var color = o.Color;
                color.r = x;
                o.Color = color;
            });
        }

        /// <summary>Create a motion and bind it to the green channel of the outline's colour.</summary>
        public static MotionHandle BindToColorG<TOptions, TAdapter>(this MotionBuilder<float, TOptions, TAdapter> builder, IOutline outline)
            where TOptions : unmanaged, IMotionOptions
            where TAdapter : unmanaged, IMotionAdapter<float, TOptions> {
            if (outline == null) throw new ArgumentNullException(nameof(outline));

            return builder.Bind(outline, static (x, o) => {
                var color = o.Color;
                color.g = x;
                o.Color = color;
            });
        }

        /// <summary>Create a motion and bind it to the blue channel of the outline's colour.</summary>
        public static MotionHandle BindToColorB<TOptions, TAdapter>(this MotionBuilder<float, TOptions, TAdapter> builder, IOutline outline)
            where TOptions : unmanaged, IMotionOptions
            where TAdapter : unmanaged, IMotionAdapter<float, TOptions> {
            if (outline == null) throw new ArgumentNullException(nameof(outline));

            return builder.Bind(outline, static (x, o) => {
                var color = o.Color;
                color.b = x;
                o.Color = color;
            });
        }

        /// <summary>Create a motion and bind it to the alpha of the outline's colour, which is its opacity.</summary>
        public static MotionHandle BindToColorA<TOptions, TAdapter>(this MotionBuilder<float, TOptions, TAdapter> builder, IOutline outline)
            where TOptions : unmanaged, IMotionOptions
            where TAdapter : unmanaged, IMotionAdapter<float, TOptions> {
            if (outline == null) throw new ArgumentNullException(nameof(outline));

            return builder.Bind(outline, static (x, o) => {
                var color = o.Color;
                color.a = x;
                o.Color = color;
            });
        }
    }
}