using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Eco.Combat;
using Eco.Interaction;
using Eco.Player;
using Eco.Puzzle;

namespace Eco.Save
{
    /// <summary>
    /// Atomic, versioned JSON save manager for ECHO's diegetic save terminals.
    /// Preserves exact player HP (low, medium, or high — never heals on load),
    /// position, unlocked equipment, puzzle states, and synced terminal records.
    /// </summary>
    public class EcoSaveManager : MonoBehaviour
    {
        public static EcoSaveManager Instance { get; private set; }

        /// <summary>
        /// Set by MainMenu when clicking "CONTINUAR REGISTRO" so Gameplay loads the saved state on Start,
        /// and cleared when clicking "INICIAR PROCESSO" for a fresh run.
        /// </summary>
        public static bool PendingLoadOnNextGameplay { get; set; }

        public const string DefaultSaveFileName = "eco_save.json";

        [Header("Persistence Settings")]
        [SerializeField] private string saveFileName = DefaultSaveFileName;
        [Tooltip("Desligado por padrão; o botão 'CONTINUAR REGISTRO' no Menu Principal ativa o carregamento via PendingLoadOnNextGameplay.")]
        [SerializeField] private bool autoLoadOnStart = false;

        private readonly HashSet<string> syncedTerminals = new HashSet<string>();
        private readonly HashSet<string> collectedFragments = new HashSet<string>();
        private readonly HashSet<string> collectedEnergyCells = new HashSet<string>();
        private readonly HashSet<string> defeatedEnemies = new HashSet<string>();
        private EcoSaveData lastLoadedData;

        public string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);
        public EcoSaveData LastSaveData => lastLoadedData;
        public bool HasSaveFile => File.Exists(SaveFilePath) || File.Exists(SaveFilePath + ".bak");

        public event Action<EcoSaveData> OnGameSaved;
        public event Action<EcoSaveData> OnGameLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            bool shouldLoad = PendingLoadOnNextGameplay || autoLoadOnStart;
            PendingLoadOnNextGameplay = false;

