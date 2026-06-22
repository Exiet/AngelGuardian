using System.Collections.Generic;
using UnityEngine;

namespace AngelGuardian.Enemies.EnemyAI
{
    /// <summary>
    /// E-015 蜂群 AI
    /// 行为模式：以蜂王为中心移动，蜂王死亡全群溃散
    /// </summary>
    public class SwarmAI : EnemyBase
    {
        [Header("=== E-015 蜂群 ===")]
        [SerializeField] private SwarmRole role = SwarmRole.Drone;
        [SerializeField] private Transform queenTransform;              // 蜂王引用
        [SerializeField] private float swarmRadius = 4f;               // 围绕蜂王的半径
        [SerializeField] private float swarmCohesion = 0.8f;           // 凝聚力
        [SerializeField] private float swarmSeparation = 1.5f;         // 个体间距
        [SerializeField] private float attackSwarmBonus = 1.5f;        // 集群攻击加成
        [SerializeField] private float scatterDuration = 3f;           // 溃散持续时间
        [SerializeField] private float scatterSpeedMultiplier = 2f;    // 溃散速度倍率
        [SerializeField] private Color queenColor = new Color(1f, 0.8f, 0f, 1f);  // 蜂王金色
        [SerializeField] private Color droneColor = new Color(0.8f, 0.6f, 0f, 1f);

        public enum SwarmRole
        {
            Queen,      // 蜂王
            Drone       // 工蜂
        }

        private List<SwarmAI> nearbyDrones = new List<SwarmAI>();
        private bool isScattering = false;
        private float scatterTimer = 0f;
        private Vector2 scatterDirection;
        private static List<SwarmAI> allSwarmMembers = new List<SwarmAI>();
        private static SwarmAI activeQueen;

        protected override void Awake()
        {
            base.Awake();
            enemyId = "E-015";
            enemyName = role == SwarmRole.Queen ? "蜂王" : "蜂群工蜂";
            type = "Swarm";
            threatTarget = ThreatTarget.Any;

            allSwarmMembers.Add(this);
        }

        protected override void Start()
        {
            base.Start();

            if (role == SwarmRole.Queen)
            {
                activeQueen = this;
                if (spriteRenderer != null)
                    spriteRenderer.color = queenColor;
                maxHP *= 3f;    // 蜂王血量3倍
                currentHP = maxHP;
            }
            else
            {
                if (spriteRenderer != null)
                    spriteRenderer.color = droneColor;

                // 寻找蜂王
                if (activeQueen != null)
                {
                    queenTransform = activeQueen.transform;
                }
            }
        }

        protected override void UpdateAI()
        {
            if (currentState == EnemyState.Dead) return;

            // 溃散计时器
            if (isScattering)
            {
                scatterTimer -= Time.deltaTime;
                if (scatterTimer <= 0f)
                {
                    isScattering = false;
                }
                UpdateScatterMovement();
                return;
            }

            // 检查蜂王状态
            if (role == SwarmRole.Drone)
            {
                if (queenTransform == null || activeQueen == null || activeQueen.CurrentState == EnemyState.Dead)
                {
                    // 蜂王死亡 → 溃散
                    StartScattering();
                    return;
                }
            }

            // 寻找附近蜂群成员
            FindNearbyDrones();

            // 蜂王逻辑
            if (role == SwarmRole.Queen)
            {
                UpdateQueenAI();
            }
            else
            {
                UpdateDroneAI();
            }
        }

