using UnityEngine;
using System;
using System.Collections.Generic;

namespace AngelGuardian.Data
{
    // ============================================================
    // WaveEnemyConfig — 单个波次中的敌人配置
    // ============================================================

    [Serializable]
    public class WaveEnemyConfig
    {
        public string enemyId;    // 敌人ID (E001-E020, B01-B02)
        public int count;         // 数量
    }

    // ============================================================
    // SpawnLocation
    // ============================================================

    public enum SpawnLocation
    {
        Top,
        Bottom,
        Left,
        Right,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        AllSides,
        Center,
        Random
    }

    // ============================================================
    // WaveData
    // ============================================================

    [Serializable]
    public class WaveData
    {
        public int waveNumber;                          // 波次编号 (1-25)
        public List<WaveEnemyConfig> enemyConfigs = new List<WaveEnemyConfig>();  // 敌人配置
        public SpawnLocation spawnLocation;             // 生成位置
        public string keyEvent;                         // 关键事件描述
        public int recommendedLevel;                    // 推荐等级
        public float spawnInterval;                     // 生成间隔(秒)
        public float waveDuration;                      // 波次预估时长(秒)
        public bool isBossWave;                         // 是否为Boss波次
        public string[] rewardPreview;                  // 奖励预览
    }

    // ============================================================
    // WaveDatabase ScriptableObject
    // ============================================================

