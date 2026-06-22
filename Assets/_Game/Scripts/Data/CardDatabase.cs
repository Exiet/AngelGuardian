using UnityEngine;
using System;
using System.Collections.Generic;

namespace AngelGuardian.Data
{
    // ============================================================
    // Enums
    // ============================================================

    /// <summary>卡牌类别 (9 categories)</summary>
    public enum CardCategory
    {
        A_Attack,           // A类 - 攻击/防御
        B_BabyControl,      // B类 - 婴灵控制
        C_Aura,             // C类 - 光环
        D_Terrain,          // D类 - 地形
        E_Passive,          // E类 - 被动
        F_Growth,           // F类 - 成长
        G_Emotion,          // G类 - 情感
        H_Combo,            // H类 - 连携
        I_TerrainActivation // I类 - 地形活化
    }

    /// <summary>稀有度</summary>
    public enum CardRarity { N, R, SR, SSR }

    /// <summary>影响对象</summary>
    public enum CardTarget { Angel, Baby, Both, Terrain }

    // ============================================================
    // CardData
    // ============================================================

    [Serializable]
    public class CardData
    {
        public string cardId;           // e.g. "A-01"
        public string cardName;         // e.g. "精神屏障"
        public CardCategory category;
        public CardRarity rarity;
        public CardTarget target;
        public string baseEffect;       // 基础效果描述
        public float baseValue;         // 基础数值
        public float valuePerLevel;     // 每级提升
        public int maxLevel;            // 最大等级
        public string maxEffect;        // 满级效果描述
        public string fusionPath;       // 融合路径 e.g. "A01+A05→F-01"
        public string playStyle;        // 玩法流派
        public string designNote;       // 策划备注
    }

    // ============================================================
    // CardDatabase ScriptableObject
    // ============================================================

    [CreateAssetMenu(fileName = "CardDatabase", menuName = "AngelGuardian/Card Database")]
    public class CardDatabase : ScriptableObject
    {
        public List<CardData> cards = new List<CardData>();

        // ============================================================
        // Query helpers
        // ============================================================

        public CardData GetCard(string cardId) => cards.Find(c => c.cardId == cardId);
        public List<CardData> GetCardsByCategory(CardCategory cat) => cards.FindAll(c => c.category == cat);
        public List<CardData> GetCardsByRarity(CardRarity r) => cards.FindAll(c => c.rarity == r);

        // ============================================================
        // Initialization — called in OnEnable so data is ready in Editor
        // ============================================================

        private void OnEnable()
        {
            if (cards == null || cards.Count == 0)
            {
                cards = CreateAllCards();
            }
        }

        // ============================================================
        // All 70 card definitions
        // ============================================================

