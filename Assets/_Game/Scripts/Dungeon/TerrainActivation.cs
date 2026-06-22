using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AngelGuardian.Dungeon
{
    /// <summary>
    /// 地形活化系统 —— 五阶段演化 + 随机灾变事件
    /// 
    /// 阶段: 静默(0) → 警觉(1) → 涌动(2) → 觉醒(3) → 升华(4)
    /// 
    /// 这是游戏的核心差异化系统，让地下城地形随着玩家行为动态变化
    /// </summary>
    public class TerrainActivation : MonoBehaviour
    {
        #region Activation Stage

        public enum ActivationStage
        {
            Silent = 0,     // 静默 —— 初始状态，地形静止
            Alert = 1,      // 警觉 —— 精神力<60%触发，墙壁藤蔓减速15%
            Surge = 2,      // 涌动 —— 杀敌≥30触发，地面圣光纹路移速+30%
            Awaken = 3,     // 觉醒 —— 杀敌≥80+精神力>50%，地图边缘崩塌
            Sublime = 4     // 升华 —— 婴儿觉醒触发，全屏圣光+全敌减速30%
        }

        #endregion

        #region Catastrophe Event Types

        public enum CatastropheType
        {
            LightPillar = 0,        // 天降光柱 —— 随机位置落下圣光柱
            PoisonSwamp = 1,        // 毒沼扩散 —— 随机区域变成毒沼
            BlessingSpring = 2,     // 祝福泉涌 —— 随机位置出现治疗泉
            TimeWarp = 3            // 时间扭曲 —— 区域内时间流速改变
        }

        #endregion

        #region Configuration

        [Header("Stage Detection")]
        [SerializeField, Range(0.5f, 5f)]
        private float detectionInterval = 1f;           // 阶段检测间隔(秒)

        [Header("Stage 1: Alert (警觉)")]
        [SerializeField, Range(0.3f, 0.8f)]
        private float alertSpiritThreshold = 0.6f;      // 精神力<60%触发

        [SerializeField, Range(0.05f, 0.3f)]
        private float vineSlowAmount = 0.15f;           // 藤蔓减速15%

        [SerializeField]
        private Color alertAmbientColor = new Color(0.8f, 0.6f, 0.2f, 0.3f);

        [Header("Stage 2: Surge (涌动)")]
        [SerializeField, Range(10, 50)]
        private int surgeKillThreshold = 30;            // 杀敌≥30触发

        [SerializeField, Range(0.1f, 0.5f)]
        private float surgeSpeedBonus = 0.3f;           // 移速+30%

        [SerializeField]
        private Color surgeAmbientColor = new Color(0.9f, 0.8f, 0.1f, 0.4f);

        [Header("Stage 3: Awaken (觉醒)")]
        [SerializeField, Range(50, 120)]
        private int awakenKillThreshold = 80;           // 杀敌≥80触发

        [SerializeField, Range(0.3f, 0.8f)]
        private float awakenSpiritThreshold = 0.5f;     // 精神力>50%

        [SerializeField, Range(50, 200)]
        private float edgeCollapseSpeed = 100f;          // 边缘崩塌速度(px/s)

        [SerializeField]
        private Color awakenAmbientColor = new Color(0.6f, 0.2f, 0.8f, 0.5f);

        [Header("Stage 4: Sublime (升华)")]
        [SerializeField, Range(0.1f, 0.5f)]
        private float sublimeEnemySlow = 0.3f;          // 全敌减速30%

        [SerializeField]
        private Color sublimeAmbientColor = new Color(1f, 0.95f, 0.7f, 0.7f);

        [Header("Catastrophe Events")]
        [SerializeField, Range(30, 120)]
        private float minEventInterval = 45f;           // 最小事件间隔(秒)

        [SerializeField, Range(60, 180)]
        private float maxEventInterval = 90f;           // 最大事件间隔(秒)

        [SerializeField, Range(3, 10)]
        private float eventDuration = 8f;               // 事件持续时间(秒)

        [Header("Event Weights (可配置)")]
        [SerializeField, Range(0f, 1f)]
        private float lightPillarWeight = 0.3f;

        [SerializeField, Range(0f, 1f)]
        private float poisonSwampWeight = 0.25f;

        [SerializeField, Range(0f, 1f)]
        private float blessingSpringWeight = 0.25f;

        [SerializeField, Range(0f, 1f)]
        private float timeWarpWeight = 0.2f;

        [Header("Debug")]
        [SerializeField]
        private bool verboseLogging = true;

        [SerializeField]
        private bool enableCatastropheEvents = true;

        #endregion

        #region Runtime State

        private ActivationStage currentStage = ActivationStage.Silent;
        private ActivationStage previousStage = ActivationStage.Silent;

        private CatastropheType? activeCatastrophe;
        private float catastropheTimer;
        private float catastropheEndTime;
        private Vector2Int catastrophePosition;
        private float catastropheRadius;

        // 外部数据引用(由GameManager注入)
        private Func<float> getSpiritPower;         // 获取当前精神力百分比
        private Func<int> getKillCount;              // 获取杀敌数
        private Func<bool> isBabyAwakened;           // 婴儿是否觉醒
        private Func<Vector2Int> getMapSize;         // 获取地图尺寸

        private Coroutine detectionRoutine;
        private Coroutine catastropheRoutine;

        // 阶段效果状态
        private bool vineSlowActive;
        private bool surgeSpeedActive;
        private bool edgeCollapseActive;
        private bool sublimeSlowActive;
        private float edgeCollapseProgress;          // 0-1, 边缘崩塌进度

        #endregion

        #region Events

        public event Action<ActivationStage, ActivationStage> OnStageChanged;     // (old, new)
        public event Action<CatastropheType, Vector2Int, float> OnCatastropheStarted; // (type, pos, radius)
        public event Action<CatastropheType> OnCatastropheEnded;
        public event Action<float> OnEdgeCollapseProgress;  // (progress 0-1)
        public event Action<float> OnVineSlowChanged;       // (slowAmount)
        public event Action<float> OnSpeedBonusChanged;     // (speedBonus)
        public event Action<float> OnEnemySlowChanged;      // (slowAmount)

        #endregion

        #region Properties

        public ActivationStage CurrentStage => currentStage;
        public CatastropheType? ActiveCatastrophe => activeCatastrophe;
        public bool IsCatastropheActive => activeCatastrophe.HasValue;
        public float EdgeCollapseProgress => edgeCollapseProgress;
        public float VineSlowAmount => vineSlowActive ? vineSlowAmount : 0f;
        public float SpeedBonus => surgeSpeedActive ? surgeSpeedBonus : 0f;
        public float EnemySlowAmount => sublimeSlowActive ? sublimeEnemySlow : 0f;
        public Color CurrentAmbientColor => GetStageAmbientColor();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            currentStage = ActivationStage.Silent;
        }

        private void Start()
        {
            // 启动阶段检测
            if (detectionRoutine != null)
                StopCoroutine(detectionRoutine);
            detectionRoutine = StartCoroutine(StageDetectionRoutine());

            // 启动灾变事件
            if (enableCatastropheEvents && catastropheRoutine != null)
                StopCoroutine(catastropheRoutine);
            if (enableCatastropheEvents)
                catastropheRoutine = StartCoroutine(CatastropheEventRoutine());

            if (verboseLogging)
                Debug.Log("[TerrainActivation] 地形活化系统已启动 - 当前阶段: 静默");
        }

        private void OnDestroy()
        {
            if (detectionRoutine != null) StopCoroutine(detectionRoutine);
            if (catastropheRoutine != null) StopCoroutine(catastropheRoutine);
        }

        private void Update()
        {
            // 边缘崩塌持续更新
            if (edgeCollapseActive && currentStage >= ActivationStage.Awaken)
            {
                UpdateEdgeCollapse();
            }

            // 灾变事件计时
            if (activeCatastrophe.HasValue && Time.time >= catastropheEndTime)
            {
                EndCatastrophe();
            }
        }

        #endregion

        #region Data Injection

        /// <summary>
        /// 注入外部数据获取函数
        /// </summary>
        public void InjectDataSources(
            Func<float> spiritPowerGetter,
            Func<int> killCountGetter,
            Func<bool> babyAwakenedGetter,
            Func<Vector2Int> mapSizeGetter)
        {
            getSpiritPower = spiritPowerGetter;
            getKillCount = killCountGetter;
            isBabyAwakened = babyAwakenedGetter;
            getMapSize = mapSizeGetter;
        }

        #endregion

        #region Stage Detection

        /// <summary>
        /// 阶段触发条件检测 —— 每1秒检测一次
        /// </summary>
        private IEnumerator StageDetectionRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(detectionInterval);

            while (true)
            {
                yield return wait;
                DetectAndTransitionStage();
            }
        }

        /// <summary>
        /// 检测并转换阶段
        /// </summary>
        private void DetectAndTransitionStage()
        {
            float spiritPower = getSpiritPower?.Invoke() ?? 1f;
            int killCount = getKillCount?.Invoke() ?? 0;
            bool babyAwakened = isBabyAwakened?.Invoke() ?? false;

            ActivationStage targetStage = DetermineTargetStage(spiritPower, killCount, babyAwakened);

            if (targetStage != currentStage)
            {
                TransitionToStage(targetStage);
            }
        }

        /// <summary>
        /// 根据条件确定目标阶段
        /// </summary>
        private ActivationStage DetermineTargetStage(float spiritPower, int killCount, bool babyAwakened)
        {
            // 升华(最高优先级): 婴儿觉醒
            if (babyAwakened)
                return ActivationStage.Sublime;

            // 觉醒: 杀敌≥80 且 精神力>50%
            if (killCount >= awakenKillThreshold && spiritPower > awakenSpiritThreshold)
                return ActivationStage.Awaken;

            // 涌动: 杀敌≥30
            if (killCount >= surgeKillThreshold)
                return ActivationStage.Surge;

            // 警觉: 精神力<60%
            if (spiritPower < alertSpiritThreshold)
                return ActivationStage.Alert;

            // 默认: 静默
            return ActivationStage.Silent;
        }

        /// <summary>
        /// 执行阶段转换
        /// </summary>
        private void TransitionToStage(ActivationStage newStage)
        {
            previousStage = currentStage;

            // 退出当前阶段效果
            ExitStageEffects(currentStage);

            currentStage = newStage;

            // 进入新阶段效果
            EnterStageEffects(newStage);

            OnStageChanged?.Invoke(previousStage, currentStage);

            if (verboseLogging)
                Debug.Log($"[TerrainActivation] 阶段转换: {previousStage} → {currentStage}");
        }

        #endregion

        #region Stage Effects

        /// <summary>
        /// 进入阶段效果
        /// </summary>
        private void EnterStageEffects(ActivationStage stage)
        {
            switch (stage)
            {
                case ActivationStage.Silent:
                    // 静默 —— 无特殊效果
                    break;

                case ActivationStage.Alert:
                    // 警觉 —— 墙壁藤蔓减速15%
                    vineSlowActive = true;
                    OnVineSlowChanged?.Invoke(vineSlowAmount);
                    break;

                case ActivationStage.Surge:
                    // 涌动 —— 地面圣光纹路移速+30%
                    surgeSpeedActive = true;
                    OnSpeedBonusChanged?.Invoke(surgeSpeedBonus);
                    break;

                case ActivationStage.Awaken:
                    // 觉醒 —— 地图边缘崩塌
                    edgeCollapseActive = true;
                    edgeCollapseProgress = 0f;
                    break;

                case ActivationStage.Sublime:
                    // 升华 —— 全屏圣光+全敌减速30%
                    sublimeSlowActive = true;
                    OnEnemySlowChanged?.Invoke(sublimeEnemySlow);

                    // 清除当前灾变事件
                    if (activeCatastrophe.HasValue)
                        EndCatastrophe();
                    break;
            }
        }

        /// <summary>
        /// 退出阶段效果
        /// </summary>
        private void ExitStageEffects(ActivationStage stage)
        {
            switch (stage)
            {
                case ActivationStage.Alert:
                    vineSlowActive = false;
                    OnVineSlowChanged?.Invoke(0f);
                    break;

                case ActivationStage.Surge:
                    surgeSpeedActive = false;
                    OnSpeedBonusChanged?.Invoke(0f);
                    break;

                case ActivationStage.Awaken:
                    edgeCollapseActive = false;
                    break;

                case ActivationStage.Sublime:
                    sublimeSlowActive = false;
                    OnEnemySlowChanged?.Invoke(0f);
                    break;
            }
        }

        /// <summary>
        /// 更新边缘崩塌
        /// </summary>
        private void UpdateEdgeCollapse()
        {
            edgeCollapseProgress += (edgeCollapseSpeed * Time.deltaTime) / 1000f;
            edgeCollapseProgress = Mathf.Clamp01(edgeCollapseProgress);

            OnEdgeCollapseProgress?.Invoke(edgeCollapseProgress);

            if (edgeCollapseProgress >= 1f)
            {
                edgeCollapseActive = false;
                if (verboseLogging)
                    Debug.Log("[TerrainActivation] 地图边缘崩塌完成");
            }
        }

        #endregion

        #region Catastrophe Events

        /// <summary>
        /// 随机灾变事件循环 —— 每45-90秒触发一次
        /// </summary>
        private IEnumerator CatastropheEventRoutine()
        {
            while (true)
            {
                // 等待随机间隔
                float interval = Random.Range(minEventInterval, maxEventInterval);
                yield return new WaitForSeconds(interval);

                // 升华阶段不触发灾变
                if (currentStage >= ActivationStage.Sublime) continue;
                // 已有灾变活跃时不触发
                if (activeCatastrophe.HasValue) continue;

                TriggerRandomCatastrophe();
            }
        }

        /// <summary>
        /// 触发随机灾变事件(按权重选择)
        /// </summary>
        public void TriggerRandomCatastrophe()
        {
            CatastropheType type = SelectCatastropheByWeight();

            Vector2Int mapSize = getMapSize?.Invoke() ?? new Vector2Int(2048, 2048);
            Vector2Int position = new Vector2Int(
                Random.Range(100, mapSize.x - 100),
                Random.Range(100, mapSize.y - 100)
            );
            float radius = Random.Range(150f, 350f);

            StartCatastrophe(type, position, radius);
        }

        /// <summary>
        /// 按权重选择灾变类型
        /// </summary>
        private CatastropheType SelectCatastropheByWeight()
        {
            float totalWeight = lightPillarWeight + poisonSwampWeight +
                               blessingSpringWeight + timeWarpWeight;

            if (totalWeight <= 0f)
            {
                // 默认均匀分布
                lightPillarWeight = 0.25f;
                poisonSwampWeight = 0.25f;
                blessingSpringWeight = 0.25f;
                timeWarpWeight = 0.25f;
                totalWeight = 1f;
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            cumulative += lightPillarWeight;
            if (roll <= cumulative) return CatastropheType.LightPillar;

            cumulative += poisonSwampWeight;
            if (roll <= cumulative) return CatastropheType.PoisonSwamp;

            cumulative += blessingSpringWeight;
            if (roll <= cumulative) return CatastropheType.BlessingSpring;

            return CatastropheType.TimeWarp;
        }

        /// <summary>
        /// 开始灾变事件
        /// </summary>
        public void StartCatastrophe(CatastropheType type, Vector2Int position, float radius)
        {
            if (activeCatastrophe.HasValue)
                EndCatastrophe();

            activeCatastrophe = type;
            catastrophePosition = position;
            catastropheRadius = radius;
            catastropheEndTime = Time.time + eventDuration;

            OnCatastropheStarted?.Invoke(type, position, radius);

            if (verboseLogging)
                Debug.Log($"[TerrainActivation] 灾变事件触发: {type} 位置:{position} 半径:{radius}");
        }

        /// <summary>
        /// 结束灾变事件
        /// </summary>
        public void EndCatastrophe()
        {
            if (!activeCatastrophe.HasValue) return;

            CatastropheType endedType = activeCatastrophe.Value;

            if (verboseLogging)
                Debug.Log($"[TerrainActivation] 灾变事件结束: {endedType}");

            activeCatastrophe = null;
            OnCatastropheEnded?.Invoke(endedType);
        }

        /// <summary>
        /// 强制触发指定灾变(调试用)
        /// </summary>
        public void ForceCatastrophe(CatastropheType type)
        {
            Vector2Int mapSize = getMapSize?.Invoke() ?? new Vector2Int(2048, 2048);
            Vector2Int pos = new Vector2Int(mapSize.x / 2, mapSize.y / 2);
            StartCatastrophe(type, pos, 250f);
        }

        #endregion

        #region Catastrophe Effects (Called by external systems)

        /// <summary>
        /// 天降光柱 —— 对区域内敌人造成伤害
        /// </summary>
        public float GetLightPillarDamage(float baseDamage = 50f)
        {
            if (activeCatastrophe != CatastropheType.LightPillar) return 0f;
            return baseDamage * (1f + (float)currentStage * 0.25f);
        }

        /// <summary>
        /// 毒沼扩散 —— 对区域内单位造成持续伤害和减速
        /// </summary>
        public (float dps, float slow) GetPoisonSwampEffect()
        {
            if (activeCatastrophe != CatastropheType.PoisonSwamp)
                return (0f, 0f);

            float dps = 15f + (float)currentStage * 5f;
            float slow = 0.2f + (float)currentStage * 0.05f;
            return (dps, slow);
        }

        /// <summary>
        /// 祝福泉涌 —— 对区域内友方单位治疗
        /// </summary>
        public float GetBlessingHealRate()
        {
            if (activeCatastrophe != CatastropheType.BlessingSpring) return 0f;
            return 25f + (float)currentStage * 10f;
        }

        /// <summary>
        /// 时间扭曲 —— 区域内时间流速改变
        /// </summary>
        public float GetTimeWarpScale()
        {
            if (activeCatastrophe != CatastropheType.TimeWarp) return 1f;
            // 对友方加速，对敌方减速
            return 1f + (float)currentStage * 0.15f;
        }

        /// <summary>
        /// 检查一个点是否在活跃灾变区域内
        /// </summary>
        public bool IsPointInCatastrophe(Vector2Int point)
        {
            if (!activeCatastrophe.HasValue) return false;
            return Vector2Int.Distance(point, catastrophePosition) <= catastropheRadius;
        }

        /// <summary>
        /// 获取活跃灾变的位置和半径
        /// </summary>
        public (Vector2Int position, float radius) GetActiveCatastropheInfo()
        {
            return (catastrophePosition, catastropheRadius);
        }

        #endregion

        #region Utility

        /// <summary>
        /// 获取当前阶段环境颜色
        /// </summary>
        private Color GetStageAmbientColor()
        {
            switch (currentStage)
            {
                case ActivationStage.Silent:
                    return Color.clear;
                case ActivationStage.Alert:
                    return alertAmbientColor;
                case ActivationStage.Surge:
                    return surgeAmbientColor;
                case ActivationStage.Awaken:
                    return awakenAmbientColor;
                case ActivationStage.Sublime:
                    return sublimeAmbientColor;
                default:
                    return Color.clear;
            }
        }

        /// <summary>
        /// 获取阶段名称(中文)
        /// </summary>
        public static string GetStageName(ActivationStage stage)
        {
            switch (stage)
            {
                case ActivationStage.Silent:  return "静默";
                case ActivationStage.Alert:   return "警觉";
                case ActivationStage.Surge:   return "涌动";
                case ActivationStage.Awaken:  return "觉醒";
                case ActivationStage.Sublime: return "升华";
                default: return "未知";
            }
        }

        /// <summary>
        /// 获取灾变类型名称(中文)
        /// </summary>
        public static string GetCatastropheName(CatastropheType type)
        {
            switch (type)
            {
                case CatastropheType.LightPillar:    return "天降光柱";
                case CatastropheType.PoisonSwamp:    return "毒沼扩散";
                case CatastropheType.BlessingSpring: return "祝福泉涌";
                case CatastropheType.TimeWarp:       return "时间扭曲";
                default: return "未知";
            }
        }

        /// <summary>
        /// 重置系统(用于新游戏)
        /// </summary>
        public void Reset()
        {
            TransitionToStage(ActivationStage.Silent);
            if (activeCatastrophe.HasValue)
                EndCatastrophe();
            edgeCollapseProgress = 0f;
            vineSlowActive = false;
            surgeSpeedActive = false;
            edgeCollapseActive = false;
            sublimeSlowActive = false;
        }

        /// <summary>
        /// 更新事件权重
        /// </summary>
        public void SetEventWeights(float lightPillar, float poisonSwamp, float blessing, float timeWarp)
        {
            lightPillarWeight = Mathf.Clamp01(lightPillar);
            poisonSwampWeight = Mathf.Clamp01(poisonSwamp);
            blessingSpringWeight = Mathf.Clamp01(blessing);
            timeWarpWeight = Mathf.Clamp01(timeWarp);
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            // 绘制当前灾变区域
            if (activeCatastrophe.HasValue)
            {
                Color eventColor;
                switch (activeCatastrophe.Value)
                {
                    case CatastropheType.LightPillar:
                        eventColor = new Color(1, 0.9f, 0.2f, 0.3f);
                        break;
                    case CatastropheType.PoisonSwamp:
                        eventColor = new Color(0.3f, 0.8f, 0.2f, 0.3f);
                        break;
                    case CatastropheType.BlessingSpring:
                        eventColor = new Color(0.2f, 0.6f, 1f, 0.3f);
                        break;
                    case CatastropheType.TimeWarp:
                        eventColor = new Color(0.7f, 0.3f, 1f, 0.3f);
                        break;
                    default:
                        eventColor = Color.white;
                        break;
                }

                Gizmos.color = eventColor;
                Gizmos.DrawSphere(
                    new Vector3(catastrophePosition.x, catastrophePosition.y, 0),
                    catastropheRadius
                );

                Gizmos.color = eventColor * 1.5f;
                Gizmos.DrawWireSphere(
                    new Vector3(catastrophePosition.x, catastrophePosition.y, 0),
                    catastropheRadius
                );
            }

            // 绘制边缘崩塌
            if (edgeCollapseActive)
            {
                Vector2Int mapSize = getMapSize?.Invoke() ?? new Vector2Int(2048, 2048);
                float collapseMargin = edgeCollapseProgress * 300f;

                Gizmos.color = new Color(1, 0, 0, 0.3f);
                // 上边
                Gizmos.DrawLine(new Vector3(0, mapSize.y - collapseMargin, 0),
                               new Vector3(mapSize.x, mapSize.y - collapseMargin, 0));
                // 下边
                Gizmos.DrawLine(new Vector3(0, collapseMargin, 0),
                               new Vector3(mapSize.x, collapseMargin, 0));
                // 左边
                Gizmos.DrawLine(new Vector3(collapseMargin, 0, 0),
                               new Vector3(collapseMargin, mapSize.y, 0));
                // 右边
                Gizmos.DrawLine(new Vector3(mapSize.x - collapseMargin, 0, 0),
                               new Vector3(mapSize.x - collapseMargin, mapSize.y, 0));
            }
        }

        private void OnGUI()
        {
            if (!verboseLogging) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Terrain Activation System");
            GUILayout.Label($"Stage: {GetStageName(currentStage)} ({currentStage})");
            GUILayout.Label($"Catastrophe: {(activeCatastrophe.HasValue ? GetCatastropheName(activeCatastrophe.Value) : "None")}");
            if (edgeCollapseActive)
                GUILayout.Label($"Edge Collapse: {edgeCollapseProgress * 100:F1}%");
            GUILayout.EndArea();
        }

        #endregion
    }
}
