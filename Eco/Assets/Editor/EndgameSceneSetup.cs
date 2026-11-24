#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Eco.Endgame;

namespace Eco.Editor
{
    public static class EndgameSceneSetup
    {
        [MenuItem("Tools/Eco/Configurar Terminal Final (Pavimento 1)")]
        public static void SetupEndgameTerminal()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[EndgameSceneSetup] Nenhuma cena válida aberta.");
                return;
            }

            Transform finalObj = null;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.name.Trim().ToLowerInvariant() == "final")
                {
                    finalObj = t;
                    break;
                }
            }

            if (finalObj == null)
            {
                Debug.LogError("[EndgameSceneSetup] Objeto 'final' não encontrado na cena.");
                return;
            }

            Undo.RecordObject(finalObj.gameObject, "Configure Final Endgame Terminal");

            EndgameTerminalUI ui = finalObj.GetComponent<EndgameTerminalUI>();
            if (ui == null) ui = Undo.AddComponent<EndgameTerminalUI>(finalObj.gameObject);

            EndgameTerminalInteractable interactable = finalObj.GetComponent<EndgameTerminalInteractable>();
            if (interactable == null) interactable = Undo.AddComponent<EndgameTerminalInteractable>(finalObj.gameObject);

            interactable.DetectScreenGeometry();

            // Dedicated CinemachineCamera for the final terminal
            CinemachineCamera vcam = finalObj.GetComponentInChildren<CinemachineCamera>(true);
            if (vcam == null)
            {
                GameObject camGo = new GameObject("TerminalCamera_Final");
                Undo.RegisterCreatedObjectUndo(camGo, "Create Final Terminal Camera");
                camGo.transform.SetParent(finalObj, true);
                vcam = camGo.AddComponent<CinemachineCamera>();
            }
            vcam.transform.position = interactable.GetCameraWorldPosition(0.62f);
            vcam.transform.rotation = interactable.GetRecommendedCameraWorldRotation();
            vcam.Lens.FieldOfView = 50f;
            vcam.Priority.Value = 0;

            // Subtle emergency light guiding the player to the final terminal after Carcass-0 is defeated
            Light emergencyLight = null;
            Transform existingLightTr = finalObj.Find("EmergencyTerminalLight");
            if (existingLightTr != null) emergencyLight = existingLightTr.GetComponent<Light>();
            if (emergencyLight == null)
            {
                GameObject lightGo = new GameObject("EmergencyTerminalLight");
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Emergency Terminal Light");
                lightGo.transform.SetParent(finalObj, true);
                lightGo.transform.position = interactable.GetCameraWorldPosition(1.35f) + Vector3.up * 1.15f;
                emergencyLight = lightGo.AddComponent<Light>();
            }
            emergencyLight.type = LightType.Point;
            emergencyLight.color = new Color(0.95f, 0.42f, 0.14f);
            emergencyLight.intensity = 2.4f;
            emergencyLight.range = 8.5f;
            emergencyLight.enabled = false;

            // Collect Pavimento 1 chamber lights (P1 L, P1 meio, P1 R)
            List<Light> p1Lights = new List<Light>();
            foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == emergencyLight) continue;
                if (l.name.StartsWith("P1") || l.transform.position.y < 15f)
                {
                    p1Lights.Add(l);
                }
            }

            // Ensure EndgameOverlayUI exists in scene
            EndgameOverlayUI overlay = Object.FindAnyObjectByType<EndgameOverlayUI>();
            if (overlay == null)
            {
                GameObject ovGo = new GameObject("EndgameOverlayUI");
                Undo.RegisterCreatedObjectUndo(ovGo, "Create EndgameOverlayUI");
                overlay = ovGo.AddComponent<EndgameOverlayUI>();
            }

            interactable.AssignReferences(vcam, emergencyLight, p1Lights.ToArray(), overlay);
            EditorUtility.SetDirty(finalObj.gameObject);
            EditorUtility.SetDirty(vcam);
            EditorUtility.SetDirty(emergencyLight);
            EditorUtility.SetDirty(overlay);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[EndgameSceneSetup] Terminal 'final' configurado com {p1Lights.Count} luz(es) da Câmara de Prensagem e salvo em '{scene.name}'.");
        }

        [MenuItem("Tools/Eco/Testar Endgame (Desbloquear Terminal Final em Play Mode)")]
        public static void UnlockEndgameInPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[EndgameSceneSetup] Entre em Play Mode para usar este atalho de teste.");
                return;
            }

            EndgameTerminalInteractable term = Object.FindAnyObjectByType<EndgameTerminalInteractable>();
            if (term != null)
            {
                term.UnlockImmediately();
            }
            else
            {
                Debug.LogWarning("[EndgameSceneSetup] EndgameTerminalInteractable não encontrado na cena.");
            }
        }
    }
}
#endif
