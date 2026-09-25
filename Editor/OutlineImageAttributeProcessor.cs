using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Outline2D.Editor {
    /// <summary>
    /// Trims what OutlineImage inherits from uGUI's Graphic to what an outline uses: Color and Maskable join the<br/>
    /// Visual group; Material, Raycast Target, Raycast Padding and On Cull State Changed are hidden, since the outline<br/>
    /// always draws with its own material and never takes input.
    /// </summary>
    internal sealed class OutlineImageAttributeProcessor<T> : OdinAttributeProcessor<T> where T : OutlineImage {
        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member, List<Attribute> attributes) {
            switch (member.Name) {
                // Inherited fields are declared first, so the Visual group would otherwise open above References.
                case "outlineShader":
                    attributes.RemoveAll(attribute => attribute is FoldoutGroupAttribute);
                    attributes.Add(new FoldoutGroupAttribute("References", -1));
                    break;

                // Visual reads like OutlineSprite's: cutoff, colour, width, then Maskable.
                case "m_Color":
                    attributes.Add(new FoldoutGroupAttribute("Visual"));
                    attributes.Add(new PropertyOrderAttribute(1));
                    break;

                case "width":
                    attributes.Add(new PropertyOrderAttribute(2));
                    break;

                case "m_Maskable":
                    attributes.Add(new FoldoutGroupAttribute("Visual"));
                    attributes.Add(new PropertyOrderAttribute(3));
                    break;

                case "m_Material":
                case "m_RaycastTarget":
                case "m_RaycastPadding":
                case "m_OnCullStateChanged":
                    attributes.Add(new HideInInspector());
                    break;
            }
        }
    }
}