    [CreateAssetMenu(fileName = "WaveDatabase", menuName = "AngelGuardian/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        public List<WaveData> waves = new List<WaveData>();

        // ============================================================
        // Query helpers
        // ============================================================

        public WaveData GetWave(int waveNumber) => waves.Find(w => w.waveNumber == waveNumber);

        public List<WaveData> GetBossWaves() => waves.FindAll(w => w.isBossWave);

        public List<WaveData> GetWavesByLevelRange(int minLevel, int maxLevel)
        {
            return waves.FindAll(w => w.recommendedLevel >= minLevel && w.recommendedLevel <= maxLevel);
        }

        // ============================================================
        // Initialization
        // ============================================================

        private void OnEnable()
        {
            if (waves == null || waves.Count == 0)
            {
                waves = CreateAllWaves();
            }
        }

        // ============================================================
        // All 25 wave definitions
        // ============================================================

        private static List<WaveData> CreateAllWaves()
        {
            return new List<WaveData>
            {
                // =============================================
                // 第一阶段：入门 (Waves 1-5, Lv.1-3)
                // =============================================
                new WaveData
                {
                    waveNumber = 1, recommendedLevel = 1,
                    spawnLocation = SpawnLocation.Top,
                    spawnInterval = 2f, waveDuration = 25f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E001", count = 5 },
                        new WaveEnemyConfig { enemyId = "E017", count = 8 }
                    },
                    keyEvent = "初次遭遇婴灵。天使感受到深渊的气息。教程提示：基础移动与攻击。",
                    rewardPreview = new[] { "圣光手枪(W01)", "卡牌A-01" }
                },
                new WaveData
                {
                    waveNumber = 2, recommendedLevel = 1,
                    spawnLocation = SpawnLocation.TopRight,
                    spawnInterval = 1.8f, waveDuration = 28f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E001", count = 6 },
                        new WaveEnemyConfig { enemyId = "E002", count = 3 },
                        new WaveEnemyConfig { enemyId = "E005", count = 2 }
                    },
                    keyEvent = "快速婴灵首次出现。教程提示：闪避与走位。",
                    rewardPreview = new[] { "金币x100", "卡牌A-02" }
                },
                new WaveData
                {
                    waveNumber = 3, recommendedLevel = 2,
                    spawnLocation = SpawnLocation.Left,
                    spawnInterval = 1.6f, waveDuration = 30f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E001", count = 4 },
                        new WaveEnemyConfig { enemyId = "E003", count = 3 },
                        new WaveEnemyConfig { enemyId = "E013", count = 2 },
                        new WaveEnemyConfig { enemyId = "E017", count = 6 }
                    },
                    keyEvent = "远程婴灵首次出现。教程提示：远程敌人应对策略。",
                    rewardPreview = new[] { "金币x150", "卡牌C-01" }
                },
                new WaveData
                {
                    waveNumber = 4, recommendedLevel = 2,
                    spawnLocation = SpawnLocation.BottomRight,
                    spawnInterval = 1.5f, waveDuration = 32f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E002", count = 4 },
                        new WaveEnemyConfig { enemyId = "E004", count = 4 },
                        new WaveEnemyConfig { enemyId = "E006", count = 3 },
                        new WaveEnemyConfig { enemyId = "E018", count = 6 }
                    },
                    keyEvent = "跳跃婴灵和蝙蝠婴灵首次出现。地形的重要性开始体现。",
                    rewardPreview = new[] { "金币x200", "圣光魔杖(W16)" }
                },
                new WaveData
                {
                    waveNumber = 5, recommendedLevel = 3,
                    spawnLocation = SpawnLocation.TopLeft,
                    spawnInterval = 1.4f, waveDuration = 35f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E003", count = 4 },
                        new WaveEnemyConfig { enemyId = "E005", count = 4 },
                        new WaveEnemyConfig { enemyId = "E009", count = 2 },
                        new WaveEnemyConfig { enemyId = "E014", count = 2 }
                    },
                    keyEvent = "坦克婴灵首次出现。第一阶段结束前的考验。解锁卡牌融合系统。",
                    rewardPreview = new[] { "金币x250", "卡牌B-01", "融合石x1" }
                },

                // =============================================
                // 第二阶段：深入 (Waves 6-10, Lv.3-5)
                // =============================================
                new WaveData
                {
                    waveNumber = 6, recommendedLevel = 3,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 1.3f, waveDuration = 38f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E001", count = 8 },
                        new WaveEnemyConfig { enemyId = "E004", count = 5 },
                        new WaveEnemyConfig { enemyId = "E007", count = 2 },
                        new WaveEnemyConfig { enemyId = "E013", count = 3 }
                    },
                    keyEvent = "闪现婴灵首次出现。全方向出怪开始，需要360度防守。",
                    rewardPreview = new[] { "金币x300", "卡牌A-03" }
                },
                new WaveData
                {
                    waveNumber = 7, recommendedLevel = 4,
                    spawnLocation = SpawnLocation.Left,
                    spawnInterval = 1.3f, waveDuration = 40f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E003", count = 5 },
                        new WaveEnemyConfig { enemyId = "E005", count = 3 },
                        new WaveEnemyConfig { enemyId = "E010", count = 2 },
                        new WaveEnemyConfig { enemyId = "E017", count = 10 }
                    },
                    keyEvent = "铁壁婴灵首次出现。提示：绕后攻击策略。解锁武器切换系统。",
                    rewardPreview = new[] { "金币x350", "天使步枪(W02)" }
                },
                new WaveData
                {
                    waveNumber = 8, recommendedLevel = 4,
                    spawnLocation = SpawnLocation.Bottom,
                    spawnInterval = 1.2f, waveDuration = 42f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E006", count = 4 },
                        new WaveEnemyConfig { enemyId = "E008", count = 3 },
                        new WaveEnemyConfig { enemyId = "E015", count = 2 },
                        new WaveEnemyConfig { enemyId = "E018", count = 8 }
                    },
                    keyEvent = "分裂婴灵和狙击婴灵首次出现。小心分裂机制和远程狙击。",
                    rewardPreview = new[] { "金币x400", "卡牌D-01" }
                },
                new WaveData
                {
                    waveNumber = 9, recommendedLevel = 5,
                    spawnLocation = SpawnLocation.TopRight,
                    spawnInterval = 1.2f, waveDuration = 45f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E009", count = 3 },
                        new WaveEnemyConfig { enemyId = "E011", count = 2 },
                        new WaveEnemyConfig { enemyId = "E014", count = 3 },
                        new WaveEnemyConfig { enemyId = "E017", count = 12 }
                    },
                    keyEvent = "再生婴灵首次出现。持续输出能力变得重要。解锁光环卡槽。",
                    rewardPreview = new[] { "金币x450", "卡牌C-02", "卡牌E-01" }
                },
                new WaveData
                {
                    waveNumber = 10, recommendedLevel = 5,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 1f, waveDuration = 50f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E007", count = 3 },
                        new WaveEnemyConfig { enemyId = "E010", count = 2 },
                        new WaveEnemyConfig { enemyId = "E012", count = 2 },
                        new WaveEnemyConfig { enemyId = "E016", count = 1 },
                        new WaveEnemyConfig { enemyId = "E019", count = 1 }
                    },
                    keyEvent = "第二阶段最终波次！护盾婴灵、召唤婴灵和首个精英敌人(怨念骑士)同时出现。击败后解锁深渊入口。",
                    rewardPreview = new[] { "金币x600", "圣光霰弹(W03)", "卡牌A-06", "强化石x2" }
                },

                // =============================================
                // 第三阶段：深渊一层 (Waves 11-15, Lv.5-8)
                // =============================================
                new WaveData
                {
                    waveNumber = 11, recommendedLevel = 6,
                    spawnLocation = SpawnLocation.BottomLeft,
                    spawnInterval = 1f, waveDuration = 48f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E003", count = 6 },
                        new WaveEnemyConfig { enemyId = "E008", count = 4 },
                        new WaveEnemyConfig { enemyId = "E009", count = 2 },
                        new WaveEnemyConfig { enemyId = "E013", count = 4 }
                    },
                    keyEvent = "进入深渊第一层。环境变暗，敌人密度增加。深渊的低语开始影响天使。",
                    rewardPreview = new[] { "金币x500", "卡牌D-02" }
                },
                new WaveData
                {
                    waveNumber = 12, recommendedLevel = 6,
                    spawnLocation = SpawnLocation.Top,
                    spawnInterval = 0.9f, waveDuration = 50f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E005", count = 5 },
                        new WaveEnemyConfig { enemyId = "E011", count = 2 },
                        new WaveEnemyConfig { enemyId = "E014", count = 3 },
                        new WaveEnemyConfig { enemyId = "E020", count = 1 },
                        new WaveEnemyConfig { enemyId = "E018", count = 8 }
                    },
                    keyEvent = "深渊祭司首次出现！其增伤光环让周围敌人更具威胁。优先击杀策略至关重要。",
                    rewardPreview = new[] { "金币x550", "圣光十字弩(W04)", "卡牌C-05" }
                },
                new WaveData
                {
                    waveNumber = 13, recommendedLevel = 7,
                    spawnLocation = SpawnLocation.Center,
                    spawnInterval = 3f, waveDuration = 90f,
                    isBossWave = true,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "B01", count = 1 },
                        new WaveEnemyConfig { enemyId = "E001", count = 4 },
                        new WaveEnemyConfig { enemyId = "E010", count = 2 }
                    },
                    keyEvent = "⚠️ BOSS战：怨念聚合体！深渊第一层的守护者。三阶段战斗，每阶段新增技能。击败后解锁新区域和武器进化系统。",
                    rewardPreview = new[] { "金币x1500", "守护之盾(W10)", "卡牌A-08(SSR)", "Boss材料x3", "大量经验" }
                },
                new WaveData
                {
                    waveNumber = 14, recommendedLevel = 7,
                    spawnLocation = SpawnLocation.Right,
                    spawnInterval = 0.9f, waveDuration = 52f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E006", count = 5 },
                        new WaveEnemyConfig { enemyId = "E012", count = 3 },
                        new WaveEnemyConfig { enemyId = "E015", count = 3 },
                        new WaveEnemyConfig { enemyId = "E016", count = 2 }
                    },
                    keyEvent = "Boss战后的喘息波次。敌人强度略有下降，但组合更加复杂。",
                    rewardPreview = new[] { "金币x600", "卡牌E-03" }
                },
                new WaveData
                {
                    waveNumber = 15, recommendedLevel = 8,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 0.8f, waveDuration = 55f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E007", count = 4 },
                        new WaveEnemyConfig { enemyId = "E009", count = 3 },
                        new WaveEnemyConfig { enemyId = "E011", count = 2 },
                        new WaveEnemyConfig { enemyId = "E019", count = 2 },
                        new WaveEnemyConfig { enemyId = "E017", count = 10 }
                    },
                    keyEvent = "第三阶段结束波次。双精英(怨念骑士×2)+大量坦克和快速敌人。深渊二层的入口显现。",
                    rewardPreview = new[] { "金币x800", "圣光连弩(W05)", "卡牌B-05(SR)", "深渊钥匙碎片x1" }
                },

                // =============================================
                // 第四阶段：深渊二层 (Waves 16-20, Lv.8-11)
                // =============================================
                new WaveData
                {
                    waveNumber = 16, recommendedLevel = 8,
                    spawnLocation = SpawnLocation.TopLeft,
                    spawnInterval = 0.8f, waveDuration = 55f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E008", count = 6 },
                        new WaveEnemyConfig { enemyId = "E010", count = 3 },
                        new WaveEnemyConfig { enemyId = "E013", count = 4 },
                        new WaveEnemyConfig { enemyId = "E018", count = 10 }
                    },
                    keyEvent = "进入深渊第二层。深渊的低语变得更加强烈。分裂婴灵数量显著增加。",
                    rewardPreview = new[] { "金币x700", "卡牌D-04" }
                },
                new WaveData
                {
                    waveNumber = 17, recommendedLevel = 9,
                    spawnLocation = SpawnLocation.BottomRight,
                    spawnInterval = 0.75f, waveDuration = 58f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E003", count = 8 },
                        new WaveEnemyConfig { enemyId = "E012", count = 3 },
                        new WaveEnemyConfig { enemyId = "E014", count = 4 },
                        new WaveEnemyConfig { enemyId = "E020", count = 2 }
                    },
                    keyEvent = "双深渊祭司+大量愤怒婴灵。祭司光环叠加使敌人攻击力大增。优先击杀祭司！",
                    rewardPreview = new[] { "金币x750", "追踪圣光(W06)", "卡牌C-06(SR)" }
                },
                new WaveData
                {
                    waveNumber = 18, recommendedLevel = 9,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 0.7f, waveDuration = 60f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E005", count = 6 },
                        new WaveEnemyConfig { enemyId = "E007", count = 4 },
                        new WaveEnemyConfig { enemyId = "E015", count = 4 },
                        new WaveEnemyConfig { enemyId = "E016", count = 2 },
                        new WaveEnemyConfig { enemyId = "E019", count = 1 }
                    },
                    keyEvent = "全方向出怪+多种特殊敌人。召唤婴灵不断补充兵力，狙击婴灵远程压制。",
                    rewardPreview = new[] { "金币x850", "卡牌E-07(SR)", "卡牌D-05(SR)" }
                },
                new WaveData
                {
                    waveNumber = 19, recommendedLevel = 10,
                    spawnLocation = SpawnLocation.Top,
                    spawnInterval = 0.65f, waveDuration = 62f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E009", count = 4 },
                        new WaveEnemyConfig { enemyId = "E011", count = 3 },
                        new WaveEnemyConfig { enemyId = "E012", count = 3 },
                        new WaveEnemyConfig { enemyId = "E017", count = 15 },
                        new WaveEnemyConfig { enemyId = "E020", count = 1 }
                    },
                    keyEvent = "坦克海+虫群海组合。考验AOE清场能力和单体爆发能力的平衡。深渊深处传来震动...",
                    rewardPreview = new[] { "金币x900", "烈焰风暴(W07)", "冰霜新星(W08)" }
                },
                new WaveData
                {
                    waveNumber = 20, recommendedLevel = 11,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 0.6f, waveDuration = 70f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E008", count = 6 },
                        new WaveEnemyConfig { enemyId = "E010", count = 3 },
                        new WaveEnemyConfig { enemyId = "E013", count = 5 },
                        new WaveEnemyConfig { enemyId = "E015", count = 3 },
                        new WaveEnemyConfig { enemyId = "E019", count = 2 },
                        new WaveEnemyConfig { enemyId = "E020", count = 1 }
                    },
                    keyEvent = "第四阶段最终波次！双怨念骑士+深渊祭司+全类型敌人混合。深渊之主即将苏醒...",
                    rewardPreview = new[] { "金币x1200", "圣光之环(W11)", "天使刺剑(W12)", "卡牌A-09(SSR)", "深渊钥匙碎片x2" }
                },

                // =============================================
                // 第五阶段：深渊最深处 (Waves 21-25, Lv.11-15)
                // =============================================
                new WaveData
                {
                    waveNumber = 21, recommendedLevel = 12,
                    spawnLocation = SpawnLocation.Bottom,
                    spawnInterval = 0.55f, waveDuration = 65f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E007", count = 6 },
                        new WaveEnemyConfig { enemyId = "E011", count = 4 },
                        new WaveEnemyConfig { enemyId = "E014", count = 5 },
                        new WaveEnemyConfig { enemyId = "E016", count = 3 },
                        new WaveEnemyConfig { enemyId = "E018", count = 12 }
                    },
                    keyEvent = "进入深渊最深处。空气中弥漫着绝望。所有敌人属性+20%(深渊buff)。解锁终极技能槽。",
                    rewardPreview = new[] { "金币x1000", "卡牌D-06(SR)", "卡牌G-01(SR)" }
                },
                new WaveData
                {
                    waveNumber = 22, recommendedLevel = 12,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 0.5f, waveDuration = 68f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E009", count = 5 },
                        new WaveEnemyConfig { enemyId = "E012", count = 4 },
                        new WaveEnemyConfig { enemyId = "E015", count = 4 },
                        new WaveEnemyConfig { enemyId = "E020", count = 2 },
                        new WaveEnemyConfig { enemyId = "E019", count = 2 }
                    },
                    keyEvent = "双精英+双祭司。需要极致的单体爆发和走位技巧。深渊之主的气息越来越近...",
                    rewardPreview = new[] { "金币x1100", "雷电之链(W09)", "卡牌C-10(SSR)" }
                },
                new WaveData
                {
                    waveNumber = 23, recommendedLevel = 13,
                    spawnLocation = SpawnLocation.TopRight,
                    spawnInterval = 0.5f, waveDuration = 70f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E008", count = 8 },
                        new WaveEnemyConfig { enemyId = "E010", count = 4 },
                        new WaveEnemyConfig { enemyId = "E013", count = 6 },
                        new WaveEnemyConfig { enemyId = "E017", count = 20 },
                        new WaveEnemyConfig { enemyId = "E019", count = 3 }
                    },
                    keyEvent = "最终Boss前的最后考验！三怨念骑士+海量敌人。必须合理使用所有技能和道具。",
                    rewardPreview = new[] { "金币x1500", "暗影匕首(W14)", "圣光长弓(W15)", "卡牌B-07(SSR)", "终极强化石x1" }
                },
                new WaveData
                {
                    waveNumber = 24, recommendedLevel = 14,
                    spawnLocation = SpawnLocation.AllSides,
                    spawnInterval = 0.4f, waveDuration = 75f,
                    isBossWave = false,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "E011", count = 5 },
                        new WaveEnemyConfig { enemyId = "E012", count = 5 },
                        new WaveEnemyConfig { enemyId = "E016", count = 3 },
                        new WaveEnemyConfig { enemyId = "E020", count = 3 },
                        new WaveEnemyConfig { enemyId = "E019", count = 2 },
                        new WaveEnemyConfig { enemyId = "E018", count = 15 }
                    },
                    keyEvent = "深渊之门前最后的防御。三祭司+双骑士+无限召唤。撑过这一波，面对最终Boss！",
                    rewardPreview = new[] { "金币x2000", "卡牌E-12(SSR)", "卡牌G-05(SSR)", "全回复药x3" }
                },
                new WaveData
                {
                    waveNumber = 25, recommendedLevel = 15,
                    spawnLocation = SpawnLocation.Center,
                    spawnInterval = 5f, waveDuration = 180f,
                    isBossWave = true,
                    enemyConfigs = new List<WaveEnemyConfig>
                    {
                        new WaveEnemyConfig { enemyId = "B02", count = 1 }
                    },
                    keyEvent = "⚠️ 最终BOSS战：深渊之主！所有婴灵的源头，千年怨念的化身。史诗级三阶段战斗，每阶段拥有毁灭性技能。击败它将拯救所有被困的婴灵灵魂，完成天使的使命。",
                    rewardPreview = new[] { "金币x5000", "陨石法杖(W13)", "大地之盾(W17)", "风暴之眼(W18)", "卡牌A-10(SSR)", "卡牌C-12(SSR)", "Boss灵魂材料x5", "海量经验", "通关成就" }
                }
            };
        }
    }
}
