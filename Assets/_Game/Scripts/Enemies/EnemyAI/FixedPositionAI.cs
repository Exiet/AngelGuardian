using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-020 深渊触手 AI
    /// 行为模式：位置固定，攻击有1秒预警，被破坏后5秒重生
    /// </summary>
    public class FixedPositionAI : EnemyBase
    {
        [Header("=== E-020 深渊触手 ===")]
        [SerializeField] private float warningDuration = 1f;           // 攻击预警时间
        [SerializeField] private float respawnTime = 5f;               // 重生时间
        [SerializeField] private int maxRespawnCount = 3;              // 最大重生次数
        [SerializeField] private float tentacleAttackRange = 4f;
        [SerializeField] private float tentacleDamage = 20f;
        [SerializeField] private GameObject warningIndicatorPrefab;
        [SerializeField] private GameObject tentacleAttackEffect;
        [SerializeField] private GameObject respawnEffect;
        [SerializeField] private GameObject undergroundStateEffect;
        [SerializeField] private AudioClip emergeSFX;
        [SerializeField] private AudioClip attackSFX;
        [SerializeField] private AudioClip burrowSFX;

        private Vector2 fixedPosition;
        private bool isWarning = false;
        private float warningTimer = 0f;
        private Vector2 warningTargetPosition;
        private GameObject currentWarningIndicator;
        private int respawnCount = 0;
        private bool isRespawning = false;
        private float respawnTimer = 0f;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-020";
            enemyName = "深渊触手";
            type = "AbyssTentacle";
            threatTarget = ThreatTarget.Any;
            fixedPosition = transform.position;
        }

        protected override void Start()
        {
            base.Start();

            // 出场特效
            if (emergeSFX != null)
            {
                AudioManager.Instance?.PlaySFX(emergeSFX, transform.position);
            }
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead)
            {
                // 检查重生
                if (isRespawning)
                {
                    respawnTimer -= Time.deltaTime;
                    if (respawnTimer <= 0f)
                    {
                        Respawn();
                    }
                }
                return;
            }

            // 固定位置
            transform.position = fixedPosition;

            // 索敌
            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                CancelWarning();
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            if (distance <= tentacleAttackRange)
            {
                ChangeState(EnemyState.Attacking);

                if (!isWarning)
                {
                    StartWarning();
                }
                else
                {
                    warningTimer -= Time.deltaTime;
                    UpdateWarningPosition();

                    if (warningTimer <= 0f)
                    {
                        ExecuteTentacleAttack();
                    }
                }
            }
            else
            {
                ChangeState(EnemyState.Idle);
                CancelWarning();
            }
        }

        /// <summary>
        /// 开始攻击预警
        /// </summary>
        private void StartWarning()
        {
            if (currentTarget == null) return;

            isWarning = true;
            warningTimer = warningDuration;
            warningTargetPosition = currentTarget.position;

            // 在目标位置显示预警指示器
            if (warningIndicatorPrefab != null)
            {
                currentWarningIndicator = Instantiate(warningIndicatorPrefab, warningTargetPosition, Quaternion.identity);
                Destroy(currentWarningIndicator, warningDuration + 0.2f);
            }

            // 触手蓄力动画
            if (animator != null)
                animator.SetTrigger("Charge");

            Debug.Log($"[{enemyName}] 攻击预警! 目标位置: {warningTargetPosition}");
        }

        /// <summary>
        /// 更新预警位置（目标移动时更新）
        /// </summary>
        private void UpdateWarningPosition()
        {
            if (currentTarget == null) return;

            // 更新预警位置跟随目标
            warningTargetPosition = currentTarget.position;

            if (currentWarningIndicator != null)
            {
                currentWarningIndicator.transform.position = warningTargetPosition;
            }
        }

        /// <summary>
        /// 取消预警
        /// </summary>
        private void CancelWarning()
        {
            isWarning = false;
            warningTimer = 0f;

            if (currentWarningIndicator != null)
            {
                Destroy(currentWarningIndicator);
                currentWarningIndicator = null;
            }
        }

        /// <summary>
        /// 执行触手攻击
        /// </summary>
        private void ExecuteTentacleAttack()
        {
            isWarning = false;
            attackTimer = attackInterval;

            // 攻击特效
            if (tentacleAttackEffect != null)
            {
                Instantiate(tentacleAttackEffect, warningTargetPosition, Quaternion.identity);
            }

            // 音效
            if (attackSFX != null)
            {
                AudioManager.Instance?.PlaySFX(attackSFX, transform.position);
            }

            // 范围伤害
            float attackRadius = 1.5f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(warningTargetPosition, attackRadius);

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null && !(damageable is EnemyBase))
                {
                    damageable.TakeDamage(tentacleDamage * difficultyAttackModifier, DamageType.Physical);
                }
            }

            if (animator != null)
                animator.SetTrigger("Attack");

            // 屏幕轻微震动
            BossBase.ScreenShakeTrigger?.Invoke(0.1f, 0.2f);

            Debug.Log($"[{enemyName}] 触手攻击! 位置: {warningTargetPosition}");
        }

        /// <summary>
        /// 重写死亡 - 支持重生
        /// </summary>
        public override void Die()
        {
            if (currentState == EnemyState.Dead) return;

            // 隐藏触手
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            if (enemyCollider != null)
                enemyCollider.enabled = false;

            // 潜地特效
            if (burrowSFX != null)
            {
                AudioManager.Instance?.PlaySFX(burrowSFX, transform.position);
            }

            CancelWarning();

            base.Die();

            // 检查是否可以重生
            if (respawnCount < maxRespawnCount)
            {
                StartRespawn();
            }
            else
            {
                // 最终死亡
                if (undergroundStateEffect != null)
                    Destroy(undergroundStateEffect);

                // 发送最终击杀事件
                OnEnemyKilled?.Invoke(enemyId, enemyName, transform.position);
            }
        }

        /// <summary>
        /// 开始重生
        /// </summary>
        private void StartRespawn()
        {
            isRespawning = true;
            respawnTimer = respawnTime;
            respawnCount++;

            // 地下状态特效
            if (undergroundStateEffect != null)
            {
                undergroundStateEffect.SetActive(true);
            }

            Debug.Log($"[{enemyName}] 被破坏! {respawnTime}秒后重生 ({respawnCount}/{maxRespawnCount})");
        }

        /// <summary>
        /// 重生
        /// </summary>
        private void Respawn()
        {
            isRespawning = false;

            // 恢复
            currentHP = maxHP;
            attackTimer = 0f;
            currentTarget = null;

            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
            if (enemyCollider != null)
                enemyCollider.enabled = true;

            ChangeState(EnemyState.Idle);

            // 重生特效
            if (respawnEffect != null)
            {
                Instantiate(respawnEffect, transform.position, Quaternion.identity);
            }

            if (emergeSFX != null)
            {
                AudioManager.Instance?.PlaySFX(emergeSFX, transform.position);
            }

            // 重置动画
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
                animator.SetTrigger("Emerge");
            }

            if (undergroundStateEffect != null)
            {
                undergroundStateEffect.SetActive(false);
            }

            Debug.Log($"[{enemyName}] 重生完成! ({respawnCount}/{maxRespawnCount})");
        }

        /// <summary>
        /// 固定位置不移动
        /// </summary>
        protected override void UpdateMovement()
        {
            // 触手固定在原位，不移动
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 无法被移动
        /// </summary>
        protected override void MoveTowards(Vector2 targetPosition, float speedMultiplier = 1f)
        {
            // 固定位置，不执行移动
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            fixedPosition = transform.position;
            respawnCount = 0;
            isRespawning = false;
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 绘制触手攻击范围
            Gizmos.color = new Color(0.6f, 0f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, tentacleAttackRange);

            // 绘制预警目标
            if (isWarning)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(warningTargetPosition, 1.5f);
                Gizmos.DrawLine(transform.position, warningTargetPosition);
            }

            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"Respawns: {respawnCount}/{maxRespawnCount}\n" +
                $"{(isRespawning ? $"Respawning in: {respawnTimer:F1}s" : "Active")}");
        }
#endif
    }
}
