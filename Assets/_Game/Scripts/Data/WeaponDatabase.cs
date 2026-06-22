using UnityEngine;
using System;
using System.Collections.Generic;

namespace AngelGuardian.Data
{
    // ============================================================
    // Enums
    // ============================================================

    public enum WeaponType { Melee, Ranged, AOE }

    public enum WeaponRarity
    {
        Normal = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Mythic = 5
    }

    // ============================================================
    // WeaponData
    // ============================================================

    [Serializable]
    public class WeaponData
    {
        public string weaponId;
        public string weaponName;
        public WeaponType type;
        public WeaponRarity rarity;
        public float baseDamage;
        public float attackInterval;       // 攻击间隔(秒)
        public float projectileSpeed;      // 弹道速度
        public int projectileCount;        // 弹道数量
        public int pierceCount;            // 穿透数(0=不穿透)
        public string specialParams;       // JSON格式特殊参数
        public string[] buffTags;          // Buff标签数组
        public string description;
        public string designIntent;
        public float baseDPS;              // 基础DPS
        public string recommendedBuild;    // 推荐Build
    }

    // ============================================================
    // WeaponDatabase ScriptableObject
    // ============================================================

    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "AngelGuardian/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        public List<WeaponData> weapons = new List<WeaponData>();

        // 连携兼容矩阵 (18x18)，值范围 [0, 1]，1=完美连携
        public float[] compatibilityMatrixFlat = new float[18 * 18];

        // 便捷的二维访问器
        public float GetCompatibility(int weaponA, int weaponB)
        {
            if (weaponA < 0 || weaponA >= 18 || weaponB < 0 || weaponB >= 18)
                return 0f;
            return compatibilityMatrixFlat[weaponA * 18 + weaponB];
        }

        public void SetCompatibility(int weaponA, int weaponB, float value)
        {
            if (weaponA < 0 || weaponA >= 18 || weaponB < 0 || weaponB >= 18)
                return;
            compatibilityMatrixFlat[weaponA * 18 + weaponB] = value;
            compatibilityMatrixFlat[weaponB * 18 + weaponA] = value; // 对称
        }

        // 保留二维数组属性供外部使用
        public float[,] compatibilityMatrix
        {
            get
            {
                float[,] mat = new float[18, 18];
                for (int i = 0; i < 18; i++)
                    for (int j = 0; j < 18; j++)
                        mat[i, j] = compatibilityMatrixFlat[i * 18 + j];
                return mat;
            }
            set
            {
                if (value.GetLength(0) == 18 && value.GetLength(1) == 18)
                {
                    for (int i = 0; i < 18; i++)
                        for (int j = 0; j < 18; j++)
                            compatibilityMatrixFlat[i * 18 + j] = value[i, j];
                }
            }
        }

        // ============================================================
        // Query helpers
        // ============================================================

        public WeaponData GetWeapon(string id) => weapons.Find(w => w.weaponId == id);

        public List<WeaponData> GetWeaponsByType(WeaponType t) => weapons.FindAll(w => w.type == t);

        public List<WeaponData> GetWeaponsByRarity(WeaponRarity r) => weapons.FindAll(w => w.rarity == r);

        // ============================================================
        // Initialization
        // ============================================================

        private void OnEnable()
        {
            if (weapons == null || weapons.Count == 0)
            {
                weapons = CreateAllWeapons();
            }
            InitializeCompatibilityMatrix();
        }

        // ============================================================
        // All 18 weapon definitions
        // ============================================================

