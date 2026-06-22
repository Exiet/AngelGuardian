using UnityEngine;

namespace AngelGuardian.Core
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Angel Guardian/Game Config", order = 0)]
    public class GameConfig : ScriptableObject
    {
        [Header("========== Map Settings ==========")]
        [Tooltip("Total map size (square). Both width and height.")]
        public int MapSize = 3000;

        [Header("Room Generation")]
        [Tooltip("Minimum number of rooms to generate.")]
        [Range(1, 50)]
        public int MinRooms = 8;

        [Tooltip("Maximum number of rooms to generate.")]
        [Range(1, 50)]
        public int MaxRooms = 14;

        [Tooltip("Minimum size of a single room in world units.")]
        [Range(100, 2000)]
        public int MinRoomSize = 400;

        [Header("Corridor")]
        [Tooltip("Width of connecting corridors.")]
        [Range(80, 300)]
        public int CorridorWidth = 150;

        [Tooltip("Allowed variance range for corridor width.")]
        public Vector2Int CorridorWidthRange = new Vector2Int(120, 180);

        [Header("Door")]
        [Tooltip("Probability of a door spawning between two adjacent rooms (0.0 - 1.0).")]
        [Range(0f, 1f)]
        public float DoorProbability = 0.4f;

        [Header("Special Rooms")]
        [Tooltip("Guaranteed number of safe rooms per map.")]
        [Range(0, 5)]
        public int SafeRoomGuarantee = 1;

        [Tooltip("Guaranteed number of loop corridors (cycles) per map.")]
        [Range(0, 5)]
        public int LoopCorridorGuarantee = 1;

        [Header("Destructibles")]
        [Tooltip("Density of destructible objects in rooms (fraction of room area).")]
        [Range(0f, 0.2f)]
        public float DestructibleDensity = 0.03f;

        [Header("========== Player / Start Settings ==========")]
        [Tooltip("Radius around the spawn point that is guaranteed safe (no enemies).")]
        [Range(50, 1000)]
        public int SafeStartRadius = 300;

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
        public float ComboCdMin = 1.0f;

        [Header("Enemies")]
        [Tooltip("Hard cap on total active enemies in the scene.")]
        [Range(50, 2000)]
        public int EnemyCountCap = 600;

        [Header("========== Baby / Mental Settings ==========")]
        [Tooltip("Maximum mental power (HP) for the Baby.")]
        [Range(10, 500)]
        public float BabyMaxMentalPower = 100f;

        [Tooltip("Interval in seconds between emotion state refresh ticks.")]
        [Range(0.1f, 5f)]
        public float EmotionTickRate = 0.5f;

        [Header("========== Progression Settings ==========")]

        [Header("Cards")]
        [Tooltip("Maximum number of cards the player can hold.")]
        [Range(4, 30)]
        public int MaxCards = 10;

        [Header("EXP")]
        [Tooltip("Multiplier applied to experience gain from all sources.")]
        [Range(0.1f, 10f)]
        public float ExpGrowthMultiplier = 1.0f;

        // ──────────── Convenience Properties ────────────

        public Vector2 MapSizeVector => new Vector2(MapSize, MapSize);
        public float MapHalfSize => MapSize * 0.5f;
        public int ClampedCorridorWidth => Mathf.Clamp(CorridorWidth, CorridorWidthRange.x, CorridorWidthRange.y);

        private void OnValidate()
        {
            if (MinRooms > MaxRooms) MinRooms = MaxRooms;
            CorridorWidth = Mathf.Clamp(CorridorWidth, CorridorWidthRange.x, CorridorWidthRange.y);
            if (CorridorWidthRange.x > CorridorWidthRange.y)
                CorridorWidthRange = new Vector2Int(CorridorWidthRange.y, CorridorWidthRange.x);
            MapSize = Mathf.Max(100, MapSize);
            MinRoomSize = Mathf.Max(50, MinRoomSize);
            SafeStartRadius = Mathf.Max(0, SafeStartRadius);
            DoorProbability = Mathf.Clamp01(DoorProbability);
            DestructibleDensity = Mathf.Clamp01(DestructibleDensity);
            MaxProjectiles = Mathf.Max(1, MaxProjectiles);
            MaxWeapons = Mathf.Max(1, MaxWeapons);
            EnemyCountCap = Mathf.Max(1, EnemyCountCap);
            MaxCards = Mathf.Max(1, MaxCards);
            ComboCdMin = Mathf.Max(0f, ComboCdMin);
            EmotionTickRate = Mathf.Max(0.01f, EmotionTickRate);
            BabyMaxMentalPower = Mathf.Max(1f, BabyMaxMentalPower);
            ExpGrowthMultiplier = Mathf.Max(0.01f, ExpGrowthMultiplier);
        }
    }
}
