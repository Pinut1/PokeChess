using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Urpixel
{
    public class PixelOutline : ScriptableRendererFeature
    {
        [System.Serializable]
        private class PixelOutlineSettings
        {
            [Header("General Outline Settings")] public Color outlineColor = Color.black;
            [Range(0.0f, 1.5f)] public float outlineScale = 1.0f;
            [Range(0.0f, .5f)] public float depthThreshold = .005f;
            public bool innerEdge;
            public float angleThreshold = 0.4f;
            public float steepAngleMultiplier = 0.4f;
        }

        private class PixelOutlinePass : ScriptableRenderPass
        {
            private readonly Material pixelOutlineMaterial;

            private FilteringSettings filteringSettings;

            private readonly List<ShaderTagId> shaderTagIdList;
            private readonly Material normalsMaterial;

            private RTHandle normals;
            private RendererList normalsRenderersList;

            RTHandle temporaryBuffer;

            public PixelOutlinePass(RenderPassEvent renderPassEvent, LayerMask layerMask, Shader outlineShader,
                PixelOutlineSettings settings)
            {
                this.renderPassEvent = renderPassEvent;

                pixelOutlineMaterial = new Material(outlineShader);
                pixelOutlineMaterial.SetColor("_OutlineColor", settings.outlineColor);
                pixelOutlineMaterial.SetFloat("_OutlineScale", settings.outlineScale);
                pixelOutlineMaterial.SetFloat("_InnerEdge", settings.innerEdge ? 1 : -1);
                pixelOutlineMaterial.SetFloat("_DepthThreshold", settings.depthThreshold);
                pixelOutlineMaterial.SetFloat("_AngleThreshold", settings.angleThreshold);
                pixelOutlineMaterial.SetFloat("_SteepAngleMultiplier", settings.steepAngleMultiplier);

                filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);

                shaderTagIdList = new List<ShaderTagId>
                {
                    new("UniversalForward"),
                    new("UniversalForwardOnly"),
                    new("LightweightForward"),
                    new("SRPDefaultUnlit")
                };

                normalsMaterial = new Material(Shader.Find("URPixel/URPixel_ViewSpaceNormals"));
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor textureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                RenderingUtils.ReAllocateIfNeeded(ref normals, textureDescriptor);
                textureDescriptor.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref temporaryBuffer, textureDescriptor, FilterMode.Bilinear);

                ConfigureTarget(normals, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.Color, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!pixelOutlineMaterial || !normalsMaterial ||
                    renderingData.cameraData.renderer.cameraColorTargetHandle.rt == null || temporaryBuffer.rt == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Normals
                DrawingSettings drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData,
                    renderingData.cameraData.defaultOpaqueSortFlags);
                drawSettings.overrideMaterial = normalsMaterial;

                RendererListParams normalsRenderersParams =
                    new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                normalsRenderersList = context.CreateRendererList(ref normalsRenderersParams);
                cmd.DrawRendererList(normalsRenderersList);

                // Pass in RT for Outlines shader
                cmd.SetGlobalTexture(Shader.PropertyToID("_SceneViewSpaceNormals"), normals.rt);

                using (new ProfilingScope(cmd, new ProfilingSampler("PixelOutline")))
                {

                    Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle,
                        temporaryBuffer, pixelOutlineMaterial, 0);
                    Blitter.BlitCameraTexture(cmd, temporaryBuffer,
                        renderingData.cameraData.renderer.cameraColorTargetHandle);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Release()
            {
                CoreUtils.Destroy(pixelOutlineMaterial);
                CoreUtils.Destroy(normalsMaterial);
                normals?.Release();
                temporaryBuffer?.Release();
            }

        }

        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
        [SerializeField] private LayerMask outlinesLayerMask;
        [SerializeField] private PixelOutlineSettings outlineSettings = new();

        private PixelOutlinePass pixelOutlinePass;

        public override void Create()
        {
            if (renderPassEvent < RenderPassEvent.BeforeRenderingPrePasses)
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;


            pixelOutlinePass = new PixelOutlinePass(renderPassEvent, outlinesLayerMask,
                Shader.Find("URPixel/URPixel_Outlines"), outlineSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pixelOutlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pixelOutlinePass?.Release();
            }
        }

    }
}