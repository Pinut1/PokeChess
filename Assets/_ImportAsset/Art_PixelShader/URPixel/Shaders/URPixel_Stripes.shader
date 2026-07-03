Shader "URPixel/URPixel_Stripes"
{
    Properties
    {
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
        _LayersAmount("LayersAmount", Float) = 3
        _TextureBrightness("TextureBrightness", Range(1, 10)) = 1
        _VerticalStripesRange("VerticalStripesRange", Float) = 0.21
        _HorizontalStripesRange("HorizontalStripesRange", Float) = 0.55
        _DarkColor("DarkColor", Color) = (0, 0, 0, 1)
        _BrightColor("BrightColor", Color) = (1, 1, 1, 1)
        _DarkColorMapPosition("DarkColorMapPosition", Float) = 0
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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
        
        void Unity_Floor_float(float In, out float Out)
        {
            Out = floor(In);
        }
        
        void Unity_Modulo_float(float A, float B, out float Out)
        {
            Out = fmod(A, B);
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
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
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
            float _Property_fe13a226c6684627bfda59a66f9d5fee_Out_0_Float = _TextureBrightness;
            UnityTexture2D _Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D.tex, _Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D.samplerstate, _Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_R_4_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.r;
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_G_5_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.g;
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_B_6_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.b;
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_A_7_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.a;
            float4 _Multiply_4e73b4f5d19f47c382a851655c4a480c_Out_2_Vector4;
            Unity_Multiply_float4_float4((_Property_fe13a226c6684627bfda59a66f9d5fee_Out_0_Float.xxxx), _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4, _Multiply_4e73b4f5d19f47c382a851655c4a480c_Out_2_Vector4);
            float4 _Property_fa106bfea48c4a3eac21b86ae23e432d_Out_0_Vector4 = _DarkColor;
            float4 _Property_cefafd2c7973496ab23efbe5144943ca_Out_0_Vector4 = _BrightColor;
            float4 _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4 = float4(IN.PixelPosition.xy, 0, 0);
            float _Split_12ac1e46829b4f288b14158a75b1b99b_R_1_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[0];
            float _Split_12ac1e46829b4f288b14158a75b1b99b_G_2_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[1];
            float _Split_12ac1e46829b4f288b14158a75b1b99b_B_3_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[2];
            float _Split_12ac1e46829b4f288b14158a75b1b99b_A_4_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[3];
            float _Floor_f0c4f380fc364cdcacc693c1065fa8d3_Out_1_Float;
            Unity_Floor_float(_Split_12ac1e46829b4f288b14158a75b1b99b_G_2_Float, _Floor_f0c4f380fc364cdcacc693c1065fa8d3_Out_1_Float);
            float _Modulo_4e47847728ce4c018035d5f914f354e2_Out_2_Float;
            Unity_Modulo_float(_Floor_f0c4f380fc364cdcacc693c1065fa8d3_Out_1_Float, 2, _Modulo_4e47847728ce4c018035d5f914f354e2_Out_2_Float);
            float _Property_53cd545efe5448d08b3a99bf56d198f0_Out_0_Float = _LayersAmount;
            float _Property_a21e50f4e07441a5a83a371cc0cba505_Out_0_Float = _DarkColorMapPosition;
            float _Property_51281513a25c49e2961b5b1625d28b55_Out_0_Float = _BrightColorMapPosition;
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
            Unity_Smoothstep_float(_Property_a21e50f4e07441a5a83a371cc0cba505_Out_0_Float, _Property_51281513a25c49e2961b5b1625d28b55_Out_0_Float, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float);
            float _Multiply_c193527448414cc0b22e1d07a6545d35_Out_2_Float;
            Unity_Multiply_float_float(_Property_53cd545efe5448d08b3a99bf56d198f0_Out_0_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Multiply_c193527448414cc0b22e1d07a6545d35_Out_2_Float);
            float _Round_e76414e7b2d34292a9e08da732eb58b7_Out_1_Float;
            Unity_Round_float(_Multiply_c193527448414cc0b22e1d07a6545d35_Out_2_Float, _Round_e76414e7b2d34292a9e08da732eb58b7_Out_1_Float);
            float _Property_997512f09b5345d1b65aa436ab2e4d59_Out_0_Float = _LayersAmount;
            float _Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float;
            Unity_Divide_float(_Round_e76414e7b2d34292a9e08da732eb58b7_Out_1_Float, _Property_997512f09b5345d1b65aa436ab2e4d59_Out_0_Float, _Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float);
            float _Property_3f9520aa98eb4938806a546553503e45_Out_0_Float = _LayersAmount;
            float _Multiply_cb3ff70aa2da4e99bf05755a1ceba031_Out_2_Float;
            Unity_Multiply_float_float(_Property_3f9520aa98eb4938806a546553503e45_Out_0_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Multiply_cb3ff70aa2da4e99bf05755a1ceba031_Out_2_Float);
            float _Property_fc723ac65e5f458a9d830c7ccf7afedb_Out_0_Float = _HorizontalStripesRange;
            float _Subtract_060ee11d33904c8bbc605ce9ff02956f_Out_2_Float;
            Unity_Subtract_float(_Multiply_cb3ff70aa2da4e99bf05755a1ceba031_Out_2_Float, _Property_fc723ac65e5f458a9d830c7ccf7afedb_Out_0_Float, _Subtract_060ee11d33904c8bbc605ce9ff02956f_Out_2_Float);
            float _Round_725b1862da1c49eba5a41ec67ac79a26_Out_1_Float;
            Unity_Round_float(_Subtract_060ee11d33904c8bbc605ce9ff02956f_Out_2_Float, _Round_725b1862da1c49eba5a41ec67ac79a26_Out_1_Float);
            float _Property_1c2fb6e616b64bbb97ede80a9145ee24_Out_0_Float = _LayersAmount;
            float _Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float;
            Unity_Divide_float(_Round_725b1862da1c49eba5a41ec67ac79a26_Out_1_Float, _Property_1c2fb6e616b64bbb97ede80a9145ee24_Out_0_Float, _Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float);
            float _ColorMask_a6db5310cd3247d29cdae4ace96705a0_Out_3_Float;
            Unity_ColorMask_float((_Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float.xxx), (_Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float.xxx), 0, _ColorMask_a6db5310cd3247d29cdae4ace96705a0_Out_3_Float, 0);
            float _OneMinus_c3cf815ce9a04eb6a296dbb8eec49782_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_a6db5310cd3247d29cdae4ace96705a0_Out_3_Float, _OneMinus_c3cf815ce9a04eb6a296dbb8eec49782_Out_1_Float);
            float _Multiply_6fdf06600f244960b189a8ab099917d6_Out_2_Float;
            Unity_Multiply_float_float(_Modulo_4e47847728ce4c018035d5f914f354e2_Out_2_Float, _OneMinus_c3cf815ce9a04eb6a296dbb8eec49782_Out_1_Float, _Multiply_6fdf06600f244960b189a8ab099917d6_Out_2_Float);
            float _Add_f3f60d69261f4a06a077088f230d3a72_Out_2_Float;
            Unity_Add_float(_Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float, 0.06, _Add_f3f60d69261f4a06a077088f230d3a72_Out_2_Float);
            float _Multiply_ce75f4668eb5431589846575d2587e9c_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_6fdf06600f244960b189a8ab099917d6_Out_2_Float, _Add_f3f60d69261f4a06a077088f230d3a72_Out_2_Float, _Multiply_ce75f4668eb5431589846575d2587e9c_Out_2_Float);
            float _Property_14e6751b377147fa9a0c85072c9ff5fe_Out_0_Float = _LayersAmount;
            float _Multiply_35a66009693c458182a46e93bc94c6fa_Out_2_Float;
            Unity_Multiply_float_float(_Property_14e6751b377147fa9a0c85072c9ff5fe_Out_0_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Multiply_35a66009693c458182a46e93bc94c6fa_Out_2_Float);
            float _Property_8686d2f6b8ee4a5782c84789670cdf79_Out_0_Float = _VerticalStripesRange;
            float _Subtract_cdf3a23b76bf4eaeb972b07da10567df_Out_2_Float;
            Unity_Subtract_float(_Multiply_35a66009693c458182a46e93bc94c6fa_Out_2_Float, _Property_8686d2f6b8ee4a5782c84789670cdf79_Out_0_Float, _Subtract_cdf3a23b76bf4eaeb972b07da10567df_Out_2_Float);
            float _Round_4c874c1633bc46d294cca798de82e024_Out_1_Float;
            Unity_Round_float(_Subtract_cdf3a23b76bf4eaeb972b07da10567df_Out_2_Float, _Round_4c874c1633bc46d294cca798de82e024_Out_1_Float);
            float _Property_0b4fcd9b7edf4c6cbc4130c399c32444_Out_0_Float = _LayersAmount;
            float _Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float;
            Unity_Divide_float(_Round_4c874c1633bc46d294cca798de82e024_Out_1_Float, _Property_0b4fcd9b7edf4c6cbc4130c399c32444_Out_0_Float, _Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float);
            float _ColorMask_096fe49695574ed29557e9acd18a332d_Out_3_Float;
            Unity_ColorMask_float((_Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float.xxx), (_Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float.xxx), 0, _ColorMask_096fe49695574ed29557e9acd18a332d_Out_3_Float, 0);
            float _OneMinus_c3d48c5e3b494ce1b7595d2a750d2dca_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_096fe49695574ed29557e9acd18a332d_Out_3_Float, _OneMinus_c3d48c5e3b494ce1b7595d2a750d2dca_Out_1_Float);
            float _Floor_71d773377d5a424899c19166ed91aa67_Out_1_Float;
            Unity_Floor_float(_Split_12ac1e46829b4f288b14158a75b1b99b_R_1_Float, _Floor_71d773377d5a424899c19166ed91aa67_Out_1_Float);
            float _Modulo_19758df038064b40978bf0f1e6ddd4f8_Out_2_Float;
            Unity_Modulo_float(_Floor_71d773377d5a424899c19166ed91aa67_Out_1_Float, 2, _Modulo_19758df038064b40978bf0f1e6ddd4f8_Out_2_Float);
            float _Multiply_ca8be42c9143470d82604d4768f26ff9_Out_2_Float;
            Unity_Multiply_float_float(_OneMinus_c3d48c5e3b494ce1b7595d2a750d2dca_Out_1_Float, _Modulo_19758df038064b40978bf0f1e6ddd4f8_Out_2_Float, _Multiply_ca8be42c9143470d82604d4768f26ff9_Out_2_Float);
            float _Add_d91b6efab0584cfb86de3c39c21bcd5c_Out_2_Float;
            Unity_Add_float(_Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float, 0.06, _Add_d91b6efab0584cfb86de3c39c21bcd5c_Out_2_Float);
            float _Multiply_41f6afed8bff4ef38926c844396b8db2_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_ca8be42c9143470d82604d4768f26ff9_Out_2_Float, _Add_d91b6efab0584cfb86de3c39c21bcd5c_Out_2_Float, _Multiply_41f6afed8bff4ef38926c844396b8db2_Out_2_Float);
            float3 _ReplaceColor_f30f8e0e6621490ab164ecce7c6c90f3_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_41f6afed8bff4ef38926c844396b8db2_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), (_Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float.xxx), 0.01, _ReplaceColor_f30f8e0e6621490ab164ecce7c6c90f3_Out_4_Vector3, 0);
            float3 _ReplaceColor_8d9ebb7af2134841a28b91021cff5f68_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_ce75f4668eb5431589846575d2587e9c_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), _ReplaceColor_f30f8e0e6621490ab164ecce7c6c90f3_Out_4_Vector3, 0.1, _ReplaceColor_8d9ebb7af2134841a28b91021cff5f68_Out_4_Vector3, 0);
            float3 _Lerp_472f20b9e01f4808b2cee5eceeab0bf6_Out_3_Vector3;
            Unity_Lerp_float3((_Property_fa106bfea48c4a3eac21b86ae23e432d_Out_0_Vector4.xyz), (_Property_cefafd2c7973496ab23efbe5144943ca_Out_0_Vector4.xyz), _ReplaceColor_8d9ebb7af2134841a28b91021cff5f68_Out_4_Vector3, _Lerp_472f20b9e01f4808b2cee5eceeab0bf6_Out_3_Vector3);
            float3 _Multiply_3ac8c2e3671d4fb9aa70298e83f094b5_Out_2_Vector3;
            Unity_Multiply_float3_float3((_Multiply_4e73b4f5d19f47c382a851655c4a480c_Out_2_Vector4.xyz), _Lerp_472f20b9e01f4808b2cee5eceeab0bf6_Out_3_Vector3, _Multiply_3ac8c2e3671d4fb9aa70298e83f094b5_Out_2_Vector3);
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_e49afc6a96a84845917a6c020660bde5;
            _MainLight_e49afc6a96a84845917a6c020660bde5.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_e49afc6a96a84845917a6c020660bde5_Direction_1_Vector3;
            float3 _MainLight_e49afc6a96a84845917a6c020660bde5_Color_2_Vector3;
            float _MainLight_e49afc6a96a84845917a6c020660bde5_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_e49afc6a96a84845917a6c020660bde5, _MainLight_e49afc6a96a84845917a6c020660bde5_Direction_1_Vector3, _MainLight_e49afc6a96a84845917a6c020660bde5_Color_2_Vector3, _MainLight_e49afc6a96a84845917a6c020660bde5_Attenuation_3_Float);
            float3 _Multiply_86dc82a772b74e59a0e53a654f6f5cde_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_3ac8c2e3671d4fb9aa70298e83f094b5_Out_2_Vector3, _MainLight_e49afc6a96a84845917a6c020660bde5_Color_2_Vector3, _Multiply_86dc82a772b74e59a0e53a654f6f5cde_Out_2_Vector3);
            surface.BaseColor = _Multiply_86dc82a772b74e59a0e53a654f6f5cde_Out_2_Vector3;
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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
        
        void Unity_Floor_float(float In, out float Out)
        {
            Out = floor(In);
        }
        
        void Unity_Modulo_float(float A, float B, out float Out)
        {
            Out = fmod(A, B);
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
        
        void Unity_Round_float(float In, out float Out)
        {
            Out = round(In);
        }
        
        void Unity_Divide_float(float A, float B, out float Out)
        {
            Out = A / B;
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
            float _Property_fe13a226c6684627bfda59a66f9d5fee_Out_0_Float = _TextureBrightness;
            UnityTexture2D _Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D = UnityBuildTexture2DStructNoScale(_MainTex);
            float4 _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4 = SAMPLE_TEXTURE2D(_Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D.tex, _Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D.samplerstate, _Property_4d596394b6f649c99df754818696fd75_Out_0_Texture2D.GetTransformedUV(IN.uv0.xy) );
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_R_4_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.r;
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_G_5_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.g;
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_B_6_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.b;
            float _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_A_7_Float = _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4.a;
            float4 _Multiply_4e73b4f5d19f47c382a851655c4a480c_Out_2_Vector4;
            Unity_Multiply_float4_float4((_Property_fe13a226c6684627bfda59a66f9d5fee_Out_0_Float.xxxx), _SampleTexture2D_5883fad4cd6d465fa0883f86a8eda8d0_RGBA_0_Vector4, _Multiply_4e73b4f5d19f47c382a851655c4a480c_Out_2_Vector4);
            float4 _Property_fa106bfea48c4a3eac21b86ae23e432d_Out_0_Vector4 = _DarkColor;
            float4 _Property_cefafd2c7973496ab23efbe5144943ca_Out_0_Vector4 = _BrightColor;
            float4 _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4 = float4(IN.PixelPosition.xy, 0, 0);
            float _Split_12ac1e46829b4f288b14158a75b1b99b_R_1_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[0];
            float _Split_12ac1e46829b4f288b14158a75b1b99b_G_2_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[1];
            float _Split_12ac1e46829b4f288b14158a75b1b99b_B_3_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[2];
            float _Split_12ac1e46829b4f288b14158a75b1b99b_A_4_Float = _ScreenPosition_97e4e56a348347f1b651c09e1417b160_Out_0_Vector4[3];
            float _Floor_f0c4f380fc364cdcacc693c1065fa8d3_Out_1_Float;
            Unity_Floor_float(_Split_12ac1e46829b4f288b14158a75b1b99b_G_2_Float, _Floor_f0c4f380fc364cdcacc693c1065fa8d3_Out_1_Float);
            float _Modulo_4e47847728ce4c018035d5f914f354e2_Out_2_Float;
            Unity_Modulo_float(_Floor_f0c4f380fc364cdcacc693c1065fa8d3_Out_1_Float, 2, _Modulo_4e47847728ce4c018035d5f914f354e2_Out_2_Float);
            float _Property_53cd545efe5448d08b3a99bf56d198f0_Out_0_Float = _LayersAmount;
            float _Property_a21e50f4e07441a5a83a371cc0cba505_Out_0_Float = _DarkColorMapPosition;
            float _Property_51281513a25c49e2961b5b1625d28b55_Out_0_Float = _BrightColorMapPosition;
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
            Unity_Smoothstep_float(_Property_a21e50f4e07441a5a83a371cc0cba505_Out_0_Float, _Property_51281513a25c49e2961b5b1625d28b55_Out_0_Float, _Multiply_43eef2274d6e4027b8087a7383dda2b3_Out_2_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float);
            float _Multiply_c193527448414cc0b22e1d07a6545d35_Out_2_Float;
            Unity_Multiply_float_float(_Property_53cd545efe5448d08b3a99bf56d198f0_Out_0_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Multiply_c193527448414cc0b22e1d07a6545d35_Out_2_Float);
            float _Round_e76414e7b2d34292a9e08da732eb58b7_Out_1_Float;
            Unity_Round_float(_Multiply_c193527448414cc0b22e1d07a6545d35_Out_2_Float, _Round_e76414e7b2d34292a9e08da732eb58b7_Out_1_Float);
            float _Property_997512f09b5345d1b65aa436ab2e4d59_Out_0_Float = _LayersAmount;
            float _Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float;
            Unity_Divide_float(_Round_e76414e7b2d34292a9e08da732eb58b7_Out_1_Float, _Property_997512f09b5345d1b65aa436ab2e4d59_Out_0_Float, _Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float);
            float _Property_3f9520aa98eb4938806a546553503e45_Out_0_Float = _LayersAmount;
            float _Multiply_cb3ff70aa2da4e99bf05755a1ceba031_Out_2_Float;
            Unity_Multiply_float_float(_Property_3f9520aa98eb4938806a546553503e45_Out_0_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Multiply_cb3ff70aa2da4e99bf05755a1ceba031_Out_2_Float);
            float _Property_fc723ac65e5f458a9d830c7ccf7afedb_Out_0_Float = _HorizontalStripesRange;
            float _Subtract_060ee11d33904c8bbc605ce9ff02956f_Out_2_Float;
            Unity_Subtract_float(_Multiply_cb3ff70aa2da4e99bf05755a1ceba031_Out_2_Float, _Property_fc723ac65e5f458a9d830c7ccf7afedb_Out_0_Float, _Subtract_060ee11d33904c8bbc605ce9ff02956f_Out_2_Float);
            float _Round_725b1862da1c49eba5a41ec67ac79a26_Out_1_Float;
            Unity_Round_float(_Subtract_060ee11d33904c8bbc605ce9ff02956f_Out_2_Float, _Round_725b1862da1c49eba5a41ec67ac79a26_Out_1_Float);
            float _Property_1c2fb6e616b64bbb97ede80a9145ee24_Out_0_Float = _LayersAmount;
            float _Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float;
            Unity_Divide_float(_Round_725b1862da1c49eba5a41ec67ac79a26_Out_1_Float, _Property_1c2fb6e616b64bbb97ede80a9145ee24_Out_0_Float, _Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float);
            float _ColorMask_a6db5310cd3247d29cdae4ace96705a0_Out_3_Float;
            Unity_ColorMask_float((_Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float.xxx), (_Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float.xxx), 0, _ColorMask_a6db5310cd3247d29cdae4ace96705a0_Out_3_Float, 0);
            float _OneMinus_c3cf815ce9a04eb6a296dbb8eec49782_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_a6db5310cd3247d29cdae4ace96705a0_Out_3_Float, _OneMinus_c3cf815ce9a04eb6a296dbb8eec49782_Out_1_Float);
            float _Multiply_6fdf06600f244960b189a8ab099917d6_Out_2_Float;
            Unity_Multiply_float_float(_Modulo_4e47847728ce4c018035d5f914f354e2_Out_2_Float, _OneMinus_c3cf815ce9a04eb6a296dbb8eec49782_Out_1_Float, _Multiply_6fdf06600f244960b189a8ab099917d6_Out_2_Float);
            float _Add_f3f60d69261f4a06a077088f230d3a72_Out_2_Float;
            Unity_Add_float(_Divide_19a7307ba68943d1bd61463796630f48_Out_2_Float, 0.06, _Add_f3f60d69261f4a06a077088f230d3a72_Out_2_Float);
            float _Multiply_ce75f4668eb5431589846575d2587e9c_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_6fdf06600f244960b189a8ab099917d6_Out_2_Float, _Add_f3f60d69261f4a06a077088f230d3a72_Out_2_Float, _Multiply_ce75f4668eb5431589846575d2587e9c_Out_2_Float);
            float _Property_14e6751b377147fa9a0c85072c9ff5fe_Out_0_Float = _LayersAmount;
            float _Multiply_35a66009693c458182a46e93bc94c6fa_Out_2_Float;
            Unity_Multiply_float_float(_Property_14e6751b377147fa9a0c85072c9ff5fe_Out_0_Float, _Smoothstep_3ccdd9e219da4f6989e6004a5c2a4c99_Out_3_Float, _Multiply_35a66009693c458182a46e93bc94c6fa_Out_2_Float);
            float _Property_8686d2f6b8ee4a5782c84789670cdf79_Out_0_Float = _VerticalStripesRange;
            float _Subtract_cdf3a23b76bf4eaeb972b07da10567df_Out_2_Float;
            Unity_Subtract_float(_Multiply_35a66009693c458182a46e93bc94c6fa_Out_2_Float, _Property_8686d2f6b8ee4a5782c84789670cdf79_Out_0_Float, _Subtract_cdf3a23b76bf4eaeb972b07da10567df_Out_2_Float);
            float _Round_4c874c1633bc46d294cca798de82e024_Out_1_Float;
            Unity_Round_float(_Subtract_cdf3a23b76bf4eaeb972b07da10567df_Out_2_Float, _Round_4c874c1633bc46d294cca798de82e024_Out_1_Float);
            float _Property_0b4fcd9b7edf4c6cbc4130c399c32444_Out_0_Float = _LayersAmount;
            float _Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float;
            Unity_Divide_float(_Round_4c874c1633bc46d294cca798de82e024_Out_1_Float, _Property_0b4fcd9b7edf4c6cbc4130c399c32444_Out_0_Float, _Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float);
            float _ColorMask_096fe49695574ed29557e9acd18a332d_Out_3_Float;
            Unity_ColorMask_float((_Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float.xxx), (_Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float.xxx), 0, _ColorMask_096fe49695574ed29557e9acd18a332d_Out_3_Float, 0);
            float _OneMinus_c3d48c5e3b494ce1b7595d2a750d2dca_Out_1_Float;
            Unity_OneMinus_float(_ColorMask_096fe49695574ed29557e9acd18a332d_Out_3_Float, _OneMinus_c3d48c5e3b494ce1b7595d2a750d2dca_Out_1_Float);
            float _Floor_71d773377d5a424899c19166ed91aa67_Out_1_Float;
            Unity_Floor_float(_Split_12ac1e46829b4f288b14158a75b1b99b_R_1_Float, _Floor_71d773377d5a424899c19166ed91aa67_Out_1_Float);
            float _Modulo_19758df038064b40978bf0f1e6ddd4f8_Out_2_Float;
            Unity_Modulo_float(_Floor_71d773377d5a424899c19166ed91aa67_Out_1_Float, 2, _Modulo_19758df038064b40978bf0f1e6ddd4f8_Out_2_Float);
            float _Multiply_ca8be42c9143470d82604d4768f26ff9_Out_2_Float;
            Unity_Multiply_float_float(_OneMinus_c3d48c5e3b494ce1b7595d2a750d2dca_Out_1_Float, _Modulo_19758df038064b40978bf0f1e6ddd4f8_Out_2_Float, _Multiply_ca8be42c9143470d82604d4768f26ff9_Out_2_Float);
            float _Add_d91b6efab0584cfb86de3c39c21bcd5c_Out_2_Float;
            Unity_Add_float(_Divide_6e4449c24bd341639c8c97529f7062ec_Out_2_Float, 0.06, _Add_d91b6efab0584cfb86de3c39c21bcd5c_Out_2_Float);
            float _Multiply_41f6afed8bff4ef38926c844396b8db2_Out_2_Float;
            Unity_Multiply_float_float(_Multiply_ca8be42c9143470d82604d4768f26ff9_Out_2_Float, _Add_d91b6efab0584cfb86de3c39c21bcd5c_Out_2_Float, _Multiply_41f6afed8bff4ef38926c844396b8db2_Out_2_Float);
            float3 _ReplaceColor_f30f8e0e6621490ab164ecce7c6c90f3_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_41f6afed8bff4ef38926c844396b8db2_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), (_Divide_8fe9d4d35809473d99c50c1b50ec1e50_Out_2_Float.xxx), 0.01, _ReplaceColor_f30f8e0e6621490ab164ecce7c6c90f3_Out_4_Vector3, 0);
            float3 _ReplaceColor_8d9ebb7af2134841a28b91021cff5f68_Out_4_Vector3;
            Unity_ReplaceColor_float((_Multiply_ce75f4668eb5431589846575d2587e9c_Out_2_Float.xxx), IsGammaSpace() ? float3(0, 0, 0) : SRGBToLinear(float3(0, 0, 0)), _ReplaceColor_f30f8e0e6621490ab164ecce7c6c90f3_Out_4_Vector3, 0.1, _ReplaceColor_8d9ebb7af2134841a28b91021cff5f68_Out_4_Vector3, 0);
            float3 _Lerp_472f20b9e01f4808b2cee5eceeab0bf6_Out_3_Vector3;
            Unity_Lerp_float3((_Property_fa106bfea48c4a3eac21b86ae23e432d_Out_0_Vector4.xyz), (_Property_cefafd2c7973496ab23efbe5144943ca_Out_0_Vector4.xyz), _ReplaceColor_8d9ebb7af2134841a28b91021cff5f68_Out_4_Vector3, _Lerp_472f20b9e01f4808b2cee5eceeab0bf6_Out_3_Vector3);
            float3 _Multiply_3ac8c2e3671d4fb9aa70298e83f094b5_Out_2_Vector3;
            Unity_Multiply_float3_float3((_Multiply_4e73b4f5d19f47c382a851655c4a480c_Out_2_Vector4.xyz), _Lerp_472f20b9e01f4808b2cee5eceeab0bf6_Out_3_Vector3, _Multiply_3ac8c2e3671d4fb9aa70298e83f094b5_Out_2_Vector3);
            Bindings_MainLight_00da36052050d4d4d9f99715bcaaea4f_float _MainLight_e49afc6a96a84845917a6c020660bde5;
            _MainLight_e49afc6a96a84845917a6c020660bde5.WorldSpacePosition = IN.WorldSpacePosition;
            float3 _MainLight_e49afc6a96a84845917a6c020660bde5_Direction_1_Vector3;
            float3 _MainLight_e49afc6a96a84845917a6c020660bde5_Color_2_Vector3;
            float _MainLight_e49afc6a96a84845917a6c020660bde5_Attenuation_3_Float;
            SG_MainLight_00da36052050d4d4d9f99715bcaaea4f_float(_MainLight_e49afc6a96a84845917a6c020660bde5, _MainLight_e49afc6a96a84845917a6c020660bde5_Direction_1_Vector3, _MainLight_e49afc6a96a84845917a6c020660bde5_Color_2_Vector3, _MainLight_e49afc6a96a84845917a6c020660bde5_Attenuation_3_Float);
            float3 _Multiply_86dc82a772b74e59a0e53a654f6f5cde_Out_2_Vector3;
            Unity_Multiply_float3_float3(_Multiply_3ac8c2e3671d4fb9aa70298e83f094b5_Out_2_Vector3, _MainLight_e49afc6a96a84845917a6c020660bde5_Color_2_Vector3, _Multiply_86dc82a772b74e59a0e53a654f6f5cde_Out_2_Vector3);
            surface.BaseColor = _Multiply_86dc82a772b74e59a0e53a654f6f5cde_Out_2_Vector3;
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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
        float _VerticalStripesRange;
        float _HorizontalStripesRange;
        float _LayersAmount;
        float4 _DarkColor;
        float4 _BrightColor;
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