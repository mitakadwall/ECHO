#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Eco.Combat;
using Eco.Enemies.Perception;
using Eco.Enemies.Striker;

namespace Eco.Editor
{
    public static class StrikerSceneSetup
    {
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string StrikerFolder = "Assets/Art/Enemies/Unit09_Striker";
        private const string StrikerFbxPath = StrikerFolder + "/striker.fbx";
        private const string StrikerMaterialsFolder = StrikerFolder + "/Materials";
        private const string AnimatorPath = StrikerFolder + "/Unit09StrikerAnimator.controller";
        private const string PrefabsRoot = "Assets/Prefabs";
        private const string EnemiesPrefabsFolder = "Assets/Prefabs/Enemies";
        private const string StrikerPrefabPath = EnemiesPrefabsFolder + "/Unit09_Striker.prefab";

        // Striker is compact yet imposing (~1.70m: slightly shorter than the 1.80m Player and 2.11m Watcher)
        private const float TargetStrikerHeight = 1.70f;
        private const float ChargeAnimationSpeed = 1.85f;

        [MenuItem("Tools/Eco/Configurar Unit-09 Striker")]
        public static void SetupStriker()
        {
            EnsureFolders();

            if (!File.Exists(StrikerFbxPath))
            {
                Debug.LogError($"[StrikerSceneSetup] FBX não encontrado em '{StrikerFbxPath}'.");
                return;
            }

            ConfigureFbxImport();
            AnimatorController controller = BuildAnimatorController();
            GameObject strikerPrefab = BuildStrikerPrefab(controller);

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Gameplay")
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            // Ensure NavMesh exists without touching existing Watchers or their waypoints
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.indices == null || tri.indices.Length == 0)
            {
                NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
                if (surface != null) surface.BuildNavMesh();
            }

