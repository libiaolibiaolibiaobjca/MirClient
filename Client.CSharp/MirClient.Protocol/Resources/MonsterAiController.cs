using System.Numerics;

namespace MirClient.Protocol.Resources;

public enum MonsterAiState
{
    Idle,   // 待机巡逻
    Roam,   // 散步游荡
    Chase,  // 仇恨追逐玩家
    Attack, // 近战攻击
    Dead    // 倒地阵亡
}

/// <summary>
/// 传奇怪物自主行为与仇恨决策控制器
/// </summary>
public class MonsterAiController
{
    public MapEntity Entity { get; }
    public Vector2 SpawnTile { get; set; }
    public MonsterAiState State { get; private set; } = MonsterAiState.Idle;

    public float AggroRadius { get; set; } = 6.0f;     // 感知警戒半径
    public float DeaggroRadius { get; set; } = 12.0f;  // 丢失仇恨半径
    public float AttackRadius { get; set; } = 1.25f;   // 攻击触达距离
    public float MoveSpeed { get; set; } = 1.6f;       // 移动步速 (瓦片/秒)

    public float AttackCooldown { get; set; } = 1.5f;  // 攻击冷却秒数
    private float _attackTimer = 0f;

    public int DamageMin { get; set; } = 12;
    public int DamageMax { get; set; } = 24;

    private float _stateTimer = 0f;
    private Vector2 _roamTarget;

    public MonsterAiController(MapEntity entity)
    {
        Entity = entity;
        SpawnTile = entity.TilePos;
        _roamTarget = entity.TilePos;

        // 根据怪物类型区分攻击威力与攻速
        if (entity.Name.Contains("白野猪"))
        {
            DamageMin = 25;
            DamageMax = 45;
            AttackCooldown = 1.8f;
            MoveSpeed = 1.8f;
        }
        else if (entity.Name.Contains("钉耙猫"))
        {
            DamageMin = 8;
            DamageMax = 16;
            AttackCooldown = 1.2f;
            MoveSpeed = 2.0f;
        }
        else if (entity.Name.Contains("稻草人"))
        {
            DamageMin = 6;
            DamageMax = 12;
            AttackCooldown = 1.5f;
            MoveSpeed = 1.2f;
        }
    }

    /// <summary>
    /// 受击激怒：立即转入追逐锁定攻击者
    /// </summary>
    public void OnDamaged(int damage)
    {
        if (Entity.IsDead)
        {
            State = MonsterAiState.Dead;
            return;
        }

        // 受到伤害立即锁定玩家激怒追击
        State = MonsterAiState.Chase;
    }

