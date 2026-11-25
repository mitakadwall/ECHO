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
using Eco.Enemies.Combat;
using Eco.Enemies.Perception;
using Eco.Enemies.Watcher;

namespace Eco.Editor
{
    public static class WatcherSceneSetup
    {
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string WatcherFolder = "Assets/Art/Enemies/Unit04_Watcher";
        private const string WatcherFbxPath = WatcherFolder + "/watcher.fbx";
        private const string WatcherMaterialsFolder = WatcherFolder + "/Materials";
        private const string AnimatorPath = WatcherFolder + "/Unit04WatcherAnimator.controller";
        private const string PrefabsRoot = "Assets/Prefabs";
        private const string EnemiesPrefabsFolder = "Assets/Prefabs/Enemies";
        private const string ProjectilePrefabPath = EnemiesPrefabsFolder + "/WatcherPulseProjectile.prefab";
        private const string ProjectileMatPath = WatcherMaterialsFolder + "/Mat_WatcherPulseProjectile.mat";
        private const string WatcherPrefabPath = EnemiesPrefabsFolder + "/Unit04_Watcher.prefab";

        [MenuItem("Tools/Eco/Configurar Unit-04 Watcher e NavMesh")]
        public static void SetupWatcherAndNavMesh()
        {
            EnsureFolders();

            if (!File.Exists(WatcherFbxPath))
            {
                Debug.LogError($"[WatcherSceneSetup] FBX não encontrado em '{WatcherFbxPath}'.");
                return;
            }

            ConfigureFbxImport();
            AnimatorController controller = BuildAnimatorController();
            PulseProjectile projectilePrefab = BuildProjectilePrefab();
            GameObject watcherPrefab = BuildWatcherPrefab(controller, projectilePrefab);

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Gameplay")
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            NavMeshSurface surface = EnsureAndBakeNavMesh();
            SpawnWatchersInScene(watcherPrefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[WatcherSceneSetup] Concluído! NavMesh triângulos={tri.indices.Length / 3}, Prefab='{WatcherPrefabPath}', Cena='{scene.name}'.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder(PrefabsRoot, "Enemies");
            EnsureFolder(WatcherFolder, "Materials");
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
            AssetDatabase.ImportAsset(WatcherFbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter importer = AssetImporter.GetAtPath(WatcherFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[WatcherSceneSetup] Falha ao obter ModelImporter do watcher.fbx.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            // Extract embedded materials so we can enable URP Lit + Emission on the visor
            importer.ExtractTextures(WatcherMaterialsFolder);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(WatcherFbxPath);
            foreach (Object obj in subAssets)
            {
                if (obj is Material mat)
                {
                    string targetMatPath = $"{WatcherMaterialsFolder}/{SanitizeFileName(mat.name)}.mat";
                    if (!File.Exists(targetMatPath))
                    {
                        AssetDatabase.ExtractAsset(mat, targetMatPath);
                    }
                }
            }

            // Configure clip loop settings
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                string lower = clips[i].name.ToLowerInvariant();
                bool shouldLoop = lower.Contains("idle") || lower.Contains("patrulha") || lower.Contains("recuo");
                clips[i].loopTime = shouldLoop;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            // Upgrade extracted materials to URP Lit and enable emission on visor
            Shader urpLit = ResolveLitShader();
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { WatcherMaterialsFolder });
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

                if (mat.name.ToLowerInvariant().Contains("visor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    Color amber = new Color(1.0f, 0.65f, 0.08f, 1f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", amber);
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", amber * Mathf.LinearToGammaSpace(4.5f));
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
            controller.AddParameter("Alert", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimationClip idleClip = FindClip("idle");
            AnimationClip patrolClip = FindClip("patrulha");
            AnimationClip alertClip = FindClip("alerta");
            AnimationClip deathClip = FindClip("morte");

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = sm.AddState("Idle", new Vector3(240, 60, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState patrolState = sm.AddState("Patrol", new Vector3(520, 60, 0));
            patrolState.motion = patrolClip;

            AnimatorState alertState = sm.AddState("Alert", new Vector3(240, -80, 0));
            alertState.motion = alertClip;

            AnimatorState deadState = sm.AddState("Dead", new Vector3(520, -80, 0));
            deadState.motion = deathClip;

            // Idle <-> Patrol
            AnimatorStateTransition idleToPatrol = idleState.AddTransition(patrolState);
            idleToPatrol.hasExitTime = false;
            idleToPatrol.duration = 0.15f;
            idleToPatrol.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition patrolToIdle = patrolState.AddTransition(idleState);
            patrolToIdle.hasExitTime = false;
            patrolToIdle.duration = 0.15f;
            patrolToIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

            // AnyState -> Alert -> Idle
            AnimatorStateTransition anyToAlert = sm.AddAnyStateTransition(alertState);
            anyToAlert.hasExitTime = false;
            anyToAlert.canTransitionToSelf = false;
            anyToAlert.duration = 0.08f;
            anyToAlert.AddCondition(AnimatorConditionMode.If, 0f, "Alert");

            AnimatorStateTransition alertToIdle = alertState.AddTransition(idleState);
            alertToIdle.hasExitTime = true;
            alertToIdle.exitTime = 0.92f;
            alertToIdle.duration = 0.12f;

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
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(WatcherFbxPath);
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

            Debug.LogWarning($"[WatcherSceneSetup] Clipe contendo '{keyword}' não encontrado em {WatcherFbxPath}.");
            return null;
        }

        private static PulseProjectile BuildProjectilePrefab()
        {
            Shader urpLit = ResolveLitShader();
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ProjectileMatPath);
            if (mat == null)
            {
                mat = new Material(urpLit);
                AssetDatabase.CreateAsset(mat, ProjectileMatPath);
            }

            Color pulseColor = new Color(1.0f, 0.22f, 0.08f, 1f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", pulseColor);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", pulseColor * Mathf.LinearToGammaSpace(6.0f));
            EditorUtility.SetDirty(mat);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WatcherPulseProjectile";
            go.transform.localScale = new Vector3(0.09f, 0.09f, 0.34f);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.startWidth = 0.06f;
            trail.endWidth = 0.005f;
            trail.sharedMaterial = mat;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            PulseProjectile proj = go.AddComponent<PulseProjectile>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
            Object.DestroyImmediate(go);
            return saved.GetComponent<PulseProjectile>();
        }

        private static GameObject BuildWatcherPrefab(AnimatorController controller, PulseProjectile projectilePrefab)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WatcherFbxPath);
            GameObject root = new GameObject("Unit04_Watcher");

            // Instantiate model under root (normalized to ~2.1m height relative to 1.8m player)
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 0.58f;

            // Compute full bounds to size CapsuleCollider & NavMeshAgent accurately
            Bounds bounds = CalculateHierarchyBounds(model);
            float modelHeight = Mathf.Clamp(bounds.size.y, 1.4f, 2.5f);
            float modelRadius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.32f, 0.32f, 0.6f);

            // Lift/align model so feet sit at y=0
            if (Mathf.Abs(bounds.min.y) > 0.02f)
            {
                model.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
                bounds = CalculateHierarchyBounds(model);
            }

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
            agent.speed = 2.1f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.35f;

            // Find weapon ('arma') and attach MuzzlePoint at its front tip
            Transform weaponTransform = FindChildByNameContains(model.transform, "arma");
            Transform muzzleParent = weaponTransform != null ? weaponTransform : root.transform;

            GameObject muzzleGo = new GameObject("MuzzlePoint");
            muzzleGo.transform.SetParent(muzzleParent, false);
            if (weaponTransform != null)
            {
                Renderer weaponRenderer = weaponTransform.GetComponentInChildren<Renderer>();
                if (weaponRenderer != null)
                {
                    // Place muzzle at the furthest forward point of the weapon's world bounds
                    Bounds wb = weaponRenderer.bounds;
                    Vector3 tipWorld = new Vector3(
                        wb.center.x,
                        wb.center.y,
                        wb.max.z);
                    muzzleGo.transform.position = tipWorld;
                }
                else
                {
                    muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                }
            }
            else
            {
                muzzleGo.transform.localPosition = new Vector3(0.32f, modelHeight * 0.68f, 0.72f);
            }

            Light muzzleFlashLight = muzzleGo.AddComponent<Light>();
            muzzleFlashLight.type = LightType.Point;
            muzzleFlashLight.color = new Color(1.0f, 0.35f, 0.1f);
            muzzleFlashLight.intensity = 4.0f;
            muzzleFlashLight.range = 4.5f;
            muzzleFlashLight.enabled = false;

            // Find visor renderer & create subtle VisorLight attached to head/visor
            Renderer visorRenderer = null;
            int visorMatIndex = 0;
            FindVisorRenderer(model.transform, out visorRenderer, out visorMatIndex);

            Transform headTransform = FindChildByNameContains(model.transform, "cabeca");
            GameObject visorLightGo = new GameObject("VisorLight");
            visorLightGo.transform.SetParent(headTransform != null ? headTransform : root.transform, false);
            if (headTransform != null)
            {
                visorLightGo.transform.localPosition = new Vector3(0f, 0.05f, 0.2f);
            }
            else
            {
                visorLightGo.transform.localPosition = new Vector3(0f, modelHeight * 0.85f, 0.25f);
            }

            Light visorLight = visorLightGo.AddComponent<Light>();
            visorLight.type = LightType.Point;
            visorLight.color = new Color(1.0f, 0.65f, 0.08f);
            visorLight.intensity = 2.2f;
            visorLight.range = 2.6f;

            EnemyHealth enemyHealth = root.AddComponent<EnemyHealth>();
            enemyHealth.ConfigureVisor(visorRenderer, visorMatIndex, visorLight);

            root.AddComponent<EnemyVisionSensor>();

            WatcherController watcherController = root.AddComponent<WatcherController>();
            watcherController.ConfigureSetup(
                "unit04_watcher_prefab",
                anim,
                muzzleGo.transform,
                weaponTransform,
                muzzleFlashLight,
                projectilePrefab,
                null);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, WatcherPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static NavMeshSurface EnsureAndBakeNavMesh()
        {
            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                GameObject go = new GameObject("Setor07_NavMeshSurface");
                Undo.RegisterCreatedObjectUndo(go, "Create Setor07_NavMeshSurface");
                surface = go.AddComponent<NavMeshSurface>();
            }

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
            return surface;
        }

        private static void SpawnWatchersInScene(GameObject watcherPrefab)
        {
            // Clean up previous generated container so re-running is idempotent
            GameObject existingRoot = GameObject.Find("Enemies_Unit04_Watchers");
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }

            GameObject container = new GameObject("Enemies_Unit04_Watchers");
            Undo.RegisterCreatedObjectUndo(container, "Create Enemies_Unit04_Watchers");

            // Resolve reference positions on Pavimento 04 and Pavimento 03 from scene landmarks
            Vector3 p4AnchorA = FindLandmarkPosition(new[] { "frag P4", "celula de energia P4" }, new Vector3(0f, 6f, 0f));
            Vector3 p4AnchorB = FindSecondFloorAnchor("P4", p4AnchorA, 7.5f);
            Vector3 p3Anchor = FindLandmarkPosition(new[] { "frag P3", "frag 2 P3", "celula de energia P3" }, new Vector3(0f, 12f, 0f));

            SpawnSingleWatcher(container.transform, watcherPrefab, "Unit04_Watcher_P4_A", "unit04_watcher_p4_a", p4AnchorA);
            SpawnSingleWatcher(container.transform, watcherPrefab, "Unit04_Watcher_P4_B", "unit04_watcher_p4_b", p4AnchorB);
            SpawnSingleWatcher(container.transform, watcherPrefab, "Unit04_Watcher_P3_A", "unit04_watcher_p3_a", p3Anchor);
        }

        private static void SpawnSingleWatcher(
            Transform parent,
            GameObject prefab,
            string instanceName,
            string enemyId,
            Vector3 referencePosition)
        {
            Vector3 spawnPos = SampleValidNavMeshPoint(referencePosition, 12f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;
            instance.transform.position = spawnPos;

            // Build 3 valid NavMesh patrol waypoints around spawnPos
            GameObject routeGo = new GameObject(instanceName + "_PatrolRoute");
            routeGo.transform.SetParent(parent, false);
            routeGo.transform.position = spawnPos;

            List<Transform> waypoints = new List<Transform>();
            Vector3[] offsets =
            {
                Vector3.zero,
                new Vector3(4.5f, 0f, 0f),
                new Vector3(0f, 0f, 4.5f),
                new Vector3(-4.5f, 0f, 0f),
                new Vector3(0f, 0f, -4.5f)
            };

            int wpIndex = 1;
            foreach (Vector3 offset in offsets)
            {
                if (waypoints.Count >= 3) break;
                Vector3 candidate = spawnPos + offset;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                {
                    if (waypoints.Count == 0 || Vector3.Distance(waypoints[waypoints.Count - 1].position, hit.position) > 1.8f)
                    {
                        GameObject wp = new GameObject($"WP_{wpIndex++}");
                        wp.transform.SetParent(routeGo.transform, true);
                        wp.transform.position = hit.position;
                        waypoints.Add(wp.transform);
                    }
                }
            }

            WatcherController ctrl = instance.GetComponent<WatcherController>();
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

            Debug.Log($"[WatcherSceneSetup] Spawned '{instanceName}' ({enemyId}) at {spawnPos} with {waypoints.Count} patrol waypoints.");
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

        private static Vector3 FindSecondFloorAnchor(string floorPrefix, Vector3 firstAnchor, float desiredSeparation)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            Vector3 best = firstAnchor + new Vector3(desiredSeparation, 0f, 0f);
            float bestDiff = float.MaxValue;

            foreach (Light l in lights)
            {
                if (l == null || !l.name.StartsWith(floorPrefix)) continue;
                Vector3 candidate = new Vector3(l.transform.position.x, firstAnchor.y, l.transform.position.z);
                float dist = Vector3.Distance(firstAnchor, candidate);
                if (dist >= 4.5f && dist <= 16f)
                {
                    float diff = Mathf.Abs(dist - desiredSeparation);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        best = candidate;
                    }
                }
            }

            return best;
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
                return new Bounds(root.transform.position + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f));
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