        /// <summary>
        /// 蜂王AI - 主动索敌，带领蜂群
        /// </summary>
        private void UpdateQueenAI()
        {
            currentTarget = FindTarget();

            if (currentTarget != null)
            {
                float distance = Vector2.Distance(transform.position, currentTarget.position);

                if (distance <= attackRange)
                {
                    StopMoving();
                    ChangeState(EnemyState.Attacking);
                    TryAttack();
                }
                else
                {
                    // 蜂王移动，蜂群跟随
                    ChangeState(EnemyState.Chasing);
                    MoveTowards(currentTarget.position, 0.7f);
                }
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }

        /// <summary>
        /// 工蜂AI - 围绕蜂王，攻击进入范围的目标
        /// </summary>
        private void UpdateDroneAI()
        {
            if (queenTransform == null)
            {
                StartScattering();
                return;
            }

            Vector2 queenPosition = queenTransform.position;

            // 计算围绕蜂王的目标位置
            Vector2 offsetFromQueen = (Vector2)transform.position - queenPosition;
            float currentDistance = offsetFromQueen.magnitude;

            // 保持蜂群间距
            Vector2 separationForce = CalculateSeparationForce();
            Vector2 cohesionForce = (queenPosition - (Vector2)transform.position).normalized * swarmCohesion;

            Vector2 targetPosition = queenPosition + offsetFromQueen.normalized * swarmRadius;

            // 寻找攻击目标
            currentTarget = FindTarget();

            if (currentTarget != null)
            {
                float distance = Vector2.Distance(transform.position, currentTarget.position);

                if (distance <= attackRange)
                {
                    // 在攻击范围 - 攻击
                    StopMoving();
                    ChangeState(EnemyState.Attacking);

                    // 集群攻击加成
                    TrySwarmAttack();
                }
                else if (distance <= detectRange)
                {
                    // 在检测范围 - 追击
                    ChangeState(EnemyState.Chasing);

                    // 在追击目标和保持编队之间平衡
                    Vector2 chaseDirection = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
                    Vector2 combinedDirection = (chaseDirection * 0.6f + cohesionForce * 0.3f + separationForce * 0.1f).normalized;

                    MoveTowards((Vector2)transform.position + combinedDirection * 2f, 0.9f);
                }
                else
                {
                    // 保持编队位置
                    MaintainFormation(targetPosition);
                }
            }
            else
            {
                // 无目标 - 保持编队
                MaintainFormation(targetPosition);
            }
        }

        /// <summary>
        /// 保持编队位置
        /// </summary>
        private void MaintainFormation(Vector2 targetPosition)
        {
            float distance = Vector2.Distance(transform.position, targetPosition);
            if (distance > 0.3f)
            {
                ChangeState(EnemyState.Chasing);
                MoveTowards(targetPosition, 0.5f);
            }
            else
            {
                ChangeState(EnemyState.Idle);
                StopMoving();
            }
        }

        /// <summary>
        /// 计算个体分离力
        /// </summary>
        private Vector2 CalculateSeparationForce()
        {
            Vector2 separation = Vector2.zero;
            int count = 0;

            foreach (var drone in nearbyDrones)
            {
                if (drone == this || drone == null) continue;

                float distance = Vector2.Distance(transform.position, drone.transform.position);
                if (distance < swarmSeparation && distance > 0.01f)
                {
                    Vector2 awayFromDrone = ((Vector2)transform.position - (Vector2)drone.transform.position).normalized;
                    separation += awayFromDrone / distance;
                    count++;
                }
            }

            if (count > 0)
            {
                separation /= count;
            }

            return separation.normalized;
        }

        /// <summary>
        /// 寻找附近蜂群成员
        /// </summary>
        private void FindNearbyDrones()
        {
            nearbyDrones.Clear();
            foreach (var member in allSwarmMembers)
            {
                if (member == null || member == this || member.CurrentState == EnemyState.Dead) continue;

                float distance = Vector2.Distance(transform.position, member.transform.position);
                if (distance <= swarmRadius * 2f)
                {
                    nearbyDrones.Add(member);
                }
            }
        }

        /// <summary>
        /// 集群攻击（攻击力加成）
        /// </summary>
        private void TrySwarmAttack()
        {
            if (attackTimer > 0f || currentTarget == null) return;

            attackTimer = attackInterval;

            // 集群攻击力加成
            float bonus = 1f + (nearbyDrones.Count * 0.15f); // 每多一只+15%
            float finalBonus = Mathf.Min(bonus, attackSwarmBonus);

            var damageable = currentTarget.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackPower * difficultyAttackModifier * finalBonus, DamageType.Physical);
            }

            if (animator != null)
                animator.SetTrigger("Attack");
        }

        /// <summary>
        /// 开始溃散
        /// </summary>
        private void StartScattering()
        {
            if (isScattering) return;

            isScattering = true;
            scatterTimer = scatterDuration;
            scatterDirection = Random.insideUnitCircle.normalized;

            if (animator != null)
                animator.SetTrigger("Scatter");

            Debug.Log($"[{enemyName}] 蜂王死亡，蜂群溃散!");
        }

        /// <summary>
        /// 溃散移动
        /// </summary>
        private void UpdateScatterMovement()
        {
            ChangeState(EnemyState.Chasing);

            if (rb != null)
            {
                rb.velocity = scatterDirection * moveSpeed * difficultySpeedModifier * scatterSpeedMultiplier;
            }

            if (scatterTimer <= 0f)
            {
                // 溃散结束，恢复正常AI
                isScattering = false;

                // 重新寻找蜂王
                if (activeQueen != null && activeQueen.CurrentState != EnemyState.Dead)
                {
                    queenTransform = activeQueen.transform;
                }
            }
        }

        /// <summary>
        /// 蜂王死亡时通知所有工蜂
        /// </summary>
        public override void Die()
        {
            if (role == SwarmRole.Queen)
            {
                activeQueen = null;

                // 通知所有工蜂溃散
                foreach (var member in allSwarmMembers)
                {
                    if (member != null && member != this && member.role == SwarmRole.Drone)
                    {
                        member.StartScattering();
                    }
                }
            }

            base.Die();
        }

        private void OnDestroy()
        {
            allSwarmMembers.Remove(this);
        }
    }
}
