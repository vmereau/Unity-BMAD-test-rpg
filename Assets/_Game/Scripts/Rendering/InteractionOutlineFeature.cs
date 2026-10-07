using System;
using System.Collections.Generic;
using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.Rendering
{
    /// <summary>
    /// Screen-space outline around renderers on the Outline rendering layer (the focused interactable —
    /// see <see cref="InteractionHighlight"/>). Pass 1 draws those renderers into an R8 mask, depth-tested
    /// against the camera depth so only the visible silhouette counts. Pass 2 dilates the mask and blends
    /// a constant-width edge over the camera color. RenderGraph only.
    /// </summary>
    public class InteractionOutlineFeature : ScriptableRendererFeature
    {
        private const string TAG = "[Outline]";

        [Serializable]
        public class Settings
        {
            public RenderingLayerMask outlineLayer;
            public Shader maskShader;
            public Shader compositeShader;
            [Range(1f, 8f)] public float widthPixels = 3f;
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        [SerializeField] private Settings _settings = new Settings();

        private Material _maskMaterial;
        private Material _compositeMaterial;
        private InteractionOutlinePass _pass;
        private bool _loggedMissingShader;

        public override void Create()
        {
            DestroyMaterials();

            if (_settings.maskShader == null || _settings.compositeShader == null)
            {
                if (!_loggedMissingShader)
                {
                    GameLog.Error(TAG, "InteractionOutlineFeature: maskShader or compositeShader not assigned — outline disabled");
                    _loggedMissingShader = true;
                }
                _pass = null;
                return;
            }
            _loggedMissingShader = false;

            _maskMaterial = CoreUtils.CreateEngineMaterial(_settings.maskShader);
            _compositeMaterial = CoreUtils.CreateEngineMaterial(_settings.compositeShader);
            _pass = new InteractionOutlinePass(_settings, _maskMaterial, _compositeMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _maskMaterial == null || _compositeMaterial == null) return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;
            if (_settings.outlineLayer.value == 0) return;
            // Always enqueued so the intermediate-texture requirement stays constant frame to frame;
            // RecordRenderGraph adds no passes while nothing is highlighted.

            _pass.renderPassEvent = _settings.passEvent;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            DestroyMaterials();
        }

        private void DestroyMaterials()
        {
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_compositeMaterial);
            _maskMaterial = null;
            _compositeMaterial = null;
        }

        private class InteractionOutlinePass : ScriptableRenderPass
        {
            private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
            private static readonly int OutlineMaskTexelSizeId = Shader.PropertyToID("_OutlineMaskTexelSize");

            private readonly Settings _settings;
            private readonly Material _maskMaterial;
            private readonly Material _compositeMaterial;
            private readonly List<ShaderTagId> _shaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
            };

            private class MaskPassData
            {
                public RendererListHandle rendererList;
            }

            private class CompositePassData
            {
                public TextureHandle mask;
                public Material material;
                public float width;
                public Vector4 texelSize;
            }

            public InteractionOutlinePass(Settings settings, Material maskMaterial, Material compositeMaterial)
            {
                _settings = settings;
                _maskMaterial = maskMaterial;
                _compositeMaterial = compositeMaterial;
                profilingSampler = new ProfilingSampler("InteractionOutline");
                // Both passes need an intermediate color/depth target (not the backbuffer).
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();

                // Nothing focused → no passes at all (clean Frame Debugger, no cost).
                if (!InteractionHighlight.AnyHighlighted) return;
                if (resourceData.isActiveTargetBackBuffer) return;

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.graphicsFormat = GraphicsFormat.R8_UNorm;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.msaaSamples = 1;
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_InteractionOutlineMask", true);

                // Pass 1 — mask of visible Outline-layer renderers.
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           "InteractionOutline Mask", out var passData, profilingSampler))
                {
                    DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(
                        _shaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                    drawing.overrideMaterial = _maskMaterial;
                    drawing.overrideMaterialPassIndex = 0;

                    var filtering = new FilteringSettings(RenderQueueRange.all)
                    {
                        renderingLayerMask = _settings.outlineLayer.value
                    };

                    var listParams = new RendererListParams(renderingData.cullResults, drawing, filtering);
                    passData.rendererList = renderGraph.CreateRendererList(listParams);

                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

                    // The mask is cleared on first use (CreateRenderGraphTexture clear: true).
                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                        context.cmd.DrawRendererList(data.rendererList));
                }

                // Pass 2 — dilate the mask and blend the edge over the camera color.
                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "InteractionOutline Composite", out var passData, profilingSampler))
                {
                    passData.mask = mask;
                    passData.material = _compositeMaterial;
                    passData.width = _settings.widthPixels;
                    passData.texelSize = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);

                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);

                    builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
                    {
                        data.material.SetFloat(OutlineWidthId, data.width);
                        data.material.SetVector(OutlineMaskTexelSizeId, data.texelSize);
                        Blitter.BlitTexture(context.cmd, data.mask, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                    });
                }
            }
        }
    }
}
