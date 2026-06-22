using UnityEngine;
using System;
using System.Collections.Generic;

namespace AngelGuardian.Data
{
    // ============================================================
    // Enums
    // ============================================================

    /// <summary>敌人类型</summary>
    public enum EnemyType
    {
        Normal,         // 普通婴灵
        Fast,           // 快速型
        Tank,           // 坦克型
        Ranged,         // 远程型
        Swarm,          // 集群型
        Elite,          // 精英
        Boss            // Boss
    }

    /// <summary>威胁目标</summary>
    public enum ThreatTarget { Angel, Baby, Both }

    /// <summary>攻击类型</summary>
    public enum AttackType { Melee, Ranged, Charge, AOE, Debuff }

    // ============================================================
    // EnemyData (普通/精英敌人)
    // ============================================================

    [Serializable]
    public class EnemyData
    {
        public string enemyId;            // e.g. "E001"
        public string enemyName;          // e.g. "迷途婴灵"
        public EnemyType type;
        public ThreatTarget threatTarget;
        public float hp;                  // 基础生命值
        public float moveSpeed;           // 移动速度
        public float attackPower;         // 攻击力
        public AttackType attackType;
        public string specialAbility;     // 特殊能力描述
        public int expReward;             // 击杀经验奖励
        public string waveAppearance;     // 出现波次描述 e.g. "1-5, 8, 12"
    }

    // ============================================================
    // BossPhaseData (Boss阶段)
    // ============================================================

    [Serializable]
    public class BossPhaseData
    {
        public int phaseNumber;           // 阶段编号 1/2/3
        public float hpThreshold;         // 触发该阶段的HP百分比阈值 e.g. 0.7 = 70%
        public string[] skills;           // 该阶段技能列表
        public string phaseDescription;   // 阶段描述
    }

    // ============================================================
    // BossData
    // ============================================================

    [Serializable]
    public class BossData
    {
        public string bossId;             // e.g. "B01"
        public string bossName;           // e.g. "怨念聚合体"
        public string bossDescription;    // Boss背景描述
        public float hp;                  // 总生命值
        public float moveSpeed;
        public float attackPower;
        public List<BossPhaseData> phases = new List<BossPhaseData>();
    }

    // ============================================================
    // EnemyDatabase ScriptableObject
    // ============================================================

    [CreateAssetMenu(fileName = "EnemyDatabase", menuName = "AngelGuardian/Enemy Database")]
    public class EnemyDatabase : ScriptableObject
    {
        public List<EnemyData> enemies = new List<EnemyData>();
        public List<BossData> bosses = new List<BossData>();

        // ============================================================
        // Query helpers
        // ============================================================

        public EnemyData GetEnemy(string id) => enemies.Find(e => e.enemyId == id);

        public List<EnemyData> GetEnemiesByType(EnemyType t) => enemies.FindAll(e => e.type == t);

