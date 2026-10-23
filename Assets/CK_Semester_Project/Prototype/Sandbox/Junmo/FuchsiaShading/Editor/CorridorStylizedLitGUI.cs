using System;
using UnityEditor;
using UnityEngine;

// URP Lit 인스펙터(내부 클래스)를 그대로 그린 뒤, 배경 보조광 속성을 덧붙인다.
// Lit 인스펙터가 키워드(_METALLICSPECGLOSSMAP 등)를 관리하므로 기존 재질 설정이 유지된다.
public sealed class CorridorStylizedLitGUI : ShaderGUI
{
    private static readonly string[] PunctualProperties =
    {
        "_PunctualMinRoughness", "_PunctualSpecular", "_PunctualMetalWash",
        "_PunctualWrap", "_PunctualAttenuationKnee",
    };

    private ShaderGUI _litGUI;
    private bool _punctualFoldout = true;

    private ShaderGUI LitGUI
    {
        get
        {
            if (_litGUI == null)
            {
                Type type = Type.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitShader, Unity.RenderPipelines.Universal.Editor");
                _litGUI = type != null ? (ShaderGUI)Activator.CreateInstance(type, true) : null;
            }
            return _litGUI;
        }
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        _punctualFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_punctualFoldout, "배경 보조광 (점광/스폿광)");
        if (_punctualFoldout)
        {
            foreach (string name in PunctualProperties)
            {
                MaterialProperty property = FindProperty(name, properties, false);
                if (property != null)
                {
                    materialEditor.ShaderProperty(property, property.displayName);
                }
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (LitGUI != null)
        {
            LitGUI.OnGUI(materialEditor, properties);
        }
        else
        {
            base.OnGUI(materialEditor, properties);
        }
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        if (LitGUI != null)
        {
            LitGUI.AssignNewShaderToMaterial(material, oldShader, newShader);
        }
        else
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
        }
    }

    public override void ValidateMaterial(Material material)
    {
        LitGUI?.ValidateMaterial(material);
    }
}
