# 开发参考：婴儿AI行为树

> 本文档供 TapTap 制造 AI 开发者直接读取实现。
> 对应策划案章节：第四章「婴儿AI与情感状态机」
> 对应数据文件：`角色属性2.0.xlsx → 情感状态机`

---

## 一、情感状态机总览

```
状态列表：[好奇, 恐惧, 愤怒, 疲惫, 觉醒]
刷新频率：每 0.5 秒
优先级：觉醒 > 愤怒 > 疲惫 > 恐惧 > 好奇
```

### 状态转换条件（伪代码）

```python
# 每帧调用（0.5秒间隔）
def update_emotion_state(baby, enemies, angel):
    # 最高优先级：觉醒
    if baby.awakening_energy >= 100:
        baby.state = EmotionState.AWAKENING
        return
    
    # 次高优先级：愤怒（连续受伤检测）
    if baby.recent_hit_count >= baby.anger_threshold:
        baby.state = EmotionState.ANGER
        baby.anger_timer = 3.0  # 持续3秒
        return
    
    # 疲惫检测
    if baby.mental_hp / baby.max_mental_hp < 0.3:
        baby.state = EmotionState.TIRED
        return
    
    # 恐惧检测（附近敌人数量）
    nearby = count_enemies_within(baby.pos, 150)  # 150px范围
    if nearby >= baby.fear_threshold:
        baby.state = EmotionState.FEAR
        return
    
    # 默认：好奇
    baby.state = EmotionState.CURIOUS
```

---

## 二、各状态行为细节

### 2.1 好奇（默认状态）

| 参数 | 数值 | 说明 |
|------|------|------|
| 移动速度 | 天使移速 × 80% | 比天使慢，保持距离感 |
| 探索范围 | 以婴儿为中心，半径 300px | 不会跑太远 |
| 决策权重 | 忠诚度% → 跟随天使；好奇度% → 随机探索 | 两者相加=100% |
| 距离约束 | 距天使 > 最大距离(400px) 时强制往天使移动 | 防走失 |

**行为伪代码：**

```python
def curious_behavior(baby, angel, dt):
    # 决策：跟随 vs 探索
    roll = random.random() * 100
    loyalty = baby.get_stat("loyalty")  # 上限95%
    
    if roll < loyalty:
        # 跟随天使
        target_pos = angel.pos
    else:
        # 随机探索
        if baby.explore_timer <= 0:
            angle = random.uniform(0, 2 * PI)
            dist = random.uniform(100, 300)
            baby.explore_target = angel.pos + Vector2(cos(angle)*dist, sin(angle)*dist)
            baby.explore_timer = random.uniform(1.0, 3.0)
        target_pos = baby.explore_target
    
    # 距离约束
    if distance(baby.pos, angel.pos) > 400:
        target_pos = angel.pos
    
    # 执行移动（带路径寻找）
    move_toward(baby, target_pos, baby.move_speed, dt)
    
    # 特殊行为（10%概率触发）
    if random.random() < 0.1:
        # 原地停留1-2秒
        baby.pause_timer = random.uniform(1.0, 2.0)
```

### 2.2 恐惧（附近有敌人）

| 参数 | 数值 |
|------|------|
| 触发条件 | 附近150px内敌人 ≥ 恐惧阈值（默认2只） |
| 移动速度 | 天使移速 × 120%（加速逃跑） |
| 移动目标 | 远离最近敌人 + 趋向天使（权重各50%） |
| 持续时间 | 直到附近敌人 < 恐惧阈值 |

```python
def fear_behavior(baby, enemies, angel, dt):
    # 找到最近的敌人
    nearest = find_nearest_enemy(baby.pos, enemies)
    if nearest:
        # 逃离方向 = 婴儿位置 - 敌人位置
        flee_dir = normalize(baby.pos - nearest.pos)
        # 趋向天使方向
        to_angel_dir = normalize(angel.pos - baby.pos)
        # 合成移动方向
        move_dir = normalize(flee_dir * 0.5 + to_angel_dir * 0.5)
        baby.move_speed = angel.move_speed * 1.2
        move_toward_dir(baby, move_dir, baby.move_speed, dt)
```

### 2.3 愤怒（连续受伤）

| 参数 | 数值 |
|------|------|
| 触发条件 | 连续2次受伤（3秒内） |
| 无敌时间 | 1秒 |
| 暴走攻击 | 婴儿攻击力 × 2，攻击范围 +50% |
| 持续时间 | 3秒（可卡因卡牌延长） |