            if (shouldLoad && HasSaveFile)
            {
                LoadAndApply();
            }
        }

        public static string GetDefaultSavePath(string fileName = DefaultSaveFileName)
        {
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        public static bool HasSavedGameOnDisk(string fileName = DefaultSaveFileName)
        {
            string p = GetDefaultSavePath(fileName);
            return File.Exists(p) || File.Exists(p + ".bak");
        }

        public static EcoSaveData PeekSavedGame(string fileName = DefaultSaveFileName)
        {
            return LoadFromDisk(GetDefaultSavePath(fileName));
        }

        public static void DeleteSavedGameOnDisk(string fileName = DefaultSaveFileName)
        {
            string basePath = GetDefaultSavePath(fileName);
            string[] files = { basePath, basePath + ".bak", basePath + ".tmp" };
            foreach (string f in files)
            {
                if (File.Exists(f)) File.Delete(f);
            }
        }

        public bool IsTerminalSynced(string terminalId)
        {
            return !string.IsNullOrEmpty(terminalId) && syncedTerminals.Contains(terminalId);
        }

        public bool IsFragmentCollected(string fragmentId)
        {
            return !string.IsNullOrEmpty(fragmentId) && collectedFragments.Contains(fragmentId);
        }

        public void MarkFragmentCollected(string fragmentId)
        {
            if (!string.IsNullOrEmpty(fragmentId)) collectedFragments.Add(fragmentId);
        }

        public bool IsEnergyCellCollected(string cellId)
        {
            return !string.IsNullOrEmpty(cellId) && collectedEnergyCells.Contains(cellId);
        }

        public void MarkEnergyCellCollected(string cellId)
        {
            if (!string.IsNullOrEmpty(cellId)) collectedEnergyCells.Add(cellId);
        }

        public bool IsEnemyDefeated(string enemyId)
        {
            return !string.IsNullOrEmpty(enemyId) && defeatedEnemies.Contains(enemyId);
        }

        public void MarkEnemyDefeated(string enemyId)
        {
            if (!string.IsNullOrEmpty(enemyId)) defeatedEnemies.Add(enemyId);
        }

        public bool SaveFromTerminal(string terminalId, string floorLabel, GameObject playerObject)
        {
            EcoSaveData data = CaptureState(terminalId, floorLabel, playerObject);
            if (!WriteAtomic(SaveFilePath, data))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(terminalId))
            {
                syncedTerminals.Add(terminalId);
            }

            lastLoadedData = data;
            OnGameSaved?.Invoke(data);
            Debug.Log($"[EcoSaveManager] Progresso sincronizado no terminal '{terminalId}' ({floorLabel}) | HP={data.currentHealth:F0}/{data.maxHealth:F0} -> {SaveFilePath}");
            return true;
        }

        public EcoSaveData CaptureState(string terminalId, string floorLabel, GameObject playerObject)
        {
            var data = new EcoSaveData
            {
                version = EcoSaveData.CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                lastTerminalId = terminalId ?? string.Empty,
                lastFloorLabel = floorLabel ?? string.Empty
            };

            if (!string.IsNullOrEmpty(terminalId))
            {
                syncedTerminals.Add(terminalId);
            }
            data.syncedTerminalIds = new List<string>(syncedTerminals);
            data.collectedFragmentIds = new List<string>(collectedFragments);
            data.collectedEnergyCellIds = new List<string>(collectedEnergyCells);
            data.defeatedEnemyIds = new List<string>(defeatedEnemies);

            GameObject player = playerObject != null ? playerObject : FindPlayerObject();
            if (player != null)
            {
                data.SetPlayerPosition(player.transform.position);
                data.playerYaw = player.transform.eulerAngles.y;

                Health health = player.GetComponent<Health>();
                if (health != null)
                {
                    data.currentHealth = health.CurrentHealth;
                    data.maxHealth = health.MaxHealth;
                }

                PlayerCombat combat = player.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    data.hasWeapon = combat.HasWeapon;
                    data.hasShield = combat.HasShield;
                }
            }

            Pavimento5PuzzleController p5 = FindAnyObjectByType<Pavimento5PuzzleController>();
            if (p5 != null) data.pavimento5PuzzleSolved = p5.IsSolved;

            Pavimento2ColorPuzzleController p2 = FindAnyObjectByType<Pavimento2ColorPuzzleController>();
            if (p2 != null) data.pavimento2PuzzleSolved = p2.IsSolved;

            return data;
        }

        public bool LoadAndApply()
        {
            EcoSaveData data = LoadFromDisk(SaveFilePath);
            if (data == null)
            {
                Debug.LogWarning("[EcoSaveManager] Nenhum save válido encontrado para carregar.");
                return false;
            }

            ApplyState(data);
            lastLoadedData = data;
            OnGameLoaded?.Invoke(data);
            Debug.Log($"[EcoSaveManager] Save carregado do terminal '{data.lastTerminalId}' | HP restaurado exatamente para {data.currentHealth:F0}/{data.maxHealth:F0}.");
            return true;
        }

        public void ApplyState(EcoSaveData data)
        {
            if (data == null) return;

            syncedTerminals.Clear();
            if (data.syncedTerminalIds != null)
            {
                foreach (string id in data.syncedTerminalIds)
                {
                    if (!string.IsNullOrEmpty(id)) syncedTerminals.Add(id);
                }
            }

            collectedFragments.Clear();
            if (data.collectedFragmentIds != null)
            {
                foreach (string id in data.collectedFragmentIds)
                {
                    if (!string.IsNullOrEmpty(id)) collectedFragments.Add(id);
                }
            }

            collectedEnergyCells.Clear();
            if (data.collectedEnergyCellIds != null)
            {
                foreach (string id in data.collectedEnergyCellIds)
                {
                    if (!string.IsNullOrEmpty(id)) collectedEnergyCells.Add(id);
                }
            }

            defeatedEnemies.Clear();
            if (data.defeatedEnemyIds != null)
            {
                foreach (string id in data.defeatedEnemyIds)
                {
                    if (!string.IsNullOrEmpty(id)) defeatedEnemies.Add(id);
                }
            }

            GameObject player = FindPlayerObject();
            if (player != null)
            {
                PlayerController controller = player.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.TeleportTo(data.GetPlayerPosition(), data.playerYaw);
                }
                else
                {
                    player.transform.SetPositionAndRotation(data.GetPlayerPosition(), Quaternion.Euler(0f, data.playerYaw, 0f));
                }

                // Restore exact HP as saved (low, medium, or high — never refills to max).
                Health health = player.GetComponent<Health>();
                if (health != null)
                {
                    health.SetHealth(data.currentHealth);
                }

                PlayerCombat combat = player.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    if (data.hasWeapon) combat.EquipWeapon();
                    if (data.hasShield) combat.EquipShield();
                }

                // Hide already-collected equipment pickups in the world.
                EquipmentPickup[] pickups = FindObjectsByType<EquipmentPickup>(FindObjectsInactive.Include);
                foreach (EquipmentPickup pickup in pickups)
                {
                    if (pickup == null) continue;
                    if (pickup.Slot == EquipmentSlot.Weapon && data.hasWeapon) pickup.gameObject.SetActive(false);
                    if (pickup.Slot == EquipmentSlot.Shield && data.hasShield) pickup.gameObject.SetActive(false);
                }
            }

            if (data.pavimento5PuzzleSolved)
            {
                Pavimento5PuzzleController p5 = FindAnyObjectByType<Pavimento5PuzzleController>();
                if (p5 != null) p5.RestoreSolvedState();
            }

            if (data.pavimento2PuzzleSolved)
            {
                Pavimento2ColorPuzzleController p2 = FindAnyObjectByType<Pavimento2ColorPuzzleController>();
                if (p2 != null) p2.RestoreSolvedState();
            }
        }

        public static bool WriteAtomic(string targetPath, EcoSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(targetPath)) return false;

            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tmpPath = targetPath + ".tmp";
            string bakPath = targetPath + ".bak";

            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(tmpPath, json);

                if (File.Exists(targetPath))
                {
                    if (File.Exists(bakPath)) File.Delete(bakPath);
                    File.Move(targetPath, bakPath);
                }

                File.Move(tmpPath, targetPath);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EcoSaveManager] Falha ao gravar save em '{targetPath}': {ex.Message}");
                return false;
            }
        }

        public static EcoSaveData LoadFromDisk(string targetPath)
        {
            string[] candidates = { targetPath, targetPath + ".bak" };
            foreach (string path in candidates)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    string json = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(json)) continue;

                    EcoSaveData parsed = JsonUtility.FromJson<EcoSaveData>(json);
                    if (parsed == null || parsed.version <= 0) continue;

                    if (parsed.version > EcoSaveData.CurrentVersion)
                    {
                        Debug.LogWarning($"[EcoSaveManager] Versão de save ({parsed.version}) é mais nova que a suportada ({EcoSaveData.CurrentVersion}). Ignorando '{path}'.");
                        continue;
                    }

                    return parsed;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[EcoSaveManager] Arquivo corrompido ou ilegível '{path}': {ex.Message}. Tentando backup...");
                }
            }

            return null;
        }

        public void DeleteSaveFiles()
        {
            string[] files = { SaveFilePath, SaveFilePath + ".bak", SaveFilePath + ".tmp" };
            foreach (string f in files)
            {
                if (File.Exists(f)) File.Delete(f);
            }
            syncedTerminals.Clear();
            collectedFragments.Clear();
            collectedEnergyCells.Clear();
            defeatedEnemies.Clear();
            lastLoadedData = null;
        }

        private static GameObject FindPlayerObject()
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            return pc != null ? pc.gameObject : null;
        }
    }
}
