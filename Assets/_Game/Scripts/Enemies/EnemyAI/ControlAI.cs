using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-016 时间法师 AI
    /// 行为模式：优先对移速最快目标施放减速，全屏时间停顿
    /// </summary>
    public class ControlAI : EnemyBase
    {
        [Header("=== E-016 时间法师 ===")]
        [SerializeField] private float slowAmount = 0.4f;               // 减速幅度 (60%减速)
        [SerializeField] private float slowDuration = 3f;
        [SerializeField] private float slowCooldown = 8f;
        [SerializeField] private float slowCastRange = 10f;
        [SerializeField] private float timeStopDuration = 2f;           // 全屏时间停顿
        [SerializeField] private float timeStopCooldown = 20f;
        [SerializeField] private float timeStopWarningDuration = 0.8f;
        [SerializeField] private GameObject slowEffectPrefab;
        [SerializeField] private GameObject timeStopEffectPrefab;
        [SerializeField] private AudioClip timeStopSFX;
        [SerializeField] private Color timeStopScreenColor = new Color(0.3f, 0.3f, 0.6f, 0.3f);

        private float slowTimer = 0f;
        private float timeStopTimer = 0f;
        private bool isCasting = false;
        private bool isTimeStopped = false;
        private List<SlowDebuff> activeSlows = new List<SlowDebuff>();

        private class SlowDebuff
        {
            public Transform target;
            public float originalSpeed;
            public float remainingDuration;
            public GameObject effectInstance;
        }

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-016";
            enemyName = "时间法师";
            type = "TimeMage";
            threatTarget = ThreatTarget.Angel;
            attackRange = 6f;
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // 更新计时器
            if (slowTimer > 0f) slowTimer -= Time.deltaTime;
            if (timeStopTimer > 0f) timeStopTimer -= Time.deltaTime;

            // 更新减速效果
            UpdateSlowDebuffs();

            currentTarget = FindTarget();

            if (currentTarget == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.position);

            // 全屏时间停顿 - 最高优先级
            if (timeStopTimer <= 0f && HPPercentage <= 0.5f && !isCasting)
            {
                StartCoroutine(CastTimeStop());
                return;
            }

            if (isCasting) return;

            // 减速最快速目标
            if (slowTimer <= 0f)
            {
                Transform fastestTarget = FindFastestTarget();
                if (fastestTarget != null && Vector2.Distance(transform.position, fastestTarget.position) <= slowCastRange)
                {
                    CastSlow(fastestTarget);
                }
            }

            // 距离管理
            if (distance < attackRange - 1f)
            {
                // 太近，后退
                Vector2 retreatDirection = ((Vector2)transform.position - (Vector2)currentTarget.position).normalized;
                MoveTowards((Vector2)transform.position + retreatDirection * 2f, 0.7f);
                ChangeState(EnemyState.Chasing);
            }
            else if (distance > attackRange + 2f)
            {
                // 太远，靠近
                MoveTowards(currentTarget.position, 0.5f);
                ChangeState(EnemyState.Chasing);
            }
            else
            {
                // 理想距离，攻击
                StopMoving();
                ChangeState(EnemyState.Attacking);
                TryAttack();
            }
        }

        /// <summary>
        /// 寻找移速最快的目标
        /// </summary>
        private Transform FindFastestTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, slowCastRange);

            Transform fastest = null;
            float highestSpeed = 0f;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Angel") && !hit.CompareTag("Baby")) continue;

                Rigidbody2D targetRb = hit.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    float speed = targetRb.linearVelocity.magnitude;
                    if (speed > highestSpeed)
                    {
                        highestSpeed = speed;
                        fastest = hit.transform;
                    }
                }
                else
                {
                    // 无法获取速度，使用默认优先级
                    if (fastest == null)
                        fastest = hit.transform;
                }
            }

            return fastest;
        }

        /// <summary>
        /// 施加减速
        /// </summary>
        private void CastSlow(Transform target)
        {
            slowTimer = slowCooldown;

            if (animator != null)
                animator.SetTrigger("CastSlow");

            // 获取目标的移动组件
            Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
            IMovementSpeed targetMovement = target.GetComponent<IMovementSpeed>();

            SlowDebuff debuff = new SlowDebuff
            {
                target = target,
                remainingDuration = slowDuration
            };

            if (targetMovement != null)
            {
                debuff.originalSpeed = targetMovement.GetSpeed();
                targetMovement.SetSpeed(debuff.originalSpeed * (1f - slowAmount));
            }
            else if (targetRb != null)
            {
                debuff.originalSpeed = targetRb.linearVelocity.magnitude;
                targetRb.linearVelocity *= (1f - slowAmount);
            }

            // 减速特效
            if (slowEffectPrefab != null)
            {
                debuff.effectInstance = Instantiate(slowEffectPrefab, target.position, Quaternion.identity, target);
            }

            activeSlows.Add(debuff);

            Debug.Log($"[{enemyName}] 对 {target.name} 施加减速 {(1f - slowAmount) * 100}%!");
        }

        /// <summary>
        /// 更新减速效果
        /// </summary>
        private void UpdateSlowDebuffs()
        {
            for (int i = activeSlows.Count - 1; i >= 0; i--)
            {
                SlowDebuff debuff = activeSlows[i];
                debuff.remainingDuration -= Time.deltaTime;

                if (debuff.remainingDuration <= 0f)
                {
                    // 恢复速度
                    if (debuff.target != null)
                    {
                        IMovementSpeed targetMovement = debuff.target.GetComponent<IMovementSpeed>();
                        if (targetMovement != null)
                        {
                            targetMovement.SetSpeed(debuff.originalSpeed);
                        }
                    }

                    if (debuff.effectInstance != null)
                        Destroy(debuff.effectInstance);

                    activeSlows.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 全屏时间停顿
        /// </summary>
        private IEnumerator CastTimeStop()
        {
            isCasting = true;
            timeStopTimer = timeStopCooldown;

            // 预警
            if (animator != null)
                animator.SetTrigger("TimeStop");

            yield return new WaitForSeconds(timeStopWarningDuration);

            // 执行时间停顿
            isTimeStopped = true;

            // 全屏特效
            if (timeStopEffectPrefab != null)
            {
                Instantiate(timeStopEffectPrefab, Vector3.zero, Quaternion.identity);
            }

            if (timeStopSFX != null)
            {
                AudioManager.Instance?.PlaySFX(timeStopSFX, transform.position);
            }

            // 暂停所有目标
            TimeStopAllTargets();

            Debug.Log($"[{enemyName}] 全屏时间停顿! 持续{timeStopDuration}秒");

            yield return new WaitForSeconds(timeStopDuration);

            // 恢复时间
            ResumeAllTargets();
            isTimeStopped = false;
            isCasting = false;
        }

        /// <summary>
        /// 时间停顿所有目标
        /// </summary>
        private void TimeStopAllTargets()
        {
            var angels = GameObject.FindGameObjectsWithTag("Angel");
            var babies = GameObject.FindGameObjectsWithTag("Baby");

            foreach (var obj in angels)
            {
                FreezeTarget(obj);
            }
            foreach (var obj in babies)
            {
                FreezeTarget(obj);
            }
        }

        private void FreezeTarget(GameObject obj)
        {
            Rigidbody2D rb2d = obj.GetComponent<Rigidbody2D>();
            if (rb2d != null)
            {
                rb2d.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            MonoBehaviour[] scripts = obj.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script != null && script != this)
                    script.enabled = false;
            }
        }

        /// <summary>
        /// 恢复所有目标
        /// </summary>
        private void ResumeAllTargets()
        {
            var angels = GameObject.FindGameObjectsWithTag("Angel");
            var babies = GameObject.FindGameObjectsWithTag("Baby");

            foreach (var obj in angels)
            {
                UnfreezeTarget(obj);
            }
            foreach (var obj in babies)
            {
                UnfreezeTarget(obj);
            }
        }

        private void UnfreezeTarget(GameObject obj)
        {
            Rigidbody2D rb2d = obj.GetComponent<Rigidbody2D>();
            if (rb2d != null)
            {
                rb2d.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            MonoBehaviour[] scripts = obj.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script != null && script != this)
                    script.enabled = true;
            }
        }

        public override void Attack()
        {
            if (attackTimer > 0f || currentTarget == null) return;

            attackTimer = attackInterval;

            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackPower * difficultyAttackModifier, DamageType.Magic);
            }

            if (animator != null)
                animator.SetTrigger("Attack");
        }
    }

    /// <summary>
    /// 移动速度接口 - 用于减速效果
    /// </summary>
    public interface IMovementSpeed
    {
        float GetSpeed();
        void SetSpeed(float speed);
    }
}