        private static List<WeaponData> CreateAllWeapons()
        {
            return new List<WeaponData>
            {
                // W01 - 圣光手枪
                new WeaponData
                {
                    weaponId = "W01", weaponName = "圣光手枪", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Normal, baseDamage = 25f, attackInterval = 0.5f,
                    projectileSpeed = 40f, projectileCount = 1, pierceCount = 0,
                    specialParams = "{\"headshot_multiplier\":1.5}",
                    buffTags = new[] { "holy", "ranged", "precision" },
                    description = "基础圣光手枪，稳定的远程输出",
                    designIntent = "新手初始武器，培养射击手感",
                    baseDPS = 50f, recommendedBuild = "精准射击流"
                },
                // W02 - 天使步枪
                new WeaponData
                {
                    weaponId = "W02", weaponName = "天使步枪", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Rare, baseDamage = 55f, attackInterval = 0.75f,
                    projectileSpeed = 60f, projectileCount = 1, pierceCount = 1,
                    specialParams = "{\"scope_zoom\":2.0,\"headshot_multiplier\":2.0}",
                    buffTags = new[] { "holy", "ranged", "precision", "pierce" },
                    description = "高精度步枪，可穿透一个敌人",
                    designIntent = "中远距离精准打击",
                    baseDPS = 73.3f, recommendedBuild = "狙击流"
                },
                // W03 - 圣光霰弹
                new WeaponData
                {
                    weaponId = "W03", weaponName = "圣光霰弹", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Normal, baseDamage = 18f, attackInterval = 0.8f,
                    projectileSpeed = 25f, projectileCount = 8, pierceCount = 0,
                    specialParams = "{\"spread_angle\":30,\"damage_falloff_start\":3,\"damage_falloff_end\":8}",
                    buffTags = new[] { "holy", "ranged", "spread", "close_range" },
                    description = "近距离扇形散射，每发造成18伤害",
                    designIntent = "近距离高爆发清怪",
                    baseDPS = 180f, recommendedBuild = "贴脸爆发流"
                },
                // W04 - 圣光十字弩
                new WeaponData
                {
                    weaponId = "W04", weaponName = "圣光十字弩", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Rare, baseDamage = 70f, attackInterval = 1.2f,
                    projectileSpeed = 50f, projectileCount = 1, pierceCount = 2,
                    specialParams = "{\"bolt_explosion_radius\":1.5,\"explosion_damage\":0.5}",
                    buffTags = new[] { "holy", "ranged", "pierce", "explosive" },
                    description = "重型弩箭，穿透2个敌人并小范围爆炸",
                    designIntent = "穿透型AOE混合武器",
                    baseDPS = 58.3f, recommendedBuild = "穿透爆炸流"
                },
                // W05 - 圣光连弩
                new WeaponData
                {
                    weaponId = "W05", weaponName = "圣光连弩", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Epic, baseDamage = 40f, attackInterval = 0.35f,
                    projectileSpeed = 45f, projectileCount = 3, pierceCount = 1,
                    specialParams = "{\"burst_count\":3,\"burst_interval\":0.1,\"reload_time\":0.8}",
                    buffTags = new[] { "holy", "ranged", "rapid_fire", "pierce" },
                    description = "三连发高速弩箭，每次发射3枚弩箭",
                    designIntent = "高频率持续输出",
                    baseDPS = 342.9f, recommendedBuild = "攻速流"
                },
                // W06 - 追踪圣光
                new WeaponData
                {
                    weaponId = "W06", weaponName = "追踪圣光", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Rare, baseDamage = 30f, attackInterval = 0.6f,
                    projectileSpeed = 30f, projectileCount = 1, pierceCount = 0,
                    specialParams = "{\"homing_strength\":0.8,\"homing_range\":10,\"turn_rate\":180}",
                    buffTags = new[] { "holy", "ranged", "homing", "tracking" },
                    description = "发射自动追踪敌人的圣光弹",
                    designIntent = "移动战利器，无需精确瞄准",
                    baseDPS = 50f, recommendedBuild = "追踪风筝流"
                },
                // W07 - 烈焰风暴
                new WeaponData
                {
                    weaponId = "W07", weaponName = "烈焰风暴", type = WeaponType.AOE,
                    rarity = WeaponRarity.Rare, baseDamage = 80f, attackInterval = 2.5f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"radius\":4,\"burn_duration\":4,\"burn_damage_per_sec\":0.3}",
                    buffTags = new[] { "fire", "aoe", "burn", "dot" },
                    description = "在目标区域召唤烈焰风暴，造成范围伤害并灼烧",
                    designIntent = "AOE清场，对密集敌人有效",
                    baseDPS = 32f, recommendedBuild = "灼烧流"
                },
                // W08 - 冰霜新星
                new WeaponData
                {
                    weaponId = "W08", weaponName = "冰霜新星", type = WeaponType.AOE,
                    rarity = WeaponRarity.Rare, baseDamage = 60f, attackInterval = 3f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"radius\":5,\"freeze_duration\":2,\"chill_slow\":0.4,\"chill_duration\":3}",
                    buffTags = new[] { "ice", "aoe", "freeze", "slow", "control" },
                    description = "以自身为中心释放冰霜新星，冻结并减速周围敌人",
                    designIntent = "防御型AOE，紧急控场",
                    baseDPS = 20f, recommendedBuild = "冰冻控制流"
                },
                // W09 - 雷电之链
                new WeaponData
                {
                    weaponId = "W09", weaponName = "雷电之链", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Epic, baseDamage = 45f, attackInterval = 0.7f,
                    projectileSpeed = 80f, projectileCount = 1, pierceCount = 0,
                    specialParams = "{\"chain_count\":4,\"chain_range\":5,\"chain_damage_falloff\":0.15}",
                    buffTags = new[] { "thunder", "ranged", "chain", "aoe" },
                    description = "发射雷电弹，命中后连锁至4个附近敌人",
                    designIntent = "连锁AOE，清群效率高",
                    baseDPS = 64.3f, recommendedBuild = "连锁雷电流"
                },
                // W10 - 守护之盾
                new WeaponData
                {
                    weaponId = "W10", weaponName = "守护之盾", type = WeaponType.Melee,
                    rarity = WeaponRarity.Rare, baseDamage = 35f, attackInterval = 0.9f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"block_chance\":0.3,\"block_damage_reduction\":0.6,\"bash_radius\":1.5}",
                    buffTags = new[] { "holy", "melee", "block", "defense" },
                    description = "近战盾击，30%概率格挡减少60%伤害",
                    designIntent = "防御型近战，生存向",
                    baseDPS = 38.9f, recommendedBuild = "格挡反击流"
                },
                // W11 - 圣光之环
                new WeaponData
                {
                    weaponId = "W11", weaponName = "圣光之环", type = WeaponType.AOE,
                    rarity = WeaponRarity.Epic, baseDamage = 100f, attackInterval = 2f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"radius\":6,\"inner_radius\":2,\"inner_damage_mult\":1.5,\"knockback\":3}",
                    buffTags = new[] { "holy", "aoe", "knockback", "defense" },
                    description = "释放圣光之环推开周围敌人并造成伤害",
                    designIntent = "保命型AOE，兼顾伤害和击退",
                    baseDPS = 50f, recommendedBuild = "击退光环流"
                },
                // W12 - 天使刺剑
                new WeaponData
                {
                    weaponId = "W12", weaponName = "天使刺剑", type = WeaponType.Melee,
                    rarity = WeaponRarity.Epic, baseDamage = 65f, attackInterval = 0.4f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"combo_window\":1.5,\"combo_max\":5,\"combo_damage_per_stack\":0.12,\"dash_range\":4}",
                    buffTags = new[] { "holy", "melee", "combo", "dash", "rapid" },
                    description = "高速刺剑，连击叠加伤害(每层+12%，最多5层)",
                    designIntent = "高风险高回报近战，操作上限高",
                    baseDPS = 162.5f, recommendedBuild = "连击流"
                },
                // W13 - 陨石法杖
                new WeaponData
                {
                    weaponId = "W13", weaponName = "陨石法杖", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Legendary, baseDamage = 200f, attackInterval = 3.5f,
                    projectileSpeed = 20f, projectileCount = 1, pierceCount = 0,
                    specialParams = "{\"meteor_radius\":4,\"fall_delay\":1.2,\"burn_ground_duration\":5,\"burn_ground_damage\":0.2}",
                    buffTags = new[] { "fire", "ranged", "meteor", "aoe", "burn", "legendary" },
                    description = "召唤陨石从天而降，造成大范围毁灭性伤害并留下灼烧地面",
                    designIntent = "终极远程AOE，清屏级武器",
                    baseDPS = 57.1f, recommendedBuild = "陨石爆发流"
                },
                // W14 - 暗影匕首
                new WeaponData
                {
                    weaponId = "W14", weaponName = "暗影匕首", type = WeaponType.Melee,
                    rarity = WeaponRarity.Epic, baseDamage = 50f, attackInterval = 0.5f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"backstab_multiplier\":2.5,\"stealth_duration\":1.5,\"stealth_cooldown\":8,\"poison_damage\":0.15,\"poison_duration\":4}",
                    buffTags = new[] { "shadow", "melee", "stealth", "poison", "backstab" },
                    description = "暗影匕首，从背后攻击造成2.5倍伤害，附带中毒",
                    designIntent = "刺杀型近战，走位要求高",
                    baseDPS = 100f, recommendedBuild = "背刺暗杀流"
                },
                // W15 - 圣光长弓
                new WeaponData
                {
                    weaponId = "W15", weaponName = "圣光长弓", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Rare, baseDamage = 90f, attackInterval = 1.5f,
                    projectileSpeed = 70f, projectileCount = 1, pierceCount = 3,
                    specialParams = "{\"charge_time\":1.0,\"full_charge_mult\":2.0,\"arrow_rain_count\":8}",
                    buffTags = new[] { "holy", "ranged", "charge", "pierce", "precision" },
                    description = "蓄力长弓，满蓄力伤害翻倍，可穿透3个敌人",
                    designIntent = "蓄力狙击型远程",
                    baseDPS = 60f, recommendedBuild = "蓄力狙击流"
                },
                // W16 - 圣光魔杖
                new WeaponData
                {
                    weaponId = "W16", weaponName = "圣光魔杖", type = WeaponType.Ranged,
                    rarity = WeaponRarity.Normal, baseDamage = 30f, attackInterval = 0.6f,
                    projectileSpeed = 35f, projectileCount = 2, pierceCount = 0,
                    specialParams = "{\"spread_angle\":10,\"mana_cost\":5,\"mana_regen_per_kill\":3}",
                    buffTags = new[] { "holy", "ranged", "magic", "multi_shot" },
                    description = "发射两枚圣光弹，基础魔法武器",
                    designIntent = "新手魔法武器，双发稳定输出",
                    baseDPS = 100f, recommendedBuild = "魔法弹幕流"
                },
                // W17 - 大地之盾
                new WeaponData
                {
                    weaponId = "W17", weaponName = "大地之盾", type = WeaponType.Melee,
                    rarity = WeaponRarity.Legendary, baseDamage = 80f, attackInterval = 1.0f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"block_chance\":0.5,\"block_damage_reduction\":0.8,\"counter_damage_mult\":1.5,\"earthquake_radius\":5,\"earthquake_cooldown\":10}",
                    buffTags = new[] { "earth", "melee", "block", "counter", "earthquake", "legendary" },
                    description = "传奇盾牌，50%格挡率减少80%伤害，格挡后反击造成地震",
                    designIntent = "终极防御武器，攻防一体",
                    baseDPS = 80f, recommendedBuild = "格挡地震流"
                },
                // W18 - 风暴之眼
                new WeaponData
                {
                    weaponId = "W18", weaponName = "风暴之眼", type = WeaponType.AOE,
                    rarity = WeaponRarity.Legendary, baseDamage = 150f, attackInterval = 4f,
                    projectileSpeed = 0f, projectileCount = 0, pierceCount = 0,
                    specialParams = "{\"radius\":8,\"pull_strength\":0.6,\"tick_interval\":0.5,\"tick_damage\":0.15,\"duration\":5,\"lightning_strikes\":3}",
                    buffTags = new[] { "storm", "aoe", "pull", "lightning", "dot", "legendary" },
                    description = "制造巨型风暴之眼，持续拉扯敌人并造成伤害，附带雷电打击",
                    designIntent = "终极AOE控制，聚怪+持续伤害",
                    baseDPS = 37.5f, recommendedBuild = "风暴聚怪流"
                }
            };
        }