```python
def anger_behavior(baby, dt):
    # 无敌
    baby.invincible_timer = 1.0
    
    # 暴走：主动冲向最近敌人攻击
    nearest = find_nearest_enemy(baby.pos, enemies)
    if nearest:
        move_toward(baby, nearest.pos, baby.move_speed * 1.5, dt)
        # 攻击
        if distance(baby.pos, nearest.pos) < baby.attack_range * 1.5:
            deal_damage(nearest, baby.attack_power * 2)
    
    baby.anger_timer -= dt
    if baby.anger_timer <= 0:
        baby.state = EmotionState.CURIOUS
```

### 2.4 疲惫（精神力低）

| 参数 | 数值 |
|------|------|
| 触发条件 | 精神力 < 30% 最大精神力 |
| 行为 | 原地喘息，不移动 |
| 恢复速度 | 每秒回复 ×2（基础）或 ×3（有G02卡） |
| 持续 | 直到精神力 > 30% |

```python
def tired_behavior(baby, dt):
    # 原地不动
    baby.move_speed = 0
    
    # 加速恢复
    recovery_mult = 2.0
    if has_card(baby, "G-02"):  # 温柔摇篮
        recovery_mult = 3.0
    baby.mental_hp += baby.recovery_rate * recovery_mult * dt
    
    if baby.mental_hp / baby.max_mental_hp > 0.3:
        baby.state = EmotionState.CURIOUS
```

### 2.5 觉醒（觉醒充能满）

| 参数 | 数值 |
|------|------|
| 触发条件 | 觉醒充能 ≥ 100（通过C07天使觉醒触发） |
| 持续时间 | 10秒（可卡因卡牌延长） |
| 效果 | 婴儿攻击力 ×3，移速 ×1.5，无视恐惧/疲惫 |
| 视觉 | 全身金色光效，屏幕边缘金边 |

```python
def awaken_behavior(baby, dt):
    baby.attack_power_mult = 3.0
    baby.move_speed_mult = 1.5
    baby.fear_ignore = True   # 无视恐惧
    baby.tired_ignore = True  # 无视疲惫
    
    # 主动猎杀
    target = find_strongest_enemy(baby.pos, enemies)
    if target:
        move_toward(baby, target.pos, baby.move_speed, dt)
        if in_attack_range(baby, target):
            deal_damage(target, baby.attack_power * baby.attack_power_mult)
    
    baby.awaken_timer -= dt
    if baby.awaken_timer <= 0:
        baby.awakening_energy = 0
        baby.state = EmotionState.CURIOUS
```

---

## 三、卡牌对AI的影响

| 卡牌 | 影响的参数 | 效果 |
|------|------------|------|
| B-01 温柔牵引 | move_speed_to_angel +20% | 恐惧/疲惫时更快回到天使身边 |
| B-04 忠诚呼唤 | loyalty +15%（上限95%） | 好奇状态更多跟随天使 |
| G-01 勇气灌输 | fear_threshold +1 | 更难触发恐惧（更勇敢） |
| G-03 暴怒之心 | anger_duration +1s，冲击波伤害+50% | 愤怒状态更强 |
| G-05 镇定光环 | 天使周围200px内 fear_threshold +2 | 天使走位保护婴儿 |

---

## 四、实现注意事项

1. **状态切换防抖**：同一帧内不要多次切换状态，设置 0.5 秒的状态锁定（除非触发觉醒）
2. **路径寻找**：使用 A* 或简单避开障碍物的算法，不要穿墙
3. **性能**：每 0.5 秒刷新一次状态，不要每帧检测（节省CPU）
4. **可视化**：在婴儿头顶显示当前状态图标（❓好奇 / 😨恐惧 / 😡愤怒 / 😴疲惫 / ✨觉醒）
5. **网络同步**（如有多人模式）：婴儿AI只在主机运行，状态同步到客户端

---

## 五、测试检查清单

- [ ] 婴儿在空旷地图会随机探索（不是一直跟着天使）
- [ ] 附近有≥2只敌人时，婴儿_SPEED  visibly 提升且往天使方向移动
- [ ] 连续被击中2次后，婴儿显示"愤怒"图标且有无敌闪光
- [ ] 精神力<30%时，婴儿停下来喘息（移动速度=0）
- [ ] 觉醒触发时，全屏特效+婴儿攻击力明显提升
- [ ] 所有状态切换都有 0.5 秒锁定，不会疯狂切换
