using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eco.Save
{
    /// <summary>
    /// Plain-data serializable snapshot of ECHO's game state.
    /// Stores only primitive values and IDs — never live engine object references.
    /// </summary>
    [Serializable]
    public class EcoSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtUtc = string.Empty;
        public string lastTerminalId = string.Empty;
        public string lastFloorLabel = string.Empty;

        public float playerPosX;
        public float playerPosY;
        public float playerPosZ;
        public float playerYaw;

        public float currentHealth = 100f;
        public float maxHealth = 100f;
        public bool hasWeapon;
        public bool hasShield;

        public bool pavimento5PuzzleSolved;
        public bool pavimento2PuzzleSolved;

        public List<string> syncedTerminalIds = new List<string>();
        public List<string> collectedFragmentIds = new List<string>();
        public List<string> collectedEnergyCellIds = new List<string>();
        public List<string> defeatedEnemyIds = new List<string>();

        public Vector3 GetPlayerPosition() => new Vector3(playerPosX, playerPosY, playerPosZ);

        public void SetPlayerPosition(Vector3 pos)
        {
            playerPosX = pos.x;
            playerPosY = pos.y;
            playerPosZ = pos.z;
        }
    }
}
