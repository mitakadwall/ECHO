#if UNITY_EDITOR
using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Eco.Save;

namespace Eco.Editor
{
    public static class SaveTerminalSceneSetup
    {
        [MenuItem("Tools/Eco/Configurar Terminais de Save Diegético")]
        public static void SetupSaveTerminalsInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[SaveTerminalSceneSetup] Nenhuma cena válida aberta.");
                return;
            }

            EnsureEventSystem();
            EnsureSaveManager();
            ConfigureCinemachineBlend();

            int configured = 0;
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in allTransforms)
            {
                string trimmed = t.name.Trim().ToLowerInvariant();
                int terminalIndex = trimmed switch
                {
                    "save 1" => 1,
                    "save 2" => 2,
                    "save 3" => 3,
                    "save 4" => 4,
                    _ => 0
                };

                if (terminalIndex == 0) continue;

                ConfigureTerminal(t.gameObject, terminalIndex);
                configured++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SaveTerminalSceneSetup] {configured} terminais configurados e salvos na cena '{scene.name}'.");
        }

        [MenuItem("Tools/Eco/Limpar Save Local")]
        public static void ClearLocalSaveFile()
        {
            string basePath = Path.Combine(Application.persistentDataPath, "eco_save.json");
            string[] paths = { basePath, basePath + ".bak", basePath + ".tmp" };
            int removed = 0;
            foreach (string p in paths)
            {
                if (File.Exists(p))
                {
                    File.Delete(p);
                    removed++;
                }
            }
            Debug.Log($"[SaveTerminalSceneSetup] Limpeza de save concluída ({removed} arquivo(s) removido(s) de {Application.persistentDataPath}).");
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
                es = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<InputSystemUIInputModule>();
            }
            else if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static void EnsureSaveManager()
        {
            EcoSaveManager existing = Object.FindAnyObjectByType<EcoSaveManager>();
            if (existing == null)
            {
                GameObject go = new GameObject("EcoSaveManager");
                Undo.RegisterCreatedObjectUndo(go, "Create EcoSaveManager");
                go.AddComponent<EcoSaveManager>();
            }
        }

        private static void ConfigureCinemachineBlend()
        {
            CinemachineBrain brain = Object.FindAnyObjectByType<CinemachineBrain>();
            if (brain != null)
            {
                Undo.RecordObject(brain, "Tune Cinemachine Blend");
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.75f);
                EditorUtility.SetDirty(brain);
            }
        }

        private static void ConfigureTerminal(GameObject target, int terminalIndex)
        {
            Undo.RecordObject(target, $"Configure Save Terminal {terminalIndex}");

            SaveTerminalUI ui = target.GetComponent<SaveTerminalUI>();
            if (ui == null) ui = Undo.AddComponent<SaveTerminalUI>(target);

            SaveTerminalInteractable interactable = target.GetComponent<SaveTerminalInteractable>();
            if (interactable == null) interactable = Undo.AddComponent<SaveTerminalInteractable>(target);

            interactable.ConfigureTerminalIndex(terminalIndex);
            interactable.DetectScreenGeometry();

            // Create or update dedicated CinemachineCamera for crisp monitor framing
            CinemachineCamera vcam = target.GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                GameObject camGo = new GameObject($"TerminalCamera_Save{terminalIndex}");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Terminal Camera");
                camGo.transform.SetParent(target.transform, true);
                vcam = camGo.AddComponent<CinemachineCamera>();
            }

            vcam.transform.position = interactable.GetRecommendedCameraWorldPosition();
            vcam.transform.rotation = interactable.GetRecommendedCameraWorldRotation();
            vcam.Lens.FieldOfView = 50f;
            vcam.Priority.Value = 0;

            interactable.AssignTerminalCamera(vcam);
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(vcam);
        }
    }
}
#endif
