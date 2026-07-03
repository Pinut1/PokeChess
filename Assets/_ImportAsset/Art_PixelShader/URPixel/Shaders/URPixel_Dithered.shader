Shader "URPixel/URPixel_Dithered"
{
    Properties
    {
        _LayersAmount("LayersAmount", Float) = 1
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
        _TextureBrightness("TextureBrightness", Range(-10, 10)) = 1
        _DitherRange1("DitherRange1", Float) = 0.15
        _DitherPattern1("DitherPattern1", Range(0, 1.22)) = 0.75
        _DitherRange2("DitherRange2", Float) = 0.15
        _DitherPattern2("DitherPattern2", Range(0, 1.22)) = 0.53
        _DarkColor("DarkColor", Color) = (0, 0, 0, 1)
        _DarkColorMapPosition("DarkColorMapPosition", Float) = 0
        _BrightColor("BrightColor", Color) = (1, 1, 1, 1)
        _BrightColorMapPosition("BrightColorMapPosition", Float) = 1
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType" = "Unlit"
            "Queue"="Geometry"
            "DisableBatching"="False"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="UniversalUnlitSubTarget"
        }
        Pass
        {
            Name "Universal Forward"
            Tags
            {
                // LightMode: <None>
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma shader_feature _ _SAMPLE_GI
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ _SOFT_SHADOWS
        
        
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_UNLIT
        #define _FOG_FRAGMENT 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 texCoord0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpacePosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 texCoord0 : INTERP0;
             float3 positionWS : INTERP1;
             float3 normalWS : INTERP2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.texCoord0.xyzw = input.texCoord0;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.texCoord0 = input.texCoord0.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        #include "Assets/URPixel/Shaders/GetLighting.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        struct Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float
        {
        float3 WorldSpacePosition;
        };
        
        void SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float IN, out float3 Direction_1, out float3 Color_2, out float Attenuation_3)
        {
        float3 _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Direction_1_Vector3;
        float3 _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Color_2_Vector3;
        float _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Attenuation_3_Float;
        GetLight_float(IN.WorldSpacePosition, _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Direction_1_Vector3, _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Color_2_Vector3, _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Attenuation_3_Float);
        Direction_1 = _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Direction_1_Vector3;
        Color_2 = _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Color_2_Vector3;
        Attenuation_3 = _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Attenuation_3_Float;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_ColorMask_float(float3 In, float3 MaskColor, float Range, out float Out, float Fuzziness)
        {
            float Distance = distance(MaskColor, In);
            Out = saturate(1 - (Distance - Range) / max(Fuzziness, 1e-5));
        }
        
        void Unity_OneMinus_float(float In, out float Out)
        {
            Out = 1 - In;
        }
        
        void Unity_Modulo_float(float A, float B, out float Out)
        {
            Out = fmod(A, B);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Divide_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A / B;
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Dither_float(float In, float4 ScreenPosition, out float Out)
        {
            float2 uv = ScreenPosition.xy * _ScreenParams.xy;
            float DITHER_THRESHOLDS[16] =
            {
                1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
            };
            uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
            Out = In - DITHER_THRESHOLDS[index];
        }
        
        void Unity_Step_float(float Edge, float In, out float Out)
        {
            Out = step(Edge, In);
        }
        
        struct Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float
        {
        float3 WorldSpacePosition;
        float2 NDCPosition;
        };
        
        void SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(float _DitherPattern, Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float IN, out float OutVector1_1)
        {
        float _Property_c3bf577e146c402aaef7ba81008410e0_Out_0_Float = _DitherPattern;
        float4 _ScreenPosition_77c8005f9acf4559bb8d237d6bf65137_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
        float _Float_bedc61aecd6a4fd7910ebd732af4dc8a_Out_0_Float = 0.02;
        float _Split_bbb05a065aab4aa4a23df433801a3091_R_1_Float = _WorldSpaceCameraPos[0];
        float _Split_bbb05a065aab4aa4a23df433801a3091_G_2_Float = _WorldSpaceCameraPos[1];
        float _Split_bbb05a065aab4aa4a23df433801a3091_B_3_Float = _WorldSpaceCameraPos[2];
        float _Split_bbb05a065aab4aa4a23df433801a3091_A_4_Float = 0;
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_R_1_Float = IN.WorldSpacePosition[0];
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_G_2_Float = IN.WorldSpacePosition[1];
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_B_3_Float = IN.WorldSpacePosition[2];
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_A_4_Float = 0;
        float _Subtract_c37c9a05432d47928366d7d9be48004d_Out_2_Float;
        Unity_Subtract_float(_Split_bbb05a065aab4aa4a23df433801a3091_G_2_Float, _Split_091ebbd2cc34440e9c39832b0efcc5ff_G_2_Float, _Subtract_c37c9a05432d47928366d7d9be48004d_Out_2_Float);
        float _Multiply_49f8a57864204b2da10290f11c9c51db_Out_2_Float;
        Unity_Multiply_float_float(_Subtract_c37c9a05432d47928366d7d9be48004d_Out_2_Float, 100, _Multiply_49f8a57864204b2da10290f11c9c51db_Out_2_Float);
        float _Round_f81e703700fa49b8a036d0d737cc6894_Out_1_Float;
        Unity_Round_float(_Multiply_49f8a57864204b2da10290f11c9c51db_Out_2_Float, _Round_f81e703700fa49b8a036d0d737cc6894_Out_1_Float);
        float _Multiply_349f7595bc704651b4d829d9b38a20e3_Out_2_Float;
        Unity_Multiply_float_float(_Float_bedc61aecd6a4fd7910ebd732af4dc8a_Out_0_Float, 2, _Multiply_349f7595bc704651b4d829d9b38a20e3_Out_2_Float);
        float _Multiply_aa6553ea6b9243de90040294c7abc689_Out_2_Float;
        Unity_Multiply_float_float(_Multiply_349f7595bc704651b4d829d9b38a20e3_Out_2_Float, 100, _Multiply_aa6553ea6b9243de90040294c7abc689_Out_2_Float);
        float _Round_38c954fe20ee487587d7b3093afcf0f0_Out_1_Float;
        Unity_Round_float(_Multiply_aa6553ea6b9243de90040294c7abc689_Out_2_Float, _Round_38c954fe20ee487587d7b3093afcf0f0_Out_1_Float);
        float _Modulo_407ed3c0a79c43d6a2fb74f58c1fb284_Out_2_Float;
        Unity_Modulo_float(_Round_f81e703700fa49b8a036d0d737cc6894_Out_1_Float, _Round_38c954fe20ee487587d7b3093afcf0f0_Out_1_Float, _Modulo_407ed3c0a79c43d6a2fb74f58c1fb284_Out_2_Float);
        float4 _Combine_de676f1c10f248d28a06677f9c2685f8_RGBA_4_Vector4;
        float3 _Combine_de676f1c10f248d28a06677f9c2685f8_RGB_5_Vector3;
        float2 _Combine_de676f1c10f248d28a06677f9c2685f8_RG_6_Vector2;
        Unity_Combine_float(0, _Modulo_407ed3c0a79c43d6a2fb74f58c1fb284_Out_2_Float, 0, 0, _Combine_de676f1c10f248d28a06677f9c2685f8_RGBA_4_Vector4, _Combine_de676f1c10f248d28a06677f9c2685f8_RGB_5_Vector3, _Combine_de676f1c10f248d28a06677f9c2685f8_RG_6_Vector2);
        float4 _Multiply_d7abba35d9e94917ba83f8d8606be576_Out_2_Vector4;
        Unity_Multiply_float4_float4((_Float_bedc61aecd6a4fd7910ebd732af4dc8a_Out_0_Float.xxxx), _Combine_de676f1c10f248d28a06677f9c2685f8_RGBA_4_Vector4, _Multiply_d7abba35d9e94917ba83f8d8606be576_Out_2_Vector4);
        float4 _Divide_c2a994a1d9364d97a8855bc29ebaeec0_Out_2_Vector4;
        Unity_Divide_float4(_Multiply_d7abba35d9e94917ba83f8d8606be576_Out_2_Vector4, (_ScreenParams.y.xxxx), _Divide_c2a994a1d9364d97a8855bc29ebaeec0_Out_2_Vector4);
        float4 _Add_0ac2beacd15542e1a26b7aebe50e118b_Out_2_Vector4;
        Unity_Add_float4(_ScreenPosition_77c8005f9acf4559bb8d237d6bf65137_Out_0_Vector4, _Divide_c2a994a1d9364d97a8855bc29ebaeec0_Out_2_Vector4, _Add_0ac2beacd15542e1a26b7aebe50e118b_Out_2_Vector4);
        float _Dither_a2800519ef734ef69227f1dcece0acd3_Out_2_Float;
        Unity_Dither_float(_Property_c3bf577e146c402aaef7ba81008410e0_Out_0_Float, _Add_0ac2beacd15542e1a26b7aebe50e118b_Out_2_Vector4, _Dither_a2800519ef734ef69227f1dcece0acd3_Out_2_Float);
        float _Step_f77508b06db9457bb31b9ece68428d43_Out_2_Float;
        Unity_Step_float(0.25, _Dither_a2800519ef734ef69227f1dcece0acd3_Out_2_Float, _Step_f77508b06db9457bb31b9ece68428d43_Out_2_Float);
        OutVector1_1 = _Step_f77508b06db9457bb31b9ece68428d43_Out_2_Float;
        }
        
        void Unity_ReplaceColor_float(float3 In, float3 From, float3 To, float Range, out float3 Out, float Fuzziness)
        {
            float Distance = distance(From, In);
            Out = lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 1e-5f)));
        }
        
        void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Property_19ae949a297a4b91ae1d4a615139e9a3_Out_0_Float = _TextureBrightness;
            UnityTexture2D _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.tex, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.samplerstate, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_R_4_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.r;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_G_5_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.g;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_B_6_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.b;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_A_7_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.a;
            float4 _Multiply_09f90c98adb2458c921e2859b3fd3a1f_Out_2_Vector4;
            Unity_Multiply_float4_float4((_Property_19ae949a297a4b91ae1d4a615139e9a3_Out_0_Float.xxxx), _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4, _Multiply_09f90c98adb2458c921e2859b3fd3a1f_Out_2_Vector4);
            float4 _Property_c8361f7e21cb470ebb1d23ec0d62d2b6_Out_0_Vector4 = _DarkColor;
            float4 _Property_79f96d9227484f64b26f9b7ecfb06b79_Out_0_Vector4 = _BrightColor;
            float _Property_63e17aaab793480391abb256b61d9b53_Out_0_Float = _LayersAmount;
            float _Property_132d690ee4f24c80a72c0061e011d610_Out_0_Float = _DarkColorMapPosition;
            float _Property_2546faab70c1457aab34e0fdc6b083e1_Out_0_Float = _BrightColorMapPosition;
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_25215c3e02914ee4a504f723e0c614b9;
            _MainLight_25215c3e02914ee4a504f723e0c614b9.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_25215c3e02914ee4a504f723e0c614b9_Direction_1_Vector3;
            float3 _MainLight_25215c3e02914ee4a504f723e0c614b9_Color_2_Vector3;
            float _MainLight_25215c3e02914ee4a504f723e0c614b9_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_25215c3e02914ee4a504f723e0c614b9, _MainLight_25215c3e02914ee4a504f723e0c614b9_Direction_1_Vector3, _MainLight_25215c3e02914ee4a504f723e0c614b9_Color_2_Vector3, _MainLight_25215c3e02914ee4a504f723e0c614b9_Attenuation_3_Float);
            float _DotProduct_5bf3a31e2bf946498452180448644ec5_Out_2_Float;
            Unity_DotProduct_float3(_MainLight_25215c3e02914ee4a504f723e0c614b9_Direction_1_Vector3, IN.WorldSpaceNormal, _DotProduct_5bf3a31e2bf946498452180448644ec5_Out_2_Float);
            float _Multiply_ca8d62b390a94591a2d18b5f74bdb304_Out_2_Float;
            Unity_Multiply_float_float(_DotProduct_5bf3a31e2bf946498452180448644ec5_Out_2_Float, _MainLight_25215c3e02914ee4a504f723e0c614b9_Attenuation_3_Float, _Multiply_ca8d62b390a94591a2d18b5f74bdb304_Out_2_Float);
            float _Add_d753fe499a0f4eca886c7ee284f80401_Out_2_Float;
            Unity_Add_float(_Multiply_ca8d62b390a94591a2d18b5f74bdb304_Out_2_Float, 1, _Add_d753fe499a0f4eca886c7ee284f80401_Out_2_Float);
            float _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float;
            Unity_Multiply_float_float(_Add_d753fe499a0f4eca886c7ee284f80401_Out_2_Float, 0.5, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float);
            float _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float;
            Unity_Smoothstep_float(_Property_132d690ee4f24c80a72c0061e011d610_Out_0_Float, _Property_2546faab70c1457aab34e0fdc6b083e1_Out_0_Float, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float);
            float _Multiply_42dec1083ddf4e05974b58595c1f2d3f_Out_2_Float;
            Unity_Multiply_float_float(_Property_63e17aaab793480391abb256b61d9b53_Out_0_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float, _Multiply_42dec1083ddf4e05974b58595c1f2d3f_Out_2_Float);
            float _Property_8e122d2b450440f79ea2be4458a946c0_Out_0_Float = _DitherRange1;
            float _Subtract_83967171278b45c3b93dabea8a76aad2_Out_2_Float;
            Unity_Subtract_float(_Multiply_42dec1083ddf4e05974b58595c1f2d3f_Out_2_Float, _Property_8e122d2b450440f79ea2be4458a946c0_Out_0_Float, _Subtract_83967171278b45c3b93dabea8a76aad2_Out_2_Float);
            float _Round_487bbacceea14f8ca930b2059b67492d_Out_1_Float;
            Unity_Round_float(_Subtract_83967171278b45c3b93dabea8a76aad2_Out_2_Float, _Round_487bbacceea14f8ca930b2059b67492d_Out_1_Float);
            float _Property_c47d527517ad4faea31eb57a256dbe37_Out_0_Float = _LayersAmount;
            float _Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float;
            Unity_Divide_float(_Round_487bbacceea14f8ca930b2059b67492d_Out_1_Float, _Property_c47d527517ad4faea31eb57a256dbe37_Out_0_Float, _Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float);
            float _Property_3b1f4f0ab8a3417fbff7af3cd9f91887_Out_0_Float = _LayersAmount;
            float _Multiply_9427ae317211429e99e500972c12cd86_Out_2_Float;
            Unity_Multiply_float_float(_Property_3b1f4f0ab8a3417fbff7af3cd9f91887_Out_0_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float, _Multiply_9427ae317211429e99e500972c12cd86_Out_2_Float);
            float _Property_099101e5eeda4a1dbd21c21a64680f0a_Out_0_Float = _DitherRange1;
            float _Property_3811e959d72841258bbf9477b927169e_Out_0_Float = _DitherRange2;
            float _Add_4878f7426f734ef9aa584be8fd4bf408_Out_2_Float;
            Unity_Add_float(_Property_099101e5eeda4a1dbd21c21a64680f0a_Out_0_Float, _Property_3811e959d72841258bbf9477b927169e_Out_0_Float, _Add_4878f7426f734ef9aa584be8fd4bf408_Out_2_Float);
            float _Subtract_1434194bb27049ae8e8d9cd5148900e1_Out_2_Float;
            Unity_Subtract_float(_Multiply_9427ae317211429e99e500972c12cd86_Out_2_Float, _Add_4878f7426f734ef9aa584be8fd4bf408_Out_2_Float, _Subtract_1434194bb27049ae8e8d9cd5148900e1_Out_2_Float);
            float _Round_dff331dac6724894b48d0c9a36f87b52_Out_1_Float;
            Unity_Round_float(_Subtract_1434194bb27049ae8e8d9cd5148900e1_Out_2_Float, _Round_dff331dac6724894b48d0c9a36f87b52_Out_1_Float);
            float _Property_d189a25865f74164a4f22dd13b118d4c_Out_0_Float = _LayersAmount;
            float _Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float;
            Unity_Divide_float(_Round_dff331dac6724894b48d0c9a36f87b52_Out_1_Float, _Property_d189a25865f74164a4f22dd13b118d4c_Out_0_Float, _Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float);
            float _ColorMask_6efe179123824c9b9dc93dd593e87568_Out_3_Float;
            Unity_ColorMask_float((_Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float.xxx), (_Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float.xxx), 0, _ColorMask_6efe179123824c9b9dc93dd593e87568_Out_3_Float, 0);
            float _OneMinus_6285405451df4c229af285cea487f67d_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_6efe179123824c9b9dc93dd593e87568_Out_3_Float, _OneMinus_6285405451df4c229af285cea487f67d_Out_1_Float);
            float _Property_48f3ec13869f402e9884c0a6cd5d9d40_Out_0_Float = _DitherPattern2;
            Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float _DitherPattern_c4ce0f37df174842b27dd20f52780ed8;
            _DitherPattern_c4ce0f37df174842b27dd20f52780ed8.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherPattern_c4ce0f37df174842b27dd20f52780ed8.NDCPosition = IN.NDCPosition;
            float _DitherPattern_c4ce0f37df174842b27dd20f52780ed8_OutVector1_1_Float;
            SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(_Property_48f3ec13869f402e9884c0a6cd5d9d40_Out_0_Float, _DitherPattern_c4ce0f37df174842b27dd20f52780ed8, _DitherPattern_c4ce0f37df174842b27dd20f52780ed8_OutVector1_1_Float);
            float _Multiply_ccd127c0abd54778b17a53d4054666d7_Out_2_Float;
            Unity_Multiply_float_float(_OneMinus_6285405451df4c229af285cea487f67d_Out_1_Float, _DitherPattern_c4ce0f37df174842b27dd20f52780ed8_OutVector1_1_Float, _Multiply_ccd127c0abd54778b17a53d4054666d7_Out_2_Float);
            float _Add_8a61643c00f1406fa9d4323db07a68a2_Out_2_Float;
            Unity_Add_float(0.06, _Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float, _Add_8a61643c00f1406fa9d4323db07a68a2_Out_2_Float);
            float _Multiply_ec2fbfeaa6de4fcb8de778504e6c98ac_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_ccd127c0abd54778b17a53d4054666d7_Out_2_Float, _Add_8a61643c00f1406fa9d4323db07a68a2_Out_2_Float, _Multiply_ec2fbfeaa6de4fcb8de778504e6c98ac_Out_2_Float);
            float _Property_5c1df849815d4fba878f6d7d029480d0_Out_0_Float = _LayersAmount;
            float _Multiply_177af9271d0a4297a271458543668986_Out_2_Float;
            Unity_Multiply_float_float(_Property_5c1df849815d4fba878f6d7d029480d0_Out_0_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float, _Multiply_177af9271d0a4297a271458543668986_Out_2_Float);
            float _Round_5783a0506ca447af8386883a2780851d_Out_1_Float;
            Unity_Round_float(_Multiply_177af9271d0a4297a271458543668986_Out_2_Float, _Round_5783a0506ca447af8386883a2780851d_Out_1_Float);
            float _Property_5bd6fcfad0854e4e874790f00053a41b_Out_0_Float = _LayersAmount;
            float _Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float;
            Unity_Divide_float(_Round_5783a0506ca447af8386883a2780851d_Out_1_Float, _Property_5bd6fcfad0854e4e874790f00053a41b_Out_0_Float, _Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float);
            float _ColorMask_eee99f4b7ac34f83ba04ff42da411469_Out_3_Float;
            Unity_ColorMask_float((_Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float.xxx), (_Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float.xxx), 0, _ColorMask_eee99f4b7ac34f83ba04ff42da411469_Out_3_Float, 0);
            float _OneMinus_7a973e6077fb43e1a5ef3ea7c4554ef1_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_eee99f4b7ac34f83ba04ff42da411469_Out_3_Float, _OneMinus_7a973e6077fb43e1a5ef3ea7c4554ef1_Out_1_Float);
            float _Property_e558418163f744978d878530975bb130_Out_0_Float = _DitherPattern1;
            Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4;
            _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4.NDCPosition = IN.NDCPosition;
            float _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4_OutVector1_1_Float;
            SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(_Property_e558418163f744978d878530975bb130_Out_0_Float, _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4, _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4_OutVector1_1_Float);
            float _Multiply_42738fe4c7bc48949196314ce0d554d2_Out_2_Float;
            Unity_Multiply_float_float(_OneMinus_7a973e6077fb43e1a5ef3ea7c4554ef1_Out_1_Float, _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4_OutVector1_1_Float, _Multiply_42738fe4c7bc48949196314ce0d554d2_Out_2_Float);
            float _Add_a142646d95aa444dbbd14f13a336ca30_Out_2_Float;
            Unity_Add_float(0.06, _Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float, _Add_a142646d95aa444dbbd14f13a336ca30_Out_2_Float);
            float _Multiply_2d86986fc330416ba32c51e64121c305_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_42738fe4c7bc48949196314ce0d554d2_Out_2_Float, _Add_a142646d95aa444dbbd14f13a336ca30_Out_2_Float, _Multiply_2d86986fc330416ba32c51e64121c305_Out_2_Float);
            float3 _ReplaceColor_0d58a1316ba74c42a53f1035ce2fe10c_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_2d86986fc330416ba32c51e64121c305_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), (_Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float.xxx), 0.06, _ReplaceColor_0d58a1316ba74c42a53f1035ce2fe10c_Out_4_Vector3, 0);
            float3 _ReplaceColor_6091281694e54b7196680c15b6593591_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_ec2fbfeaa6de4fcb8de778504e6c98ac_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), _ReplaceColor_0d58a1316ba74c42a53f1035ce2fe10c_Out_4_Vector3, 0.01, _ReplaceColor_6091281694e54b7196680c15b6593591_Out_4_Vector3, 0);
            float3 _Lerp_e32fa53b900544ce8e35f62a4ed3bd5d_Out_3_Vector3;
            Unity_Lerp_float3((_Property_c8361f7e21cb470ebb1d23ec0d62d2b6_Out_0_Vector4.xyz), (_Property_79f96d9227484f64b26f9b7ecfb06b79_Out_0_Vector4.xyz), _ReplaceColor_6091281694e54b7196680c15b6593591_Out_4_Vector3, _Lerp_e32fa53b900544ce8e35f62a4ed3bd5d_Out_3_Vector3);
            float3 _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3;
            Unity_Multiply_float3_float3((_Multiply_09f90c98adb2458c921e2859b3fd3a1f_Out_2_Vector4.xyz), _Lerp_e32fa53b900544ce8e35f62a4ed3bd5d_Out_3_Vector3, _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3);
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_051af6d000ef4bb8b83cc0b19b0296e1;
            _MainLight_051af6d000ef4bb8b83cc0b19b0296e1.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Direction_1_Vector3;
            float3 _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Color_2_Vector3;
            float _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_051af6d000ef4bb8b83cc0b19b0296e1, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Direction_1_Vector3, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Color_2_Vector3, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Attenuation_3_Float);
            float3 _Multiply_eb0176f19421456c9f5c744ea2435cd6_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Color_2_Vector3, _Multiply_eb0176f19421456c9f5c744ea2435cd6_Out_2_Vector3);
            surface.BaseColor = _Multiply_eb0176f19421456c9f5c744ea2435cd6_Out_2_Vector3;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
            output.uv0 = input.texCoord0;
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        ColorMask R
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/DepthNormalsOnlyPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
        
        // Render State
        Cull Back
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define VARYINGS_NEED_NORMAL_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SHADOWCASTER
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float3 normalWS : INTERP0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 4.5
        #pragma exclude_renderers gles gles3 glcore
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma instancing_options renderinglayer
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
        #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
        #pragma multi_compile _ _SOFT_SHADOWS
        
        
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_GBUFFER
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 texCoord0;
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpacePosition;
             float2 NDCPosition;
             float2 PixelPosition;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP0;
            #endif
             float4 texCoord0 : INTERP1;
             float3 positionWS : INTERP2;
             float3 normalWS : INTERP3;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.texCoord0.xyzw = input.texCoord0;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.texCoord0 = input.texCoord0.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        #include "Assets/URPixel/Shaders/GetLighting.hlsl"
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        struct Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float
        {
        float3 WorldSpacePosition;
        };
        
        void SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float IN, out float3 Direction_1, out float3 Color_2, out float Attenuation_3)
        {
        float3 _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Direction_1_Vector3;
        float3 _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Color_2_Vector3;
        float _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Attenuation_3_Float;
        GetLight_float(IN.WorldSpacePosition, _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Direction_1_Vector3, _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Color_2_Vector3, _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Attenuation_3_Float);
        Direction_1 = _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Direction_1_Vector3;
        Color_2 = _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Color_2_Vector3;
        Attenuation_3 = _GetLightCustomFunction_86b52da1d86747a1a421aaaa081eff05_Attenuation_3_Float;
        }
        
        void Unity_DotProduct_float3(float3 A, float3 B, out float Out)
        {
            Out = dot(A, B);
        }
        
        void Unity_Multiply_float_float(float A, float B, out float Out)
        {
            Out = A * B;
        }
        
        void Unity_Add_float(float A, float B, out float Out)
        {
            Out = A + B;
        }
        
        void Unity_Smoothstep_float(float Edge1, float Edge2, float In, out float Out)
        {
            Out = smoothstep(Edge1, Edge2, In);
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
        }
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
        }
        
        void Unity_ColorMask_float(float3 In, float3 MaskColor, float Range, out float Out, float Fuzziness)
        {
            float Distance = distance(MaskColor, In);
            Out = saturate(1 - (Distance - Range) / max(Fuzziness, 1e-5));
        }
        
        void Unity_OneMinus_float(float In, out float Out)
        {
            Out = 1 - In;
        }
        
        void Unity_Modulo_float(float A, float B, out float Out)
        {
            Out = fmod(A, B);
        }
        
        void Unity_Combine_float(float R, float G, float B, float A, out float4 RGBA, out float3 RGB, out float2 RG)
        {
            RGBA = float4(R, G, B, A);
            RGB = float3(R, G, B);
            RG = float2(R, G);
        }
        
        void Unity_Divide_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A / B;
        }
        
        void Unity_Add_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A + B;
        }
        
        void Unity_Dither_float(float In, float4 ScreenPosition, out float Out)
        {
            float2 uv = ScreenPosition.xy * _ScreenParams.xy;
            float DITHER_THRESHOLDS[16] =
            {
                1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
                13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
                4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
                16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
            };
            uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
            Out = In - DITHER_THRESHOLDS[index];
        }
        
        void Unity_Step_float(float Edge, float In, out float Out)
        {
            Out = step(Edge, In);
        }
        
        struct Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float
        {
        float3 WorldSpacePosition;
        float2 NDCPosition;
        };
        
        void SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(float _DitherPattern, Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float IN, out float OutVector1_1)
        {
        float _Property_c3bf577e146c402aaef7ba81008410e0_Out_0_Float = _DitherPattern;
        float4 _ScreenPosition_77c8005f9acf4559bb8d237d6bf65137_Out_0_Vector4 = float4(IN.NDCPosition.xy, 0, 0);
        float _Float_bedc61aecd6a4fd7910ebd732af4dc8a_Out_0_Float = 0.02;
        float _Split_bbb05a065aab4aa4a23df433801a3091_R_1_Float = _WorldSpaceCameraPos[0];
        float _Split_bbb05a065aab4aa4a23df433801a3091_G_2_Float = _WorldSpaceCameraPos[1];
        float _Split_bbb05a065aab4aa4a23df433801a3091_B_3_Float = _WorldSpaceCameraPos[2];
        float _Split_bbb05a065aab4aa4a23df433801a3091_A_4_Float = 0;
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_R_1_Float = IN.WorldSpacePosition[0];
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_G_2_Float = IN.WorldSpacePosition[1];
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_B_3_Float = IN.WorldSpacePosition[2];
        float _Split_091ebbd2cc34440e9c39832b0efcc5ff_A_4_Float = 0;
        float _Subtract_c37c9a05432d47928366d7d9be48004d_Out_2_Float;
        Unity_Subtract_float(_Split_bbb05a065aab4aa4a23df433801a3091_G_2_Float, _Split_091ebbd2cc34440e9c39832b0efcc5ff_G_2_Float, _Subtract_c37c9a05432d47928366d7d9be48004d_Out_2_Float);
        float _Multiply_49f8a57864204b2da10290f11c9c51db_Out_2_Float;
        Unity_Multiply_float_float(_Subtract_c37c9a05432d47928366d7d9be48004d_Out_2_Float, 100, _Multiply_49f8a57864204b2da10290f11c9c51db_Out_2_Float);
        float _Round_f81e703700fa49b8a036d0d737cc6894_Out_1_Float;
        Unity_Round_float(_Multiply_49f8a57864204b2da10290f11c9c51db_Out_2_Float, _Round_f81e703700fa49b8a036d0d737cc6894_Out_1_Float);
        float _Multiply_349f7595bc704651b4d829d9b38a20e3_Out_2_Float;
        Unity_Multiply_float_float(_Float_bedc61aecd6a4fd7910ebd732af4dc8a_Out_0_Float, 2, _Multiply_349f7595bc704651b4d829d9b38a20e3_Out_2_Float);
        float _Multiply_aa6553ea6b9243de90040294c7abc689_Out_2_Float;
        Unity_Multiply_float_float(_Multiply_349f7595bc704651b4d829d9b38a20e3_Out_2_Float, 100, _Multiply_aa6553ea6b9243de90040294c7abc689_Out_2_Float);
        float _Round_38c954fe20ee487587d7b3093afcf0f0_Out_1_Float;
        Unity_Round_float(_Multiply_aa6553ea6b9243de90040294c7abc689_Out_2_Float, _Round_38c954fe20ee487587d7b3093afcf0f0_Out_1_Float);
        float _Modulo_407ed3c0a79c43d6a2fb74f58c1fb284_Out_2_Float;
        Unity_Modulo_float(_Round_f81e703700fa49b8a036d0d737cc6894_Out_1_Float, _Round_38c954fe20ee487587d7b3093afcf0f0_Out_1_Float, _Modulo_407ed3c0a79c43d6a2fb74f58c1fb284_Out_2_Float);
        float4 _Combine_de676f1c10f248d28a06677f9c2685f8_RGBA_4_Vector4;
        float3 _Combine_de676f1c10f248d28a06677f9c2685f8_RGB_5_Vector3;
        float2 _Combine_de676f1c10f248d28a06677f9c2685f8_RG_6_Vector2;
        Unity_Combine_float(0, _Modulo_407ed3c0a79c43d6a2fb74f58c1fb284_Out_2_Float, 0, 0, _Combine_de676f1c10f248d28a06677f9c2685f8_RGBA_4_Vector4, _Combine_de676f1c10f248d28a06677f9c2685f8_RGB_5_Vector3, _Combine_de676f1c10f248d28a06677f9c2685f8_RG_6_Vector2);
        float4 _Multiply_d7abba35d9e94917ba83f8d8606be576_Out_2_Vector4;
        Unity_Multiply_float4_float4((_Float_bedc61aecd6a4fd7910ebd732af4dc8a_Out_0_Float.xxxx), _Combine_de676f1c10f248d28a06677f9c2685f8_RGBA_4_Vector4, _Multiply_d7abba35d9e94917ba83f8d8606be576_Out_2_Vector4);
        float4 _Divide_c2a994a1d9364d97a8855bc29ebaeec0_Out_2_Vector4;
        Unity_Divide_float4(_Multiply_d7abba35d9e94917ba83f8d8606be576_Out_2_Vector4, (_ScreenParams.y.xxxx), _Divide_c2a994a1d9364d97a8855bc29ebaeec0_Out_2_Vector4);
        float4 _Add_0ac2beacd15542e1a26b7aebe50e118b_Out_2_Vector4;
        Unity_Add_float4(_ScreenPosition_77c8005f9acf4559bb8d237d6bf65137_Out_0_Vector4, _Divide_c2a994a1d9364d97a8855bc29ebaeec0_Out_2_Vector4, _Add_0ac2beacd15542e1a26b7aebe50e118b_Out_2_Vector4);
        float _Dither_a2800519ef734ef69227f1dcece0acd3_Out_2_Float;
        Unity_Dither_float(_Property_c3bf577e146c402aaef7ba81008410e0_Out_0_Float, _Add_0ac2beacd15542e1a26b7aebe50e118b_Out_2_Vector4, _Dither_a2800519ef734ef69227f1dcece0acd3_Out_2_Float);
        float _Step_f77508b06db9457bb31b9ece68428d43_Out_2_Float;
        Unity_Step_float(0.25, _Dither_a2800519ef734ef69227f1dcece0acd3_Out_2_Float, _Step_f77508b06db9457bb31b9ece68428d43_Out_2_Float);
        OutVector1_1 = _Step_f77508b06db9457bb31b9ece68428d43_Out_2_Float;
        }
        
        void Unity_ReplaceColor_float(float3 In, float3 From, float3 To, float Range, out float3 Out, float Fuzziness)
        {
            float Distance = distance(From, In);
            Out = lerp(To, In, saturate((Distance - Range) / max(Fuzziness, 1e-5f)));
        }
        
        void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
        {
            Out = lerp(A, B, T);
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float _Property_19ae949a297a4b91ae1d4a615139e9a3_Out_0_Float = _TextureBrightness;
            UnityTexture2D _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.tex, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.samplerstate, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_R_4_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.r;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_G_5_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.g;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_B_6_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.b;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_A_7_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.a;
            float4 _Multiply_09f90c98adb2458c921e2859b3fd3a1f_Out_2_Vector4;
            Unity_Multiply_float4_float4((_Property_19ae949a297a4b91ae1d4a615139e9a3_Out_0_Float.xxxx), _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4, _Multiply_09f90c98adb2458c921e2859b3fd3a1f_Out_2_Vector4);
            float4 _Property_c8361f7e21cb470ebb1d23ec0d62d2b6_Out_0_Vector4 = _DarkColor;
            float4 _Property_79f96d9227484f64b26f9b7ecfb06b79_Out_0_Vector4 = _BrightColor;
            float _Property_63e17aaab793480391abb256b61d9b53_Out_0_Float = _LayersAmount;
            float _Property_132d690ee4f24c80a72c0061e011d610_Out_0_Float = _DarkColorMapPosition;
            float _Property_2546faab70c1457aab34e0fdc6b083e1_Out_0_Float = _BrightColorMapPosition;
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_25215c3e02914ee4a504f723e0c614b9;
            _MainLight_25215c3e02914ee4a504f723e0c614b9.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_25215c3e02914ee4a504f723e0c614b9_Direction_1_Vector3;
            float3 _MainLight_25215c3e02914ee4a504f723e0c614b9_Color_2_Vector3;
            float _MainLight_25215c3e02914ee4a504f723e0c614b9_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_25215c3e02914ee4a504f723e0c614b9, _MainLight_25215c3e02914ee4a504f723e0c614b9_Direction_1_Vector3, _MainLight_25215c3e02914ee4a504f723e0c614b9_Color_2_Vector3, _MainLight_25215c3e02914ee4a504f723e0c614b9_Attenuation_3_Float);
            float _DotProduct_5bf3a31e2bf946498452180448644ec5_Out_2_Float;
            Unity_DotProduct_float3(_MainLight_25215c3e02914ee4a504f723e0c614b9_Direction_1_Vector3, IN.WorldSpaceNormal, _DotProduct_5bf3a31e2bf946498452180448644ec5_Out_2_Float);
            float _Multiply_ca8d62b390a94591a2d18b5f74bdb304_Out_2_Float;
            Unity_Multiply_float_float(_DotProduct_5bf3a31e2bf946498452180448644ec5_Out_2_Float, _MainLight_25215c3e02914ee4a504f723e0c614b9_Attenuation_3_Float, _Multiply_ca8d62b390a94591a2d18b5f74bdb304_Out_2_Float);
            float _Add_d753fe499a0f4eca886c7ee284f80401_Out_2_Float;
            Unity_Add_float(_Multiply_ca8d62b390a94591a2d18b5f74bdb304_Out_2_Float, 1, _Add_d753fe499a0f4eca886c7ee284f80401_Out_2_Float);
            float _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float;
            Unity_Multiply_float_float(_Add_d753fe499a0f4eca886c7ee284f80401_Out_2_Float, 0.5, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float);
            float _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float;
            Unity_Smoothstep_float(_Property_132d690ee4f24c80a72c0061e011d610_Out_0_Float, _Property_2546faab70c1457aab34e0fdc6b083e1_Out_0_Float, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float);
            float _Multiply_42dec1083ddf4e05974b58595c1f2d3f_Out_2_Float;
            Unity_Multiply_float_float(_Property_63e17aaab793480391abb256b61d9b53_Out_0_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float, _Multiply_42dec1083ddf4e05974b58595c1f2d3f_Out_2_Float);
            float _Property_8e122d2b450440f79ea2be4458a946c0_Out_0_Float = _DitherRange1;
            float _Subtract_83967171278b45c3b93dabea8a76aad2_Out_2_Float;
            Unity_Subtract_float(_Multiply_42dec1083ddf4e05974b58595c1f2d3f_Out_2_Float, _Property_8e122d2b450440f79ea2be4458a946c0_Out_0_Float, _Subtract_83967171278b45c3b93dabea8a76aad2_Out_2_Float);
            float _Round_487bbacceea14f8ca930b2059b67492d_Out_1_Float;
            Unity_Round_float(_Subtract_83967171278b45c3b93dabea8a76aad2_Out_2_Float, _Round_487bbacceea14f8ca930b2059b67492d_Out_1_Float);
            float _Property_c47d527517ad4faea31eb57a256dbe37_Out_0_Float = _LayersAmount;
            float _Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float;
            Unity_Divide_float(_Round_487bbacceea14f8ca930b2059b67492d_Out_1_Float, _Property_c47d527517ad4faea31eb57a256dbe37_Out_0_Float, _Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float);
            float _Property_3b1f4f0ab8a3417fbff7af3cd9f91887_Out_0_Float = _LayersAmount;
            float _Multiply_9427ae317211429e99e500972c12cd86_Out_2_Float;
            Unity_Multiply_float_float(_Property_3b1f4f0ab8a3417fbff7af3cd9f91887_Out_0_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float, _Multiply_9427ae317211429e99e500972c12cd86_Out_2_Float);
            float _Property_099101e5eeda4a1dbd21c21a64680f0a_Out_0_Float = _DitherRange1;
            float _Property_3811e959d72841258bbf9477b927169e_Out_0_Float = _DitherRange2;
            float _Add_4878f7426f734ef9aa584be8fd4bf408_Out_2_Float;
            Unity_Add_float(_Property_099101e5eeda4a1dbd21c21a64680f0a_Out_0_Float, _Property_3811e959d72841258bbf9477b927169e_Out_0_Float, _Add_4878f7426f734ef9aa584be8fd4bf408_Out_2_Float);
            float _Subtract_1434194bb27049ae8e8d9cd5148900e1_Out_2_Float;
            Unity_Subtract_float(_Multiply_9427ae317211429e99e500972c12cd86_Out_2_Float, _Add_4878f7426f734ef9aa584be8fd4bf408_Out_2_Float, _Subtract_1434194bb27049ae8e8d9cd5148900e1_Out_2_Float);
            float _Round_dff331dac6724894b48d0c9a36f87b52_Out_1_Float;
            Unity_Round_float(_Subtract_1434194bb27049ae8e8d9cd5148900e1_Out_2_Float, _Round_dff331dac6724894b48d0c9a36f87b52_Out_1_Float);
            float _Property_d189a25865f74164a4f22dd13b118d4c_Out_0_Float = _LayersAmount;
            float _Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float;
            Unity_Divide_float(_Round_dff331dac6724894b48d0c9a36f87b52_Out_1_Float, _Property_d189a25865f74164a4f22dd13b118d4c_Out_0_Float, _Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float);
            float _ColorMask_6efe179123824c9b9dc93dd593e87568_Out_3_Float;
            Unity_ColorMask_float((_Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float.xxx), (_Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float.xxx), 0, _ColorMask_6efe179123824c9b9dc93dd593e87568_Out_3_Float, 0);
            float _OneMinus_6285405451df4c229af285cea487f67d_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_6efe179123824c9b9dc93dd593e87568_Out_3_Float, _OneMinus_6285405451df4c229af285cea487f67d_Out_1_Float);
            float _Property_48f3ec13869f402e9884c0a6cd5d9d40_Out_0_Float = _DitherPattern2;
            Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float _DitherPattern_c4ce0f37df174842b27dd20f52780ed8;
            _DitherPattern_c4ce0f37df174842b27dd20f52780ed8.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherPattern_c4ce0f37df174842b27dd20f52780ed8.NDCPosition = IN.NDCPosition;
            float _DitherPattern_c4ce0f37df174842b27dd20f52780ed8_OutVector1_1_Float;
            SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(_Property_48f3ec13869f402e9884c0a6cd5d9d40_Out_0_Float, _DitherPattern_c4ce0f37df174842b27dd20f52780ed8, _DitherPattern_c4ce0f37df174842b27dd20f52780ed8_OutVector1_1_Float);
            float _Multiply_ccd127c0abd54778b17a53d4054666d7_Out_2_Float;
            Unity_Multiply_float_float(_OneMinus_6285405451df4c229af285cea487f67d_Out_1_Float, _DitherPattern_c4ce0f37df174842b27dd20f52780ed8_OutVector1_1_Float, _Multiply_ccd127c0abd54778b17a53d4054666d7_Out_2_Float);
            float _Add_8a61643c00f1406fa9d4323db07a68a2_Out_2_Float;
            Unity_Add_float(0.06, _Divide_30d9595d2dd044e1bfc35f84bc0f01d5_Out_2_Float, _Add_8a61643c00f1406fa9d4323db07a68a2_Out_2_Float);
            float _Multiply_ec2fbfeaa6de4fcb8de778504e6c98ac_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_ccd127c0abd54778b17a53d4054666d7_Out_2_Float, _Add_8a61643c00f1406fa9d4323db07a68a2_Out_2_Float, _Multiply_ec2fbfeaa6de4fcb8de778504e6c98ac_Out_2_Float);
            float _Property_5c1df849815d4fba878f6d7d029480d0_Out_0_Float = _LayersAmount;
            float _Multiply_177af9271d0a4297a271458543668986_Out_2_Float;
            Unity_Multiply_float_float(_Property_5c1df849815d4fba878f6d7d029480d0_Out_0_Float, _Smoothstep_424032463fab451cb26bfbf7ad25b081_Out_3_Float, _Multiply_177af9271d0a4297a271458543668986_Out_2_Float);
            float _Round_5783a0506ca447af8386883a2780851d_Out_1_Float;
            Unity_Round_float(_Multiply_177af9271d0a4297a271458543668986_Out_2_Float, _Round_5783a0506ca447af8386883a2780851d_Out_1_Float);
            float _Property_5bd6fcfad0854e4e874790f00053a41b_Out_0_Float = _LayersAmount;
            float _Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float;
            Unity_Divide_float(_Round_5783a0506ca447af8386883a2780851d_Out_1_Float, _Property_5bd6fcfad0854e4e874790f00053a41b_Out_0_Float, _Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float);
            float _ColorMask_eee99f4b7ac34f83ba04ff42da411469_Out_3_Float;
            Unity_ColorMask_float((_Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float.xxx), (_Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float.xxx), 0, _ColorMask_eee99f4b7ac34f83ba04ff42da411469_Out_3_Float, 0);
            float _OneMinus_7a973e6077fb43e1a5ef3ea7c4554ef1_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_eee99f4b7ac34f83ba04ff42da411469_Out_3_Float, _OneMinus_7a973e6077fb43e1a5ef3ea7c4554ef1_Out_1_Float);
            float _Property_e558418163f744978d878530975bb130_Out_0_Float = _DitherPattern1;
            Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4;
            _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4.NDCPosition = IN.NDCPosition;
            float _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4_OutVector1_1_Float;
            SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(_Property_e558418163f744978d878530975bb130_Out_0_Float, _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4, _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4_OutVector1_1_Float);
            float _Multiply_42738fe4c7bc48949196314ce0d554d2_Out_2_Float;
            Unity_Multiply_float_float(_OneMinus_7a973e6077fb43e1a5ef3ea7c4554ef1_Out_1_Float, _DitherPattern_b320f254cee345c09b8b2f42fbdd5ca4_OutVector1_1_Float, _Multiply_42738fe4c7bc48949196314ce0d554d2_Out_2_Float);
            float _Add_a142646d95aa444dbbd14f13a336ca30_Out_2_Float;
            Unity_Add_float(0.06, _Divide_d3dc30f7a1b042639c26e2106fab65e2_Out_2_Float, _Add_a142646d95aa444dbbd14f13a336ca30_Out_2_Float);
            float _Multiply_2d86986fc330416ba32c51e64121c305_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_42738fe4c7bc48949196314ce0d554d2_Out_2_Float, _Add_a142646d95aa444dbbd14f13a336ca30_Out_2_Float, _Multiply_2d86986fc330416ba32c51e64121c305_Out_2_Float);
            float3 _ReplaceColor_0d58a1316ba74c42a53f1035ce2fe10c_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_2d86986fc330416ba32c51e64121c305_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), (_Divide_cd85622a55174eb78f7c533f5a3980e6_Out_2_Float.xxx), 0.06, _ReplaceColor_0d58a1316ba74c42a53f1035ce2fe10c_Out_4_Vector3, 0);
            float3 _ReplaceColor_6091281694e54b7196680c15b6593591_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_ec2fbfeaa6de4fcb8de778504e6c98ac_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), _ReplaceColor_0d58a1316ba74c42a53f1035ce2fe10c_Out_4_Vector3, 0.01, _ReplaceColor_6091281694e54b7196680c15b6593591_Out_4_Vector3, 0);
            float3 _Lerp_e32fa53b900544ce8e35f62a4ed3bd5d_Out_3_Vector3;
            Unity_Lerp_float3((_Property_c8361f7e21cb470ebb1d23ec0d62d2b6_Out_0_Vector4.xyz), (_Property_79f96d9227484f64b26f9b7ecfb06b79_Out_0_Vector4.xyz), _ReplaceColor_6091281694e54b7196680c15b6593591_Out_4_Vector3, _Lerp_e32fa53b900544ce8e35f62a4ed3bd5d_Out_3_Vector3);
            float3 _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3;
            Unity_Multiply_float3_float3((_Multiply_09f90c98adb2458c921e2859b3fd3a1f_Out_2_Vector4.xyz), _Lerp_e32fa53b900544ce8e35f62a4ed3bd5d_Out_3_Vector3, _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3);
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_051af6d000ef4bb8b83cc0b19b0296e1;
            _MainLight_051af6d000ef4bb8b83cc0b19b0296e1.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Direction_1_Vector3;
            float3 _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Color_2_Vector3;
            float _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_051af6d000ef4bb8b83cc0b19b0296e1, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Direction_1_Vector3, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Color_2_Vector3, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Attenuation_3_Float);
            float3 _Multiply_eb0176f19421456c9f5c744ea2435cd6_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3, _MainLight_051af6d000ef4bb8b83cc0b19b0296e1_Color_2_Vector3, _Multiply_eb0176f19421456c9f5c744ea2435cd6_Out_2_Vector3);
            surface.BaseColor = _Multiply_eb0176f19421456c9f5c744ea2435cd6_Out_2_Vector3;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
        
            output.WorldSpacePosition = input.positionWS;
        
            #if UNITY_UV_STARTS_AT_TOP
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x < 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #else
            output.PixelPosition = float2(input.positionCS.x, (_ProjectionParams.x > 0) ? (_ScaledScreenParams.y - input.positionCS.y) : input.positionCS.y);
            #endif
        
            output.NDCPosition = output.PixelPosition.xy / _ScaledScreenParams.xy;
            output.NDCPosition.y = 1.0f - output.NDCPosition.y;
        
            output.uv0 = input.texCoord0;
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitGBufferPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENESELECTIONPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull Back
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 2.0
        #pragma vertex vert
        #pragma fragment frag
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define SCENEPICKINGPASS 1
        #define ALPHA_CLIP_THRESHOLD 1
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_TexelSize;
        float _DitherPattern1;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _LayersAmount;
        float _TextureBrightness;
        float _DarkColorMapPosition;
        float _BrightColorMapPosition;
        CBUFFER_END
        
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        #ifdef HAVE_VFX_MODIFICATION
        #define VFX_SRP_ATTRIBUTES Attributes
        #define VFX_SRP_VARYINGS Varyings
        #define VFX_SRP_SURFACE_INPUTS SurfaceDescriptionInputs
        #endif
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
        #ifdef HAVE_VFX_MODIFICATION
        #if VFX_USE_GRAPH_VALUES
            uint instanceActiveIndex = asuint(UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceActiveIndex));
            /* WARNING: $splice Could not find named fragment 'VFXLoadGraphValues' */
        #endif
            /* WARNING: $splice Could not find named fragment 'VFXSetFragInputs' */
        
        #endif
        
            
        
        
        
        
        
        
            #if UNITY_UV_STARTS_AT_TOP
            #else
            #endif
        
        
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
        
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"
        
        // --------------------------------------------------
        // Visual Effect Vertex Invocations
        #ifdef HAVE_VFX_MODIFICATION
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/VisualEffectVertex.hlsl"
        #endif
        
        ENDHLSL
        }
    }
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    CustomEditorForRenderPipeline "UnityEditor.ShaderGraphUnlitGUI" "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
    FallBack "Hidden/Shader Graph/FallbackError"
}