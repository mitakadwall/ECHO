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
using Eco.Endgame;
using Eco.Enemies.Boss;
using Eco.Enemies.Perception;
using Eco.UI;

namespace Eco.Editor
{
    public static class BossCarcassSceneSetup
    {
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
        private const string BossFolder = "Assets/Art/Enemies/Boss_Carcass0";
        private const string BossFbxPath = BossFolder + "/boss.fbx";
        private const string BossMaterialsFolder = BossFolder + "/Materials";
        private const string AnimatorPath = BossFolder + "/BossCarcassAnimator.controller";
        private const string PrefabsRoot = "Assets/Prefabs";
        private const string EnemiesPrefabsFolder = "Assets/Prefabs/Enemies";
        private const string BossPrefabPath = EnemiesPrefabsFolder + "/Boss_Carcass0.prefab";

        private const float TargetBossHeight = 3.40f;

        [MenuItem("Tools/Eco/Configurar Chefe Carcass-0 (Pavimento 01)")]
        public static void SetupBossCarcass()
        {
            EnsureFolders();

            if (!File.Exists(BossFbxPath))
            {
                Debug.LogError($"[BossCarcassSceneSetup] FBX não encontrado em '{BossFbxPath}'.");
                return;
            }

            ConfigureFbxImport();
            AnimatorController controller = BuildAnimatorController();
            GameObject bossPrefab = BuildBossPrefab(controller);

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Gameplay")
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.indices == null || tri.indices.Length == 0)
            {
                NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
                if (surface != null) surface.BuildNavMesh();
            }

