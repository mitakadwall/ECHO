using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NexusTools
{
    public static class NexusUnitySceneImporter
    {
        [Serializable]
        private class SceneManifest
        {
            public string scene_name;
            public string fbx_file;
            public List<LightEntry> lights;
            public List<MaterialEntry> materials;
        }

        [Serializable]
        private class LightEntry
        {
            public string name;
            public string type;
            public float[] blender_pos;
            public float[] blender_rot_deg;
            public float[] color;
            public float energy;
            public float size_x;
            public float size_y;
            public float shadow_soft_size;
            public bool use_shadow;
        }

        [Serializable]
        private class MaterialEntry
        {
            public string name;
            public float[] base_color;
            public float metallic;
            public float roughness;
            public float[] emission_color;
            public float emission_strength;
            public float alpha;
        }

        [MenuItem("Tools/NEXUS/1. Montar Cena Completa (Luzes Realtime + Materiais)")]
        public static void ImportFullSceneRealtime()
        {
            BuildScene(useBakedAreaLights: false, showDialog: true);
        }

        [MenuItem("Tools/NEXUS/2. Montar Cena para Lightmap Bake (Area Lights Baked)")]
        public static void ImportFullSceneBakedArea()
        {
            BuildScene(useBakedAreaLights: true, showDialog: true);
        }

        [MenuItem("Tools/NEXUS/3. Adicionar MeshColliders nos Objetos Selecionados")]
        public static void AddMeshCollidersToSelection()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                EditorUtility.DisplayDialog("NEXUS", "Selecione o objeto raiz da cena (Area_Treinamento_NEXUS) na Hierarchy primeiro.", "OK");
                return;
            }

            AddMeshColliders(root, showDialog: true);
        }

        public static int AddMeshColliders(GameObject root, bool showDialog = true)
        {
            if (root == null) return 0;

            int added = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = mf.gameObject.name.ToLowerInvariant();
                if (n.Contains("holo") || n.Contains("linha") || n.Contains("luminaria") || n.Contains("mesh"))
                    continue;
                if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null)
                {
                    Undo.AddComponent<MeshCollider>(mf.gameObject);
                    added++;
                }
            }

            if (showDialog)
                EditorUtility.DisplayDialog("NEXUS", $"Adicionados {added} MeshColliders!", "OK");
            else
                Debug.Log($"[NEXUS] Adicionados {added} MeshColliders em '{root.name}'.");

            return added;
        }

        public static GameObject BuildScene(bool useBakedAreaLights, bool showDialog = true)
        {
            string jsonAssetPath = FindAssetPath("nexus_scene_data");
            string fbxAssetPath = FindAssetPath("area de treinamento", "t:Model")
                               ?? FindAssetPath("area_treinamento_nexus", "t:Model");

            if (string.IsNullOrEmpty(jsonAssetPath) || string.IsNullOrEmpty(fbxAssetPath))
            {
                string errMsg =
                    "Nao encontrei 'area de treinamento.fbx' (ou 'area_treinamento_nexus.fbx') ou 'nexus_scene_data.json' dentro da pasta Assets/.\n" +
                    "Arraste a pasta 'area de treinamento' inteira para dentro de Assets/ na Unity e tente novamente.";
                if (showDialog)
                    EditorUtility.DisplayDialog("NEXUS Importer", errMsg, "OK");
                else
                    Debug.LogError("[NEXUS] " + errMsg);
                return null;
            }

            // 1. Ensure FBX imports lights, cameras, and normals cleanly
            var importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
            if (importer != null)
            {
                importer.importLights = true;
                importer.importCameras = true;
                importer.useFileUnits = true;
                importer.globalScale = 1.0f;
                importer.SaveAndReimport();
            }

            // 2. Load JSON manifest
            string fullJsonPath = Path.GetFullPath(jsonAssetPath);
            var manifest = JsonUtility.FromJson<SceneManifest>(File.ReadAllText(fullJsonPath));

            // 3. Create / update Unity materials with exact PBR + HDR Emission + Alpha
            string baseFolder = Path.GetDirectoryName(fbxAssetPath).Replace("\\", "/");
            string matFolder = baseFolder + "/Materials_NEXUS";
            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder(baseFolder, "Materials_NEXUS");

            var matMap = CreateUnityMaterials(manifest.materials, matFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 4. Instantiate FBX in the current scene
            var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            instance.name = "Area_Treinamento_NEXUS";
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(instance, "Importar Area de Treinamento NEXUS");

            // 5. Assign exact materials to all MeshRenderers
            int renderersUpdated = 0;
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    string cleanName = shared[i].name.Replace(" (Instance)", "").Trim();
                    if (matMap.TryGetValue(cleanName, out var exactMat))
                    {
                        shared[i] = exactMat;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = shared;
                    renderersUpdated++;
                }
            }

            // 6. Configure ALL 15 Lights with exact Blender color, scale, position & intensity
            int lightsConfigured = ConfigureLights(instance, manifest.lights, useBakedAreaLights);

            // 7. Set dark underground ambient lighting to match Blender World
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.055f, 0.075f, 1.0f);

            Selection.activeGameObject = instance;
            string summary =
                $"Cena montada com sucesso!\n\n" +
                $"- Renderers com materiais exatos (PBR + Emissao): {renderersUpdated}\n" +
                $"- Materiais criados em {matFolder}: {matMap.Count}\n" +
                $"- Luzes configuradas ({(useBakedAreaLights ? "Baked Area" : "Realtime")}): {lightsConfigured}/{manifest.lights.Count}\n\n" +
                "Dica: Ative Bloom no Volume (URP/HDRP) para o brilho neon ciano/ambar/vermelho igual ao Blender!";

            if (showDialog)
                EditorUtility.DisplayDialog("NEXUS — Importacao Concluida!", summary, "OK");
            else
                Debug.Log("[NEXUS] " + summary);

            return instance;
        }

        private static Dictionary<string, Material> CreateUnityMaterials(List<MaterialEntry> entries, string matFolder)
        {
            var result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            bool isURP = Shader.Find("Universal Render Pipeline/Lit") != null;
            bool isHDRP = !isURP && Shader.Find("HDRP/Lit") != null;

            Shader targetShader = isURP
                ? Shader.Find("Universal Render Pipeline/Lit")
                : (isHDRP ? Shader.Find("HDRP/Lit") : Shader.Find("Standard"));

            foreach (var entry in entries)
            {
                string safeName = SanitizeFileName(entry.name);
                string matPath = $"{matFolder}/{safeName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(targetShader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    mat.shader = targetShader;
                }

                Color baseCol = new Color(entry.base_color[0], entry.base_color[1], entry.base_color[2], entry.alpha);
                float smoothness = Mathf.Clamp01(1.0f - entry.roughness);

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseCol);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", entry.metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

                // Emission (HDR glow!)
                if (entry.emission_strength > 0.01f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    // Scale Blender emission strength into clean Unity HDR range
                    float hdrFactor = Mathf.Min(entry.emission_strength * 0.65f, 4.5f);
                    Color emCol = new Color(
                        entry.emission_color[0] * hdrFactor,
                        entry.emission_color[1] * hdrFactor,
                        entry.emission_color[2] * hdrFactor,
                        1.0f
                    );
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emCol);
                    if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emCol);
                }

                // Transparency for Glass / Holograms
                bool isTransparent = entry.alpha < 0.99f || entry.name.ToLowerInvariant().Contains("glass");
                if (isTransparent)
                {
                    if (entry.name.ToLowerInvariant().Contains("glass") && entry.alpha >= 0.99f)
                    {
                        baseCol.a = 0.35f;
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseCol);
                    }

                    if (isURP)
                    {
                        mat.SetFloat("_Surface", 1f); // Transparent
                        mat.SetFloat("_Blend", 0f);   // Alpha
                        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.renderQueue = (int)RenderQueue.Transparent;
                    }
                    else if (!isHDRP)
                    {
                        mat.SetFloat("_Mode", 3f); // Transparent in Built-in Standard
                        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite", 0);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = (int)RenderQueue.Transparent;
                    }
                }

                EditorUtility.SetDirty(mat);
                result[entry.name] = mat;
            }
            return result;
        }

        private static int ConfigureLights(GameObject root, List<LightEntry> lightEntries, bool useBakedAreaLights)
        {
            // Map all existing transforms inside the instantiated FBX by name
            var transformMap = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!transformMap.ContainsKey(t.name))
                    transformMap[t.name] = t;
            }

            // Create a clean Lights container if any light wasn't found as a child
            var lightsParent = new GameObject("NEXUS_Lights").transform;
            lightsParent.SetParent(root.transform, false);

            bool isURP = Shader.Find("Universal Render Pipeline/Lit") != null;
            int count = 0;

            foreach (var entry in lightEntries)
            {
                Transform targetT = null;
                if (transformMap.TryGetValue(entry.name, out var existing))
                {
                    targetT = existing;
                }
                else
                {
                    var go = new GameObject(entry.name);
                    go.transform.SetParent(lightsParent, false);
                    // Blender (X, Y, Z) -> Unity FBX (-Z forward, Y up, baked): (-X, Z, -Y)
                    go.transform.localPosition = new Vector3(-entry.blender_pos[0], entry.blender_pos[2], -entry.blender_pos[1]);
                    targetT = go.transform;
                }

                var light = targetT.GetComponent<Light>();
                if (light == null)
                    light = targetT.gameObject.AddComponent<Light>();

                light.color = new Color(entry.color[0], entry.color[1], entry.color[2], 1.0f);
                light.shadows = entry.use_shadow ? LightShadows.Soft : LightShadows.None;

                // Convert Blender Watts (300-850W) into pleasant Unity intensity
                float baseIntensity = Mathf.Clamp(entry.energy / (isURP ? 140.0f : 180.0f), 1.5f, 8.0f);

                if (entry.type == "AREA")
                {
                    // Point straight down (-Y in Unity) matching Blender Area light default (-Z in Blender)
                    targetT.rotation = Quaternion.Euler(90f, 0f, 0f);

                    if (useBakedAreaLights)
                    {
                        light.type = LightType.Rectangle;
                        light.lightmapBakeType = LightmapBakeType.Baked;
                        light.areaSize = new Vector2(Mathf.Max(0.5f, entry.size_x), Mathf.Max(0.5f, entry.size_y));
                        light.intensity = baseIntensity * 1.4f;
                        light.range = Mathf.Clamp(Mathf.Sqrt(entry.energy) * 0.9f, 12f, 28f);
                    }
                    else
                    {
                        // Wide soft downward Spot Light so it illuminates immediately in Realtime / Play Mode!
                        light.type = LightType.Spot;
                        light.lightmapBakeType = LightmapBakeType.Mixed;
                        light.spotAngle = 135f;
                        light.innerSpotAngle = 70f;
                        light.range = Mathf.Clamp(Mathf.Sqrt(entry.energy) * 0.95f, 14f, 30f);
                        light.intensity = baseIntensity * 1.6f;
                    }
                }
                else if (entry.type == "POINT")
                {
                    light.type = LightType.Point;
                    light.lightmapBakeType = LightmapBakeType.Mixed;
                    light.range = Mathf.Clamp(Mathf.Sqrt(entry.energy) * 0.85f, 12f, 24f);
                    light.intensity = baseIntensity * 1.25f;
                }
                else if (entry.type == "SPOT")
                {
                    light.type = LightType.Spot;
                    light.lightmapBakeType = LightmapBakeType.Mixed;
                    light.range = Mathf.Clamp(Mathf.Sqrt(entry.energy) * 0.9f, 14f, 28f);
                    light.intensity = baseIntensity * 1.5f;
                }
                else
                {
                    light.type = LightType.Directional;
                    light.intensity = Mathf.Clamp(entry.energy * 0.25f, 0.5f, 3.0f);
                }

                count++;
            }

            // Clean up empty container if all lights were found on FBX transforms
            if (lightsParent.childCount == 0)
                UnityEngine.Object.DestroyImmediate(lightsParent.gameObject);

            return count;
        }

        private static string FindAssetPath(string namePart, string filterExtra = "")
        {
            string query = string.IsNullOrEmpty(filterExtra) ? namePart : $"{namePart} {filterExtra}";
            foreach (var guid in AssetDatabase.FindAssets(query))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).Equals(namePart, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
