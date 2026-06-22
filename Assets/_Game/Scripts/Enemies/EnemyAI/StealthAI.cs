using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-011 影子刺客 & E-018 幻影弓手 AI
    /// E-011: 未移动时在阴影中不可被命中，移动后显形2秒
    /// E-018: 完全隐身，攻击时显形1秒，被光环破除
    /// </summary>
    public class StealthAI : EnemyBase
    {
        [Header("=== 潜行 AI ===")]
        [SerializeField] private StealthType stealthType = StealthType.ShadowAssassin;
        [SerializeField] private float revealDuration = 2f;             // 显形持续时间
        [SerializeField] private float stealthTransitionTime = 0.3f;   // 隐身/显形过渡时间
        [SerializeField] private float attackRevealDuration = 1f;      // 攻击显形时间 (E-018)
        [SerializeField] private bool canBeHitInShadow = false;        // 阴影中是否可被命中
        [SerializeField] private bool canBeRevealedByAura = true;      // 可被光环破除
        [SerializeField] private float auraDetectionRadius = 5f;       // 光环检测范围
        [SerializeField] private LayerMask auraLayer;

        public enum StealthType
        {
            ShadowAssassin, // E-011 影子刺客
            PhantomArcher   // E-018 幻影弓手
        }

        private bool isStealthed = false;
        private bool isMoving = false;
        private float revealTimer = 0f;
        private Vector2 lastPosition;
        private float movementThreshold = 0.1f;
        private Coroutine stealthTransition;
        private Color stealthedColor = new Color(1f, 1f, 1f, 0.2f);
        private Color revealedColor = Color.white;

        protected override void Awake()
        {
            base.Awake();

            if (stealthType == StealthType.PhantomArcher)
            {
                enemyId = "E-018";
                enemyName = "幻影弓手";
                type = "PhantomArcher";
                threatTarget = ThreatTarget.Angel;
                attackRange = 8f; // 远程
            }
            else
            {
                enemyId = "E-011";
                enemyName = "影子刺客";
                type = "ShadowAssassin";
                threatTarget = ThreatTarget.Angel;
            }
        }

        protected override void Start()
        {
            base.Start();
            lastPosition = transform.position;
            EnterStealth();
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // 检测是否移动
            isMoving = Vector2.Distance(transform.position, lastPosition) > movementThreshold;
            lastPosition = transform.position;

            // 检查光环破除
            if (canBeRevealedByAura && isStealthed)
            {
                CheckAuraReveal();
            }

            // 显形计时器
            if (revealTimer > 0f)
            {
                revealTimer -= Time.deltaTime;
                if (revealTimer <= 0f && isStealthed == false)
                {
                    // 检查是否可以重新隐身
                    if (stealthType == StealthType.ShadowAssassin && !isMoving)
                    {
                        EnterStealth();
                    }
                    else if (stealthType == StealthType.PhantomArcher)
                    {
                        EnterStealth();
                    }
                }
            }

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);

                // 未移动时进入阴影
                if (stealthType == StealthType.ShadowAssassin && !isMoving && !isStealthed && revealTimer <= 0f)
                {
                    EnterStealth();
                }
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // E-011: 在阴影中不可被命中，靠近目标
            // E-018: 完全隐身，保持距离
            if (stealthType == StealthType.ShadowAssassin)
            {
                UpdateShadowAssassin(distance);
            }
            else
            {
                UpdatePhantomArcher(distance);
            }
        }

        /// <summary>
        /// E-011 影子刺客逻辑
        /// </summary>
        private void UpdateShadowAssassin(float distance)
        {
            if (distance <= attackRange)
            {
                // 在攻击范围内 - 显形并攻击
                if (isStealthed)
                {
                    RevealFromMovement();
                }

                TryAttack();
            }
            else if (distance <= detectRange)
            {
                // 在检测范围内 - 潜行接近
                ChangeState(EnemyState.Chasing);
                MoveTowards(currentTarget.position, isStealthed ? 0.5f : 1f);

                // 移动导致显形
                if (isStealthed && isMoving)
                {
                    RevealFromMovement();
                }
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }

        /// <summary>
        /// E-018 幻影弓手逻辑
        /// </summary>
        private void UpdatePhantomArcher(float distance)
        {
            float preferredDistance = attackRange * 0.8f;

            if (distance > preferredDistance + 1f)
            {
                // 太远，靠近
                ChangeState(EnemyState.Chasing);
                MoveTowards(currentTarget.position, 0.6f);
            }
            else if (distance < preferredDistance - 1f)
            {
                // 太近，后退
                Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
                MoveTowards((Vector2)transform.position + retreatDirection * 2f, 0.5f);
            }
            else
            {
                // 理想距离 - 攻击
                StopMoving();
                ChangeState(EnemyState.Attacking);

                if (attackTimer <= 0f)
                {
                    // 攻击时显形1秒
                    RevealForAttack();
                }
            }
        }

        /// <summary>
        /// 进入隐身
        /// </summary>
        private void EnterStealth()
        {
            if (isStealthed) return;

            isStealthed = true;

            if (stealthTransition != null)
                StopCoroutine(stealthTransition);

            stealthTransition = StartCoroutine(FadeToAlpha(stealthedColor.a, stealthTransitionTime));

            // 阴影中不可被命中
            if (!canBeHitInShadow)
            {
                // 标记为不可命中（通过Tag或Layer实现，这里简化处理）
                gameObject.tag = "Untagged";
            }

            if (animator != null)
                animator.SetBool("Stealth", true);
        }

        /// <summary>
        /// 退出隐身
        /// </summary>
        private void ExitStealth()
        {
            if (!isStealthed) return;

            isStealthed = false;

            if (stealthTransition != null)
                StopCoroutine(stealthTransition);

            stealthTransition = StartCoroutine(FadeToAlpha(revealedColor.a, stealthTransitionTime));

            gameObject.tag = "Enemy";

            if (animator != null)
                animator.SetBool("Stealth", false);
        }

        /// <summary>
        /// 移动导致显形
        /// </summary>
        private void RevealFromMovement()
        {
            ExitStealth();
            revealTimer = revealDuration;
            Debug.Log($"[{enemyName}] 移动显形! {revealDuration}秒后重新隐身");
        }

        /// <summary>
        /// 攻击导致显形 (E-018)
        /// </summary>
        private void RevealForAttack()
        {
            ExitStealth();
            revealTimer = attackRevealDuration;

            // 执行远程攻击
            attackTimer = attackInterval;

            if (currentTarget != null)
            {
                var damageable = currentTarget.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    // 隐身攻击有额外伤害加成
                    float stealthBonus = 1.3f;
                    damageable.TakeDamage(attackPower * difficultyAttackModifier * stealthBonus, DamageType.Physical);
                }
            }

            if (animator != null)
                animator.SetTrigger("Attack");

            Debug.Log($"[{enemyName}] 攻击显形! {attackRevealDuration}秒后重新隐身");
        }

        /// <summary>
        /// 检查光环破除隐身
        /// </summary>
        private void CheckAuraReveal()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, auraDetectionRadius, auraLayer);
            if (hits.Length > 0)
            {
                ExitStealth();
                revealTimer = revealDuration;
                Debug.Log($"[{enemyName}] 被光环破除隐身!");
            }
        }

        /// <summary>
        /// 透明度渐变
        /// </summary>
        private IEnumerator FadeToAlpha(float targetAlpha, float duration)
        {
            if (spriteRenderer == null) yield break;

            Color startColor = spriteRenderer.color;
            Color targetColor = new Color(startColor.r, startColor.g, startColor.b, targetAlpha);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }

            spriteRenderer.color = targetColor;
        }

        /// <summary>
        /// 重写受伤 - E-011在阴影中不可被命中
        /// </summary>
        public override void TakeDamage(float damage, DamageType type)
        {
            if (stealthType == StealthType.ShadowAssassin && isStealthed && !canBeHitInShadow)
            {
                // 在阴影中，免疫伤害
                Debug.Log($"[{enemyName}] 在阴影中，免疫伤害!");
                return;
            }

            base.TakeDamage(damage, type);

            // 受到伤害后显形
            if (isStealthed)
            {
                ExitStealth();
                revealTimer = revealDuration;
            }
        }
    }
}
