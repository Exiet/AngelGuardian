using UnityEngine;

namespace AngelGuardian.Core
{
    /// <summary>
    /// ScriptableObject containing all global configuration parameters for Angel Guardian.
    /// Create via: Assets → Create → Angel Guardian → Game Config
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Angel Guardian/Game Config", order = 0)]
    public class GameConfig : ScriptableObject
    {
        [Header("========== Map Settings ==========")]
        [Tooltip("Total map size (square). Both width and height.")]
        public int mapSize = 3000;

        [Header("Room Generation")]
        [Tooltip("Minimum number of rooms to generate.")]
        [Range(1, 50)]
        public int minRooms = 8;

        [Tooltip("Maximum number of rooms to generate.")]
        [Range(1, 50)]
        public int maxRooms = 14;

        [Tooltip("Minimum size of a single room in world units.")]
        [Range(100, 2000)]
        public int minRoomSize = 400;

        [Header("Corridor")]
        [Tooltip("Width of connecting corridors.")]
        [Range(80, 300)]
        public int corridorWidth = 150;

        [Tooltip("Allowed variance range for corridor width.")]
        public Vector2Int corridorWidthRange = new Vector2Int(120, 180);

        [Header("Door")]
        [Tooltip("Probability of a door spawning between two adjacent rooms (0.0 - 1.0).")]
        [Range(0f, 1f)]
        public float doorProbability = 0.4f;

        [Header("Special Rooms")]
        [Tooltip("Guaranteed number of safe rooms per map.")]
        [Range(0, 5)]
        public int safeRoomGuarantee = 1;

        [Tooltip("Guaranteed number of loop corridors (cycles) per map.")]
        [Range(0, 5)]
        public int loopCorridorGuarantee = 1;

        [Header("Destructibles")]
        [Tooltip("Density of destructible objects in rooms (fraction of room area).")]
        [Range(0f, 0.2f)]
        public float destructibleDensity = 0.03f;

        [Header("========== Player / Start Settings ==========")]
        [Tooltip("Radius around the spawn point that is guaranteed safe (no enemies).")]
        [Range(50, 1000)]
        public int safeStartRadius = 300;

        [Header("========== Combat Settings ==========")]

        [Header("Projectiles")]
        [Tooltip("Absolute hard cap on total active projectiles in the scene.")]
        [Range(10, 200)]
        public int MaxProjectiles = 45;

        [Header("Weapons")]
        [Tooltip("Maximum number of weapons the player can hold simultaneously.")]
        [Range(1, 12)]
        public int MaxWeapons = 6;

        [Header("Combo")]
        [Tooltip("Minimum cooldown in seconds between combo activations.")]
        [Range(0.1f, 10f)]
        public float comboCdMin = 1.0f;

        [Header("Enemies")]
        [Tooltip("Hard cap on total active enemies in the scene.")]
        [Range(50, 2000)]
        public int enemyCountCap = 600;

        [Header("========== Baby / Mental Settings ==========")]
        [Tooltip("Maximum mental power (HP) for the Baby.")]
        [Range(10, 500)]
        public float BabyMaxMentalPower = 100f;

        [Tooltip("Interval in seconds between emotion state refresh ticks.")]
        [Range(0.1f, 5f)]
        public float emotionTickRate = 0.5f;

        [Header("========== Progression Settings ==========")]

        [Header("Cards")]
        [Tooltip("Maximum number of cards the player can hold.")]
        [Range(4, 30)]
        public int maxCards = 10;

        [Header("EXP")]
        [Tooltip("Multiplier applied to experience gain from all sources.")]
        [Range(0.1f, 10f)]
        public float expGrowthMultiplier = 1.0f;

        // ──────────── Convenience Properties ────────────

        /// <summary>
        /// Returns the map size as a Vector2 for easy use with Rect operations.
        /// </summary>
        public Vector2 MapSizeVector => new Vector2(mapSize, mapSize);

        /// <summary>
        /// Returns half the map size (the "radius" of the map from center).
        /// </summary>
        public float MapHalfSize => mapSize * 0.5f;

        /// <summary>
        /// Returns the corridor width clamped to the configured range.
        /// </summary>
        public int ClampedCorridorWidth => Mathf.Clamp(corridorWidth, corridorWidthRange.x, corridorWidthRange.y);

        // ──────────── Validation ────────────

        private void OnValidate()
        {
            // Ensure min <= max for rooms
            if (minRooms > maxRooms)
                minRooms = maxRooms;

            // Clamp corridor width
            corridorWidth = Mathf.Clamp(corridorWidth, corridorWidthRange.x, corridorWidthRange.y);

            // Ensure range ordering
            if (corridorWidthRange.x > corridorWidthRange.y)
                corridorWidthRange = new Vector2Int(corridorWidthRange.y, corridorWidthRange.x);

            // Positive sizes
            mapSize = Mathf.Max(100, mapSize);
            minRoomSize = Mathf.Max(50, minRoomSize);
            safeStartRadius = Mathf.Max(0, safeStartRadius);

            // Range [0, 1]
            doorProbability = Mathf.Clamp01(doorProbability);
            destructibleDensity = Mathf.Clamp01(destructibleDensity);

            // Positive caps
            MaxProjectiles = Mathf.Max(1, MaxProjectiles);
            MaxWeapons = Mathf.Max(1, MaxWeapons);
            enemyCountCap = Mathf.Max(1, enemyCountCap);
            maxCards = Mathf.Max(1, maxCards);

            // Positive timers / rates
            comboCdMin = Mathf.Max(0f, comboCdMin);
            emotionTickRate = Mathf.Max(0.01f, emotionTickRate);
            BabyMaxMentalPower = Mathf.Max(1f, BabyMaxMentalPower);
            expGrowthMultiplier = Mathf.Max(0.01f, expGrowthMultiplier);
        }
    }
}