            SpawnStrikersInPavimento3(strikerPrefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StrikerSceneSetup] Concluído! Prefab='{StrikerPrefabPath}', Cena='{scene.name}'.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder(PrefabsRoot, "Enemies");
            EnsureFolder(StrikerFolder, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureFbxImport()
        {
            AssetDatabase.ImportAsset(StrikerFbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter importer = AssetImporter.GetAtPath(StrikerFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[StrikerSceneSetup] Falha ao obter ModelImporter do striker.fbx.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            importer.ExtractTextures(StrikerMaterialsFolder);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(StrikerFbxPath);
            foreach (Object obj in subAssets)
            {
                if (obj is Material mat)
                {
                    string targetMatPath = $"{StrikerMaterialsFolder}/{SanitizeFileName(mat.name)}.mat";
                    if (!File.Exists(targetMatPath))
                    {
                        AssetDatabase.ExtractAsset(mat, targetMatPath);
                    }
                }
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                string lower = clips[i].name.ToLowerInvariant();
                bool shouldLoop = lower.Contains("idle") || lower.Contains("patrulha") || lower.Contains("investida");
                clips[i].loopTime = shouldLoop;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Shader urpLit = ResolveLitShader();
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { StrikerMaterialsFolder });
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (urpLit != null && mat.shader != urpLit)
                {
                    mat.shader = urpLit;
                }

                // Double-sided rendering prevents mirrored Blender arm/limb meshes from appearing hollow
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

                string lowerName = mat.name.ToLowerInvariant();
                if (lowerName.Contains("visor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    Color amber = new Color(1.0f, 0.65f, 0.08f, 1f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", amber);
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", amber * Mathf.LinearToGammaSpace(4.5f));
                }
                else if (lowerName.Contains("lamina"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    Color bladeGlow = new Color(1.0f, 0.32f, 0.04f, 1f);
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", bladeGlow * Mathf.LinearToGammaSpace(0.45f));
                }

                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
        }

        private static AnimatorController BuildAnimatorController()
        {
            if (File.Exists(AnimatorPath))
            {
                AssetDatabase.DeleteAsset(AnimatorPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsCharging", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimationClip idleClip = FindClip("idle");
            AnimationClip patrolClip = FindClip("patrulha");
            AnimationClip chargeClip = FindClip("investida");
            AnimationClip attackClip = FindClip("ataque");
            AnimationClip deathClip = FindClip("morte");

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = sm.AddState("Idle", new Vector3(240, 60, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState patrolState = sm.AddState("Patrol", new Vector3(520, 60, 0));
            patrolState.motion = patrolClip;

            AnimatorState chargeState = sm.AddState("Charge", new Vector3(380, 190, 0));
            chargeState.motion = chargeClip;
            chargeState.speed = ChargeAnimationSpeed; // Accelerated so investida reads as a real sprint

            AnimatorState attackState = sm.AddState("Attack", new Vector3(240, -80, 0));
            attackState.motion = attackClip;

            AnimatorState deadState = sm.AddState("Dead", new Vector3(520, -80, 0));
            deadState.motion = deathClip;

            // Idle <-> Patrol
            AnimatorStateTransition idleToPatrol = idleState.AddTransition(patrolState);
            idleToPatrol.hasExitTime = false;
            idleToPatrol.duration = 0.14f;
            idleToPatrol.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToPatrol.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCharging");

            AnimatorStateTransition patrolToIdle = patrolState.AddTransition(idleState);
            patrolToIdle.hasExitTime = false;
            patrolToIdle.duration = 0.14f;
            patrolToIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

            // Idle/Patrol <-> Charge
            AnimatorStateTransition idleToCharge = idleState.AddTransition(chargeState);
            idleToCharge.hasExitTime = false;
            idleToCharge.duration = 0.1f;
            idleToCharge.AddCondition(AnimatorConditionMode.If, 0f, "IsCharging");

            AnimatorStateTransition patrolToCharge = patrolState.AddTransition(chargeState);
            patrolToCharge.hasExitTime = false;
            patrolToCharge.duration = 0.1f;
            patrolToCharge.AddCondition(AnimatorConditionMode.If, 0f, "IsCharging");

            AnimatorStateTransition chargeToIdle = chargeState.AddTransition(idleState);
            chargeToIdle.hasExitTime = false;
            chargeToIdle.duration = 0.14f;
            chargeToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCharging");

            // AnyState -> Attack -> Idle
            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attackState);
            anyToAttack.hasExitTime = false;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.duration = 0.08f;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.92f;
            attackToIdle.duration = 0.12f;

            // AnyState -> Dead (terminal)
            AnimatorStateTransition anyToDead = sm.AddAnyStateTransition(deadState);
            anyToDead.hasExitTime = false;
            anyToDead.canTransitionToSelf = false;
            anyToDead.duration = 0.08f;
            anyToDead.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip FindClip(string keyword)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(StrikerFbxPath);
            foreach (Object obj in assets)
            {
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    if (clip.name.ToLowerInvariant().Contains(keyword))
                    {
                        return clip;
                    }
                }
            }

            Debug.LogWarning($"[StrikerSceneSetup] Clipe contendo '{keyword}' não encontrado em {StrikerFbxPath}.");
            return null;
        }

        private static GameObject BuildStrikerPrefab(AnimatorController controller)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StrikerFbxPath);
            GameObject root = new GameObject("Unit09_Striker");

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Measure raw FBX height and scale down to compact TargetStrikerHeight (~1.35m)
            Bounds rawBounds = CalculateHierarchyBounds(model);
            float scaleFactor = rawBounds.size.y > 0.05f ? (TargetStrikerHeight / rawBounds.size.y) : 0.4f;
            model.transform.localScale = Vector3.one * scaleFactor;

            Bounds bounds = CalculateHierarchyBounds(model);
            if (Mathf.Abs(bounds.min.y) > 0.01f)
            {
                model.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
                bounds = CalculateHierarchyBounds(model);
            }

            float modelHeight = Mathf.Clamp(bounds.size.y, 1.15f, 1.55f);
            float modelRadius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.34f, 0.38f, 0.58f);

            Animator anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = modelHeight;
            capsule.radius = modelRadius;
            capsule.center = new Vector3(0f, modelHeight * 0.5f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = modelRadius;
            agent.height = modelHeight;
            agent.speed = 2.0f;
            agent.angularSpeed = 420f;
            agent.acceleration = 16f;
            agent.stoppingDistance = 0.4f;

            // Visor renderer & VisorLight
            Renderer visorRenderer;
            int visorMatIndex;
            FindVisorRenderer(model.transform, out visorRenderer, out visorMatIndex);

            Transform headTransform = FindChildByNameContains(model.transform, "cabeca");
            GameObject visorLightGo = new GameObject("VisorLight");
            visorLightGo.transform.SetParent(headTransform != null ? headTransform : root.transform, false);
            visorLightGo.transform.localPosition = headTransform != null
                ? new Vector3(0f, 0.04f, 0.18f)
                : new Vector3(0f, modelHeight * 0.82f, 0.25f);

            Light visorLight = visorLightGo.AddComponent<Light>();
            visorLight.type = LightType.Point;
            visorLight.color = new Color(1.0f, 0.65f, 0.08f);
            visorLight.intensity = 2.2f;
            visorLight.range = 2.5f;

            // EnemyHealth configured for 150 HP
            EnemyHealth enemyHealth = root.AddComponent<EnemyHealth>();
            {
                SerializedObject so = new SerializedObject(enemyHealth);
                so.FindProperty("maxHealth").floatValue = 150f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            enemyHealth.ConfigureVisor(visorRenderer, visorMatIndex, visorLight);

            // Vision sensor tuned for Striker's lower eye height
            EnemyVisionSensor sensor = root.AddComponent<EnemyVisionSensor>();
            {
                SerializedObject so = new SerializedObject(sensor);
                so.FindProperty("viewDistance").floatValue = 20f;
                so.FindProperty("viewAngle").floatValue = 140f;
                so.FindProperty("proximityDetectDistance").floatValue = 4.5f;
                so.FindProperty("eyeOffset").vector3Value = new Vector3(0f, modelHeight * 0.82f, 0f);
                so.FindProperty("aimVerticalBias").floatValue = 0.05f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Blade telegraph light in front of chest/blades
            GameObject bladeLightGo = new GameObject("BladeTelegraphLight");
            bladeLightGo.transform.SetParent(root.transform, false);
            bladeLightGo.transform.localPosition = new Vector3(0f, modelHeight * 0.55f, 0.55f);
            Light bladeLight = bladeLightGo.AddComponent<Light>();
            bladeLight.type = LightType.Point;
            bladeLight.color = new Color(1.0f, 0.32f, 0.04f);
            bladeLight.intensity = 3.2f;
            bladeLight.range = 3.6f;
            bladeLight.enabled = false;

            Renderer[] bladeRenderers = FindBladeRenderers(model.transform);

            StrikerController strikerController = root.AddComponent<StrikerController>();
            strikerController.ConfigureSetup(
                "unit09_striker_prefab",
                anim,
                bladeRenderers,
                bladeLight,
                null);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, StrikerPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static void SpawnStrikersInPavimento3(GameObject strikerPrefab)
        {
            GameObject existingRoot = GameObject.Find("Enemies_Unit09_Strikers");
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }

            GameObject container = new GameObject("Enemies_Unit09_Strikers");
            Undo.RegisterCreatedObjectUndo(container, "Create Enemies_Unit09_Strikers");

            // Find Pavimento 3 baseline Y from known P3 landmarks
            Vector3 p3Ref = FindLandmarkPosition(new[] { "frag P3", "frag 2 P3", "celula de energia P3" }, new Vector3(-3f, 21.5f, -2f));
            List<Vector3> capsulePositions = FindPavimento3CapsulePositions(p3Ref.y);

            Vector3 anchorA;
            Vector3 anchorB;

            if (capsulePositions.Count >= 2)
            {
                anchorA = capsulePositions[0];
                anchorB = capsulePositions[capsulePositions.Count - 1];
                if (Vector3.Distance(anchorA, anchorB) < 3.5f)
                {
                    anchorB = anchorA + new Vector3(4.5f, 0f, 2.5f);
                }
            }
            else if (capsulePositions.Count == 1)
            {
                anchorA = capsulePositions[0] + new Vector3(2.2f, 0f, 0f);
                anchorB = capsulePositions[0] + new Vector3(-2.8f, 0f, 2.2f);
            }
            else
            {
                anchorA = p3Ref + new Vector3(3.0f, 0f, 2.0f);
                anchorB = p3Ref + new Vector3(-3.5f, 0f, -2.5f);
            }

            SpawnSingleStriker(container.transform, strikerPrefab, "Unit09_Striker_P3_A", "unit09_striker_p3_a", anchorA);
            SpawnSingleStriker(container.transform, strikerPrefab, "Unit09_Striker_P3_B", "unit09_striker_p3_b", anchorB);
        }

        private static List<Vector3> FindPavimento3CapsulePositions(float p3FloorY)
        {
            List<Vector3> results = new List<Vector3>();
            Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);

            foreach (Renderer r in allRenderers)
            {
                if (r == null) continue;
                Vector3 pos = r.bounds.center;
                if (Mathf.Abs(pos.y - p3FloorY) > 5.5f) continue;

                string lowerName = r.name.ToLowerInvariant();
                bool isCapsuleByName = lowerName.Contains("capsul") || lowerName.Contains("pod") || lowerName.Contains("tanque") || lowerName.Contains("tubo");
                bool isGlassContainment = false;

                Material[] mats = r.sharedMaterials;
                if (mats != null)
                {
                    foreach (Material m in mats)
                    {
                        if (m != null && m.name.Contains("Mat_Glass_Containment"))
                        {
                            isGlassContainment = true;
                            break;
                        }
                    }
                }

                if (isCapsuleByName || isGlassContainment)
                {
                    results.Add(new Vector3(pos.x, p3FloorY, pos.z));
                }
            }

            return results;
        }

        private static void SpawnSingleStriker(
            Transform parent,
            GameObject prefab,
            string instanceName,
            string enemyId,
            Vector3 referencePosition)
        {
            Vector3 spawnPos = SampleValidNavMeshPoint(referencePosition, 10f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.position = spawnPos;

            GameObject routeGo = new GameObject(instanceName + "_PatrolRoute");
            routeGo.transform.SetParent(parent, false);
            routeGo.transform.position = spawnPos;

            List<Transform> waypoints = new List<Transform>();
            Vector3[] offsets =
            {
                Vector3.zero,
                new Vector3(3.8f, 0f, 0f),
                new Vector3(0f, 0f, 3.8f),
                new Vector3(-3.8f, 0f, 0f),
                new Vector3(0f, 0f, -3.8f)
            };

            int wpIndex = 1;
            foreach (Vector3 offset in offsets)
            {
                if (waypoints.Count >= 3) break;
                Vector3 candidate = spawnPos + offset;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                {
                    if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1].position, hit.position) > 1.6f)
                    {
                        GameObject wp = new GameObject($"WP_{wpIndex++}");
                        wp.transform.SetParent(routeGo.transform, true);
                        wp.transform.position = hit.position;
                        waypoints.Add(wp.transform);
                    }
                }
            }

            StrikerController ctrl = instance.GetComponent<StrikerController>();
            if (ctrl != null)
            {
                SerializedObject so = new SerializedObject(ctrl);
                so.FindProperty("enemyId").stringValue = enemyId;
                SerializedProperty wpProp = so.FindProperty("patrolWaypoints");
                wpProp.arraySize = waypoints.Count;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    wpProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Bounds b = CalculateHierarchyBounds(instance);
            Debug.Log($"[StrikerSceneSetup] Spawned '{instanceName}' ({enemyId}) at {spawnPos} | height={b.size.y:F2}m | waypoints={waypoints.Count}.");
        }

        private static Vector3 FindLandmarkPosition(string[] candidateNames, Vector3 fallback)
        {
            Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (string targetName in candidateNames)
            {
                string lower = targetName.ToLowerInvariant();
                foreach (Transform t in all)
                {
                    if (t.name.Trim().ToLowerInvariant() == lower)
                    {
                        return t.position;
                    }
                }
            }
            return fallback;
        }

        private static Vector3 SampleValidNavMeshPoint(Vector3 origin, float maxRadius)
        {
            if (NavMesh.SamplePosition(origin, out NavMeshHit hit, maxRadius, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return origin;
        }

        private static Bounds CalculateHierarchyBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position + Vector3.up * 0.65f, new Vector3(0.8f, 1.35f, 0.8f));
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        private static Transform FindChildByNameContains(Transform parent, string keyword)
        {
            string lower = keyword.ToLowerInvariant();
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.name.ToLowerInvariant().Contains(lower))
                {
                    return t;
                }
            }
            return null;
        }

        private static void FindVisorRenderer(Transform root, out Renderer foundRenderer, out int foundMatIndex)
        {
            foundRenderer = null;
            foundMatIndex = 0;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null && mats[i].name.ToLowerInvariant().Contains("visor"))
                    {
                        foundRenderer = r;
                        foundMatIndex = i;
                        return;
                    }
                }
            }
        }

        private static Renderer[] FindBladeRenderers(Transform root)
        {
            List<Renderer> list = new List<Renderer>();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                Material[] mats = r.sharedMaterials;
                foreach (Material m in mats)
                {
                    if (m != null && m.name.ToLowerInvariant().Contains("lamina"))
                    {
                        list.Add(r);
                        break;
                    }
                }
            }
            return list.ToArray();
        }

        private static Shader ResolveLitShader()
        {
            RenderPipelineAsset activeRP = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            bool isURP = activeRP != null && activeRP.GetType().Name.Contains("Universal");
            Shader s = isURP ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            return s != null ? s : Shader.Find("Standard");
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }
}
#endif
