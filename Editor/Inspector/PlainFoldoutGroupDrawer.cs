using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Outline2D.Editor {
    /// <summary>Draws <see cref="PlainFoldoutGroupAttribute"/>: Odin's foldout without its box, the members indented under it.</summary>
    internal sealed class PlainFoldoutGroupDrawer : OdinGroupDrawer<PlainFoldoutGroupAttribute> {
        private LocalPersistentContext<bool> expanded;

        protected override void Initialize() {
            expanded = this.GetPersistentValue("expanded", false);
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            expanded.Value = SirenixEditorGUI.Foldout(expanded.Value, label);

            if (SirenixEditorGUI.BeginFadeGroup(this, expanded.Value)) {
                GUIHelper.PushIndentLevel(EditorGUI.indentLevel + 1);

                foreach (var child in Property.Children) child.Draw(child.Label);

                GUIHelper.PopIndentLevel();
            }

            SirenixEditorGUI.EndFadeGroup();
        }
    }
}