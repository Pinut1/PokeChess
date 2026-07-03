using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Urpixel
{
    public class DepthNormalsFeature : ScriptableRendererFeature
    {
        class DepthNormalsPass : ScriptableRenderPass
        {
            int kDepthBufferBits = 32;
            // private RenderTargetHandle depthAttachmentHandle { get; set; } // Unity 2023.1부터 제거됨 → int ID + RTHandle로 교체
            private int depthAttachmentId { get; set; }
            private RTHandle depthAttachmentHandle;
            internal RenderTextureDescriptor descriptor { get; private set; }

            private Material depthNormalsMaterial = null;
            private FilteringSettings m_FilteringSettings;
            string m_ProfilerTag = "DepthNormals Prepass";
            ShaderTagId m_ShaderTagId = new ShaderTagId("DepthOnly");

            public DepthNormalsPass(RenderQueueRange renderQueueRange, LayerMask layerMask, Material material)
            {
                m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
                depthNormalsMaterial = material;
            }

            // public void Setup(RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle) // Unity 2023.1부터 제거됨
            // → 파라미터를 RenderTargetHandle 대신 int(셰이더 프로퍼티 ID)로 변경
            public void Setup(RenderTextureDescriptor baseDescriptor, int depthAttachmentId)
            {
                // this.depthAttachmentHandle = depthAttachmentHandle; // Unity 2023.1부터 제거됨
                this.depthAttachmentId = depthAttachmentId;
                // ConfigureTarget에 RTHandle이 필요하므로 int ID를 RTHandle로 감싸서 보관
                depthAttachmentHandle = RTHandles.Alloc(new RenderTargetIdentifier(depthAttachmentId), "_CameraDepthNormalsTexture");
                baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
                baseDescriptor.depthBufferBits = kDepthBufferBits;
                descriptor = baseDescriptor;
            }

            // 렌더 패스 실행 전 호출됨.
            // 렌더 타겟 설정 및 클리어 상태를 지정하거나 임시 렌더 텍스쳐를 생성할 때 사용.
            // CommandBuffer.SetRenderTarget 대신 ConfigureTarget / ConfigureClear를 사용해야 함.
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // cmd.GetTemporaryRT(depthAttachmentHandle.id, descriptor, FilterMode.Point); // Unity 2023.1부터 제거됨
                // ConfigureTarget(depthAttachmentHandle.Identifier());                        // Unity 2023.1부터 제거됨
                cmd.GetTemporaryRT(depthAttachmentId, descriptor, FilterMode.Point);
                ConfigureTarget(depthAttachmentHandle); // RTHandle을 직접 전달
                ConfigureClear(ClearFlag.All, Color.black);
            }

            // 렌더링 로직 구현부.
            // ScriptableRenderContext로 드로우 커맨드를 발행하거나 커맨드 버퍼를 실행함.
            // ScriptableRenderContext.submit은 렌더 파이프라인이 자동으로 호출하므로 직접 호출 불필요.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

                using (new ProfilingSample(cmd, m_ProfilerTag))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortFlags);
                    drawSettings.perObjectData = PerObjectData.None;


                    ref CameraData cameraData = ref renderingData.cameraData;
                    Camera camera = cameraData.camera;
                    if (cameraData.xr.enabled)
                        context.StartMultiEye(camera);


                    drawSettings.overrideMaterial = depthNormalsMaterial;


                    context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                        ref m_FilteringSettings);

                    // cmd.SetGlobalTexture("_CameraDepthNormalsTexture", depthAttachmentHandle.id); // Unity 2023.1부터 제거됨
                    cmd.SetGlobalTexture("_CameraDepthNormalsTexture", depthAttachmentId);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            // 렌더 패스 실행 후 할당된 리소스를 해제함.
            public override void FrameCleanup(CommandBuffer cmd)
            {
                // Unity 2023.1부터 RenderTargetHandle.CameraTarget 비교 불가 → 항상 ReleaseTemporaryRT 호출로 단순화
                // if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
                // {
                //     cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
                //     depthAttachmentHandle = RenderTargetHandle.CameraTarget;
                // }
                cmd.ReleaseTemporaryRT(depthAttachmentId);
            }
        }

        DepthNormalsPass depthNormalsPass;
        // RenderTargetHandle depthNormalsTexture; // Unity 2023.1부터 제거됨 → int ID로 교체
        int depthNormalsTextureId;
        Material depthNormalsMaterial;

        public override void Create()
        {
            depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
            depthNormalsPass = new DepthNormalsPass(RenderQueueRange.opaque, -1, depthNormalsMaterial);
            depthNormalsPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            // depthNormalsTexture.Init("_CameraDepthNormalsTexture"); // Unity 2023.1부터 제거됨
            // → Shader.PropertyToID로 셰이더 프로퍼티 ID를 직접 가져와 저장
            depthNormalsTextureId = Shader.PropertyToID("_CameraDepthNormalsTexture");
        }

        // 카메라당 한 번 호출되며, 렌더러에 렌더 패스를 등록함.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // depthNormalsPass.Setup(renderingData.cameraData.cameraTargetDescriptor, depthNormalsTexture); // Unity 2023.1부터 제거됨
            depthNormalsPass.Setup(renderingData.cameraData.cameraTargetDescriptor, depthNormalsTextureId);
            renderer.EnqueuePass(depthNormalsPass);
        }
    }
}
