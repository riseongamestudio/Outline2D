using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

// The drawer lives in the Editor assembly.
[assembly: InternalsVisibleTo("RiseOn.Outline2D.Editor")]

namespace RiseOn.Outline2D {
    /// <summary>
    /// Groups settings under a foldout that is only an arrow and a label, with no box around it, like the sub-sections<br/>
    /// of Unity's own inspectors (a SpriteRenderer's Additional Settings); Odin's FoldoutGroup always draws a box.<br/>
    /// Collapsed until opened.
    /// </summary>
    internal sealed class PlainFoldoutGroupAttribute : PropertyGroupAttribute {
        public PlainFoldoutGroupAttribute(string groupId) : base(groupId) { }
    }
}