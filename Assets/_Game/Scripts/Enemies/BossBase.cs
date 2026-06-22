using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AngelGuardian.Enemies
{
    /// <summary>
    /// Boss阶段数据
    /// </summary>
    [Serializable]
    public class BossPhase
    {
        [Tooltip("阶段名称")]
        public string phaseName = "Phase 1";

        [Tooltip("触发此阶段的HP阈值 (0~1)")]
        public float hpThreshold = 1f;

        [Tooltip("阶段攻击力倍率")]
        public float attackMultiplier = 1f;

        [Tooltip("阶段速度倍率")]
        public float speedMultiplier = 1f;

        [Tooltip("阶段防御倍率")]
        public float defenseMultiplier = 1f;

        [Tooltip("阶段新增技能ID列表")]
        public List<string> newSkillIds = new List<string>();

        [Tooltip("阶段切换特效预制体")]
        public GameObject phaseTransitionEffect;

        [Tooltip("阶段切换音效")]
        public AudioClip phaseTransitionSFX;

        [Tooltip("阶段持续期间特效")]
        public GameObject phaseAuraEffect;

        [Tooltip("阶段是否已激活")]
        [HideInInspector] public bool isActivated = false;
    }

    /// <summary>
    /// Boss基类 - 继承EnemyBase，增加多阶段管理、出场动画、专属UI等功能
    /// </summary>
    public abstract class BossBase : EnemyBase
    {
        #region Boss属性

        [Header("=== Boss信息 ===")]
        [SerializeField] protected string bossTitle = "Boss";
        [SerializeField] protected bool showBossIntro = true;
        [SerializeField] protected float introDuration = 3f;

        #endregion

        #region 多阶段管理

        [Header("=== 阶段配置 ===")]
        [SerializeField] protected List<BossPhase> phases = new List<BossPhase>();
        protected int currentPhaseIndex = -1;
        protected int previousPhaseIndex = -1;

        public int CurrentPhaseIndex => currentPhaseIndex;
        public BossPhase CurrentPhase => currentPhaseIndex >= 0 && currentPhaseIndex < phases.Count ? phases[currentPhaseIndex] : null;

        #endregion

        #region Boss UI

        [Header("=== Boss UI ===")]
        [SerializeField] protected GameObject bossHPBarPrefab;
        [SerializeField] protected Vector3 hpBarOffset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] protected Color bossHPColor = new Color(0.8f, 0.1f, 0.1f, 1f);

        protected GameObject bossHPBarInstance;

        #endregion

        #region Boss特效

        [Header("=== Boss特效 ===")]
        [SerializeField] protected GameObject entranceEffect;
        [SerializeField] protected AudioClip entranceSFX;
        [SerializeField] protected AudioClip bossBGMTrack;
        [SerializeField] protected GameObject deathEffect;
        [SerializeField] protected AudioClip deathSFX;
        [SerializeField] protected float deathEffectDuration = 3f;

        #endregion

        #region 特殊掉落

        [Header("=== Boss掉落 ===")]
        [SerializeField] protected GameObject[] bossSpecialDrops;
        [SerializeField] protected float guaranteedDropChance = 1f;
        [SerializeField] protected int bonusExperience = 500;
        [SerializeField] protected int bonusGold = 200;

        #endregion

        #region 事件

        /// <summary>阶段切换事件</summary>
        public event Action<string, int, string> OnPhaseChanged;

        /// <summary>Boss出场完成事件</summary>
        public event Action<string> OnIntroComplete;

        /// <summary>Boss技能释放事件</summary>
        public event Action<string, string> OnSkillUsed;

        #endregion

        #region Unity生命周期

        protected override void Start()
        {
            base.Start();

            // 创建Boss血条
            CreateBossHPBar();

            // 初始化阶段
            InitializePhases();

            // 开始出场动画
            if (showBossIntro)
            {
                StartCoroutine(BossIntroSequence());
            }
        }

        protected override void Update()
        {
            base.Update();

            // 检查阶段切换
            CheckPhaseTransition();

            // 更新Boss血条位置
            UpdateBossHPBar();
        }

        #endregion

        #region 阶段管理

        /// <summary>
        /// 初始化所有阶段
        /// </summary>
        protected virtual void InitializePhases()
        {
            // 确保阶段按HP阈值降序排列（高HP → 低HP）
            phases.Sort((a, b) => b.hpThreshold.CompareTo(a.hpThreshold));

            // 激活第一个阶段（满血阶段）
            if (phases.Count > 0)
            {
                TransitionToPhase(0);
            }
        }

        /// <summary>
        /// 检查是否需要阶段切换
        /// </summary>
        protected virtual void CheckPhaseTransition()
        {
            if (currentState == EnemyState.Dead) return;

            float hpPercent = HPPercentage;

            // 从高阶段向低阶段检查
            for (int i = phases.Count - 1; i >= 0; i--)
            {
                BossPhase phase = phases[i];
                if (!phase.isActivated && hpPercent <= phase.hpThreshold)
                {
                    TransitionToPhase(i);
                    break;
                }
            }
        }

        /// <summary>
        /// 切换到指定阶段
        /// </summary>
        protected virtual void TransitionToPhase(int newPhaseIndex)
        {
            if (newPhaseIndex < 0 || newPhaseIndex >= phases.Count) return;
            if (phases[newPhaseIndex].isActivated) return;

            previousPhaseIndex = currentPhaseIndex;

            // 停用旧阶段光环
            if (currentPhaseIndex >= 0 && currentPhaseIndex < phases.Count)
            {
                if (phases[currentPhaseIndex].phaseAuraEffect != null)
                {
                    Destroy(phases[currentPhaseIndex].phaseAuraEffect);
                }
            }

            currentPhaseIndex = newPhaseIndex;
            BossPhase newPhase = phases[newPhaseIndex];
            newPhase.isActivated = true;

            // 应用阶段属性修正
            ApplyPhaseModifiers(newPhase);

            // 播放阶段切换特效
            PlayPhaseTransitionEffect(newPhase);

            // 激活新阶段光环
            if (newPhase.phaseAuraEffect != null)
            {
                GameObject aura = Instantiate(newPhase.phaseAuraEffect, transform);
                aura.transform.localPosition = Vector3.zero;
            }

            // 更新Boss血条颜色
            UpdateBossHPBarColor(newPhaseIndex);

            OnPhaseChanged?.Invoke(enemyId, newPhaseIndex, newPhase.phaseName);
            Debug.Log($"[Boss] {bossTitle} 进入阶段 {newPhaseIndex + 1}: {newPhase.phaseName} (HP <= {newPhase.hpThreshold * 100}%)");
        }

        /// <summary>
        /// 应用阶段属性修正
        /// </summary>
        protected virtual void ApplyPhaseModifiers(BossPhase phase)
        {
            // 注意：这里修改的是临时战斗属性，不改变原始配置
            attackPower *= phase.attackMultiplier;
            moveSpeed *= phase.speedMultiplier;
            defense *= phase.defenseMultiplier;

            Debug.Log($"[Boss] 阶段修正 - 攻击×{phase.attackMultiplier}, 速度×{phase.speedMultiplier}, 防御×{phase.defenseMultiplier}");
        }

        /// <summary>
        /// 播放阶段切换特效
        /// </summary>
        protected virtual void PlayPhaseTransitionEffect(BossPhase phase)
        {
            if (phase.phaseTransitionEffect != null)
            {
                Instantiate(phase.phaseTransitionEffect, transform.position, Quaternion.identity);
            }

            if (phase.phaseTransitionSFX != null)
            {
                PlaySFX(phase.phaseTransitionSFX);
            }

            // 全屏震动效果（通过事件发送）
            ScreenShakeTrigger?.Invoke(0.3f, 0.5f);
        }

        /// <summary>
        /// 获取当前阶段索引（通过HP百分比）
        /// </summary>
        protected int GetPhaseIndexByHP(float hpPercent)
        {
            for (int i = phases.Count - 1; i >= 0; i--)
            {
                if (hpPercent <= phases[i].hpThreshold)
                    return i;
            }
            return 0;
        }

        #endregion

        #region 出场动画

        /// <summary>
        /// Boss出场序列
        /// </summary>
        protected virtual IEnumerator BossIntroSequence()
        {
            // 出场特效
            if (entranceEffect != null)
            {
                Instantiate(entranceEffect, transform.position, Quaternion.identity);
            }

            // 出场音效
            if (entranceSFX != null)
            {
                PlaySFX(entranceSFX);
            }

            // Boss BGM
            if (bossBGMTrack != null)
            {
                PlayBGM(bossBGMTrack);
            }

            // 屏幕震动
            ScreenShakeTrigger?.Invoke(0.5f, 1f);

            // 显示Boss血条
            if (bossHPBarInstance != null)
            {
                bossHPBarInstance.SetActive(true);
            }

            // 等待出场时间
            yield return new WaitForSeconds(introDuration);

            OnIntroComplete?.Invoke(enemyId);
            Debug.Log($"[Boss] {bossTitle} 出场完成!");
        }

        #endregion

        #region Boss血条UI

        /// <summary>
        /// 创建Boss血条
        /// </summary>
        protected virtual void CreateBossHPBar()
        {
            if (bossHPBarPrefab == null) return;

            bossHPBarInstance = Instantiate(bossHPBarPrefab);
            bossHPBarInstance.SetActive(showBossIntro ? false : true);
        }

        /// <summary>
        /// 更新Boss血条位置和数值
        /// </summary>
        protected virtual void UpdateBossHPBar()
        {
            if (bossHPBarInstance == null) return;

            // 跟随Boss位置
            bossHPBarInstance.transform.position = transform.position + hpBarOffset;

            // 更新血量显示
            var hpBar = bossHPBarInstance.GetComponent<IBossHPBar>();
            if (hpBar != null)
            {
                hpBar.UpdateHP(currentHP, maxHP);
                hpBar.UpdateBossName(bossTitle);
                if (CurrentPhase != null)
                {
                    hpBar.UpdatePhase(CurrentPhase.phaseName);
                }
            }
        }

        /// <summary>
        /// 根据阶段更新血条颜色
        /// </summary>
        protected virtual void UpdateBossHPBarColor(int phaseIndex)
        {
            if (bossHPBarInstance == null) return;

            var hpBar = bossHPBarInstance.GetComponent<IBossHPBar>();
            if (hpBar != null)
            {
                // 阶段越深颜色越红
                float t = phases.Count > 1 ? (float)phaseIndex / (phases.Count - 1) : 0f;
                Color phaseColor = Color.Lerp(bossHPColor, new Color(1f, 0.1f, 0.1f, 1f), t);
                hpBar.UpdateBarColor(phaseColor);
            }
        }

        #endregion

        #region 死亡系统（重写）

        public override void Die()
        {
            if (currentState == EnemyState.Dead) return;

            // Boss死亡特效
            if (deathEffect != null)
            {
                GameObject effect = Instantiate(deathEffect, transform.position, Quaternion.identity);
                Destroy(effect, deathEffectDuration);
            }

            // 死亡音效
            if (deathSFX != null)
            {
                PlaySFX(deathSFX);
            }

            // 全屏震动
            ScreenShakeTrigger?.Invoke(1f, 2f);

            // 隐藏血条
            if (bossHPBarInstance != null)
            {
                Destroy(bossHPBarInstance, deathEffectDuration);
            }

            // 恢复BGM
            StopBGM();

            base.Die();
        }

        /// <summary>
        /// 重写掉落处理 - Boss专属掉落
        /// </summary>
        protected override void HandleDrops()
        {
            // 基础掉落
            base.HandleDrops();

            // 额外经验
            if (bonusExperience > 0)
            {
                DropExperience(bonusExperience);
            }

            // 额外金币
            if (bonusGold > 0)
            {
                DropGold(bonusGold);
            }

            // Boss专属掉落（必定掉落）
            if (bossSpecialDrops != null && bossSpecialDrops.Length > 0)
            {
                foreach (var drop in bossSpecialDrops)
                {
                    if (drop != null && Random.value <= guaranteedDropChance)
                    {
                        Instantiate(drop, transform.position + (Vector3)(Random.insideUnitCircle * 1f), Quaternion.identity);
                    }
                }
            }
        }

        #endregion

        #region 音效辅助

        /// <summary>
        /// 播放音效
        /// </summary>
        protected virtual void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;

            // 通过AudioManager播放（单例模式）
            AudioManager.Instance?.PlaySFX(clip, transform.position);
        }

        /// <summary>
        /// 播放BGM
        /// </summary>
        protected virtual void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            AudioManager.Instance?.PlayBGM(clip);
        }

        /// <summary>
        /// 停止BGM
        /// </summary>
        protected virtual void StopBGM()
        {
            AudioManager.Instance?.StopBGM();
        }

        #endregion

        #region 技能系统

        /// <summary>
        /// 使用指定技能
        /// </summary>
        protected virtual void UseSkill(string skillId)
        {
            OnSkillUsed?.Invoke(enemyId, skillId);
            Debug.Log($"[Boss] {bossTitle} 使用技能: {skillId}");
        }

        /// <summary>
        /// 获取当前阶段可用技能
        /// </summary>
        protected virtual List<string> GetAvailableSkills()
        {
            List<string> skills = new List<string>();
            if (CurrentPhase != null)
            {
                skills.AddRange(CurrentPhase.newSkillIds);
            }
            return skills;
        }

        #endregion

        #region 静态事件（用于全局触发）

        /// <summary>屏幕震动事件（全局）</summary>
        public static event Action<float, float> ScreenShakeTrigger;

        #endregion
    }
}