            GameObject bossInstance = SpawnBossInPavimento1(bossPrefab);
            ConfigureBreakableColumnsInPavimento1();
            WireEndgameTerminalGating(bossInstance);
            EnsureBossHealthBarUI();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BossCarcassSceneSetup] Concluído! Prefab='{BossPrefabPath}', Cena='{scene.name}'.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder(PrefabsRoot, "Enemies");
            EnsureFolder(BossFolder, "Materials");
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
            AssetDatabase.ImportAsset(BossFbxPath, ImportAssetOptions.ForceUpdate);
            ModelImporter importer = AssetImporter.GetAtPath(BossFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[BossCarcassSceneSetup] Falha ao obter ModelImporter do boss.fbx.");
                return;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            importer.ExtractTextures(BossMaterialsFolder);
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(BossFbxPath);
            foreach (Object obj in subAssets)
            {
                if (obj is Material mat)
                {
                    string targetMatPath = $"{BossMaterialsFolder}/{SanitizeFileName(mat.name)}.mat";
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
                bool shouldLoop = lower.Contains("idle") || lower.Contains("caminhada") || lower.Contains("investida");
                clips[i].loopTime = shouldLoop;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Shader urpLit = ResolveLitShader();
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { BossMaterialsFolder });
            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                if (urpLit != null && mat.shader != urpLit)
                {
                    mat.shader = urpLit;
                }

                // Double-sided rendering prevents mirrored Blender limbs from appearing hollow
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);

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
            controller.AddParameter("Prepare", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Slam", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CancelAttack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("PhaseTransition", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorControllerParameter animSpeedParam = new AnimatorControllerParameter
            {
                name = "AnimSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            };
            controller.AddParameter(animSpeedParam);

            AnimationClip idleClip = FindClip("idle");
            AnimationClip walkClip = FindClip("caminhada");
            AnimationClip prepareClip = FindClip("preparacao");
            AnimationClip slamClip = FindClip("golpe");
            AnimationClip chargeClip = FindClip("investida");
            AnimationClip transitionClip = FindClip("transicao");
            AnimationClip deathClip = FindClip("morte");

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState idleState = sm.AddState("Idle", new Vector3(240, 60, 0));
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            AnimatorState walkState = sm.AddState("Walk", new Vector3(520, 60, 0));
            walkState.motion = walkClip;
            walkState.speedParameterActive = true;
            walkState.speedParameter = "AnimSpeed";

            AnimatorState chargeState = sm.AddState("Charge", new Vector3(380, 190, 0));
            chargeState.motion = chargeClip;
            chargeState.speed = 1.65f;
            chargeState.speedParameterActive = true;
            chargeState.speedParameter = "AnimSpeed";

            AnimatorState prepareState = sm.AddState("Prepare", new Vector3(120, -100, 0));
            prepareState.motion = prepareClip;
            prepareState.speedParameterActive = true;
            prepareState.speedParameter = "AnimSpeed";

            AnimatorState slamState = sm.AddState("Slam", new Vector3(380, -100, 0));
            slamState.motion = slamClip;
            slamState.speedParameterActive = true;
            slamState.speedParameter = "AnimSpeed";

            AnimatorState phaseTransState = sm.AddState("PhaseTransition", new Vector3(640, -100, 0));
            phaseTransState.motion = transitionClip;

            AnimatorState deadState = sm.AddState("Dead", new Vector3(640, 190, 0));
            deadState.motion = deathClip;

            // Idle <-> Walk
            AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCharging");

            AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.15f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");

            // Idle/Walk <-> Charge
            AnimatorStateTransition idleToCharge = idleState.AddTransition(chargeState);
            idleToCharge.hasExitTime = false;
            idleToCharge.duration = 0.1f;
            idleToCharge.AddCondition(AnimatorConditionMode.If, 0f, "IsCharging");

            AnimatorStateTransition walkToCharge = walkState.AddTransition(chargeState);
            walkToCharge.hasExitTime = false;
            walkToCharge.duration = 0.1f;
            walkToCharge.AddCondition(AnimatorConditionMode.If, 0f, "IsCharging");

            AnimatorStateTransition chargeToIdle = chargeState.AddTransition(idleState);
            chargeToIdle.hasExitTime = false;
            chargeToIdle.duration = 0.14f;
            chargeToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCharging");

            // Any -> Prepare -> Slam -> Idle (or Prepare -> CancelAttack -> Idle)
            AnimatorStateTransition anyToPrepare = sm.AddAnyStateTransition(prepareState);
            anyToPrepare.hasExitTime = false;
            anyToPrepare.canTransitionToSelf = false;
            anyToPrepare.duration = 0.1f;
            anyToPrepare.AddCondition(AnimatorConditionMode.If, 0f, "Prepare");

            AnimatorStateTransition prepareToSlam = prepareState.AddTransition(slamState);
            prepareToSlam.hasExitTime = false;
            prepareToSlam.duration = 0.08f;
            prepareToSlam.AddCondition(AnimatorConditionMode.If, 0f, "Slam");

            AnimatorStateTransition prepareCancelled = prepareState.AddTransition(idleState);
            prepareCancelled.hasExitTime = false;
            prepareCancelled.duration = 0.12f;
            prepareCancelled.AddCondition(AnimatorConditionMode.If, 0f, "CancelAttack");

            AnimatorStateTransition slamToIdle = slamState.AddTransition(idleState);
            slamToIdle.hasExitTime = true;
            slamToIdle.exitTime = 0.94f;
            slamToIdle.duration = 0.15f;

            // Any -> PhaseTransition -> Idle
            AnimatorStateTransition anyToPhase = sm.AddAnyStateTransition(phaseTransState);
            anyToPhase.hasExitTime = false;
            anyToPhase.canTransitionToSelf = false;
            anyToPhase.duration = 0.1f;
            anyToPhase.AddCondition(AnimatorConditionMode.If, 0f, "PhaseTransition");

            AnimatorStateTransition phaseToIdle = phaseTransState.AddTransition(idleState);
            phaseToIdle.hasExitTime = true;
            phaseToIdle.exitTime = 0.94f;
            phaseToIdle.duration = 0.15f;

            // Any -> Dead (terminal)
            AnimatorStateTransition anyToDead = sm.AddAnyStateTransition(deadState);
            anyToDead.hasExitTime = false;
            anyToDead.canTransitionToSelf = false;
            anyToDead.duration = 0.1f;
            anyToDead.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip FindClip(string keyword)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(BossFbxPath);
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

            Debug.LogWarning($"[BossCarcassSceneSetup] Clipe contendo '{keyword}' não encontrado em {BossFbxPath}.");
            return null;
        }

        private static GameObject BuildBossPrefab(AnimatorController controller)
        {
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BossFbxPath);
            GameObject root = new GameObject("Boss_Carcass0");

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Scale to imposing ~3.40m height
            Bounds rawBounds = CalculateHierarchyBounds(model);
            float scaleFactor = rawBounds.size.y > 0.05f ? (TargetBossHeight / rawBounds.size.y) : 1f;
            model.transform.localScale = Vector3.one * scaleFactor;

            Bounds bounds = CalculateHierarchyBounds(model);
            if (Mathf.Abs(bounds.min.y) > 0.02f)
            {
                model.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
                bounds = CalculateHierarchyBounds(model);
            }

            float modelHeight = Mathf.Clamp(bounds.size.y, 2.8f, 3.8f);
            float modelRadius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.34f, 0.85f, 1.25f);

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
            agent.speed = 2.6f;
            agent.angularSpeed = 240f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 2.6f;

            // EnemyHealth configured to 220 HP (no face visor on Carcass-0)
            EnemyHealth enemyHealth = root.AddComponent<EnemyHealth>();
            {
                SerializedObject so = new SerializedObject(enemyHealth);
                so.FindProperty("maxHealth").floatValue = 220f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Vision sensor
            EnemyVisionSensor sensor = root.AddComponent<EnemyVisionSensor>();
            {
                SerializedObject so = new SerializedObject(sensor);
                so.FindProperty("viewDistance").floatValue = 28f;
                so.FindProperty("viewAngle").floatValue = 180f;
                so.FindProperty("proximityDetectDistance").floatValue = 14f;
                so.FindProperty("eyeOffset").vector3Value = new Vector3(0f, modelHeight * 0.72f, 0f);
                so.FindProperty("aimVerticalBias").floatValue = 0.05f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Attach steam + ember ParticleSystems to the 3 rear pistons (tubo.C, tubo.L, tubo.R)
            string[] pistonNames = { "tubo.C", "tubo.L", "tubo.R" };
            List<ParticleSystem> steamList = new List<ParticleSystem>();
            List<ParticleSystem> emberList = new List<ParticleSystem>();

            Material steamMat = CreateParticleMaterial(new Color(0.82f, 0.86f, 0.9f, 0.45f));
            Material emberMat = CreateParticleMaterial(new Color(1.0f, 0.36f, 0.06f, 0.95f));

            foreach (string pName in pistonNames)
            {
                Transform pistonBone = FindChildByExactName(model.transform, pName);
                Transform parentTr = pistonBone != null ? pistonBone : root.transform;

                ParticleSystem steam = CreatePistonParticle(parentTr, $"Steam_{pName}", steamMat, isEmber: false);
                ParticleSystem ember = CreatePistonParticle(parentTr, $"Embers_{pName}", emberMat, isEmber: true);

                if (pistonBone == null)
                {
                    float xOff = pName.EndsWith(".L") ? -0.45f : (pName.EndsWith(".R") ? 0.45f : 0f);
                    steam.transform.localPosition = new Vector3(xOff, modelHeight * 0.72f, -0.55f);
                    ember.transform.localPosition = new Vector3(xOff, modelHeight * 0.72f, -0.55f);
                }

                steamList.Add(steam);
                emberList.Add(ember);
            }

            // Overheat back light behind the 3 pistons (Phase 2)
            GameObject backLightGo = new GameObject("OverheatBackLight");
            backLightGo.transform.SetParent(root.transform, false);
            backLightGo.transform.localPosition = new Vector3(0f, modelHeight * 0.68f, -0.85f);
            Light backLight = backLightGo.AddComponent<Light>();
            backLight.type = LightType.Point;
            backLight.color = new Color(1.0f, 0.32f, 0.06f);
            backLight.intensity = 3.5f;
            backLight.range = 5.5f;
            backLight.enabled = false;

            // Sync prepareDuration / slamDuration to actual clip lengths
            AnimationClip prepClip = FindClip("preparacao");
            AnimationClip golpeClip = FindClip("golpe");
            AnimationClip transClip = FindClip("transicao");

            BossCarcassController bossCtrl = root.AddComponent<BossCarcassController>();
            bossCtrl.ConfigureSetup(
                BossCarcassController.DefaultBossId,
                anim,
                steamList.ToArray(),
                emberList.ToArray(),
                backLight);

            {
                SerializedObject so = new SerializedObject(bossCtrl);
                if (prepClip != null) so.FindProperty("prepareDuration").floatValue = prepClip.length;
                if (golpeClip != null) so.FindProperty("slamDuration").floatValue = golpeClip.length;
                if (transClip != null) so.FindProperty("phaseTransitionDuration").floatValue = transClip.length;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static ParticleSystem CreatePistonParticle(Transform parent, string name, Material mat, bool isEmber)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(-35f, 180f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = isEmber
                ? new ParticleSystem.MinMaxCurve(0.45f, 0.95f)
                : new ParticleSystem.MinMaxCurve(0.75f, 1.35f);
            main.startSpeed = isEmber
                ? new ParticleSystem.MinMaxCurve(2.2f, 5.2f)
                : new ParticleSystem.MinMaxCurve(1.0f, 2.4f);
            main.startSize = isEmber
                ? new ParticleSystem.MinMaxCurve(0.05f, 0.13f)
                : new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startColor = isEmber
                ? new Color(1.0f, 0.38f, 0.08f, 0.95f)
                : new Color(0.84f, 0.88f, 0.92f, 0.38f);
            main.gravityModifier = isEmber ? 0.55f : -0.22f;

            var emission = ps.emission;
            emission.rateOverTime = isEmber ? 0f : 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = isEmber ? 28f : 18f;
            shape.radius = 0.12f;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            if (rend != null && mat != null)
            {
                rend.sharedMaterial = mat;
            }

            return ps;
        }

        private static Material CreateParticleMaterial(Color color)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s == null) return null;

            string matName = color.r > 0.95f ? "Mat_Carcass_Embers.mat" : "Mat_Carcass_Steam.mat";
            string matPath = $"{BossMaterialsFolder}/{matName}";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(s);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject SpawnBossInPavimento1(GameObject bossPrefab)
        {
            GameObject existing = GameObject.Find("Boss_Carcass0");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            // Center of Pavimento 01 (Câmara de Prensagem), slightly in front of 'final' terminal (0, 3.35, -9.80)
            Vector3 desiredPos = new Vector3(0f, 0.5f, -1.5f);
            if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 12f, NavMesh.AllAreas))
            {
                desiredPos = hit.position;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
            instance.name = "Boss_Carcass0";
            instance.transform.position = desiredPos;
            instance.transform.rotation = Quaternion.LookRotation(Vector3.forward); // Facing into the chamber

            Bounds b = CalculateHierarchyBounds(instance);
            Debug.Log($"[BossCarcassSceneSetup] Spawned 'Boss_Carcass0' at {desiredPos} | height={b.size.y:F2}m.");
            return instance;
        }

        private static void ConfigureBreakableColumnsInPavimento1()
        {
            GameObject colObj = GameObject.Find("colunas quebraveis");
            if (colObj != null)
            {
                BreakableColumn bc = colObj.GetComponent<BreakableColumn>();
                if (bc == null)
                {
                    bc = Undo.AddComponent<BreakableColumn>(colObj);
                }
                EditorUtility.SetDirty(colObj);
                Debug.Log("[BossCarcassSceneSetup] BreakableColumn configurado em 'colunas quebraveis' (30 dano ao boss ao colidir).");
            }
        }

        private static void WireEndgameTerminalGating(GameObject bossInstance)
        {
            EndgameTerminalInteractable terminal = Object.FindAnyObjectByType<EndgameTerminalInteractable>();
            if (terminal == null)
            {
                Debug.LogWarning("[BossCarcassSceneSetup] EndgameTerminalInteractable não encontrado na cena.");
                return;
            }

            EnemyHealth bossHealth = bossInstance != null ? bossInstance.GetComponent<EnemyHealth>() : null;
            SerializedObject so = new SerializedObject(terminal);
            so.FindProperty("bossEnemyHealth").objectReferenceValue = bossHealth;
            so.FindProperty("unlockImmediatelyForTesting").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(terminal);

            Debug.Log("[BossCarcassSceneSetup] Terminal final bloqueado até a morte do Carcass-0.");
        }

        private static void EnsureBossHealthBarUI()
        {
            BossHealthBarUI existing = Object.FindAnyObjectByType<BossHealthBarUI>();
            if (existing == null)
            {
                GameObject go = new GameObject("BossHealthBarUI");
                Undo.RegisterCreatedObjectUndo(go, "Create BossHealthBarUI");
                go.AddComponent<BossHealthBarUI>();
            }
        }

        private static Bounds CalculateHierarchyBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position + Vector3.up * 1.7f, new Vector3(1.8f, 3.4f, 1.8f));
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        private static Transform FindChildByExactName(Transform parent, string exactName)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.name == exactName) return t;
            }
            return null;
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
