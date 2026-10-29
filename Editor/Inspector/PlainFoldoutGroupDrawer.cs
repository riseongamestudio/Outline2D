using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Outline2D.Editor {
    /// <summary>
    /// Draws <see cref="PlainFoldoutGroupAttribute"/>: Odin's foldout without its box, the members indented under it, and<br/>
    /// the group's toggle, if any, as a checkbox between the arrow and the label.
    /// </summary>
    internal sealed class PlainFoldoutGroupDrawer : OdinGroupDrawer<PlainFoldoutGroupAttribute> {
        private const float ARROW_WIDTH = 14;

        private LocalPersistentContext<bool> expanded;

        protected override void Initialize() {
            expanded = this.GetPersistentValue("expanded", false);
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            var toggle = Attribute.Toggle == null ? null : Property.Children[Attribute.Toggle];
            bool open;

            if (toggle == null) {
                open = expanded.Value = SirenixEditorGUI.Foldout(expanded.Value, label);
            } else {
                open = DrawToggleHeader(toggle, label);
            }

            if (SirenixEditorGUI.BeginFadeGroup(this, open)) {
                GUIHelper.PushIndentLevel(EditorGUI.indentLevel + 1);

                foreach (var child in Property.Children) {
                    if (child != toggle) child.Draw(child.Label);
                }

                GUIHelper.PopIndentLevel();
            }

            SirenixEditorGUI.EndFadeGroup();
        }

        // The arrow, locked shut while the toggle is off, then the toggle with the group's label. Turning it on opens the
        // group. Writes go through the value entry, so they reach every selected object with undo.
        private bool DrawToggleHeader(InspectorProperty toggle, GUIContent label) {
            var entry = toggle.ValueEntry;
            var on = (bool)entry.WeakSmartValue;
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());

            GUIHelper.PushIndentLevel(0);
            GUIHelper.PushGUIEnabled(GUI.enabled && on);

            var open = EditorGUI.Foldout(new Rect(rect.x, rect.y, ARROW_WIDTH, rect.height), on && expanded.Value, GUIContent.none, true);

            GUIHelper.PopGUIEnabled();

            if (on) expanded.Value = open;

            EditorGUI.showMixedValue = entry.ValueState == PropertyValueState.PrimitiveValueConflict;
            EditorGUI.BeginChangeCheck();

            var next = EditorGUI.ToggleLeft(new Rect(rect.x + ARROW_WIDTH, rect.y, rect.width - ARROW_WIDTH, rect.height), label, on);

            EditorGUI.showMixedValue = false;

            if (EditorGUI.EndChangeCheck()) {
                entry.WeakSmartValue = next;

                if (next) expanded.Value = true;
            }

            GUIHelper.PopIndentLevel();

            return on && expanded.Value;
        }
    }
}