Shader "URPixel/URPixel_MonoDithered"
{
    Properties
    {
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
        _DitherRange1("DitherRange1", Range(0, 0.8)) = 0.07
        _DitherRange2("DitherRange2", Range(0, 0.8)) = 0.16
        _DitherRange3("DitherRange3", Range(0, 0.8)) = 0.2
        _DitherPattern1("DitherPattern1", Range(0, 1.22)) = 0.75
        _DitherPattern2("DitherPattern2", Range(0, 1.22)) = 0.5
        _DitherPattern3("DitherPattern3", Range(0, 1.22)) = 0.4
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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
        
        void Unity_SampleGradientV1_float(Gradient Gradient, float Time, out float4 Out)
        {
            float3 color = Gradient.colors[0].rgb;
            [unroll]
            for (int c = 1; c < Gradient.colorsLength; c++)
            {
                float colorPos = saturate((Time - Gradient.colors[c - 1].w) / (Gradient.colors[c].w - Gradient.colors[c - 1].w)) * step(c, Gradient.colorsLength - 1);
                color = lerp(color, Gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), Gradient.type));
            }
        #ifdef UNITY_COLORSPACE_GAMMA
            color = LinearToSRGB(color);
        #endif
            float alpha = Gradient.alphas[0].x;
            [unroll]
            for (int a = 1; a < Gradient.alphasLength; a++)
            {
                float alphaPos = saturate((Time - Gradient.alphas[a - 1].y) / (Gradient.alphas[a].y - Gradient.alphas[a - 1].y)) * step(a, Gradient.alphasLength - 1);
                alpha = lerp(alpha, Gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), Gradient.type));
            }
            Out = float4(color, alpha);
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
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
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
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
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
        Out = A * B;
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
        
        struct Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float
        {
        float3 WorldSpacePosition;
        float2 NDCPosition;
        };
        
        void SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(Gradient _ShadowGradient, float _RemappedLighting, float _DitherPattern, float _DitherRange, float4 _SampleGradient, float _PreviousLayerRegion, float4 _LayerToOverlap, float _ShadowLayers, Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float IN, out float3 DitheredLayer_2, out float LayerRegion_1)
        {
        Gradient _Property_1529940912174adeb365bf919bb0fa8e_Out_0_Gradient = _ShadowGradient;
        float _Property_df101f40691342998999bd64fe075644_Out_0_Float = _RemappedLighting;
        float _Property_90df58982a18410ab50042fdc0d9a9ae_Out_0_Float = _DitherRange;
        float _Subtract_1ee52772b0b04f3b9abfbda69f20ff22_Out_2_Float;
        Unity_Subtract_float(_Property_df101f40691342998999bd64fe075644_Out_0_Float, _Property_90df58982a18410ab50042fdc0d9a9ae_Out_0_Float, _Subtract_1ee52772b0b04f3b9abfbda69f20ff22_Out_2_Float);
        float4 _SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4;
        Unity_SampleGradientV1_float(_Property_1529940912174adeb365bf919bb0fa8e_Out_0_Gradient, _Subtract_1ee52772b0b04f3b9abfbda69f20ff22_Out_2_Float, _SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4);
        float4 _Property_3b85c366901e4598ad9bfa454ecd0ecf_Out_0_Vector4 = _SampleGradient;
        float _ColorMask_6a3a6bea0be544d3b7be08e7517859b5_Out_3_Float;
        Unity_ColorMask_float((_Property_3b85c366901e4598ad9bfa454ecd0ecf_Out_0_Vector4.xyz), (_SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4.xyz), 0, _ColorMask_6a3a6bea0be544d3b7be08e7517859b5_Out_3_Float, 0);
        float _OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float;
        Unity_OneMinus_float(_ColorMask_6a3a6bea0be544d3b7be08e7517859b5_Out_3_Float, _OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float);
        float _Property_a8844f357b6c434bade798c7e744a8ce_Out_0_Float = _PreviousLayerRegion;
        float _Subtract_927cbc51b96b4d349b58dc386ef90454_Out_2_Float;
        Unity_Subtract_float(_OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float, _Property_a8844f357b6c434bade798c7e744a8ce_Out_0_Float, _Subtract_927cbc51b96b4d349b58dc386ef90454_Out_2_Float);
        float _Property_25d5faea77e4413385a056ca606f6171_Out_0_Float = _DitherPattern;
        Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5;
        _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5.WorldSpacePosition = IN.WorldSpacePosition;
        _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5.NDCPosition = IN.NDCPosition;
        float _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5_OutVector1_1_Float;
        SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(_Property_25d5faea77e4413385a056ca606f6171_Out_0_Float, _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5, _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5_OutVector1_1_Float);
        float _Multiply_8951cc3f9eac48fca452f6cf14f19a1a_Out_2_Float;
        Unity_Multiply_float_float(_Subtract_927cbc51b96b4d349b58dc386ef90454_Out_2_Float, _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5_OutVector1_1_Float, _Multiply_8951cc3f9eac48fca452f6cf14f19a1a_Out_2_Float);
        float4 _Multiply_d0c9158b2e54494ca28941928047c1ae_Out_2_Vector4;
        Unity_Multiply_float4_float4(_SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4, (_Multiply_8951cc3f9eac48fca452f6cf14f19a1a_Out_2_Float.xxxx), _Multiply_d0c9158b2e54494ca28941928047c1ae_Out_2_Vector4);
        float4 _Property_b80ff65484ce4563b1e9aa45587168e8_Out_0_Vector4 = _LayerToOverlap;
        float3 _ReplaceColor_ece827a108f94db1b08a82e21849558a_Out_4_Vector3;
        Unity_ReplaceColor_float((_Multiply_d0c9158b2e54494ca28941928047c1ae_Out_2_Vector4.xyz), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), (_Property_b80ff65484ce4563b1e9aa45587168e8_Out_0_Vector4.xyz), 0, _ReplaceColor_ece827a108f94db1b08a82e21849558a_Out_4_Vector3, 0);
        DitheredLayer_2 = _ReplaceColor_ece827a108f94db1b08a82e21849558a_Out_4_Vector3;
        LayerRegion_1 = _OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float;
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
        {
            Out = lerp(A, B, T);
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
            UnityTexture2D _Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D.tex, _Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D.samplerstate, _Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_R_4_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.r;
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_G_5_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.g;
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_B_6_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.b;
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_A_7_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.a;
            float4 _Property_d391d82866824671a54fe87093ab65cf_Out_0_Vector4 = _DarkColor;
            float4 _Property_fc8e9a8f1c3449fa942a90d888dffaad_Out_0_Vector4 = _BrightColor;
            UnityTexture2D _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.tex, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.samplerstate, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_R_4_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.r;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_G_5_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.g;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_B_6_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.b;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_A_7_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.a;
            Gradient _Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient = NewGradient(1, 2, 2, float4(0.003921569, 0.003921569, 0.003921569, 0.5000076),float4(1, 1, 1, 1),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0), float2(1, 0),float2(1, 1),float2(0, 0),float2(0, 0),float2(0, 0),float2(0, 0),float2(0, 0),float2(0, 0));
            float _Property_19ee55b6ab6a4eac854d6fa6d535d7a0_Out_0_Float = _DarkColorMapPosition;
            float _Property_57dbb17780ea4b36b00d7085efd3ae4c_Out_0_Float = _BrightColorMapPosition;
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
            float _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float;
            Unity_Smoothstep_float(_Property_19ee55b6ab6a4eac854d6fa6d535d7a0_Out_0_Float, _Property_57dbb17780ea4b36b00d7085efd3ae4c_Out_0_Float, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float);
            float _Property_64896b333f59427cabeb38960bb7b3a8_Out_0_Float = _DitherPattern3;
            float _Property_9724b567468c4d9faf9b213e960e109a_Out_0_Float = _DitherRange3;
            float4 _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4;
            Unity_SampleGradientV1_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4);
            float _Property_5da06d006ddb437bbd68fa64e5214a7b_Out_0_Float = _DitherPattern2;
            float _Property_9228cd2b746f46f9aadc49e6af396349_Out_0_Float = _DitherRange2;
            float _Property_33c708b3aa7f4afdb3c17198643023c2_Out_0_Float = _DitherPattern1;
            float _Property_4d0637b7c7de4fbd8014b78ebd961e6b_Out_0_Float = _DitherRange1;
            Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float _DitherLayer_c8134e946bdb4f718b21cf712ef17688;
            _DitherLayer_c8134e946bdb4f718b21cf712ef17688.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherLayer_c8134e946bdb4f718b21cf712ef17688.NDCPosition = IN.NDCPosition;
            float3 _DitherLayer_c8134e946bdb4f718b21cf712ef17688_DitheredLayer_2_Vector3;
            float _DitherLayer_c8134e946bdb4f718b21cf712ef17688_LayerRegion_1_Float;
            SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Property_33c708b3aa7f4afdb3c17198643023c2_Out_0_Float, _Property_4d0637b7c7de4fbd8014b78ebd961e6b_Out_0_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, 0, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, 0, _DitherLayer_c8134e946bdb4f718b21cf712ef17688, _DitherLayer_c8134e946bdb4f718b21cf712ef17688_DitheredLayer_2_Vector3, _DitherLayer_c8134e946bdb4f718b21cf712ef17688_LayerRegion_1_Float);
            Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0;
            _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0.NDCPosition = IN.NDCPosition;
            float3 _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_DitheredLayer_2_Vector3;
            float _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_LayerRegion_1_Float;
            SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Property_5da06d006ddb437bbd68fa64e5214a7b_Out_0_Float, _Property_9228cd2b746f46f9aadc49e6af396349_Out_0_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, _DitherLayer_c8134e946bdb4f718b21cf712ef17688_LayerRegion_1_Float, (float4(_DitherLayer_c8134e946bdb4f718b21cf712ef17688_DitheredLayer_2_Vector3, 1.0)), 0, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_DitheredLayer_2_Vector3, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_LayerRegion_1_Float);
            Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float _DitherLayer_93c038bfcdf0492d8808d578c50e22d6;
            _DitherLayer_93c038bfcdf0492d8808d578c50e22d6.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherLayer_93c038bfcdf0492d8808d578c50e22d6.NDCPosition = IN.NDCPosition;
            float3 _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_DitheredLayer_2_Vector3;
            float _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_LayerRegion_1_Float;
            SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Property_64896b333f59427cabeb38960bb7b3a8_Out_0_Float, _Property_9724b567468c4d9faf9b213e960e109a_Out_0_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_LayerRegion_1_Float, (float4(_DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_DitheredLayer_2_Vector3, 1.0)), 0, _DitherLayer_93c038bfcdf0492d8808d578c50e22d6, _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_DitheredLayer_2_Vector3, _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_LayerRegion_1_Float);
            float3 _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3;
            Unity_Multiply_float3_float3((_SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.xyz), _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_DitheredLayer_2_Vector3, _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3);
            float3 _Lerp_b456082ddfd64e689e2d024db092dc6c_Out_3_Vector3;
            Unity_Lerp_float3((_Property_d391d82866824671a54fe87093ab65cf_Out_0_Vector4.xyz), (_Property_fc8e9a8f1c3449fa942a90d888dffaad_Out_0_Vector4.xyz), _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3, _Lerp_b456082ddfd64e689e2d024db092dc6c_Out_3_Vector3);
            float3 _Multiply_60cd072da53947b4a790c2d33e3e5997_Out_2_Vector3;
            Unity_Multiply_float3_float3((_SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.xyz), _Lerp_b456082ddfd64e689e2d024db092dc6c_Out_3_Vector3, _Multiply_60cd072da53947b4a790c2d33e3e5997_Out_2_Vector3);
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_4532668aca874134968ebc9c4d942ec6;
            _MainLight_4532668aca874134968ebc9c4d942ec6.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_4532668aca874134968ebc9c4d942ec6_Direction_1_Vector3;
            float3 _MainLight_4532668aca874134968ebc9c4d942ec6_Color_2_Vector3;
            float _MainLight_4532668aca874134968ebc9c4d942ec6_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_4532668aca874134968ebc9c4d942ec6, _MainLight_4532668aca874134968ebc9c4d942ec6_Direction_1_Vector3, _MainLight_4532668aca874134968ebc9c4d942ec6_Color_2_Vector3, _MainLight_4532668aca874134968ebc9c4d942ec6_Attenuation_3_Float);
            float3 _Multiply_87c1ce7a85544df8929012e29e8644aa_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_60cd072da53947b4a790c2d33e3e5997_Out_2_Vector3, _MainLight_4532668aca874134968ebc9c4d942ec6_Color_2_Vector3, _Multiply_87c1ce7a85544df8929012e29e8644aa_Out_2_Vector3);
            surface.BaseColor = _Multiply_87c1ce7a85544df8929012e29e8644aa_Out_2_Vector3;
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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
        
        void Unity_SampleGradientV1_float(Gradient Gradient, float Time, out float4 Out)
        {
            float3 color = Gradient.colors[0].rgb;
            [unroll]
            for (int c = 1; c < Gradient.colorsLength; c++)
            {
                float colorPos = saturate((Time - Gradient.colors[c - 1].w) / (Gradient.colors[c].w - Gradient.colors[c - 1].w)) * step(c, Gradient.colorsLength - 1);
                color = lerp(color, Gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), Gradient.type));
            }
        #ifdef UNITY_COLORSPACE_GAMMA
            color = LinearToSRGB(color);
        #endif
            float alpha = Gradient.alphas[0].x;
            [unroll]
            for (int a = 1; a < Gradient.alphasLength; a++)
            {
                float alphaPos = saturate((Time - Gradient.alphas[a - 1].y) / (Gradient.alphas[a].y - Gradient.alphas[a - 1].y)) * step(a, Gradient.alphasLength - 1);
                alpha = lerp(alpha, Gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), Gradient.type));
            }
            Out = float4(color, alpha);
        }
        
        void Unity_Subtract_float(float A, float B, out float Out)
        {
            Out = A - B;
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
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
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
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
        Out = A * B;
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
        
        struct Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float
        {
        float3 WorldSpacePosition;
        float2 NDCPosition;
        };
        
        void SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(Gradient _ShadowGradient, float _RemappedLighting, float _DitherPattern, float _DitherRange, float4 _SampleGradient, float _PreviousLayerRegion, float4 _LayerToOverlap, float _ShadowLayers, Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float IN, out float3 DitheredLayer_2, out float LayerRegion_1)
        {
        Gradient _Property_1529940912174adeb365bf919bb0fa8e_Out_0_Gradient = _ShadowGradient;
        float _Property_df101f40691342998999bd64fe075644_Out_0_Float = _RemappedLighting;
        float _Property_90df58982a18410ab50042fdc0d9a9ae_Out_0_Float = _DitherRange;
        float _Subtract_1ee52772b0b04f3b9abfbda69f20ff22_Out_2_Float;
        Unity_Subtract_float(_Property_df101f40691342998999bd64fe075644_Out_0_Float, _Property_90df58982a18410ab50042fdc0d9a9ae_Out_0_Float, _Subtract_1ee52772b0b04f3b9abfbda69f20ff22_Out_2_Float);
        float4 _SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4;
        Unity_SampleGradientV1_float(_Property_1529940912174adeb365bf919bb0fa8e_Out_0_Gradient, _Subtract_1ee52772b0b04f3b9abfbda69f20ff22_Out_2_Float, _SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4);
        float4 _Property_3b85c366901e4598ad9bfa454ecd0ecf_Out_0_Vector4 = _SampleGradient;
        float _ColorMask_6a3a6bea0be544d3b7be08e7517859b5_Out_3_Float;
        Unity_ColorMask_float((_Property_3b85c366901e4598ad9bfa454ecd0ecf_Out_0_Vector4.xyz), (_SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4.xyz), 0, _ColorMask_6a3a6bea0be544d3b7be08e7517859b5_Out_3_Float, 0);
        float _OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float;
        Unity_OneMinus_float(_ColorMask_6a3a6bea0be544d3b7be08e7517859b5_Out_3_Float, _OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float);
        float _Property_a8844f357b6c434bade798c7e744a8ce_Out_0_Float = _PreviousLayerRegion;
        float _Subtract_927cbc51b96b4d349b58dc386ef90454_Out_2_Float;
        Unity_Subtract_float(_OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float, _Property_a8844f357b6c434bade798c7e744a8ce_Out_0_Float, _Subtract_927cbc51b96b4d349b58dc386ef90454_Out_2_Float);
        float _Property_25d5faea77e4413385a056ca606f6171_Out_0_Float = _DitherPattern;
        Bindings_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5;
        _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5.WorldSpacePosition = IN.WorldSpacePosition;
        _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5.NDCPosition = IN.NDCPosition;
        float _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5_OutVector1_1_Float;
        SG_DitherPattern_41404538928cc854bb9a5c9341d0fba6_float(_Property_25d5faea77e4413385a056ca606f6171_Out_0_Float, _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5, _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5_OutVector1_1_Float);
        float _Multiply_8951cc3f9eac48fca452f6cf14f19a1a_Out_2_Float;
        Unity_Multiply_float_float(_Subtract_927cbc51b96b4d349b58dc386ef90454_Out_2_Float, _DitherPattern_1682ea22cc6541a487e38e0e48e0ffa5_OutVector1_1_Float, _Multiply_8951cc3f9eac48fca452f6cf14f19a1a_Out_2_Float);
        float4 _Multiply_d0c9158b2e54494ca28941928047c1ae_Out_2_Vector4;
        Unity_Multiply_float4_float4(_SampleGradient_6b92360ef4454b6faa1218f7f1409a35_Out_2_Vector4, (_Multiply_8951cc3f9eac48fca452f6cf14f19a1a_Out_2_Float.xxxx), _Multiply_d0c9158b2e54494ca28941928047c1ae_Out_2_Vector4);
        float4 _Property_b80ff65484ce4563b1e9aa45587168e8_Out_0_Vector4 = _LayerToOverlap;
        float3 _ReplaceColor_ece827a108f94db1b08a82e21849558a_Out_4_Vector3;
        Unity_ReplaceColor_float((_Multiply_d0c9158b2e54494ca28941928047c1ae_Out_2_Vector4.xyz), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), (_Property_b80ff65484ce4563b1e9aa45587168e8_Out_0_Vector4.xyz), 0, _ReplaceColor_ece827a108f94db1b08a82e21849558a_Out_4_Vector3, 0);
        DitheredLayer_2 = _ReplaceColor_ece827a108f94db1b08a82e21849558a_Out_4_Vector3;
        LayerRegion_1 = _OneMinus_8a736ee9e43f491e920a8753f5291e1c_Out_1_Float;
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        void Unity_Lerp_float3(float3 A, float3 B, float3 T, out float3 Out)
        {
            Out = lerp(A, B, T);
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
            UnityTexture2D _Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D.tex, _Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D.samplerstate, _Property_50d1ba06786c436781c24401514fa890_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_R_4_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.r;
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_G_5_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.g;
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_B_6_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.b;
            float _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_A_7_Float = _SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.a;
            float4 _Property_d391d82866824671a54fe87093ab65cf_Out_0_Vector4 = _DarkColor;
            float4 _Property_fc8e9a8f1c3449fa942a90d888dffaad_Out_0_Vector4 = _BrightColor;
            UnityTexture2D _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.tex, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.samplerstate, _Property_43544b67bb6648b98af47743162bc434_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_R_4_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.r;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_G_5_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.g;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_B_6_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.b;
            float _SampleTexture2D_cf72802ce3f046109f2aee022557650d_A_7_Float = _SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.a;
            Gradient _Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient = NewGradient(1, 2, 2, float4(0.003921569, 0.003921569, 0.003921569, 0.5000076),float4(1, 1, 1, 1),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0),float4(0, 0, 0, 0), float2(1, 0),float2(1, 1),float2(0, 0),float2(0, 0),float2(0, 0),float2(0, 0),float2(0, 0),float2(0, 0));
            float _Property_19ee55b6ab6a4eac854d6fa6d535d7a0_Out_0_Float = _DarkColorMapPosition;
            float _Property_57dbb17780ea4b36b00d7085efd3ae4c_Out_0_Float = _BrightColorMapPosition;
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
            float _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float;
            Unity_Smoothstep_float(_Property_19ee55b6ab6a4eac854d6fa6d535d7a0_Out_0_Float, _Property_57dbb17780ea4b36b00d7085efd3ae4c_Out_0_Float, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float);
            float _Property_64896b333f59427cabeb38960bb7b3a8_Out_0_Float = _DitherPattern3;
            float _Property_9724b567468c4d9faf9b213e960e109a_Out_0_Float = _DitherRange3;
            float4 _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4;
            Unity_SampleGradientV1_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4);
            float _Property_5da06d006ddb437bbd68fa64e5214a7b_Out_0_Float = _DitherPattern2;
            float _Property_9228cd2b746f46f9aadc49e6af396349_Out_0_Float = _DitherRange2;
            float _Property_33c708b3aa7f4afdb3c17198643023c2_Out_0_Float = _DitherPattern1;
            float _Property_4d0637b7c7de4fbd8014b78ebd961e6b_Out_0_Float = _DitherRange1;
            Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float _DitherLayer_c8134e946bdb4f718b21cf712ef17688;
            _DitherLayer_c8134e946bdb4f718b21cf712ef17688.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherLayer_c8134e946bdb4f718b21cf712ef17688.NDCPosition = IN.NDCPosition;
            float3 _DitherLayer_c8134e946bdb4f718b21cf712ef17688_DitheredLayer_2_Vector3;
            float _DitherLayer_c8134e946bdb4f718b21cf712ef17688_LayerRegion_1_Float;
            SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Property_33c708b3aa7f4afdb3c17198643023c2_Out_0_Float, _Property_4d0637b7c7de4fbd8014b78ebd961e6b_Out_0_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, 0, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, 0, _DitherLayer_c8134e946bdb4f718b21cf712ef17688, _DitherLayer_c8134e946bdb4f718b21cf712ef17688_DitheredLayer_2_Vector3, _DitherLayer_c8134e946bdb4f718b21cf712ef17688_LayerRegion_1_Float);
            Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0;
            _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0.NDCPosition = IN.NDCPosition;
            float3 _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_DitheredLayer_2_Vector3;
            float _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_LayerRegion_1_Float;
            SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Property_5da06d006ddb437bbd68fa64e5214a7b_Out_0_Float, _Property_9228cd2b746f46f9aadc49e6af396349_Out_0_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, _DitherLayer_c8134e946bdb4f718b21cf712ef17688_LayerRegion_1_Float, (float4(_DitherLayer_c8134e946bdb4f718b21cf712ef17688_DitheredLayer_2_Vector3, 1.0)), 0, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_DitheredLayer_2_Vector3, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_LayerRegion_1_Float);
            Bindings_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float _DitherLayer_93c038bfcdf0492d8808d578c50e22d6;
            _DitherLayer_93c038bfcdf0492d8808d578c50e22d6.WorldSpacePosition = IN.WorldSpacePosition;
            _DitherLayer_93c038bfcdf0492d8808d578c50e22d6.NDCPosition = IN.NDCPosition;
            float3 _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_DitheredLayer_2_Vector3;
            float _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_LayerRegion_1_Float;
            SG_DitherLayer_0e47f5571b58c4e4e98275dbd004b5b6_float(_Gradient_c4997c2e23864c34b8632bcd95c14706_Out_0_Gradient, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Property_64896b333f59427cabeb38960bb7b3a8_Out_0_Float, _Property_9724b567468c4d9faf9b213e960e109a_Out_0_Float, _SampleGradient_d373bc5b383a4e709e2e812aa6e91fc4_Out_2_Vector4, _DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_LayerRegion_1_Float, (float4(_DitherLayer_2e0e8924a5624fc79aeb5e15168126e0_DitheredLayer_2_Vector3, 1.0)), 0, _DitherLayer_93c038bfcdf0492d8808d578c50e22d6, _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_DitheredLayer_2_Vector3, _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_LayerRegion_1_Float);
            float3 _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3;
            Unity_Multiply_float3_float3((_SampleTexture2D_cf72802ce3f046109f2aee022557650d_RGBA_0_Vector4.xyz), _DitherLayer_93c038bfcdf0492d8808d578c50e22d6_DitheredLayer_2_Vector3, _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3);
            float3 _Lerp_b456082ddfd64e689e2d024db092dc6c_Out_3_Vector3;
            Unity_Lerp_float3((_Property_d391d82866824671a54fe87093ab65cf_Out_0_Vector4.xyz), (_Property_fc8e9a8f1c3449fa942a90d888dffaad_Out_0_Vector4.xyz), _Multiply_3fa9d33174c744fcbea5a61e2459266b_Out_2_Vector3, _Lerp_b456082ddfd64e689e2d024db092dc6c_Out_3_Vector3);
            float3 _Multiply_60cd072da53947b4a790c2d33e3e5997_Out_2_Vector3;
            Unity_Multiply_float3_float3((_SampleTexture2D_6cd9504dd06b4c5d87a2b068d5c645ec_RGBA_0_Vector4.xyz), _Lerp_b456082ddfd64e689e2d024db092dc6c_Out_3_Vector3, _Multiply_60cd072da53947b4a790c2d33e3e5997_Out_2_Vector3);
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_4532668aca874134968ebc9c4d942ec6;
            _MainLight_4532668aca874134968ebc9c4d942ec6.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_4532668aca874134968ebc9c4d942ec6_Direction_1_Vector3;
            float3 _MainLight_4532668aca874134968ebc9c4d942ec6_Color_2_Vector3;
            float _MainLight_4532668aca874134968ebc9c4d942ec6_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_4532668aca874134968ebc9c4d942ec6, _MainLight_4532668aca874134968ebc9c4d942ec6_Direction_1_Vector3, _MainLight_4532668aca874134968ebc9c4d942ec6_Color_2_Vector3, _MainLight_4532668aca874134968ebc9c4d942ec6_Attenuation_3_Float);
            float3 _Multiply_87c1ce7a85544df8929012e29e8644aa_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_60cd072da53947b4a790c2d33e3e5997_Out_2_Vector3, _MainLight_4532668aca874134968ebc9c4d942ec6_Color_2_Vector3, _Multiply_87c1ce7a85544df8929012e29e8644aa_Out_2_Vector3);
            surface.BaseColor = _Multiply_87c1ce7a85544df8929012e29e8644aa_Out_2_Vector3;
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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
        float _DitherRange3;
        float _DitherRange1;
        float _DitherPattern2;
        float _DitherPattern3;
        float _DitherRange2;
        float4 _DarkColor;
        float4 _BrightColor;
        float _BrightColorMapPosition;
        float _DarkColorMapPosition;
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