#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Eco.Interaction;
using Eco.MemoryFragments;

namespace Eco.Editor
{
    public static class MemoryFragmentSceneSetup
    {
        private const string GameplayPath = "Assets/Scenes/Gameplay.unity";

        [MenuItem("Tools/Eco/Configurar Fragmentos de Memória e Células de Energia")]
        public static void SetupFragmentsAndEnergyCells()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Gameplay")
            {
                scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            }

            // Ensure shared MemoryFragmentOverlayUI exists
            MemoryFragmentOverlayUI overlay = Object.FindAnyObjectByType<MemoryFragmentOverlayUI>();
            if (overlay == null)
            {
                GameObject ovGo = new GameObject("MemoryFragmentOverlayUI");
                Undo.RegisterCreatedObjectUndo(ovGo, "Create MemoryFragmentOverlayUI");
                overlay = ovGo.AddComponent<MemoryFragmentOverlayUI>();
            }

            // Collect Pavimento 2 lights for Fragmento 05 oscillation
            List<Light> p2Lights = new List<Light>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l != null && l.name.StartsWith("P2"))
                {
                    p2Lights.Add(l);
                }
            }

            int fragCount = 0;
            int cellCount = 0;
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

            foreach (Transform t in allTransforms)
            {
                string norm = t.name.Trim().ToLowerInvariant();

                // 1. Memory Fragments (5 exact objects)
                MemoryFragmentKind? kind = norm switch
                {
                    "frag p4" => MemoryFragmentKind.Frag01_PrimeiroRegistro,
                    "frag p3" => MemoryFragmentKind.Frag02_RegistroDeAusencia,
                    "frag 2 p3" => MemoryFragmentKind.Frag03_AVoz,
                    "frag p2" => MemoryFragmentKind.Frag04_ONome,
                    "frag 2 p2" => MemoryFragmentKind.Frag05_AOrigem,
                    _ => null
                };

                if (kind.HasValue)
                {
                    ConfigureFragment(t.gameObject, kind.Value, kind.Value == MemoryFragmentKind.Frag05_AOrigem ? p2Lights.ToArray() : null);
                    fragCount++;
                    continue;
                }

                // 2. Energy Cells (all objects named 'celula de energia...')
                if (norm.Contains("celula de energia") || norm.Contains("célula de energia"))
                {
                    ConfigureEnergyCell(t.gameObject, norm.Replace(" ", "_"));
                    cellCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[MemoryFragmentSceneSetup] {fragCount} fragmentos de memória e {cellCount} células de energia configurados e salvos em '{scene.name}'.");
        }

        private static void ConfigureFragment(GameObject go, MemoryFragmentKind kind, Light[] floorLights)
        {
            Undo.RecordObject(go, $"Configure Memory Fragment {kind}");

            // Add trigger BoxCollider if missing so PlayerInteractor can raycast/detect [E]
            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(go);
            col.isTrigger = true;
            col.size = Vector3.Max(col.size, new Vector3(0.95f, 0.95f, 0.95f));

            // Add subtle disabled local PointLight for active event glow (especially intense blue on Frag 04)
            Light localLight = go.GetComponentInChildren<Light>(true);
            if (localLight == null)
            {
                GameObject lGo = new GameObject("FragmentEffectLight");
                Undo.RegisterCreatedObjectUndo(lGo, "Create FragmentEffectLight");
                lGo.transform.SetParent(go.transform, false);
                lGo.transform.localPosition = Vector3.zero;
                localLight = lGo.AddComponent<Light>();
            }
            localLight.type = LightType.Point;
            localLight.range = (kind == MemoryFragmentKind.Frag04_ONome) ? 9.0f : 5.5f;
            localLight.intensity = (kind == MemoryFragmentKind.Frag04_ONome) ? 5.2f : 2.6f;
            localLight.color = new Color(0.08f, 0.72f, 1.0f);
            localLight.enabled = false;

            MemoryFragmentInteractable interactable = go.GetComponent<MemoryFragmentInteractable>();
            if (interactable == null) interactable = Undo.AddComponent<MemoryFragmentInteractable>(go);
            interactable.Configure(kind, localLight, floorLights);

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(localLight);
        }

        private static void ConfigureEnergyCell(GameObject go, string id)
        {
            Undo.RecordObject(go, $"Configure Energy Cell {id}");

            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(go);
            col.isTrigger = true;
            col.size = Vector3.Max(col.size, new Vector3(0.9f, 0.9f, 0.9f));

            EnergyCellInteractable cell = go.GetComponent<EnergyCellInteractable>();
            if (cell == null) cell = Undo.AddComponent<EnergyCellInteractable>(go);
            cell.Configure(id);

            EditorUtility.SetDirty(go);
        }
    }
}
#endif