        public List<EnemyData> GetEnemiesByWave(int wave)
        {
            return enemies.FindAll(e =>
            {
                // 解析 waveAppearance 字符串来判断是否在该波次出现
                if (string.IsNullOrEmpty(e.waveAppearance)) return false;

                string[] ranges = e.waveAppearance.Split(',');
                foreach (string range in ranges)
                {
                    string trimmed = range.Trim();
                    if (trimmed.Contains("-"))
                    {
                        string[] parts = trimmed.Split('-');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out int start) &&
                            int.TryParse(parts[1], out int end) &&
                            wave >= start && wave <= end)
                            return true;
                    }
                    else if (int.TryParse(trimmed, out int single) && wave == single)
                    {
                        return true;
                    }
                }
                return false;
            });
        }

        public BossData GetBoss(string id) => bosses.Find(b => b.bossId == id);

        // ============================================================
        // Initialization
        // ============================================================

        private void OnEnable()
        {
            if (enemies == null || enemies.Count == 0)
            {
                enemies = CreateAllEnemies();
            }
            if (bosses == null || bosses.Count == 0)
            {
                bosses = CreateAllBosses();
            }
        }

        // ============================================================
        // 20 Enemy definitions (E001-E020)
        // ============================================================

        private static List<EnemyData> CreateAllEnemies()
        {
            return new List<EnemyData>
            {
                // --- Normal 普通婴灵 (E001-E004) ---
                new EnemyData
                {
                    enemyId = "E001", enemyName = "迷途婴灵", type = EnemyType.Normal,
                    threatTarget = ThreatTarget.Angel, hp = 80f, moveSpeed = 2.5f,
                    attackPower = 10f, attackType = AttackType.Melee,
                    specialAbility = "无特殊能力，最基础的婴灵",
                    expReward = 10, waveAppearance = "1-25"
                },
                new EnemyData
                {
                    enemyId = "E002", enemyName = "哭泣婴灵", type = EnemyType.Normal,
                    threatTarget = ThreatTarget.Baby, hp = 70f, moveSpeed = 3f,
                    attackPower = 8f, attackType = AttackType.Melee,
                    specialAbility = "优先攻击婴灵目标，死亡时发出哭声减速周围友军",
                    expReward = 12, waveAppearance = "1-20"
                },
                new EnemyData
                {
                    enemyId = "E003", enemyName = "愤怒婴灵", type = EnemyType.Normal,
                    threatTarget = ThreatTarget.Angel, hp = 100f, moveSpeed = 2f,
                    attackPower = 15f, attackType = AttackType.Charge,
                    specialAbility = "生命低于30%时进入狂暴：攻速+50%，移速+30%",
                    expReward = 15, waveAppearance = "3-25"
                },
                new EnemyData
                {
                    enemyId = "E004", enemyName = "游荡婴灵", type = EnemyType.Normal,
                    threatTarget = ThreatTarget.Both, hp = 60f, moveSpeed = 3.5f,
                    attackPower = 7f, attackType = AttackType.Melee,
                    specialAbility = "移动轨迹不可预测，有15%概率闪避攻击",
                    expReward = 12, waveAppearance = "2-22"
                },

                // --- Fast 快速型 (E005-E008) ---
                new EnemyData
                {
                    enemyId = "E005", enemyName = "疾走婴灵", type = EnemyType.Fast,
                    threatTarget = ThreatTarget.Angel, hp = 45f, moveSpeed = 6f,
                    attackPower = 12f, attackType = AttackType.Charge,
                    specialAbility = "高速冲向天使，首次接触造成双倍伤害",
                    expReward = 15, waveAppearance = "2-18"
                },
                new EnemyData
                {
                    enemyId = "E006", enemyName = "跳跃婴灵", type = EnemyType.Fast,
                    threatTarget = ThreatTarget.Baby, hp = 50f, moveSpeed = 5.5f,
                    attackPower = 10f, attackType = AttackType.Melee,
                    specialAbility = "可跳跃越过地形障碍，落地造成小范围AOE伤害",
                    expReward = 18, waveAppearance = "4-20"
                },
                new EnemyData
                {
                    enemyId = "E007", enemyName = "闪现婴灵", type = EnemyType.Fast,
                    threatTarget = ThreatTarget.Angel, hp = 40f, moveSpeed = 4f,
                    attackPower = 14f, attackType = AttackType.Melee,
                    specialAbility = "每隔8秒可短距离闪现至天使身旁",
                    expReward = 20, waveAppearance = "6-22"
                },
                new EnemyData
                {
                    enemyId = "E008", enemyName = "分裂婴灵", type = EnemyType.Fast,
                    threatTarget = ThreatTarget.Both, hp = 55f, moveSpeed = 5f,
                    attackPower = 9f, attackType = AttackType.Melee,
                    specialAbility = "死亡时分裂为2个小型婴灵(HP=50%原HP，伤害=60%)",
                    expReward = 22, waveAppearance = "8-24"
                },

                // --- Tank 坦克型 (E009-E012) ---
                new EnemyData
                {
                    enemyId = "E009", enemyName = "巨石婴灵", type = EnemyType.Tank,
                    threatTarget = ThreatTarget.Angel, hp = 350f, moveSpeed = 1.5f,
                    attackPower = 20f, attackType = AttackType.Melee,
                    specialAbility = "高生命值，受到伤害的15%反弹给攻击者",
                    expReward = 30, waveAppearance = "5-25"
                },
                new EnemyData
                {
                    enemyId = "E010", enemyName = "铁壁婴灵", type = EnemyType.Tank,
                    threatTarget = ThreatTarget.Baby, hp = 400f, moveSpeed = 1.2f,
                    attackPower = 25f, attackType = AttackType.Melee,
                    specialAbility = "正面受到的伤害减少50%，需要从背后攻击",
                    expReward = 35, waveAppearance = "7-25"
                },
                new EnemyData
                {
                    enemyId = "E011", enemyName = "再生婴灵", type = EnemyType.Tank,
                    threatTarget = ThreatTarget.Angel, hp = 300f, moveSpeed = 2f,
                    attackPower = 18f, attackType = AttackType.Melee,
                    specialAbility = "每秒回复1%最大生命值，持续受到伤害时回复减半",
                    expReward = 35, waveAppearance = "9-25"
                },
                new EnemyData
                {
                    enemyId = "E012", enemyName = "护盾婴灵", type = EnemyType.Tank,
                    threatTarget = ThreatTarget.Both, hp = 250f, moveSpeed = 1.8f,
                    attackPower = 15f, attackType = AttackType.Debuff,
                    specialAbility = "每15秒为自己和周围友军施加吸收100伤害的护盾",
                    expReward = 40, waveAppearance = "10-25"
                },

                // --- Ranged 远程型 (E013-E016) ---
                new EnemyData
                {
                    enemyId = "E013", enemyName = "投掷婴灵", type = EnemyType.Ranged,
                    threatTarget = ThreatTarget.Angel, hp = 60f, moveSpeed = 2f,
                    attackPower = 16f, attackType = AttackType.Ranged,
                    specialAbility = "远程投掷怨念弹，射程8，弹道速度中等",
                    expReward = 15, waveAppearance = "3-20"
                },
                new EnemyData
                {
                    enemyId = "E014", enemyName = "诅咒婴灵", type = EnemyType.Ranged,
                    threatTarget = ThreatTarget.Baby, hp = 55f, moveSpeed = 1.8f,
                    attackPower = 12f, attackType = AttackType.Debuff,
                    specialAbility = "远程施加诅咒：目标受到伤害+20%，持续5秒",
                    expReward = 18, waveAppearance = "5-22"
                },
                new EnemyData
                {
                    enemyId = "E015", enemyName = "狙击婴灵", type = EnemyType.Ranged,
                    threatTarget = ThreatTarget.Angel, hp = 50f, moveSpeed = 1.5f,
                    attackPower = 30f, attackType = AttackType.Ranged,
                    specialAbility = "超远程攻击(射程15)，高伤害，但攻击间隔长(3秒)",
                    expReward = 22, waveAppearance = "8-24"
                },
                new EnemyData
                {
                    enemyId = "E016", enemyName = "召唤婴灵", type = EnemyType.Ranged,
                    threatTarget = ThreatTarget.Both, hp = 80f, moveSpeed = 1.5f,
                    attackPower = 8f, attackType = AttackType.Ranged,
                    specialAbility = "每12秒召唤2只迷途婴灵(E001)，最多同时存在4只召唤物",
                    expReward = 30, waveAppearance = "10-25"
                },

                // --- Swarm 集群型 (E017-E018) ---
                new EnemyData
                {
                    enemyId = "E017", enemyName = "虫群婴灵", type = EnemyType.Swarm,
                    threatTarget = ThreatTarget.Angel, hp = 25f, moveSpeed = 3f,
                    attackPower = 5f, attackType = AttackType.Melee,
                    specialAbility = "成群出现(每群8-12只)，数量优势弥补个体弱",
                    expReward = 5, waveAppearance = "1-15"
                },
                new EnemyData
                {
                    enemyId = "E018", enemyName = "蝙蝠婴灵", type = EnemyType.Swarm,
                    threatTarget = ThreatTarget.Both, hp = 30f, moveSpeed = 4f,
                    attackPower = 6f, attackType = AttackType.Melee,
                    specialAbility = "飞行单位，可穿越地形，死亡时有20%概率掉落回复道具",
                    expReward = 8, waveAppearance = "4-18"
                },

                // --- Elite 精英 (E019-E020) ---
                new EnemyData
                {
                    enemyId = "E019", enemyName = "怨念骑士", type = EnemyType.Elite,
                    threatTarget = ThreatTarget.Angel, hp = 600f, moveSpeed = 3f,
                    attackPower = 35f, attackType = AttackType.Charge,
                    specialAbility = "精英单位：周期性冲锋，冲锋路径上造成200%伤害并击退。免疫减速效果",
                    expReward = 80, waveAppearance = "10, 15, 20, 23"
                },
                new EnemyData
                {
                    enemyId = "E020", enemyName = "深渊祭司", type = EnemyType.Elite,
                    threatTarget = ThreatTarget.Both, hp = 500f, moveSpeed = 2f,
                    attackPower = 25f, attackType = AttackType.AOE,
                    specialAbility = "精英单位：释放暗影波(AOE范围4)，为周围敌人提供攻击力+20%光环。死亡时对全场敌人施加10秒狂暴buff",
                    expReward = 90, waveAppearance = "12, 17, 22, 24"
                }
            };
        }

        // ============================================================
        // 2 Boss definitions (B01-B02)
        // ============================================================

        private static List<BossData> CreateAllBosses()
        {
            return new List<BossData>
            {
                // B01 - 怨念聚合体 (中期Boss, Wave 13)
                new BossData
                {
                    bossId = "B01",
                    bossName = "怨念聚合体",
                    bossDescription = "无数婴灵怨念的聚合体，是深渊第一层的守护者。由被抛弃的婴灵怨念融合而成，拥有扭曲空间的力量。",
                    hp = 5000f,
                    moveSpeed = 1.5f,
                    attackPower = 40f,
                    phases = new List<BossPhaseData>
                    {
                        new BossPhaseData
                        {
                            phaseNumber = 1,
                            hpThreshold = 1.0f, // 100%-70%
                            skills = new[]
                            {
                                "怨念波：向前方扇形区域释放怨念冲击波，造成80%攻击力伤害",
                                "婴灵召唤：召唤4只迷途婴灵(E001)和2只哭泣婴灵(E002)",
                                "暗影爪击：近战三连击，每次造成100%攻击力伤害"
                            },
                            phaseDescription = "第一阶段：使用远程怨念波和近战爪击，间歇召唤婴灵辅助"
                        },
                        new BossPhaseData
                        {
                            phaseNumber = 2,
                            hpThreshold = 0.7f, // 70%-35%
                            skills = new[]
                            {
                                "怨念漩涡：在场地中心制造怨念漩涡，持续拉扯玩家并每秒造成50%攻击力伤害，持续8秒",
                                "强化召唤：召唤2只铁壁婴灵(E010)和1只诅咒婴灵(E014)",
                                "怨念弹幕：向全场发射12枚怨念弹，每枚造成60%攻击力伤害",
                                "暗影爪击(强化)：近战五连击，每次造成120%攻击力伤害"
                            },
                            phaseDescription = "第二阶段：新增AOE漩涡和弹幕攻击，召唤更强力的辅助敌人"
                        },
                        new BossPhaseData
                        {
                            phaseNumber = 3,
                            hpThreshold = 0.35f, // 35%-0%
                            skills = new[]
                            {
                                "绝望领域：全屏AOE，每秒造成80%攻击力伤害，持续6秒(可被打断)",
                                "怨念分身：分裂为3个分身(各拥有本体30%属性)，分身全部被击败后本体重新出现",
                                "终极怨念波：蓄力3秒后释放，对全场造成300%攻击力伤害(蓄力期间受到伤害+50%)",
                                "疯狂召唤：连续召唤3波敌人：虫群婴灵×8 → 巨石婴灵×2 → 怨念骑士×1"
                            },
                            phaseDescription = "第三阶段(狂暴)：全屏AOE、分身、蓄力大招，需要快速击杀"
                        }
                    }
                },

                // B02 - 深渊之主 (最终Boss, Wave 25)
                new BossData
                {
                    bossId = "B02",
                    bossName = "深渊之主",
                    bossDescription = "深渊最深处的终极存在，是所有婴灵的源头。据说是第一位夭折婴儿的怨念经过千年演化形成的存在，拥有近乎神级的力量。",
                    hp = 12000f,
                    moveSpeed = 2f,
                    attackPower = 60f,
                    phases = new List<BossPhaseData>
                    {
                        new BossPhaseData
                        {
                            phaseNumber = 1,
                            hpThreshold = 1.0f, // 100%-75%
                            skills = new[]
                            {
                                "暗影洪流：释放暗影洪流覆盖半场，每秒造成60%攻击力伤害，持续5秒",
                                "深渊凝视：锁定一名目标，3秒后造成200%攻击力的单体伤害",
                                "虚空之触：近战范围攻击，造成150%攻击力伤害并施加暗影debuff(受到伤害+25%，持续8秒)",
                                "婴灵潮汐：召唤8只随机普通婴灵(E001-E004)"
                            },
                            phaseDescription = "第一阶段：试探性攻击，技能范围有限，间歇召唤婴灵"
                        },
                        new BossPhaseData
                        {
                            phaseNumber = 2,
                            hpThreshold = 0.75f, // 75%-40%
                            skills = new[]
                            {
                                "深渊裂隙：在地面制造3道裂隙，每道持续造成伤害并减速经过的敌人",
                                "暗影分身：制造2个暗影分身(各拥有50%本体属性)，分身被击败后爆炸造成AOE伤害",
                                "虚空风暴：全屏风暴，随机位置生成虚空球，触碰造成100%攻击力伤害",
                                "黑暗新星：以自身为中心释放黑暗能量，造成200%攻击力伤害并击退",
                                "精英召唤：召唤1只怨念骑士(E019)和1只深渊祭司(E020)"
                            },
                            phaseDescription = "第二阶段：战场控制能力增强，新增地形改变和分身机制"
                        },
                        new BossPhaseData
                        {
                            phaseNumber = 3,
                            hpThreshold = 0.4f, // 40%-0%
                            skills = new[]
                            {
                                "终焉：蓄力5秒后释放毁灭性能量，全屏造成500%攻击力伤害(必须用无敌技能或地形躲避)",
                                "深渊吞噬：吞噬场上一半的婴灵(包括召唤物)，每个吞噬的婴灵回复Boss 3%最大生命值",
                                "虚空行走：进入虚空状态3秒(无敌)，重新出现时造成300%攻击力的AOE伤害",
                                "绝望召唤：召唤2只怨念骑士(E019)、2只深渊祭司(E020)、4只铁壁婴灵(E010)",
                                "黑暗祝福：为自身施加黑暗祝福，攻击力+50%，攻击速度+30%，持续15秒",
                                "死亡凋零：被动光环，周围敌人每秒受到2%最大生命值的伤害"
                            },
                            phaseDescription = "第三阶段(终焉)：拥有秒杀级大招、无敌机制和强力自我buff，需要极限操作"
                        }
                    }
                }
            };
        }
    }
}
