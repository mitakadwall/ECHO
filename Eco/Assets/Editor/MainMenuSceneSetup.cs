#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Eco.Endgame;
using Eco.UI;

namespace Eco.Editor
{
    public static class MainMenuSceneSetup
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GameplayPath = "Assets/Scenes/Gameplay.unity";

        [MenuItem("Tools/Eco/Criar e Configurar Cena MainMenu")]
        public static void CreateAndWireMainMenu()
        {
            // 1. Update EndgameOverlayUI inside Gameplay.unity to point to "MainMenu"
            if (System.IO.File.Exists(GameplayPath))
            {
                Scene gameplayScene = EditorSceneManager.OpenScene(GameplayPath, OpenSceneMode.Single);
                EndgameOverlayUI overlay = Object.FindAnyObjectByType<EndgameOverlayUI>();
                if (overlay != null)
                {
                    SerializedObject so = new SerializedObject(overlay);
                    SerializedProperty prop = so.FindProperty("mainMenuSceneName");
                    if (prop != null)
                    {
                        prop.stringValue = "MainMenu";
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(overlay);
                    }
                }
                EditorSceneManager.MarkSceneDirty(gameplayScene);
                EditorSceneManager.SaveScene(gameplayScene);
            }

            // 2. Create clean MainMenu.unity scene
            Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.024f, 0.034f, 1f);
            camGo.AddComponent<AudioListener>();

            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            GameObject menuGo = new GameObject("MainMenuController");
            menuGo.AddComponent<MainMenuUI>();

            EditorSceneManager.SaveScene(menuScene, MainMenuPath);

            // 3. Register MainMenu (index 0) and Gameplay (index 1) in Build Settings
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GameplayPath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == MainMenuPath || existing.path == GameplayPath) continue;
                buildScenes.Add(existing);
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();

            Debug.Log("[MainMenuSceneSetup] Cena 'Assets/Scenes/MainMenu.unity' criada, conectada ao Endgame/Save e registrada no Build Settings (índice 0).");
        }
    }
}
#endif
