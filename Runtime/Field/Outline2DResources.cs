using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RiseOn.Outline2D {
    /// <summary>
    /// The package's internal resources, kept in the project's URP Global Settings the way URP keeps its own: the<br/>
    /// Editor fills each field from its ResourcePath, relative to the package root, and the settings asset carries<br/>
    /// them into every build, so there is nothing to assign and nothing to show.
    /// </summary>
    // The Graphics settings window reads this on the class and leaves the entry out, as URP does for its own resources.
    // ReSharper disable once Unity.RedundantAttributeOnTarget
    [Serializable, HideInInspector]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    internal sealed class Outline2DResources : IRenderPipelineResources {
        [SerializeField]
        private int version;

        [SerializeField, ResourcePath("Runtime/Shaders/OutlineMask.shader")]
        private Shader maskShader;

        public Shader MaskShader => maskShader;

        int IRenderPipelineGraphicsSettings.version => version;
        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;
    }
}