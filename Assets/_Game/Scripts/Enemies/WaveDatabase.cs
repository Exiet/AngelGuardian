using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// 生成位置类型
    /// </summary>
    public enum SpawnPositionType
    {
        AllDirections,      // 四面八方
        RallyPoint,         // 集结
        SkyDrop,            // 天空
        BackLine,           // 后排
        FrontLine,          // 前排
        LeftFlank,          // 左侧
        RightFlank,         // 右侧
        BossPosition        // Boss专属位置
    }

    /// <summary>
    /// 敌人类型枚举
    /// </summary>
    public enum EnemyType
    {
        E001_DemonSoldier,      // 恶魔小兵
        E002_FlyingImp,         // 飞行小鬼
        E003_RangedArcher,      // 远程弓箭手
        E004_SuicideBug,        // 自爆虫
        E005_ShieldGuard,       // 护盾卫士
        E006_EliteDemon,        // 精英强化恶魔
        E008_Summoner,          // Boss召唤师
        E009_TeleportGhost,     // 传送幽灵
        E010_WallDestroyer,     // 城墙破坏者
        E011_ShadowAssassin,    // 影子刺客
        E012_Cultist,           // 献祭教徒
        E014_StoneGiant,        // 石头巨人
        E015_Swarm,             // 蜂群
        E016_TimeMage,          // 时间法师
        E017_Cerberus,          // 地狱三头犬
        E018_PhantomArcher,     // 幻影弓手
        E019_Zombie,            // 腐烂僵尸
        E020_AbyssTentacle      // 深渊触手
    }

    /// <summary>
    /// 波次中单个敌人生成条目
    /// </summary>
    [Serializable]
    public class WaveEnemyEntry
    {
        [Tooltip("敌人类型")]
        public EnemyType enemyType;

        [Tooltip("生成数量")]
        public int count = 1;

        [Tooltip("生成位置类型")]
        public SpawnPositionType spawnPositionType = SpawnPositionType.AllDirections;

        [Tooltip("是否Boss")]
        public bool isBoss = false;

        [Tooltip("生成延迟（秒）")]
        public float spawnDelay = 0f;
    }

    /// <summary>
    /// 单个波次配置
    /// </summary>
    [Serializable]
    public class WaveConfig
    {
        [Tooltip("波次名称")]
        public string waveName = "Wave";

        [Tooltip("波次开始前等待时间（秒）")]
        public float preWaveDelay = 3f;

        [Tooltip("该波次中要生成的敌人列表")]
        public List<WaveEnemyEntry> enemyEntries = new List<WaveEnemyEntry>();

        [Tooltip("同屏最大敌人数")]
        public int maxEnemiesOnScreen = 20;

        [Tooltip("敌人生成间隔（秒）")]
        public float spawnInterval = 1.5f;

        [Tooltip("难度倍率 - HP")]
        public float difficultyHPMultiplier = 1f;

        [Tooltip("难度倍率 - 攻击力")]
        public float difficultyAttackMultiplier = 1f;

        [Tooltip("难度倍率 - 速度")]
        public float difficultySpeedMultiplier = 1f;

        [Tooltip("安全初始区域半径")]
        public float safeZoneRadius = 300f;
    }

    /// <summary>
    /// 波次数据库 - ScriptableObject存储所有波次配置
    /// </summary>
    [CreateAssetMenu(fileName = "WaveDatabase", menuName = "Angel Guardian/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        [Header("=== 波次配置 ===")]
        [Tooltip("所有波次")]
        public List<WaveConfig> waves = new List<WaveConfig>();

        [Header("=== 敌人生成预设 ===")]
        [Tooltip("每种敌人对应的预制体")]
        public List<EnemyPrefabMapping> enemyPrefabs = new List<EnemyPrefabMapping>();

        /// <summary>
        /// 根据敌人类型获取预制体
        /// </summary>
        public GameObject GetEnemyPrefab(EnemyType type)
        {
            var mapping = enemyPrefabs.Find(m => m.enemyType == type);
            return mapping?.prefab;
        }

        /// <summary>
        /// 获取指定波次的配置
        /// </summary>
        public WaveConfig GetWave(int index)
        {
            if (index < 0 || index >= waves.Count) return null;
            return waves[index];
        }

        /// <summary>
        /// 总波次数
        /// </summary>
        public int TotalWaves => waves.Count;
    }

    /// <summary>
    /// 敌人类型与预制体映射
    /// </summary>
    [Serializable]
    public class EnemyPrefabMapping
    {
        [Tooltip("敌人类型")]
        public EnemyType enemyType;

        [Tooltip("对应预制体")]
        public GameObject prefab;
    }
}