    /// <summary>
    /// 每帧推进 AI 决策状态机
    /// </summary>
    public bool Update(float dt, Vector2 playerTilePos, Func<int, int, bool> isBlocked, out int damageDealt)
    {
        damageDealt = 0;
        if (Entity.IsDead)
        {
            State = MonsterAiState.Dead;
            return false;
        }

        _attackTimer += dt;
        _stateTimer += dt;

        float distToPlayer = Vector2.Distance(Entity.TilePos, playerTilePos);

        switch (State)
        {
            case MonsterAiState.Idle:
            {
                // 检测感知玩家
                if (distToPlayer <= AttackRadius)
                {
                    State = MonsterAiState.Attack;
                    _stateTimer = 0f;
                    break;
                }
                if (distToPlayer <= AggroRadius)
                {
                    State = MonsterAiState.Chase;
                    _stateTimer = 0f;
                    break;
                }

                // 待机 2.5 秒后随机游荡散步
                if (_stateTimer >= 2.5f)
                {
                    _stateTimer = 0f;
                    // 在出生点 3 格范围内选择一个随机目标
                    float ox = (Random.Shared.NextSingle() * 6f) - 3f;
                    float oy = (Random.Shared.NextSingle() * 6f) - 3f;
                    Vector2 target = SpawnTile + new Vector2(ox, oy);
                    if (!isBlocked((int)target.X, (int)target.Y))
                    {
                        _roamTarget = target;
                        State = MonsterAiState.Roam;
                    }
                }
                break;
            }

            case MonsterAiState.Roam:
            {
                // 游荡途中若发现玩家，立即攻击或追击
                if (distToPlayer <= AttackRadius)
                {
                    State = MonsterAiState.Attack;
                    _stateTimer = 0f;
                    break;
                }
                if (distToPlayer <= AggroRadius)
                {
                    State = MonsterAiState.Chase;
                    _stateTimer = 0f;
                    break;
                }

                Vector2 diff = _roamTarget - Entity.TilePos;
                if (diff.Length() > 0.1f)
                {
                    Vector2 step = Vector2.Normalize(diff) * MoveSpeed * 0.6f * dt;
                    Vector2 next = Entity.TilePos + step;
                    if (!isBlocked((int)next.X, (int)next.Y))
                    {
                        Entity.TilePos = next;
                        UpdateDirection(diff);
                    }
                    else
                    {
                        State = MonsterAiState.Idle; // 遇阻停步
                    }
                }
                else
                {
                    State = MonsterAiState.Idle;
                    _stateTimer = 0f;
                }
                break;
            }

            case MonsterAiState.Chase:
            {
                // 玩家超出脱离仇恨距离，失去目标
                if (distToPlayer > DeaggroRadius)
                {
                    State = MonsterAiState.Idle;
                    _stateTimer = 0f;
                    break;
                }

                // 接近到攻击距离内，准备攻击
                if (distToPlayer <= AttackRadius)
                {
                    State = MonsterAiState.Attack;
                    break;
                }

                // 朝玩家方向奔跑逼近
                Vector2 diff = playerTilePos - Entity.TilePos;
                Vector2 step = Vector2.Normalize(diff) * MoveSpeed * dt;
                Vector2 next = Entity.TilePos + step;
                if (!isBlocked((int)next.X, (int)next.Y))
                {
                    Entity.TilePos = next;
                    UpdateDirection(diff);
                }
                break;
            }

            case MonsterAiState.Attack:
            {
                // 玩家拉开距离，继续追击
                if (distToPlayer > AttackRadius * 1.25f)
                {
                    State = MonsterAiState.Chase;
                    break;
                }

                // 面向玩家
                UpdateDirection(playerTilePos - Entity.TilePos);

                // 攻击冷却完毕，发动攻击
                if (_attackTimer >= AttackCooldown)
                {
                    _attackTimer = 0f;
                    damageDealt = Random.Shared.Next(DamageMin, DamageMax + 1);
                    return true;
                }
                break;
            }

            case MonsterAiState.Dead:
            default:
                break;
        }

        return false;
    }

    /// <summary>
    /// 重生复活
    /// </summary>
    public void Respawn()
    {
        Entity.TilePos = SpawnTile;
        Entity.Hp = Entity.MaxHp;
        State = MonsterAiState.Idle;
        _attackTimer = 0f;
        _stateTimer = 0f;
    }

    private void UpdateDirection(Vector2 dir)
    {
        if (dir.LengthSquared() < 0.001f) return;
        float angle = MathF.Atan2(dir.Y, dir.X) * (180.0f / MathF.PI);
        if (angle < 0) angle += 360f;

        Entity.Direction = angle switch
        {
            >= 247.5f and < 292.5f => 0, // 上
            >= 292.5f and < 337.5f => 1, // 右上
            >= 337.5f or < 22.5f   => 2, // 右
            >= 22.5f and < 67.5f   => 3, // 右下
            >= 67.5f and < 112.5f  => 4, // 下
            >= 112.5f and < 157.5f => 5, // 左下
            >= 157.5f and < 202.5f => 6, // 左
            _                      => 7  // 左上
        };
    }
}