        // ============================================================
        // Compatibility Matrix initialization
        // ============================================================

        private void InitializeCompatibilityMatrix()
        {
            // 检查是否需要初始化（全为0才初始化，避免覆盖编辑器中的自定义值）
            bool needsInit = true;
            for (int i = 0; i < compatibilityMatrixFlat.Length; i++)
            {
                if (compatibilityMatrixFlat[i] != 0f)
                {
                    needsInit = false;
                    break;
                }
            }
            if (!needsInit) return;

            // 对角线：自身兼容性 = 1
            for (int i = 0; i < 18; i++)
                SetCompatibility(i, i, 1f);

            // --- 同类型武器连携 ---
            // Ranged组: W01, W02, W03, W04, W05, W06, W09, W13, W15, W16
            int[] rangedGroup = { 0, 1, 2, 3, 4, 5, 8, 12, 14, 15 };
            foreach (int a in rangedGroup)
                foreach (int b in rangedGroup)
                    if (a != b) SetCompatibility(a, b, 0.3f);

            // Melee组: W10, W12, W14, W17
            int[] meleeGroup = { 9, 11, 13, 16 };
            foreach (int a in meleeGroup)
                foreach (int b in meleeGroup)
                    if (a != b) SetCompatibility(a, b, 0.35f);

            // AOE组: W07, W08, W11, W18
            int[] aoeGroup = { 6, 7, 10, 17 };
            foreach (int a in aoeGroup)
                foreach (int b in aoeGroup)
                    if (a != b) SetCompatibility(a, b, 0.3f);

            // --- 特殊连携 (基于设计意图) ---
            // W01(手枪) + W12(刺剑) = 经典枪剑组合
            SetCompatibility(0, 11, 0.5f);

            // W02(步枪) + W15(长弓) = 精准远程组合
            SetCompatibility(1, 14, 0.55f);

            // W03(霰弹) + W11(圣光之环) = 近距清场组合
            SetCompatibility(2, 10, 0.6f);

            // W05(连弩) + W09(雷电之链) = 高频连锁组合
            SetCompatibility(4, 8, 0.65f);

            // W07(烈焰风暴) + W13(陨石法杖) = 火焰毁灭组合
            SetCompatibility(6, 12, 0.7f);

            // W08(冰霜新星) + W18(风暴之眼) = 冰风控场组合
            SetCompatibility(7, 17, 0.65f);

            // W10(守护之盾) + W17(大地之盾) = 双盾防御组合
            SetCompatibility(9, 16, 0.7f);

            // W12(刺剑) + W14(暗影匕首) = 高速刺杀组合
            SetCompatibility(11, 13, 0.6f);

            // W06(追踪) + W16(魔杖) = 魔法追踪组合
            SetCompatibility(5, 15, 0.5f);

            // W04(十字弩) + W07(烈焰风暴) = 爆炸火焰组合
            SetCompatibility(3, 6, 0.5f);

            // --- 跨类型通用连携 ---
            // Holy主题武器互连: W01, W02, W03, W04, W05, W06, W10, W11, W12, W15, W16
            int[] holyGroup = { 0, 1, 2, 3, 4, 5, 9, 10, 11, 14, 15 };
            foreach (int a in holyGroup)
                foreach (int b in holyGroup)
                    if (a != b && GetCompatibility(a, b) < 0.2f)
                        SetCompatibility(a, b, 0.2f);

            // Legendary武器之间的连携
            SetCompatibility(12, 16, 0.4f); // W13(陨石) + W17(大地之盾)
            SetCompatibility(12, 17, 0.4f); // W13(陨石) + W18(风暴之眼)
            SetCompatibility(16, 17, 0.5f); // W17(大地之盾) + W18(风暴之眼) = 元素守护
        }
    }
}