        private static List<CardData> CreateAllCards()
        {
            return new List<CardData>
            {
                // =============================================
                // A类 - 攻击/防御 (10张: A01-A10)
                // =============================================
                new CardData
                {
                    cardId = "A-01", cardName = "精神屏障", category = CardCategory.A_Attack,
                    rarity = CardRarity.N, target = CardTarget.Angel,
                    baseEffect = "为天使增加护盾，吸收等同于天使最大生命值5%的伤害，持续8秒",
                    baseValue = 5f, valuePerLevel = 1.5f, maxLevel = 10,
                    maxEffect = "为天使增加护盾，吸收等同于天使最大生命值20%的伤害，持续12秒，护盾破碎时对周围敌人造成150%攻击力伤害",
                    fusionPath = "A01+A05→F-01", playStyle = "护盾流",
                    designNote = "基础护盾卡，新手教程解锁"
                },
                new CardData
                {
                    cardId = "A-02", cardName = "圣光弹", category = CardCategory.A_Attack,
                    rarity = CardRarity.N, target = CardTarget.Baby,
                    baseEffect = "向最近的婴灵发射圣光弹，造成80%攻击力伤害",
                    baseValue = 80f, valuePerLevel = 10f, maxLevel = 10,
                    maxEffect = "向最近的婴灵发射强化圣光弹，造成180%攻击力伤害，并弹射至2个额外目标",
                    fusionPath = "A02+A09→F-02", playStyle = "直伤流",
                    designNote = "基础远程攻击卡，初期主要输出手段"
                },
                new CardData
                {
                    cardId = "A-03", cardName = "圣光之墙", category = CardCategory.A_Attack,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "在天使前方生成光墙，阻挡飞行物并造成60%攻击力伤害",
                    baseValue = 60f, valuePerLevel = 8f, maxLevel = 8,
                    maxEffect = "生成强化光墙，阻挡所有飞行物并反弹造成120%攻击力伤害",
                    fusionPath = "A03+D01→F-03", playStyle = "阵地流",
                    designNote = "防御型卡牌，与地形卡有协同"
                },
                new CardData
                {
                    cardId = "A-04", cardName = "审判之剑", category = CardCategory.A_Attack,
                    rarity = CardRarity.R, target = CardTarget.Baby,
                    baseEffect = "召唤圣剑从天而降，对目标区域造成200%攻击力的AOE伤害",
                    baseValue = 200f, valuePerLevel = 25f, maxLevel = 8,
                    maxEffect = "召唤巨型圣剑，造成400%攻击力伤害并留下持续灼烧地面",
                    fusionPath = "A04+D04→F-04", playStyle = "爆发流",
                    designNote = "AOE清场卡，CD较长"
                },
                new CardData
                {
                    cardId = "A-05", cardName = "天使之翼", category = CardCategory.A_Attack,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "天使展开光翼，移动速度+20%，闪避率+15%，持续6秒",
                    baseValue = 20f, valuePerLevel = 3f, maxLevel = 8,
                    maxEffect = "天使展开强化光翼，移动速度+50%，闪避率+35%，穿过敌人造成伤害",
                    fusionPath = "A05+A01→F-01", playStyle = "机动流",
                    designNote = "机动性卡，配合A01可融合"
                },
                new CardData
                {
                    cardId = "A-06", cardName = "圣光洗礼", category = CardCategory.A_Attack,
                    rarity = CardRarity.SR, target = CardTarget.Both,
                    baseEffect = "释放净化光环，为天使回复15%生命值，对婴灵造成100%攻击力伤害",
                    baseValue = 15f, valuePerLevel = 3f, maxLevel = 7,
                    maxEffect = "释放强化净化光环，为天使回复35%生命值，对婴灵造成200%攻击力伤害并减速",
                    fusionPath = "A06+C03→F-05", playStyle = "回复流",
                    designNote = "兼顾治疗与伤害的SR卡"
                },
                new CardData
                {
                    cardId = "A-07", cardName = "天堂之怒", category = CardCategory.A_Attack,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "天使进入狂怒状态，攻击力+40%，攻击速度+25%，持续8秒",
                    baseValue = 40f, valuePerLevel = 5f, maxLevel = 7,
                    maxEffect = "天使进入终极狂怒，攻击力+80%，攻击速度+50%，击杀刷新持续时间",
                    fusionPath = "A07+E03→H-01", playStyle = "狂暴流",
                    designNote = "爆发型buff，适合Boss战"
                },
                new CardData
                {
                    cardId = "A-08", cardName = "圣光锁链", category = CardCategory.A_Attack,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "发射锁链束缚目标婴灵3秒，期间每秒造成50%攻击力伤害",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 7,
                    maxEffect = "发射强化锁链束缚目标及周围敌人5秒，期间每秒造成100%攻击力伤害",
                    fusionPath = "A08+I02→H-02", playStyle = "控制流",
                    designNote = "单体控制卡，对精英怪有效"
                },
                new CardData
                {
                    cardId = "A-09", cardName = "圣光弹幕", category = CardCategory.A_Attack,
                    rarity = CardRarity.SSR, target = CardTarget.Baby,
                    baseEffect = "向前方扇形区域发射12枚圣光弹，每枚造成60%攻击力伤害",
                    baseValue = 60f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "发射24枚强化圣光弹，每枚造成120%攻击力伤害，命中同一目标伤害递增",
                    fusionPath = "A09+A02→F-02", playStyle = "弹幕流",
                    designNote = "高密度弹幕，对大型敌人伤害极高"
                },
                new CardData
                {
                    cardId = "A-10", cardName = "天使降临", category = CardCategory.A_Attack,
                    rarity = CardRarity.SSR, target = CardTarget.Both,
                    baseEffect = "天使完全解放力量，全属性+30%，所有技能冷却减半，持续10秒",
                    baseValue = 30f, valuePerLevel = 5f, maxLevel = 6,
                    maxEffect = "天使完全解放力量，全属性+60%，所有技能无冷却，持续15秒",
                    fusionPath = "A10+H05→SSR终", playStyle = "全能流",
                    designNote = "终极爆发卡，游戏后期核心"
                },

                // =============================================
                // B类 - 婴灵控制 (8张: B01-B08)
                // =============================================
                new CardData
                {
                    cardId = "B-01", cardName = "安抚摇篮曲", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.N, target = CardTarget.Baby,
                    baseEffect = "降低周围婴灵20%移动速度，持续5秒",
                    baseValue = 20f, valuePerLevel = 3f, maxLevel = 10,
                    maxEffect = "降低周围婴灵50%移动速度并使其有30%概率陷入沉睡3秒",
                    fusionPath = "B01+G02→F-06", playStyle = "控场流",
                    designNote = "基础减速控制卡"
                },
                new CardData
                {
                    cardId = "B-02", cardName = "灵魂引导", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.N, target = CardTarget.Baby,
                    baseEffect = "引导一个婴灵改变移动方向，持续3秒",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 10,
                    maxEffect = "引导最多3个婴灵改变移动方向，持续6秒，引导期间婴灵互相碰撞造成伤害",
                    fusionPath = "B02+B05→H-03", playStyle = "引导流",
                    designNote = "方向控制型卡牌"
                },
                new CardData
                {
                    cardId = "B-03", cardName = "婴灵退散", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.R, target = CardTarget.Baby,
                    baseEffect = "击退周围婴灵并造成100%攻击力伤害",
                    baseValue = 100f, valuePerLevel = 12f, maxLevel = 8,
                    maxEffect = "击退周围婴灵造成200%攻击力伤害，并使其眩晕2秒",
                    fusionPath = "B03+D02→F-07", playStyle = "击退流",
                    designNote = "紧急保命卡"
                },
                new CardData
                {
                    cardId = "B-04", cardName = "灵魂链接", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.R, target = CardTarget.Both,
                    baseEffect = "与目标婴灵建立链接，将其受到的30%伤害转化为天使生命回复",
                    baseValue = 30f, valuePerLevel = 4f, maxLevel = 8,
                    maxEffect = "与目标婴灵建立强化链接，将其受到的60%伤害转化为天使生命回复并减速",
                    fusionPath = "B04+E07→F-08", playStyle = "吸血流",
                    designNote = "续航型控制卡"
                },
                new CardData
                {
                    cardId = "B-05", cardName = "群体魅惑", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "魅惑范围内最多3个婴灵，使其为天使战斗8秒",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 7,
                    maxEffect = "魅惑范围内最多6个婴灵，使其为天使战斗15秒，被魅惑单位攻击力+30%",
                    fusionPath = "B05+B02→H-03", playStyle = "召唤流",
                    designNote = "核心控制卡，可将敌人转化为友军"
                },
                new CardData
                {
                    cardId = "B-06", cardName = "灵魂囚笼", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "囚禁一个婴灵5秒，期间无法移动和攻击，每秒受到80%攻击力伤害",
                    baseValue = 80f, valuePerLevel = 10f, maxLevel = 7,
                    maxEffect = "囚禁一个婴灵及周围敌人8秒，期间无法行动，每秒受到150%攻击力伤害",
                    fusionPath = "B06+I01→H-04", playStyle = "禁锢流",
                    designNote = "对精英/Boss级婴灵有额外效果"
                },
                new CardData
                {
                    cardId = "B-07", cardName = "灵魂风暴", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.SSR, target = CardTarget.Baby,
                    baseEffect = "制造灵魂风暴，范围内所有婴灵被持续拉扯到中心，每秒造成120%攻击力伤害，持续6秒",
                    baseValue = 120f, valuePerLevel = 15f, maxLevel = 6,
                    maxEffect = "制造巨型灵魂风暴，全屏婴灵被拉扯并每秒造成250%攻击力伤害，持续10秒",
                    fusionPath = "B07+C07→SSR终", playStyle = "黑洞流",
                    designNote = "终极控场卡，全屏聚怪"
                },
                new CardData
                {
                    cardId = "B-08", cardName = "净化之泪", category = CardCategory.B_BabyControl,
                    rarity = CardRarity.SSR, target = CardTarget.Baby,
                    baseEffect = "天使流下净化之泪，范围内婴灵每秒损失5%最大生命值，持续8秒",
                    baseValue = 5f, valuePerLevel = 1f, maxLevel = 6,
                    maxEffect = "天使流下圣泪，范围内婴灵每秒损失12%最大生命值，精英/Boss额外受到20%伤害",
                    fusionPath = "B08+G05→SSR终", playStyle = "百分比流",
                    designNote = "百分比伤害卡，对高血量敌人极为有效"
                },

                // =============================================
                // C类 - 光环 (12张: C01-C12)
                // =============================================
                new CardData
                {
                    cardId = "C-01", cardName = "攻击光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.N, target = CardTarget.Angel,
                    baseEffect = "周围友军攻击力+10%",
                    baseValue = 10f, valuePerLevel = 1.5f, maxLevel = 10,
                    maxEffect = "周围友军攻击力+25%，击杀敌人后攻击力额外+10%持续3秒",
                    fusionPath = "C01+C03→F-09", playStyle = "光环流",
                    designNote = "基础攻击光环"
                },
                new CardData
                {
                    cardId = "C-02", cardName = "防御光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.N, target = CardTarget.Angel,
                    baseEffect = "周围友军受到的伤害-10%",
                    baseValue = 10f, valuePerLevel = 1.5f, maxLevel = 10,
                    maxEffect = "周围友军受到的伤害-25%，生命低于30%时减伤翻倍",
                    fusionPath = "C02+C04→F-10", playStyle = "光环流",
                    designNote = "基础防御光环"
                },
                new CardData
                {
                    cardId = "C-03", cardName = "回复光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "周围友军每秒回复1%最大生命值",
                    baseValue = 1f, valuePerLevel = 0.15f, maxLevel = 8,
                    maxEffect = "周围友军每秒回复2.5%最大生命值，生命满时转为护盾",
                    fusionPath = "C03+C01→F-09", playStyle = "光环流",
                    designNote = "续航光环"
                },
                new CardData
                {
                    cardId = "C-04", cardName = "速度光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "周围友军移动速度+15%，攻击速度+10%",
                    baseValue = 15f, valuePerLevel = 2f, maxLevel = 8,
                    maxEffect = "周围友军移动速度+35%，攻击速度+25%，闪避率+15%",
                    fusionPath = "C04+C02→F-10", playStyle = "光环流",
                    designNote = "机动性光环"
                },
                new CardData
                {
                    cardId = "C-05", cardName = "荆棘光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "周围友军受到攻击时反弹30%伤害给攻击者",
                    baseValue = 30f, valuePerLevel = 4f, maxLevel = 8,
                    maxEffect = "周围友军受到攻击时反弹60%伤害并附加灼烧效果",
                    fusionPath = "C05+D03→F-11", playStyle = "反伤流",
                    designNote = "反伤光环"
                },
                new CardData
                {
                    cardId = "C-06", cardName = "恐惧光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "进入光环范围的婴灵移动速度-25%，有10%概率逃跑",
                    baseValue = 25f, valuePerLevel = 3f, maxLevel = 7,
                    maxEffect = "进入光环范围的婴灵移动速度-50%，有30%概率逃跑，逃跑时受到双倍伤害",
                    fusionPath = "C06+G03→F-12", playStyle = "恐惧流",
                    designNote = "敌方减益光环"
                },
                new CardData
                {
                    cardId = "C-07", cardName = "圣光光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SR, target = CardTarget.Both,
                    baseEffect = "光环内友军全属性+8%，敌人全属性-8%",
                    baseValue = 8f, valuePerLevel = 1f, maxLevel = 7,
                    maxEffect = "光环内友军全属性+15%，敌人全属性-15%，持续伤害每秒3%",
                    fusionPath = "C07+C11→SSR终", playStyle = "全能光环流",
                    designNote = "双向光环，攻防一体"
                },
                new CardData
                {
                    cardId = "C-08", cardName = "幸运光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "周围友军暴击率+15%，暴击伤害+20%",
                    baseValue = 15f, valuePerLevel = 2f, maxLevel = 7,
                    maxEffect = "周围友军暴击率+30%，暴击伤害+40%，暴击时回复5%生命值",
                    fusionPath = "C08+E11→F-13", playStyle = "暴击流",
                    designNote = "暴击增益光环"
                },
                new CardData
                {
                    cardId = "C-09", cardName = "元素光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "周围友军攻击附带随机元素伤害(火/冰/雷)，额外造成25%伤害",
                    baseValue = 25f, valuePerLevel = 3f, maxLevel = 7,
                    maxEffect = "周围友军攻击附带强化元素伤害，额外造成50%伤害并触发元素反应",
                    fusionPath = "C09+I03→H-05", playStyle = "元素流",
                    designNote = "元素增伤光环"
                },
                new CardData
                {
                    cardId = "C-10", cardName = "吸血光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SSR, target = CardTarget.Angel,
                    baseEffect = "周围友军造成伤害的8%转化为生命回复",
                    baseValue = 8f, valuePerLevel = 1f, maxLevel = 6,
                    maxEffect = "周围友军造成伤害的15%转化为生命回复，过量治疗转为护盾",
                    fusionPath = "C10+B04→SSR终", playStyle = "吸血光环流",
                    designNote = "强力续航光环"
                },
                new CardData
                {
                    cardId = "C-11", cardName = "时间光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SSR, target = CardTarget.Both,
                    baseEffect = "光环内友军技能冷却速度+25%，敌人攻击和移动速度-15%",
                    baseValue = 25f, valuePerLevel = 3f, maxLevel = 6,
                    maxEffect = "光环内友军技能冷却速度+50%，敌人攻击和移动速度-35%并偶尔冻结",
                    fusionPath = "C11+C07→SSR终", playStyle = "时间流",
                    designNote = "改变战斗节奏的核心光环"
                },
                new CardData
                {
                    cardId = "C-12", cardName = "终极光环", category = CardCategory.C_Aura,
                    rarity = CardRarity.SSR, target = CardTarget.Both,
                    baseEffect = "同时获得攻击+12%、防御+12%、回复每秒1.5%、速度+12%光环效果",
                    baseValue = 12f, valuePerLevel = 2f, maxLevel = 6,
                    maxEffect = "同时获得攻击+25%、防御+25%、回复每秒3%、速度+25%光环效果，免疫控制",
                    fusionPath = "C12+全C类→终极", playStyle = "全能光环流",
                    designNote = "终极光环，需要收集全部C类卡牌解锁融合"
                },

                // =============================================
                // D类 - 地形 (8张: D01-D08)
                // =============================================
                new CardData
                {
                    cardId = "D-01", cardName = "圣光地面", category = CardCategory.D_Terrain,
                    rarity = CardRarity.N, target = CardTarget.Terrain,
                    baseEffect = "将脚下地面变为圣光地面，友军在其上每秒回复1%生命值，持续10秒",
                    baseValue = 1f, valuePerLevel = 0.2f, maxLevel = 10,
                    maxEffect = "圣光地面持续15秒，友军每秒回复3%生命值且攻击力+15%",
                    fusionPath = "D01+D05→F-14", playStyle = "阵地流",
                    designNote = "基础地形改造卡"
                },
                new CardData
                {
                    cardId = "D-02", cardName = "荆棘之地", category = CardCategory.D_Terrain,
                    rarity = CardRarity.N, target = CardTarget.Terrain,
                    baseEffect = "制造荆棘地面，敌人经过时每秒受到40%攻击力伤害，持续8秒",
                    baseValue = 40f, valuePerLevel = 5f, maxLevel = 10,
                    maxEffect = "制造强化荆棘地面持续12秒，每秒造成80%攻击力伤害并减速30%",
                    fusionPath = "D02+D06→F-15", playStyle = "陷阱流",
                    designNote = "伤害型地形"
                },
                new CardData
                {
                    cardId = "D-03", cardName = "圣火之墙", category = CardCategory.D_Terrain,
                    rarity = CardRarity.R, target = CardTarget.Terrain,
                    baseEffect = "生成一道横向火墙，敌人穿过时受到150%攻击力伤害并灼烧5秒",
                    baseValue = 150f, valuePerLevel = 20f, maxLevel = 8,
                    maxEffect = "生成强化火墙，敌人穿过时受到300%攻击力伤害，灼烧10秒且无法熄灭",
                    fusionPath = "D03+C05→F-11", playStyle = "火墙流",
                    designNote = "阻挡型地形"
                },
                new CardData
                {
                    cardId = "D-04", cardName = "圣雷领域", category = CardCategory.D_Terrain,
                    rarity = CardRarity.R, target = CardTarget.Terrain,
                    baseEffect = "制造雷电场域，每2秒对范围内敌人造成100%攻击力的雷电伤害，持续10秒",
                    baseValue = 100f, valuePerLevel = 12f, maxLevel = 8,
                    maxEffect = "制造强化雷电场域持续15秒，每1.5秒造成200%伤害并连锁3个敌人",
                    fusionPath = "D04+A04→F-04", playStyle = "雷电场流",
                    designNote = "持续性AOE地形"
                },
                new CardData
                {
                    cardId = "D-05", cardName = "冰封之地", category = CardCategory.D_Terrain,
                    rarity = CardRarity.SR, target = CardTarget.Terrain,
                    baseEffect = "制造冰封地面，敌人移动速度-40%，有10%概率冻结2秒，持续10秒",
                    baseValue = 40f, valuePerLevel = 5f, maxLevel = 7,
                    maxEffect = "制造强化冰封地面持续15秒，敌人移速-70%，冻结概率30%且冻结时间5秒",
                    fusionPath = "D05+D01→F-14", playStyle = "冰冻流",
                    designNote = "控制型地形"
                },
                new CardData
                {
                    cardId = "D-06", cardName = "毒雾沼泽", category = CardCategory.D_Terrain,
                    rarity = CardRarity.SR, target = CardTarget.Terrain,
                    baseEffect = "制造毒雾沼泽，敌人每秒受到60%攻击力伤害并叠加中毒(每层+10%伤害)，持续10秒",
                    baseValue = 60f, valuePerLevel = 8f, maxLevel = 7,
                    maxEffect = "制造强化毒雾沼泽持续15秒，中毒可无限叠加，满10层立即造成500%伤害",
                    fusionPath = "D06+D02→F-15", playStyle = "中毒流",
                    designNote = "叠毒型地形"
                },
                new CardData
                {
                    cardId = "D-07", cardName = "时空裂隙", category = CardCategory.D_Terrain,
                    rarity = CardRarity.SSR, target = CardTarget.Terrain,
                    baseEffect = "制造时空裂隙，范围内敌人时间流速减半，友军技能冷却加速50%，持续8秒",
                    baseValue = 50f, valuePerLevel = 5f, maxLevel = 6,
                    maxEffect = "制造强化时空裂隙持续12秒，敌人几乎静止，友军技能冷却加速100%",
                    fusionPath = "D07+C11→SSR终", playStyle = "时空流",
                    designNote = "改变时间流速的高级地形"
                },
                new CardData
                {
                    cardId = "D-08", cardName = "天堂之门", category = CardCategory.D_Terrain,
                    rarity = CardRarity.SSR, target = CardTarget.Terrain,
                    baseEffect = "在地面开启天堂之门，持续12秒，每3秒召唤一个天使幻影协助战斗",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 6,
                    maxEffect = "天堂之门持续20秒，每2秒召唤一个强化天使幻影，最多同时存在5个",
                    fusionPath = "D08+I05→SSR终", playStyle = "召唤流",
                    designNote = "召唤型终极地形"
                },

                // =============================================
                // E类 - 被动 (12张: E01-E12)
                // =============================================
                new CardData
                {
                    cardId = "E-01", cardName = "坚韧之心", category = CardCategory.E_Passive,
                    rarity = CardRarity.N, target = CardTarget.Angel,
                    baseEffect = "天使最大生命值永久+8%",
                    baseValue = 8f, valuePerLevel = 1f, maxLevel = 10,
                    maxEffect = "天使最大生命值永久+18%，受到致命伤害时保留1点生命(冷却120秒)",
                    fusionPath = "E01+E02→F-16", playStyle = "肉盾流",
                    designNote = "基础生存被动"
                },
                new CardData
                {
                    cardId = "E-02", cardName = "不屈意志", category = CardCategory.E_Passive,
                    rarity = CardRarity.N, target = CardTarget.Angel,
                    baseEffect = "天使受到的控制效果持续时间-15%",
                    baseValue = 15f, valuePerLevel = 2f, maxLevel = 10,
                    maxEffect = "天使受到的控制效果持续时间-35%，被控制时有50%概率立即解除",
                    fusionPath = "E02+E01→F-16", playStyle = "韧性流",
                    designNote = "抗控制被动"
                },
                new CardData
                {
                    cardId = "E-03", cardName = "战斗狂热", category = CardCategory.E_Passive,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "天使每次攻击提升1%攻击力，最多叠加15层，持续5秒",
                    baseValue = 15f, valuePerLevel = 2f, maxLevel = 8,
                    maxEffect = "天使每次攻击提升1.5%攻击力，最多叠加30层，满层时攻击速度+30%",
                    fusionPath = "E03+A07→H-01", playStyle = "叠加流",
                    designNote = "越战越强的被动"
                },
                new CardData
                {
                    cardId = "E-04", cardName = "精准打击", category = CardCategory.E_Passive,
                    rarity = CardRarity.R, target = CardTarget.Angel,
                    baseEffect = "天使暴击率永久+8%，暴击伤害+15%",
                    baseValue = 8f, valuePerLevel = 1f, maxLevel = 8,
                    maxEffect = "天使暴击率永久+16%，暴击伤害+30%，暴击时无视目标20%防御",
                    fusionPath = "E04+E11→F-17", playStyle = "暴击流",
                    designNote = "暴击被动"
                },
                new CardData
                {
                    cardId = "E-05", cardName = "灵魂共鸣", category = CardCategory.E_Passive,
                    rarity = CardRarity.R, target = CardTarget.Both,
                    baseEffect = "天使击杀婴灵时回复3%最大生命值",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 8,
                    maxEffect = "天使击杀婴灵时回复8%最大生命值，过量治疗转化为护盾",
                    fusionPath = "E05+C10→F-18", playStyle = "击杀回复流",
                    designNote = "击杀回复被动"
                },
                new CardData
                {
                    cardId = "E-06", cardName = "光之庇护", category = CardCategory.E_Passive,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "每30秒获得一个吸收伤害10%最大生命值的护盾",
                    baseValue = 10f, valuePerLevel = 1.5f, maxLevel = 7,
                    maxEffect = "每20秒获得一个吸收伤害25%最大生命值的护盾，护盾存在期间攻击力+20%",
                    fusionPath = "E06+A01→F-19", playStyle = "护盾流",
                    designNote = "周期性护盾被动"
                },
                new CardData
                {
                    cardId = "E-07", cardName = "生命窃取", category = CardCategory.E_Passive,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "天使造成的所有伤害的5%转化为生命回复",
                    baseValue = 5f, valuePerLevel = 0.8f, maxLevel = 7,
                    maxEffect = "天使造成的所有伤害的12%转化为生命回复，生命满时提升攻击力",
                    fusionPath = "E07+B04→F-08", playStyle = "吸血流",
                    designNote = "全局吸血被动"
                },
                new CardData
                {
                    cardId = "E-08", cardName = "复仇天使", category = CardCategory.E_Passive,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "天使生命每降低10%，攻击力+3%",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 7,
                    maxEffect = "天使生命每降低10%，攻击力+7%，生命低于20%时额外+30%",
                    fusionPath = "E08+G01→F-20", playStyle = "绝境流",
                    designNote = "低血量高伤害被动"
                },
                new CardData
                {
                    cardId = "E-09", cardName = "圣光印记", category = CardCategory.E_Passive,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "天使攻击会给敌人施加圣光印记，叠满5层引爆造成200%攻击力伤害",
                    baseValue = 200f, valuePerLevel = 25f, maxLevel = 7,
                    maxEffect = "圣光印记叠满5层引爆造成400%攻击力伤害，并传染给周围敌人1层",
                    fusionPath = "E09+I04→H-04", playStyle = "印记流",
                    designNote = "叠层引爆被动"
                },
                new CardData
                {
                    cardId = "E-10", cardName = "光速反应", category = CardCategory.E_Passive,
                    rarity = CardRarity.SSR, target = CardTarget.Angel,
                    baseEffect = "天使闪避率永久+12%，成功闪避后下一次攻击必定暴击",
                    baseValue = 12f, valuePerLevel = 2f, maxLevel = 6,
                    maxEffect = "天使闪避率永久+25%，成功闪避后进入子弹时间2秒(冷却15秒)",
                    fusionPath = "E10+A05→SSR终", playStyle = "闪避流",
                    designNote = "高风险高回报被动"
                },
                new CardData
                {
                    cardId = "E-11", cardName = "致命节奏", category = CardCategory.E_Passive,
                    rarity = CardRarity.SSR, target = CardTarget.Angel,
                    baseEffect = "天使暴击后攻击速度+5%，最多叠加6层，持续4秒",
                    baseValue = 6f, valuePerLevel = 1f, maxLevel = 6,
                    maxEffect = "天使暴击后攻击速度+8%，最多叠加12层，满层时所有攻击必定暴击",
                    fusionPath = "E11+E04→F-17", playStyle = "攻速暴击流",
                    designNote = "暴击加速被动"
                },
                new CardData
                {
                    cardId = "E-12", cardName = "不朽守护", category = CardCategory.E_Passive,
                    rarity = CardRarity.SSR, target = CardTarget.Angel,
                    baseEffect = "天使死亡时立即复活并回复30%生命值(每场战斗仅一次)",
                    baseValue = 30f, valuePerLevel = 5f, maxLevel = 6,
                    maxEffect = "天使死亡时立即复活并回复60%生命值，复活后3秒内无敌且攻击力+100%",
                    fusionPath = "E12+全E类→终极", playStyle = "复活流",
                    designNote = "终极保命被动"
                },

                // =============================================
                // F类 - 成长 (7张: F01-F07)
                // =============================================
                new CardData
                {
                    cardId = "F-01", cardName = "圣光进化·护盾", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "融合A01+A05：护盾吸收量+50%，护盾破碎时释放圣光冲击",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "护盾吸收量+100%，护盾破碎时释放圣光冲击并重置天使之翼冷却",
                    fusionPath = "F01→终极护盾", playStyle = "护盾融合流",
                    designNote = "A01+A05融合卡"
                },
                new CardData
                {
                    cardId = "F-02", cardName = "圣光进化·弹幕", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "融合A02+A09：圣光弹数量+50%，弹幕密度翻倍",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "圣光弹数量翻倍，每颗弹丸可分裂为2颗小弹丸",
                    fusionPath = "F02→终极弹幕", playStyle = "弹幕融合流",
                    designNote = "A02+A09融合卡"
                },
                new CardData
                {
                    cardId = "F-03", cardName = "圣光进化·壁垒", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "融合A03+D01：光墙持续时间+50%，光墙附带回复光环",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "光墙持续时间+100%，光墙附带攻击+回复双重光环",
                    fusionPath = "F03→终极壁垒", playStyle = "阵地融合流",
                    designNote = "A03+D01融合卡"
                },
                new CardData
                {
                    cardId = "F-04", cardName = "圣光进化·审判", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "融合A04+D04：审判之剑落地后生成雷电场域，持续造成伤害",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "审判之剑落地后生成超大雷电场域，每剑落地召唤一道连锁闪电",
                    fusionPath = "F04→终极审判", playStyle = "雷火融合流",
                    designNote = "A04+D04融合卡"
                },
                new CardData
                {
                    cardId = "F-05", cardName = "圣光进化·净化", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Both,
                    baseEffect = "融合A06+C03：净化范围+50%，同时移除友军debuff和敌人buff",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "净化范围+100%，净化时友军获得短暂无敌，敌人被沉默3秒",
                    fusionPath = "F05→终极净化", playStyle = "净化融合流",
                    designNote = "A06+C03融合卡"
                },
                new CardData
                {
                    cardId = "F-06", cardName = "圣光进化·安眠", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "融合B01+G02：沉睡概率+50%，沉睡中的敌人受到的首次伤害翻倍",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "沉睡概率+100%，沉睡中的敌人受到伤害+200%且无法被唤醒",
                    fusionPath = "F06→终极安眠", playStyle = "睡眠融合流",
                    designNote = "B01+G02融合卡"
                },
                new CardData
                {
                    cardId = "F-07", cardName = "圣光进化·震退", category = CardCategory.F_Growth,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "融合B03+D02：击退距离+100%，被击退敌人撞墙受到额外伤害",
                    baseValue = 50f, valuePerLevel = 8f, maxLevel = 6,
                    maxEffect = "击退距离+200%，撞墙伤害翻倍，击退路径上所有敌人也被击退",
                    fusionPath = "F07→终极震退", playStyle = "击退融合流",
                    designNote = "B03+D02融合卡"
                },

                // =============================================
                // G类 - 情感 (5张: G01-G05)
                // =============================================
                new CardData
                {
                    cardId = "G-01", cardName = "守护之心", category = CardCategory.G_Emotion,
                    rarity = CardRarity.SR, target = CardTarget.Angel,
                    baseEffect = "天使对婴灵的伤害+15%，天使受到婴灵的伤害-15%",
                    baseValue = 15f, valuePerLevel = 3f, maxLevel = 7,
                    maxEffect = "天使对婴灵的伤害+35%，天使受到婴灵的伤害-35%，婴灵优先攻击天使",
                    fusionPath = "G01+E08→F-20", playStyle = "守护流",
                    designNote = "守护情感的具现化"
                },
                new CardData
                {
                    cardId = "G-02", cardName = "怜悯之歌", category = CardCategory.G_Emotion,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "婴灵有15%概率放弃攻击并原地徘徊3秒",
                    baseValue = 15f, valuePerLevel = 2f, maxLevel = 7,
                    maxEffect = "婴灵有30%概率放弃攻击并原地徘徊6秒，徘徊结束后有概率直接消失",
                    fusionPath = "G02+B01→F-06", playStyle = "怜悯流",
                    designNote = "怜悯情感的具现化"
                },
                new CardData
                {
                    cardId = "G-03", cardName = "恐惧之触", category = CardCategory.G_Emotion,
                    rarity = CardRarity.SR, target = CardTarget.Baby,
                    baseEffect = "天使暴击时使目标婴灵陷入恐惧2秒(逃跑并无法攻击)",
                    baseValue = 2f, valuePerLevel = 0.3f, maxLevel = 7,
                    maxEffect = "天使暴击时使目标及周围婴灵陷入恐惧4秒，恐惧中的敌人受到伤害+30%",
                    fusionPath = "G03+C06→F-12", playStyle = "恐惧流",
                    designNote = "恐惧情感的具现化"
                },
                new CardData
                {
                    cardId = "G-04", cardName = "希望之光", category = CardCategory.G_Emotion,
                    rarity = CardRarity.SSR, target = CardTarget.Both,
                    baseEffect = "每波次开始时，天使获得一层希望(攻击力+3%)，最多5层",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 6,
                    maxEffect = "每波次开始时获得希望，最多10层(每层+5%攻击力)，满层时解锁终极技能",
                    fusionPath = "G04+E12→SSR终", playStyle = "希望流",
                    designNote = "希望情感的具现化，越战越强"
                },
                new CardData
                {
                    cardId = "G-05", cardName = "爱之牺牲", category = CardCategory.G_Emotion,
                    rarity = CardRarity.SSR, target = CardTarget.Both,
                    baseEffect = "天使消耗15%当前生命值，对全场婴灵造成等同于消耗生命值300%的伤害",
                    baseValue = 300f, valuePerLevel = 40f, maxLevel = 6,
                    maxEffect = "天使消耗25%当前生命值，对全场婴灵造成等同于消耗生命值600%的伤害并眩晕",
                    fusionPath = "G05+B08→SSR终", playStyle = "牺牲流",
                    designNote = "爱之牺牲的具现化，以血换伤"
                },

                // =============================================
                // H类 - 连携 (5张: H01-H05)
                // =============================================
                new CardData
                {
                    cardId = "H-01", cardName = "狂怒连携", category = CardCategory.H_Combo,
                    rarity = CardRarity.SSR, target = CardTarget.Angel,
                    baseEffect = "A07+E03融合：天堂之怒期间每次攻击额外叠加战斗狂热层数",
                    baseValue = 2f, valuePerLevel = 0.5f, maxLevel = 5,
                    maxEffect = "天堂之怒期间每次攻击叠3层战斗狂热，满层后天堂之怒持续时间刷新",
                    fusionPath = "H01→终极狂怒", playStyle = "狂怒连携流",
                    designNote = "攻击+被动连携，产生协同爆发"
                },
                new CardData
                {
                    cardId = "H-02", cardName = "束缚连携", category = CardCategory.H_Combo,
                    rarity = CardRarity.SSR, target = CardTarget.Baby,
                    baseEffect = "A08+I02融合：圣光锁链束缚期间，地形活化伤害+100%",
                    baseValue = 100f, valuePerLevel = 15f, maxLevel = 5,
                    maxEffect = "锁链束缚期间地形活化伤害+200%，锁链断裂时造成剩余伤害总和",
                    fusionPath = "H02→终极束缚", playStyle = "束缚连携流",
                    designNote = "控制+地形活化连携"
                },
                new CardData
                {
                    cardId = "H-03", cardName = "魅惑连携", category = CardCategory.H_Combo,
                    rarity = CardRarity.SSR, target = CardTarget.Baby,
                    baseEffect = "B05+B02融合：被魅惑的婴灵自动使用灵魂引导效果，引导其他婴灵",
                    baseValue = 1f, valuePerLevel = 0.2f, maxLevel = 5,
                    maxEffect = "被魅惑的婴灵自动引导其他婴灵，引导成功后自身能力翻倍",
                    fusionPath = "H03→终极魅惑", playStyle = "魅惑连携流",
                    designNote = "控制+引导连携，滚雪球效应"
                },
                new CardData
                {
                    cardId = "H-04", cardName = "印记连携", category = CardCategory.H_Combo,
                    rarity = CardRarity.SSR, target = CardTarget.Baby,
                    baseEffect = "E09+B06融合：被囚笼困住的敌人自动叠加圣光印记(每秒2层)",
                    baseValue = 2f, valuePerLevel = 0.5f, maxLevel = 5,
                    maxEffect = "被囚笼困住的敌人每秒叠加4层印记，引爆时必定暴击且伤害翻倍",
                    fusionPath = "H04→终极印记", playStyle = "印记连携流",
                    designNote = "被动+控制连携，快速叠层引爆"
                },
                new CardData
                {
                    cardId = "H-05", cardName = "元素连携", category = CardCategory.H_Combo,
                    rarity = CardRarity.SSR, target = CardTarget.Both,
                    baseEffect = "C09+I03融合：元素光环激活时，所有地形活化效果触发元素反应",
                    baseValue = 1f, valuePerLevel = 0.2f, maxLevel = 5,
                    maxEffect = "元素光环激活时地形活化触发双重元素反应，伤害+150%且附加debuff",
                    fusionPath = "H05→终极元素", playStyle = "元素连携流",
                    designNote = "光环+地形活化连携，元素反应体系核心"
                },

                // =============================================
                // I类 - 地形活化 (5张: I01-I05)
                // =============================================
                new CardData
                {
                    cardId = "I-01", cardName = "活化荆棘", category = CardCategory.I_TerrainActivation,
                    rarity = CardRarity.SR, target = CardTarget.Terrain,
                    baseEffect = "激活场上所有荆棘地形，荆棘自动追踪并攻击最近的敌人，持续6秒",
                    baseValue = 6f, valuePerLevel = 1f, maxLevel = 7,
                    maxEffect = "激活荆棘追踪攻击持续12秒，每次攻击附带中毒效果",
                    fusionPath = "I01+B06→H-04", playStyle = "活化流",
                    designNote = "荆棘地形活化"
                },
                new CardData
                {
                    cardId = "I-02", cardName = "活化圣火", category = CardCategory.I_TerrainActivation,
                    rarity = CardRarity.SR, target = CardTarget.Terrain,
                    baseEffect = "激活场上所有火墙/圣火地形，火焰蔓延至相邻区域，持续8秒",
                    baseValue = 8f, valuePerLevel = 1f, maxLevel = 7,
                    maxEffect = "激活火墙蔓延持续15秒，火焰可跨越间隙，灼烧伤害翻倍",
                    fusionPath = "I02+A08→H-02", playStyle = "活化流",
                    designNote = "火焰地形活化"
                },
                new CardData
                {
                    cardId = "I-03", cardName = "活化雷电", category = CardCategory.I_TerrainActivation,
                    rarity = CardRarity.SR, target = CardTarget.Terrain,
                    baseEffect = "激活场上所有雷电场域，雷电连锁范围翻倍，持续8秒",
                    baseValue = 8f, valuePerLevel = 1f, maxLevel = 7,
                    maxEffect = "激活雷电场域持续15秒，雷电连锁范围翻3倍，连锁数量无上限",
                    fusionPath = "I03+C09→H-05", playStyle = "活化流",
                    designNote = "雷电地形活化"
                },
                new CardData
                {
                    cardId = "I-04", cardName = "活化冰霜", category = CardCategory.I_TerrainActivation,
                    rarity = CardRarity.SR, target = CardTarget.Terrain,
                    baseEffect = "激活场上所有冰封地形，冻结范围内的所有敌人3秒",
                    baseValue = 3f, valuePerLevel = 0.5f, maxLevel = 7,
                    maxEffect = "激活冰封地形冻结所有敌人6秒，冻结结束后敌人碎裂受到最大生命值20%伤害",
                    fusionPath = "I04+E09→H-04", playStyle = "活化流",
                    designNote = "冰霜地形活化"
                },
                new CardData
                {
                    cardId = "I-05", cardName = "活化天堂", category = CardCategory.I_TerrainActivation,
                    rarity = CardRarity.SSR, target = CardTarget.Terrain,
                    baseEffect = "激活场上所有天堂之门，召唤速度翻倍，召唤物属性+50%，持续10秒",
                    baseValue = 10f, valuePerLevel = 1.5f, maxLevel = 6,
                    maxEffect = "激活天堂之门持续18秒，召唤速度翻3倍，召唤物属性+100%且无敌",
                    fusionPath = "I05+D08→SSR终", playStyle = "活化流",
                    designNote = "天堂之门终极活化"
                }
            };
        }
    }
}
