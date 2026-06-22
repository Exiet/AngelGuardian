using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-009 传送幽灵 AI
    /// 行为模式：选择目标背后 → 瞬移 → 攻击，背刺造成2倍伤害
    /// </summary>
    public class TeleportAI : EnemyBase
    {
        [Header("=== E-009 传送幽灵 ===")]
        [SerializeField] private float teleportCooldown = 4f;
        [SerializeField] private float teleportRange = 8f;
        [SerializeField] private float teleportAnimationDuration = 0.3f;
        [SerializeField] private float backstabDamageMultiplier = 2f;
        [SerializeField] private float backstabAngleThreshold = 45f; // 背刺判定角度
        [SerializeField] private GameObject teleportStartEffect;
        [SerializeField] private GameObject teleportEndEffect;
        [SerializeField] private AudioClip teleportSFX;
        [SerializeField] private AudioClip backstabSFX;

        private float teleportCooldownTimer = 0f;
        private bool isTeleporting = false;
        private Vector2 teleportDestination;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-009";
            enemyName = "传送幽灵";
            type = "TeleportGhost";
            threatTarget = ThreatTarget.Angel;
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead || isTeleporting) return;

            if (teleportCooldownTimer > 0f)
                teleportCooldownTimer -= Time.deltaTime;

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // 在传送范围内且冷却就绪 → 瞬移到背后
            if (distance <= teleportRange && teleportCooldownTimer <= 0f && distance > attackRange * 0.5f)
            {
                StartCoroutine(TeleportBehindTarget());
            }
            // 在攻击范围内 → 背刺攻击
            else if (distance <= attackRange)
            {
                TryBackstabAttack();
            }
            // 太远 → 追逐
            else if (distance > teleportRange)
            {
                ChangeState(EnemyState.Chasing);
                MoveTowards(currentTarget.position, 0.8f);
            }
            else
            {
                // 冷却中，徘徊
                ChangeState(EnemyState.Idle);
            }
        }

        /// <summary>
        /// 瞬移到目标背后
        /// </summary>
        private IEnumerator TeleportBehindTarget()
        {
            if (currentTarget == null) yield break;
            isTeleporting = true;

            // 计算背后位置
            Vector2 targetForward = currentTarget.right; // 假设目标面向右
            Vector2 behindPosition = (Vector2)currentTarget.position - targetForward * 2f;

            // 验证背后位置在传送范围内
            if (Vector2.Distance(transform.position, behindPosition) > teleportRange)
            {
                behindPosition = (Vector2)currentTarget.position +
                    ((Vector2)transform.position - (Vector2)currentTarget.position).normalized * 1.5f;
            }

            teleportDestination = behindPosition;
            teleportCooldownTimer = teleportCooldown;

            // 消失特效
            if (teleportStartEffect != null)
            {
                Instantiate(teleportStartEffect, transform.position, Quaternion.identity);
            }

            // 音效
            if (teleportSFX != null)
            {
                AudioManager.Instance?.PlaySFX(teleportSFX, transform.position);
            }

            // 隐身
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            if (enemyCollider != null)
                enemyCollider.enabled = false;

            // 传送延迟
            yield return new WaitForSeconds(teleportAnimationDuration);

            // 移动到背后
            transform.position = teleportDestination;

            // 出现特效
            if (teleportEndEffect != null)
            {
                Instantiate(teleportEndEffect, transform.position, Quaternion.identity);
            }

            // 显形
            if (spriteRenderer != null)
                spriteRenderer.enabled = true;
            if (enemyCollider != null)
                enemyCollider.enabled = true;

            // 面向目标
            if (currentTarget != null)
            {
                UpdateFacing(currentTarget.position.x - transform.position.x);
            }

            isTeleporting = false;
            Debug.Log($"[{enemyName}] 瞬移到目标背后!");
        }

        /// <summary>
        /// 尝试背刺攻击
        /// </summary>
        private void TryBackstabAttack()
        {
            if (attackTimer > 0f || currentTarget == null) return;

            attackTimer = attackInterval;
            ChangeState(EnemyState.Attacking);

            bool isBackstab = IsBackstabAngle();
            float damageMultiplier = isBackstab ? backstabDamageMultiplier : 1f;

            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                float finalDamage = attackPower * difficultyAttackModifier * damageMultiplier;
                damageable.TakeDamage(finalDamage, DamageType.Physical);

                if (isBackstab)
                {
                    Debug.Log($"[{enemyName}] 背刺! 伤害×{backstabDamageMultiplier}!");
                    if (backstabSFX != null)
                        AudioManager.Instance?.PlaySFX(backstabSFX, transform.position);
                }
            }

            if (animator != null)
                animator.SetTrigger("Attack");
        }

        /// <summary>
        /// 判断是否在背刺角度内
        /// </summary>
        private bool IsBackstabAngle()
        {
            if (currentTarget == null) return false;

            // 计算从目标背后到敌人的方向
            Vector2 targetForward = currentTarget.right; // 目标面朝方向
            Vector2 toEnemy = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;

            // 敌人是否在目标后方
            float angle = Vector2.Angle(targetForward, toEnemy);

            return angle > (180f - backstabAngleThreshold);
        }

#if UNITY_EDITOR
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 绘制传送范围
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, teleportRange);

            // 绘制传送目标
            if (isTeleporting)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(teleportDestination, 0.5f);
                Gizmos.DrawLine(transform.position, teleportDestination);
            }
        }
#endif
    }
}
