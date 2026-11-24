#if UNITY_EDITOR
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Eco.Cutscene;

namespace Eco.Editor
{
    public static class IntroCutsceneSceneSetup
    {
        private const string GameplayPath = "Assets/Scenes/Gameplay.unity";

        [MenuItem("Tools/Eco/Configurar Cutscene Inicial (Sala 5A)")]
        public static void SetupIntroCutsceneInGameplay()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Gameplay")
            {
                scene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
            }

            IntroWakeCutsceneController existing = Object.FindAnyObjectByType<IntroWakeCutsceneController>();
            GameObject rootGo;
            if (existing != null)
            {
                rootGo = existing.gameObject;
            }
            else
            {
                rootGo = new GameObject("IntroWakeCutscene");
                Undo.RegisterCreatedObjectUndo(rootGo, "Create IntroWakeCutscene");
                existing = rootGo.AddComponent<IntroWakeCutsceneController>();
            }

            // Dedicated CinemachineCamera starting on top of the metal 'maca' in Sala 5A
            CinemachineCamera vcam = rootGo.GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                GameObject camGo = new GameObject("IntroCutsceneCamera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create IntroCutsceneCamera");
                camGo.transform.SetParent(rootGo.transform, true);
                vcam = camGo.AddComponent<CinemachineCamera>();
            }
            vcam.transform.position = new Vector3(14.00f, 43.34f, -8.25f);
            vcam.transform.rotation = Quaternion.Euler(-78f, -15f, 4f);
            vcam.Lens.FieldOfView = 64f;
            vcam.Priority.Value = 0;

            // Quarantine chamber exit target (gap between 'parede ' at x=7 and 'vidro' at x=11.35, z=-2)
            Transform doorTargetTr = rootGo.transform.Find("QuarantineExitDoorTarget");
            if (doorTargetTr == null)
            {
                GameObject dtGo = new GameObject("QuarantineExitDoorTarget");
                Undo.RegisterCreatedObjectUndo(dtGo, "Create QuarantineExitDoorTarget");
                dtGo.transform.SetParent(rootGo.transform, true);
                doorTargetTr = dtGo.transform;
            }
            doorTargetTr.position = new Vector3(9.20f, 44.10f, -2.00f);

            // Remove any floating placeholder lock/emergency objects if previously generated
            Transform oldLightTr = rootGo.transform.Find("QuarantineEmergencyLight");
            if (oldLightTr != null) Object.DestroyImmediate(oldLightTr.gameObject);

            Transform oldLockTr = rootGo.transform.Find("QuarantineDoorLockIndicator");
            if (oldLockTr != null) Object.DestroyImmediate(oldLockTr.gameObject);

            existing.AssignReferences(vcam, null, null, doorTargetTr);
            EditorUtility.SetDirty(rootGo);
            EditorUtility.SetDirty(vcam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[IntroCutsceneSceneSetup] Cutscene inicial configurada na Sala 5A (sem luz/cubo flutuante) e salva em Gameplay.unity.");
        }
    }
}
#endif
