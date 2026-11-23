#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Coloque este script dentro de uma pasta chamada "Editor" no seu projeto da Unity (ex: Assets/Editor/Setor07UnitySetup.cs).
/// 1) Ao importar o FBX, ele ativa automaticamente Luzes, Câmeras e MeshColliders.
/// 2) No menu superior da Unity, clique em: "Tools > Setor 07 > Configurar Luzes, Emissão e Atmosfera"
///    para calibrar as 15 luzes e ativar o brilho HDR (Emission) em todos os materiais (compatível com URP e Built-in).
/// </summary>
public class Setor07UnitySetup : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (assetPath.Contains("Setor_Contencao_07") || assetPath.Contains("PAVIMENTO_") || assetPath.Contains("ESTRUTURA"))
        {
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.importLights = true;
            importer.importCameras = true;
            importer.addCollider = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }

    [MenuItem("Tools/Setor 07/Configurar Luzes, Emissão e Atmosfera")]
    public static void ConfigureSceneAndMaterials()
    {
        ConfigureAllMaterials();
        CalibrateSceneLights();
        ConfigureDarkAtmosphere();
        Debug.Log("[Setor 07] Luzes, materiais emissivos (HDR) e atmosfera sombria configurados com sucesso!");
    }

    private static void ConfigureAllMaterials()
    {
        RenderPipelineAsset activeRP = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : GraphicsSettings.defaultRenderPipeline;
        bool isURP = activeRP != null && activeRP.GetType().Name.Contains("Universal");
        Shader targetShader = isURP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");

        string[] guids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || !mat.name.StartsWith("Mat_")) continue;

            if (targetShader != null && mat.shader != targetShader)
                mat.shader = targetShader;

            // Default PBR
            ApplyPBR(mat, isURP);
        }
        AssetDatabase.SaveAssets();
    }

    private static void ApplyPBR(Material mat, bool isURP)
    {
        string n = mat.name;
        Color emitColor = Color.black;
        float emitIntensity = 0f;

        if (n.Contains("Mat_Emit_Cyan_Diegetic")) { emitColor = new Color(0.1f, 0.9f, 1.0f); emitIntensity = 3.2f; }
        else if (n.Contains("Mat_Emit_Red_NEXUS")) { emitColor = new Color(1.0f, 0.08f, 0.05f); emitIntensity = 3.5f; }
        else if (n.Contains("Mat_Emit_Amber_Hazard")) { emitColor = new Color(1.0f, 0.6f, 0.05f); emitIntensity = 3.0f; }
        else if (n.Contains("Mat_Emit_Green_Sync")) { emitColor = new Color(0.1f, 1.0f, 0.45f); emitIntensity = 2.8f; }
        else if (n.Contains("Mat_Puzzle_Circuit_Purple")) { emitColor = new Color(0.7f, 0.25f, 1.0f); emitIntensity = 3.2f; }
        else if (n.Contains("Mat_Breakable_Grate")) { emitColor = new Color(1.0f, 0.3f, 0.05f); emitIntensity = 1.2f; }
        else if (n.Contains("Mat_WallRun_Surface")) { emitColor = new Color(0.9f, 0.45f, 0.05f); emitIntensity = 1.1f; }
        else if (n.Contains("Mat_Glass_Containment")) { emitColor = new Color(0.08f, 0.5f, 0.65f); emitIntensity = 1.2f; }

        if (emitIntensity > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Color hdr = emitColor * Mathf.LinearToGammaSpace(emitIntensity);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", hdr);
        }

        EditorUtility.SetDirty(mat);
    }

    private static void CalibrateSceneLights()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Point && (l.name.StartsWith("P1") || l.name.StartsWith("P2") ||
                l.name.StartsWith("P3") || l.name.StartsWith("P4") || l.name.StartsWith("P5")))
            {
                Undo.RecordObject(l, "Calibrate Setor 07 Light");
                // Converte os 900W do Blender para intensidade e alcance calibrados na Unity
                l.intensity = 3.8f;
                l.range = 19.5f;
                l.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(l);
            }
        }
    }

    private static void ConfigureDarkAtmosphere()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.045f, 0.06f);
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.015f, 0.02f, 0.028f);
        }
    }
}
#endif
