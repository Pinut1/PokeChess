Shader "URPixel/URPixel_Outlines"
{
    Properties
    {
        _OutlineScale("OutlineScale", Float) = 1
        _DepthThreshold("DepthThreshold", Float) = 1.5
        _OutlineColor("OutlineColor", Color) = (0, 0, 0, 1)
        _SteepAngleMultiplier("SteepAngleMultiplier", Float) = 10.5
        _InnerEdge("InnerEdge", Float) = 1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            // RenderType: <None>
            // Queue: <None>
            // DisableBatching: <None>
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalFullscreenSubTarget"
        }
        Pass
        {
            Name "DrawProcedural"
        
        // Render State
        Cull Off
        Blend Off
        ZTest Off
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 3.0
        #pragma vertex vert
        #pragma fragment frag
        // #pragma enable_d3d11_debug_symbols
        
        /* WARNING: $splice Could not find named fragment 'DotsInstancingOptions' */
        /* WARNING: $splice Could not find named fragment 'HybridV1InjectedBuiltinProperties' */
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        #define FULLSCREEN_SHADERGRAPH
        
        // Defines
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_VERTEXID
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD1
        
        // Force depth texture because we need it for almost every nodes
        // TODO: dependency system that triggers this define from position or view direction usage
        #define REQUIRE_DEPTH_TEXTURE
        #define REQUIRE_NORMAL_TEXTURE
        
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DRAWPROCEDURAL
        #define REQUIRE_DEPTH_TEXTURE
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/Fullscreen/Includes/FullscreenShaderPass.cs.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
        #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
             uint vertexID : VERTEXID_SEMANTIC;
        };
        struct SurfaceDescriptionInputs
        {
             float3 ViewSpacePosition;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float4 uv0;
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
             float4 texCoord1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 texCoord1 : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.texCoord1.xyzw = input.texCoord1;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.texCoord1 = input.texCoord1.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _OutlineScale;
        float _DepthThreshold;
        float4 _OutlineColor;
        float _InnerEdge;
        float _SteepAngleMultiplier;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SceneViewSpaceNormals);
        SAMPLER(sampler_SceneViewSpaceNormals);
        float4 _SceneViewSpaceNormals_TexelSize;
        float _FlipY;
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        
        TEXTURE2D_X(_BlitTexture);
        float4 Unity_Universal_SampleBuffer_BlitSource_float(float2 uv)
        {
            uint2 pixelCoords = uint2(uv * _ScreenSize.xy);
            return LOAD_TEXTURE2D_X_LOD(_BlitTexture, pixelCoords, 0);
        }
        
        void Unity_SceneDepth_Raw_float(float4 UV, out float Out)
        {
            Out = SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
        {
            Out = A / B;
        }
        
        void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float2(float2 A, float2 B, out float2 Out)
        {
            Out = A + B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax, out float3 Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_OneMinus_float(float In, out float Out)
        {
            Out = 1 - In;
        }
        
        void Unity_Step_float(float Edge, float In, out float Out)
        {
            Out = step(Edge, In);
        }
        
        void Unity_Any_float4(float4 In, out float Out)
        {
            Out = any(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Blend_Overwrite_float3(float3 Base, float3 Blend, out float3 Out, float Opacity)
        {
            Out = lerp(Base, Blend, Opacity);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        // GraphVertex: <None>
        
        // Custom interpolators, pre surface
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreSurface' */
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _URPSampleBuffer_411780b3c1e34886adc5d7e1c3094ffd_Output_2_Vector4 = Unity_Universal_SampleBuffer_BlitSource_float(float4(IN.NDCPosition.xy, 0, 0).xy);
            float4 _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4 = _OutlineColor;
            float _Split_ba51af8e9119405ab0e1e29daa901578_R_1_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[0];
            float _Split_ba51af8e9119405ab0e1e29daa901578_G_2_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[1];
            float _Split_ba51af8e9119405ab0e1e29daa901578_B_3_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[2];
            float _Split_ba51af8e9119405ab0e1e29daa901578_A_4_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[3];
            float3 _Vector3_8746e484a95444d697f594997323902b_Out_0_Vector3 = float3(_Split_ba51af8e9119405ab0e1e29daa901578_R_1_Float, _Split_ba51af8e9119405ab0e1e29daa901578_G_2_Float, _Split_ba51af8e9119405ab0e1e29daa901578_B_3_Float);
            float _Property_577cd41f456942d299588ecd0efac7c9_Out_0_Float = _InnerEdge;
            float4 _UV_ae34baf17ac94457ad3d71f2c8940fc4_Out_0_Vector4 = IN.uv0;
            float _SceneDepth_ff95ce72338849a29c8fbc0abdc57c97_Out_1_Float;
            Unity_SceneDepth_Raw_float(_UV_ae34baf17ac94457ad3d71f2c8940fc4_Out_0_Vector4, _SceneDepth_ff95ce72338849a29c8fbc0abdc57c97_Out_1_Float);
            float _Multiply_f74711c3f4d6417caff69de659401f75_Out_2_Float;
            Unity_Multiply_float_float(_SceneDepth_ff95ce72338849a29c8fbc0abdc57c97_Out_1_Float, 4, _Multiply_f74711c3f4d6417caff69de659401f75_Out_2_Float);
            float4 _UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4 = IN.uv0;
            float _Property_ed727859c8cb4b8cbff1966530cacae9_Out_0_Float = _OutlineScale;
            float2 _Vector2_352ce01474f24bd2afc899a63d4e3f58_Out_0_Vector2 = float2(_ScreenParams.x, _ScreenParams.y);
            float2 _Divide_f7c0022af0f54c299b896c578f0cd9f7_Out_2_Vector2;
            Unity_Divide_float2(float2(1, 1), _Vector2_352ce01474f24bd2afc899a63d4e3f58_Out_0_Vector2, _Divide_f7c0022af0f54c299b896c578f0cd9f7_Out_2_Vector2);
            float2 _Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2;
            Unity_Multiply_float2_float2((_Property_ed727859c8cb4b8cbff1966530cacae9_Out_0_Float.xx), _Divide_f7c0022af0f54c299b896c578f0cd9f7_Out_2_Vector2, _Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2);
            float2 _Multiply_17a486a943b0434d8e2d790ebbff2ba7_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(1, 0), _Multiply_17a486a943b0434d8e2d790ebbff2ba7_Out_2_Vector2);
            float2 _Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_17a486a943b0434d8e2d790ebbff2ba7_Out_2_Vector2, _Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2);
            float _SceneDepth_6e8fe12d63444b8eb4f9a3f7c3802d51_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_6e8fe12d63444b8eb4f9a3f7c3802d51_Out_1_Float);
            float2 _Multiply_85d41e0b4ab9469daa8880a067016b9d_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(-1, 0), _Multiply_85d41e0b4ab9469daa8880a067016b9d_Out_2_Vector2);
            float2 _Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_85d41e0b4ab9469daa8880a067016b9d_Out_2_Vector2, _Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2);
            float _SceneDepth_fc544b01561c404f8c1c8aee18370e88_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_fc544b01561c404f8c1c8aee18370e88_Out_1_Float);
            float _Add_fcabc8e83733452a9e173cc9459ad622_Out_2_Float;
            Unity_Add_float(_SceneDepth_6e8fe12d63444b8eb4f9a3f7c3802d51_Out_1_Float, _SceneDepth_fc544b01561c404f8c1c8aee18370e88_Out_1_Float, _Add_fcabc8e83733452a9e173cc9459ad622_Out_2_Float);
            float2 _Multiply_cc47a2dc94b34a75a67d89504ebbfbdb_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(0, 1), _Multiply_cc47a2dc94b34a75a67d89504ebbfbdb_Out_2_Vector2);
            float2 _Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_cc47a2dc94b34a75a67d89504ebbfbdb_Out_2_Vector2, _Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2);
            float _SceneDepth_2ff359beb1124644a529d493f7052015_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_2ff359beb1124644a529d493f7052015_Out_1_Float);
            float2 _Multiply_de47545d08d6462886111b906fd49a8d_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(0, -1), _Multiply_de47545d08d6462886111b906fd49a8d_Out_2_Vector2);
            float2 _Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_de47545d08d6462886111b906fd49a8d_Out_2_Vector2, _Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2);
            float _SceneDepth_7760fe013b3e4a1392be8eb7eb0dd688_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_7760fe013b3e4a1392be8eb7eb0dd688_Out_1_Float);
            float _Add_86e1977c378b43968e2c5381363e692c_Out_2_Float;
            Unity_Add_float(_SceneDepth_2ff359beb1124644a529d493f7052015_Out_1_Float, _SceneDepth_7760fe013b3e4a1392be8eb7eb0dd688_Out_1_Float, _Add_86e1977c378b43968e2c5381363e692c_Out_2_Float);
            float _Add_fd2533150eb1419596f00f030b7d1951_Out_2_Float;
            Unity_Add_float(_Add_fcabc8e83733452a9e173cc9459ad622_Out_2_Float, _Add_86e1977c378b43968e2c5381363e692c_Out_2_Float, _Add_fd2533150eb1419596f00f030b7d1951_Out_2_Float);
            float _Subtract_909d542ce66a4db188e65d1dfac5ea49_Out_2_Float;
            Unity_Subtract_float(_Multiply_f74711c3f4d6417caff69de659401f75_Out_2_Float, _Add_fd2533150eb1419596f00f030b7d1951_Out_2_Float, _Subtract_909d542ce66a4db188e65d1dfac5ea49_Out_2_Float);
            float _Multiply_6999575206934346a66ab439c69b1f26_Out_2_Float;
            Unity_Multiply_float_float(_Property_577cd41f456942d299588ecd0efac7c9_Out_0_Float, _Subtract_909d542ce66a4db188e65d1dfac5ea49_Out_2_Float, _Multiply_6999575206934346a66ab439c69b1f26_Out_2_Float);
            float _Property_44cdb40968a346daa3879063c1545100_Out_0_Float = _DepthThreshold;
            float _Property_aae7dd7acbe24bd6ad7f183fa8cacab8_Out_0_Float = _SteepAngleMultiplier;
            UnityTexture2D _Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SceneViewSpaceNormals);
            float4 _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D.tex, _Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D.samplerstate, _Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D.GetTransformedUV((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy)) );
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_R_4_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.r;
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_G_5_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.g;
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_B_6_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.b;
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_A_7_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.a;
            float4 _Remap_06502ea863004b88b29643f915e16259_Out_3_Vector4;
            Unity_Remap_float4(_SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4, float2 (0, 1), float2 (-1, 1), _Remap_06502ea863004b88b29643f915e16259_Out_3_Vector4);
            float3 _Remap_bd2c591c59be40bca5fcc0aa2efc2780_Out_3_Vector3;
            Unity_Remap_float3(IN.ViewSpacePosition, float2 (0, 1), float2 (1, -1), _Remap_bd2c591c59be40bca5fcc0aa2efc2780_Out_3_Vector3);
            float _DotProduct_13db9d5fe8df460e88d66db091bb6928_Out_2_Float;
            Unity_DotProduct_float3((_Remap_06502ea863004b88b29643f915e16259_Out_3_Vector4.xyz), _Remap_bd2c591c59be40bca5fcc0aa2efc2780_Out_3_Vector3, _DotProduct_13db9d5fe8df460e88d66db091bb6928_Out_2_Float);
            float _OneMinus_a97105028f164577b35b5b5d245e43cb_Out_1_Float;
            Unity_OneMinus_float(_DotProduct_13db9d5fe8df460e88d66db091bb6928_Out_2_Float, _OneMinus_a97105028f164577b35b5b5d245e43cb_Out_1_Float);
            float _Multiply_e30e2d9c961e431db1a4360f87867ad3_Out_2_Float;
            Unity_Multiply_float_float(_Property_aae7dd7acbe24bd6ad7f183fa8cacab8_Out_0_Float, _OneMinus_a97105028f164577b35b5b5d245e43cb_Out_1_Float, _Multiply_e30e2d9c961e431db1a4360f87867ad3_Out_2_Float);
            float _Add_90f30174d5854d27a0480b88d7be2daf_Out_2_Float;
            Unity_Add_float(1, _Multiply_e30e2d9c961e431db1a4360f87867ad3_Out_2_Float, _Add_90f30174d5854d27a0480b88d7be2daf_Out_2_Float);
            float4 _UV_1f8e034b00e949079ad0b7aa0258b00d_Out_0_Vector4 = IN.uv0;
            float _SceneDepth_1de9e7cb5eb946739abdff80ec64b520_Out_1_Float;
            Unity_SceneDepth_Raw_float(_UV_1f8e034b00e949079ad0b7aa0258b00d_Out_0_Vector4, _SceneDepth_1de9e7cb5eb946739abdff80ec64b520_Out_1_Float);
            float _Multiply_1f4e60f4cd8f403abf932ad8eda47517_Out_2_Float;
            Unity_Multiply_float_float(_Add_90f30174d5854d27a0480b88d7be2daf_Out_2_Float, _SceneDepth_1de9e7cb5eb946739abdff80ec64b520_Out_1_Float, _Multiply_1f4e60f4cd8f403abf932ad8eda47517_Out_2_Float);
            float _Multiply_b3488e8d46254a7482da57847ceabfa7_Out_2_Float;
            Unity_Multiply_float_float(_Property_44cdb40968a346daa3879063c1545100_Out_0_Float, _Multiply_1f4e60f4cd8f403abf932ad8eda47517_Out_2_Float, _Multiply_b3488e8d46254a7482da57847ceabfa7_Out_2_Float);
            float _Step_46d07fd645514c66a620da6d45360c05_Out_2_Float;
            Unity_Step_float(_Multiply_6999575206934346a66ab439c69b1f26_Out_2_Float, _Multiply_b3488e8d46254a7482da57847ceabfa7_Out_2_Float, _Step_46d07fd645514c66a620da6d45360c05_Out_2_Float);
            float _OneMinus_b88afb7379db48668e05de651369f4a3_Out_1_Float;
            Unity_OneMinus_float(_Step_46d07fd645514c66a620da6d45360c05_Out_2_Float, _OneMinus_b88afb7379db48668e05de651369f4a3_Out_1_Float);
            float _Multiply_555aa90ade054dada5bc0a4951972cc3_Out_2_Float;
            Unity_Multiply_float_float(_Split_ba51af8e9119405ab0e1e29daa901578_A_4_Float, _OneMinus_b88afb7379db48668e05de651369f4a3_Out_1_Float, _Multiply_555aa90ade054dada5bc0a4951972cc3_Out_2_Float);
            UnityTexture2D _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SceneViewSpaceNormals);
            float4 _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2) );
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_R_4_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.r;
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_G_5_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.g;
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_B_6_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.b;
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_A_7_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.a;
            float4 _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2) );
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_R_4_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.r;
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_G_5_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.g;
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_B_6_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.b;
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_A_7_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.a;
            float4 _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2) );
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_R_4_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.r;
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_G_5_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.g;
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_B_6_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.b;
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_A_7_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.a;
            float4 _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2) );
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_R_4_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.r;
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_G_5_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.g;
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_B_6_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.b;
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_A_7_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.a;
            float4 _Vector4_e908fc51977e42b4974ad2629668640a_Out_0_Vector4 = float4(_SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_A_7_Float, _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_A_7_Float, _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_A_7_Float, _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_A_7_Float);
            float _Any_c9c3c5b1653548dab30f10682cc18ece_Out_1_Boolean;
            Unity_Any_float4(_Vector4_e908fc51977e42b4974ad2629668640a_Out_0_Vector4, _Any_c9c3c5b1653548dab30f10682cc18ece_Out_1_Boolean);
            float _Branch_4827914961cf4625854ec1c86e411529_Out_3_Float;
            Unity_Branch_float(_Any_c9c3c5b1653548dab30f10682cc18ece_Out_1_Boolean, 1, 0, _Branch_4827914961cf4625854ec1c86e411529_Out_3_Float);
            float _Multiply_0e5256229ffa44c593ecd5d3582440d5_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_555aa90ade054dada5bc0a4951972cc3_Out_2_Float, _Branch_4827914961cf4625854ec1c86e411529_Out_3_Float, _Multiply_0e5256229ffa44c593ecd5d3582440d5_Out_2_Float);
            float3 _Blend_ff80c604456f4e808be8c87fa3fbfc76_Out_2_Vector3;
            Unity_Blend_Overwrite_float3((_URPSampleBuffer_411780b3c1e34886adc5d7e1c3094ffd_Output_2_Vector4.xyz), _Vector3_8746e484a95444d697f594997323902b_Out_0_Vector3, _Blend_ff80c604456f4e808be8c87fa3fbfc76_Out_2_Vector3, _Multiply_0e5256229ffa44c593ecd5d3582440d5_Out_2_Float);
            surface.BaseColor = _Blend_ff80c604456f4e808be8c87fa3fbfc76_Out_2_Vector3;
            surface.Alpha = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            float3 normalWS = SHADERGRAPH_SAMPLE_SCENE_NORMAL(input.texCoord0.xy);
            float4 tangentWS = float4(0, 1, 0, 0); // We can't access the tangent in screen space
        
        
        
        
            float3 viewDirWS = normalize(input.texCoord1.xyz);
            float linearDepth = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(input.texCoord0.xy), _ZBufferParams);
            float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
            float camearDistance = linearDepth / dot(viewDirWS, cameraForward);
            float3 positionWS = viewDirWS * camearDistance + GetCameraPositionWS();
        
        
            output.WorldSpacePosition = positionWS;
            output.ViewSpacePosition = TransformWorldToView(positionWS);
            output.ScreenPosition = float4(input.texCoord0.xy, 0, 1);
            output.uv0 = input.texCoord0;
            output.NDCPosition = input.texCoord0.xy;
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/Fullscreen/Includes/FullscreenCommon.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/Fullscreen/Includes/FullscreenDrawProcedural.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "Blit"
        
        // Render State
        Cull Off
        Blend Off
        ZTest Off
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 3.0
        #pragma vertex vert
        #pragma fragment frag
        // #pragma enable_d3d11_debug_symbols
        
        /* WARNING: $splice Could not find named fragment 'DotsInstancingOptions' */
        /* WARNING: $splice Could not find named fragment 'HybridV1InjectedBuiltinProperties' */
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        #define FULLSCREEN_SHADERGRAPH
        
        // Defines
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_VERTEXID
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD1
        
        // Force depth texture because we need it for almost every nodes
        // TODO: dependency system that triggers this define from position or view direction usage
        #define REQUIRE_DEPTH_TEXTURE
        #define REQUIRE_NORMAL_TEXTURE
        
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_BLIT
        #define REQUIRE_DEPTH_TEXTURE
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/Fullscreen/Includes/FullscreenShaderPass.cs.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
        #include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
             uint vertexID : VERTEXID_SEMANTIC;
             float3 positionOS : POSITION;
        };
        struct SurfaceDescriptionInputs
        {
             float3 ViewSpacePosition;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float4 uv0;
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0;
             float4 texCoord1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
        };
        struct VertexDescriptionInputs
        {
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float4 texCoord1 : INTERP1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.texCoord1.xyzw = input.texCoord1;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.texCoord1 = input.texCoord1.xyzw;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float _OutlineScale;
        float _DepthThreshold;
        float4 _OutlineColor;
        float _InnerEdge;
        float _SteepAngleMultiplier;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_SceneViewSpaceNormals);
        SAMPLER(sampler_SceneViewSpaceNormals);
        float4 _SceneViewSpaceNormals_TexelSize;
        float _FlipY;
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        
        TEXTURE2D_X(_BlitTexture);
        float4 Unity_Universal_SampleBuffer_BlitSource_float(float2 uv)
        {
            uint2 pixelCoords = uint2(uv * _ScreenSize.xy);
            return LOAD_TEXTURE2D_X_LOD(_BlitTexture, pixelCoords, 0);
        }
        
        void Unity_SceneDepth_Raw_float(float4 UV, out float Out)
        {
            Out = SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV.xy);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Divide_float2(float2 A, float2 B, out float2 Out)
        {
            Out = A / B;
        }
        
        void Unity_Multiply_float2_float2(float2 A, float2 B, out float2 Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float2(float2 A, float2 B, out float2 Out)
        {
            Out = A + B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax, out float4 Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax, out float3 Out)
        {
            Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_OneMinus_float(float In, out float Out)
        {
            Out = 1 - In;
        }
        
        void Unity_Step_float(float Edge, float In, out float Out)
        {
            Out = step(Edge, In);
        }
        
        void Unity_Any_float4(float4 In, out float Out)
        {
            Out = any(In);
        }
        
        void Unity_Branch_float(float Predicate, float True, float False, out float Out)
        {
            Out = Predicate ? True : False;
        }
        
        void Unity_Blend_Overwrite_float3(float3 Base, float3 Blend, out float3 Out, float Opacity)
        {
            Out = lerp(Base, Blend, Opacity);
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        // GraphVertex: <None>
        
        // Custom interpolators, pre surface
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreSurface' */
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float Alpha;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _URPSampleBuffer_411780b3c1e34886adc5d7e1c3094ffd_Output_2_Vector4 = Unity_Universal_SampleBuffer_BlitSource_float(float4(IN.NDCPosition.xy, 0, 0).xy);
            float4 _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4 = _OutlineColor;
            float _Split_ba51af8e9119405ab0e1e29daa901578_R_1_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[0];
            float _Split_ba51af8e9119405ab0e1e29daa901578_G_2_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[1];
            float _Split_ba51af8e9119405ab0e1e29daa901578_B_3_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[2];
            float _Split_ba51af8e9119405ab0e1e29daa901578_A_4_Float = _Property_63ff6d485e9b4584839f86f609f0415f_Out_0_Vector4[3];
            float3 _Vector3_8746e484a95444d697f594997323902b_Out_0_Vector3 = float3(_Split_ba51af8e9119405ab0e1e29daa901578_R_1_Float, _Split_ba51af8e9119405ab0e1e29daa901578_G_2_Float, _Split_ba51af8e9119405ab0e1e29daa901578_B_3_Float);
            float _Property_577cd41f456942d299588ecd0efac7c9_Out_0_Float = _InnerEdge;
            float4 _UV_ae34baf17ac94457ad3d71f2c8940fc4_Out_0_Vector4 = IN.uv0;
            float _SceneDepth_ff95ce72338849a29c8fbc0abdc57c97_Out_1_Float;
            Unity_SceneDepth_Raw_float(_UV_ae34baf17ac94457ad3d71f2c8940fc4_Out_0_Vector4, _SceneDepth_ff95ce72338849a29c8fbc0abdc57c97_Out_1_Float);
            float _Multiply_f74711c3f4d6417caff69de659401f75_Out_2_Float;
            Unity_Multiply_float_float(_SceneDepth_ff95ce72338849a29c8fbc0abdc57c97_Out_1_Float, 4, _Multiply_f74711c3f4d6417caff69de659401f75_Out_2_Float);
            float4 _UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4 = IN.uv0;
            float _Property_ed727859c8cb4b8cbff1966530cacae9_Out_0_Float = _OutlineScale;
            float2 _Vector2_352ce01474f24bd2afc899a63d4e3f58_Out_0_Vector2 = float2(_ScreenParams.x, _ScreenParams.y);
            float2 _Divide_f7c0022af0f54c299b896c578f0cd9f7_Out_2_Vector2;
            Unity_Divide_float2(float2(1, 1), _Vector2_352ce01474f24bd2afc899a63d4e3f58_Out_0_Vector2, _Divide_f7c0022af0f54c299b896c578f0cd9f7_Out_2_Vector2);
            float2 _Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2;
            Unity_Multiply_float2_float2((_Property_ed727859c8cb4b8cbff1966530cacae9_Out_0_Float.xx), _Divide_f7c0022af0f54c299b896c578f0cd9f7_Out_2_Vector2, _Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2);
            float2 _Multiply_17a486a943b0434d8e2d790ebbff2ba7_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(1, 0), _Multiply_17a486a943b0434d8e2d790ebbff2ba7_Out_2_Vector2);
            float2 _Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_17a486a943b0434d8e2d790ebbff2ba7_Out_2_Vector2, _Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2);
            float _SceneDepth_6e8fe12d63444b8eb4f9a3f7c3802d51_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_6e8fe12d63444b8eb4f9a3f7c3802d51_Out_1_Float);
            float2 _Multiply_85d41e0b4ab9469daa8880a067016b9d_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(-1, 0), _Multiply_85d41e0b4ab9469daa8880a067016b9d_Out_2_Vector2);
            float2 _Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_85d41e0b4ab9469daa8880a067016b9d_Out_2_Vector2, _Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2);
            float _SceneDepth_fc544b01561c404f8c1c8aee18370e88_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_fc544b01561c404f8c1c8aee18370e88_Out_1_Float);
            float _Add_fcabc8e83733452a9e173cc9459ad622_Out_2_Float;
            Unity_Add_float(_SceneDepth_6e8fe12d63444b8eb4f9a3f7c3802d51_Out_1_Float, _SceneDepth_fc544b01561c404f8c1c8aee18370e88_Out_1_Float, _Add_fcabc8e83733452a9e173cc9459ad622_Out_2_Float);
            float2 _Multiply_cc47a2dc94b34a75a67d89504ebbfbdb_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(0, 1), _Multiply_cc47a2dc94b34a75a67d89504ebbfbdb_Out_2_Vector2);
            float2 _Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_cc47a2dc94b34a75a67d89504ebbfbdb_Out_2_Vector2, _Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2);
            float _SceneDepth_2ff359beb1124644a529d493f7052015_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_2ff359beb1124644a529d493f7052015_Out_1_Float);
            float2 _Multiply_de47545d08d6462886111b906fd49a8d_Out_2_Vector2;
            Unity_Multiply_float2_float2(_Multiply_8ebc1b03387e45458053f973303d6776_Out_2_Vector2, float2(0, -1), _Multiply_de47545d08d6462886111b906fd49a8d_Out_2_Vector2);
            float2 _Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2;
            Unity_Add_float2((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy), _Multiply_de47545d08d6462886111b906fd49a8d_Out_2_Vector2, _Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2);
            float _SceneDepth_7760fe013b3e4a1392be8eb7eb0dd688_Out_1_Float;
            Unity_SceneDepth_Raw_float((float4(_Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2, 0.0, 1.0)), _SceneDepth_7760fe013b3e4a1392be8eb7eb0dd688_Out_1_Float);
            float _Add_86e1977c378b43968e2c5381363e692c_Out_2_Float;
            Unity_Add_float(_SceneDepth_2ff359beb1124644a529d493f7052015_Out_1_Float, _SceneDepth_7760fe013b3e4a1392be8eb7eb0dd688_Out_1_Float, _Add_86e1977c378b43968e2c5381363e692c_Out_2_Float);
            float _Add_fd2533150eb1419596f00f030b7d1951_Out_2_Float;
            Unity_Add_float(_Add_fcabc8e83733452a9e173cc9459ad622_Out_2_Float, _Add_86e1977c378b43968e2c5381363e692c_Out_2_Float, _Add_fd2533150eb1419596f00f030b7d1951_Out_2_Float);
            float _Subtract_909d542ce66a4db188e65d1dfac5ea49_Out_2_Float;
            Unity_Subtract_float(_Multiply_f74711c3f4d6417caff69de659401f75_Out_2_Float, _Add_fd2533150eb1419596f00f030b7d1951_Out_2_Float, _Subtract_909d542ce66a4db188e65d1dfac5ea49_Out_2_Float);
            float _Multiply_6999575206934346a66ab439c69b1f26_Out_2_Float;
            Unity_Multiply_float_float(_Property_577cd41f456942d299588ecd0efac7c9_Out_0_Float, _Subtract_909d542ce66a4db188e65d1dfac5ea49_Out_2_Float, _Multiply_6999575206934346a66ab439c69b1f26_Out_2_Float);
            float _Property_44cdb40968a346daa3879063c1545100_Out_0_Float = _DepthThreshold;
            float _Property_aae7dd7acbe24bd6ad7f183fa8cacab8_Out_0_Float = _SteepAngleMultiplier;
            UnityTexture2D _Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SceneViewSpaceNormals);
            float4 _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D.tex, _Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D.samplerstate, _Property_57e3bfa2acc841f9a6c5f164dd35025c_Out_0_Texture2D.GetTransformedUV((_UV_730704332c774c97932433c5bb2999bd_Out_0_Vector4.xy)) );
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_R_4_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.r;
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_G_5_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.g;
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_B_6_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.b;
            float _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_A_7_Float = _SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4.a;
            float4 _Remap_06502ea863004b88b29643f915e16259_Out_3_Vector4;
            Unity_Remap_float4(_SampleTexture2D_19dac375ac1d4ec7a1cbdb54873e1b84_RGBA_0_Vector4, float2 (0, 1), float2 (-1, 1), _Remap_06502ea863004b88b29643f915e16259_Out_3_Vector4);
            float3 _Remap_bd2c591c59be40bca5fcc0aa2efc2780_Out_3_Vector3;
            Unity_Remap_float3(IN.ViewSpacePosition, float2 (0, 1), float2 (1, -1), _Remap_bd2c591c59be40bca5fcc0aa2efc2780_Out_3_Vector3);
            float _DotProduct_13db9d5fe8df460e88d66db091bb6928_Out_2_Float;
            Unity_DotProduct_float3((_Remap_06502ea863004b88b29643f915e16259_Out_3_Vector4.xyz), _Remap_bd2c591c59be40bca5fcc0aa2efc2780_Out_3_Vector3, _DotProduct_13db9d5fe8df460e88d66db091bb6928_Out_2_Float);
            float _OneMinus_a97105028f164577b35b5b5d245e43cb_Out_1_Float;
            Unity_OneMinus_float(_DotProduct_13db9d5fe8df460e88d66db091bb6928_Out_2_Float, _OneMinus_a97105028f164577b35b5b5d245e43cb_Out_1_Float);
            float _Multiply_e30e2d9c961e431db1a4360f87867ad3_Out_2_Float;
            Unity_Multiply_float_float(_Property_aae7dd7acbe24bd6ad7f183fa8cacab8_Out_0_Float, _OneMinus_a97105028f164577b35b5b5d245e43cb_Out_1_Float, _Multiply_e30e2d9c961e431db1a4360f87867ad3_Out_2_Float);
            float _Add_90f30174d5854d27a0480b88d7be2daf_Out_2_Float;
            Unity_Add_float(1, _Multiply_e30e2d9c961e431db1a4360f87867ad3_Out_2_Float, _Add_90f30174d5854d27a0480b88d7be2daf_Out_2_Float);
            float4 _UV_1f8e034b00e949079ad0b7aa0258b00d_Out_0_Vector4 = IN.uv0;
            float _SceneDepth_1de9e7cb5eb946739abdff80ec64b520_Out_1_Float;
            Unity_SceneDepth_Raw_float(_UV_1f8e034b00e949079ad0b7aa0258b00d_Out_0_Vector4, _SceneDepth_1de9e7cb5eb946739abdff80ec64b520_Out_1_Float);
            float _Multiply_1f4e60f4cd8f403abf932ad8eda47517_Out_2_Float;
            Unity_Multiply_float_float(_Add_90f30174d5854d27a0480b88d7be2daf_Out_2_Float, _SceneDepth_1de9e7cb5eb946739abdff80ec64b520_Out_1_Float, _Multiply_1f4e60f4cd8f403abf932ad8eda47517_Out_2_Float);
            float _Multiply_b3488e8d46254a7482da57847ceabfa7_Out_2_Float;
            Unity_Multiply_float_float(_Property_44cdb40968a346daa3879063c1545100_Out_0_Float, _Multiply_1f4e60f4cd8f403abf932ad8eda47517_Out_2_Float, _Multiply_b3488e8d46254a7482da57847ceabfa7_Out_2_Float);
            float _Step_46d07fd645514c66a620da6d45360c05_Out_2_Float;
            Unity_Step_float(_Multiply_6999575206934346a66ab439c69b1f26_Out_2_Float, _Multiply_b3488e8d46254a7482da57847ceabfa7_Out_2_Float, _Step_46d07fd645514c66a620da6d45360c05_Out_2_Float);
            float _OneMinus_b88afb7379db48668e05de651369f4a3_Out_1_Float;
            Unity_OneMinus_float(_Step_46d07fd645514c66a620da6d45360c05_Out_2_Float, _OneMinus_b88afb7379db48668e05de651369f4a3_Out_1_Float);
            float _Multiply_555aa90ade054dada5bc0a4951972cc3_Out_2_Float;
            Unity_Multiply_float_float(_Split_ba51af8e9119405ab0e1e29daa901578_A_4_Float, _OneMinus_b88afb7379db48668e05de651369f4a3_Out_1_Float, _Multiply_555aa90ade054dada5bc0a4951972cc3_Out_2_Float);
            UnityTexture2D _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_SceneViewSpaceNormals);
            float4 _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_df031765ce3549909cecc65082617f4f_Out_2_Vector2) );
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_R_4_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.r;
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_G_5_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.g;
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_B_6_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.b;
            float _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_A_7_Float = _SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_RGBA_0_Vector4.a;
            float4 _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_e7d9267c9bf343db8920148ecf4cff61_Out_2_Vector2) );
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_R_4_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.r;
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_G_5_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.g;
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_B_6_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.b;
            float _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_A_7_Float = _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_RGBA_0_Vector4.a;
            float4 _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_5207aecbe3d5444cbf2e181045af342c_Out_2_Vector2) );
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_R_4_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.r;
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_G_5_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.g;
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_B_6_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.b;
            float _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_A_7_Float = _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_RGBA_0_Vector4.a;
            float4 _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.tex, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.samplerstate, _Property_85bd7c0312cb4cff9b5d532ff72e8b65_Out_0_Texture2D.GetTransformedUV(_Add_80cf21cfcfb64dbdb534499d909c512b_Out_2_Vector2) );
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_R_4_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.r;
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_G_5_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.g;
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_B_6_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.b;
            float _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_A_7_Float = _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_RGBA_0_Vector4.a;
            float4 _Vector4_e908fc51977e42b4974ad2629668640a_Out_0_Vector4 = float4(_SampleTexture2D_6846e4a1faed4609a98bee47e5e1618e_A_7_Float, _SampleTexture2D_c21e12618959414f8beb59f31b1319e3_A_7_Float, _SampleTexture2D_12b0a31b326345d7ba55a0ce8f6182cf_A_7_Float, _SampleTexture2D_135f86c5f2ca4354a5a9d527a1a3cc31_A_7_Float);
            float _Any_c9c3c5b1653548dab30f10682cc18ece_Out_1_Boolean;
            Unity_Any_float4(_Vector4_e908fc51977e42b4974ad2629668640a_Out_0_Vector4, _Any_c9c3c5b1653548dab30f10682cc18ece_Out_1_Boolean);
            float _Branch_4827914961cf4625854ec1c86e411529_Out_3_Float;
            Unity_Branch_float(_Any_c9c3c5b1653548dab30f10682cc18ece_Out_1_Boolean, 1, 0, _Branch_4827914961cf4625854ec1c86e411529_Out_3_Float);
            float _Multiply_0e5256229ffa44c593ecd5d3582440d5_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_555aa90ade054dada5bc0a4951972cc3_Out_2_Float, _Branch_4827914961cf4625854ec1c86e411529_Out_3_Float, _Multiply_0e5256229ffa44c593ecd5d3582440d5_Out_2_Float);
            float3 _Blend_ff80c604456f4e808be8c87fa3fbfc76_Out_2_Vector3;
            Unity_Blend_Overwrite_float3((_URPSampleBuffer_411780b3c1e34886adc5d7e1c3094ffd_Output_2_Vector4.xyz), _Vector3_8746e484a95444d697f594997323902b_Out_0_Vector3, _Blend_ff80c604456f4e808be8c87fa3fbfc76_Out_2_Vector3, _Multiply_0e5256229ffa44c593ecd5d3582440d5_Out_2_Float);
            surface.BaseColor = _Blend_ff80c604456f4e808be8c87fa3fbfc76_Out_2_Vector3;
            surface.Alpha = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            float3 normalWS = SHADERGRAPH_SAMPLE_SCENE_NORMAL(input.texCoord0.xy);
            float4 tangentWS = float4(0, 1, 0, 0); // We can't access the tangent in screen space
        
        
        
        
            float3 viewDirWS = normalize(input.texCoord1.xyz);
            float linearDepth = LinearEyeDepth(SHADERGRAPH_SAMPLE_SCENE_DEPTH(input.texCoord0.xy), _ZBufferParams);
            float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
            float camearDistance = linearDepth / dot(viewDirWS, cameraForward);
            float3 positionWS = viewDirWS * camearDistance + GetCameraPositionWS();
        
        
            output.WorldSpacePosition = positionWS;
            output.ViewSpacePosition = TransformWorldToView(positionWS);
            output.ScreenPosition = float4(input.texCoord0.xy, 0, 1);
            output.uv0 = input.texCoord0;
            output.NDCPosition = input.texCoord0.xy;
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/Fullscreen/Includes/FullscreenCommon.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/Fullscreen/Includes/FullscreenBlit.hlsl"
        
        ENDHLSL
        }
    }
    CustomEditor "UnityEditor.Rendering.Fullscreen.ShaderGraph.FullscreenShaderGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}