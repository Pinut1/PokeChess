#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TMP 폰트의 머티리얼 프리셋을 발광/테두리 설정과 함께 만들어 준다.
///
/// TMP 기본 머티리얼을 직접 고치면 그 폰트를 쓰는 모든 텍스트가 같이 바뀐다.
/// 그래서 특정 텍스트만 꾸미려면 프리셋(복제 머티리얼)이 필요한데,
/// 손으로 만들면 복제 → 이름 변경 → 키워드 활성화 순서를 매번 반복해야 한다.
/// 이 메뉴가 그 과정을 대신하고, 값 조정은 만들어진 머티리얼 인스펙터에서 하면 된다.
///
/// 사용: Project 창에서 폰트 에셋(*SDF.asset)을 선택 → PokeChess/UI/TMP 발광 프리셋 만들기
/// </summary>
public static class TmpMaterialPresetCreator
{
    private const string MENU = "PokeChess/UI/TMP 발광 프리셋 만들기";

    // TMP Distance Field 셰이더의 프로퍼티/키워드 이름. 인스펙터 라벨과 1:1로 대응한다.
    private const string GLOW_KEYWORD    = "GLOW_ON";
    private const string OUTLINE_KEYWORD = "OUTLINE_ON";
    // 입체 조명(Local Lighting). 이 키워드가 꺼져 있으면 _LightAngle을 돌려도 셰이더가 무시한다
    // (TMP_SDF.shader의 조명 계산 전체가 #if BEVEL_ON 안에 있다).
    private const string BEVEL_KEYWORD   = "BEVEL_ON";

    [MenuItem(MENU, true)]
    private static bool Validate() => Selection.activeObject is TMP_FontAsset;

    [MenuItem(MENU)]
    private static void Create()
    {
        var font = Selection.activeObject as TMP_FontAsset;
        if (font == null) return;

        Material source = font.material;
        if (source == null)
        {
            Debug.LogError($"[TMP] '{font.name}'에 기본 머티리얼이 없습니다.");
            return;
        }

        var preset = new Material(source) { name = $"{font.name} - Glow Outline" };

        // ── 테두리: 맵 텍스처 위에서 글자가 묻히지 않게 ──
        preset.EnableKeyword(OUTLINE_KEYWORD);
        preset.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
        preset.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.15f);

        // ── 발광: 은은하게 깔리는 정도 ──
        preset.EnableKeyword(GLOW_KEYWORD);
        preset.SetColor(ShaderUtilities.ID_GlowColor, new Color(1f, 0.9f, 0.5f, 1f));
        preset.SetFloat(ShaderUtilities.ID_GlowPower,  0.35f);
        preset.SetFloat(ShaderUtilities.ID_GlowOuter,  0.3f);
        preset.SetFloat(ShaderUtilities.ID_GlowInner,  0.05f);
        preset.SetFloat(ShaderUtilities.ID_GlowOffset, 0f);

        // ── 입체 조명: LightAngle을 돌려 빛이 도는 연출을 쓰려면 필수 ──
        preset.EnableKeyword(BEVEL_KEYWORD);
        preset.SetFloat(ShaderUtilities.ID_BevelAmount, 0.5f);
        preset.SetFloat(ShaderUtilities.ID_LightAngle,  Mathf.PI); // 기본값. 런타임에 회전시킨다
        preset.SetColor("_SpecularColor", Color.white);
        preset.SetFloat("_SpecularPower", 2f);
        preset.SetFloat("_Reflectivity",  10f);
        preset.SetFloat("_Diffuse",       0.5f);
        preset.SetFloat("_Ambient",       0.5f);

        string dir  = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font));
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{preset.name}.mat");

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);

        Debug.Log($"[TMP] 프리셋 생성: {path}\n" +
                  "텍스트 오브젝트의 Material Preset 드롭다운에서 이걸 고르면 적용됩니다. " +
                  "재질감을 주려면 이 머티리얼의 Face → Texture에 노이즈를 넣고 Speed X를 올리세요.");
    }
}
#endif
