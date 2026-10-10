using System.Numerics;

namespace MirClient.Protocol.Resources;

public enum EntityType
{
    Player = 0,
    Npc = 1,
    Monster = 2
}

/// <summary>
/// 盛大传奇场景实体（玩家、NPC、怪物）统一领域模型
/// </summary>
public class MapEntity
{
    public int RecogId { get; set; }
    public string Name { get; set; } = "";
    public EntityType Type { get; set; } = EntityType.Monster;
    public int Appr { get; set; } = 0; // 外观ID: NPC对应NpcLib偏置，怪物对应MonLib偏置

    // 坐标与朝向 (8 方向: 0:上, 1:右上, 2:右, 3:右下, 4:下, 5:左下, 6:左, 7:左上)
    public Vector2 TilePos { get; set; }
    public int Direction { get; set; } = 4;

    // 生命值
    public int Hp { get; set; } = 100;
    public int MaxHp { get; set; } = 100;
    public bool IsDead => Hp <= 0;

    // 动作与动画时钟
    public HumanActionType CurrentAction { get; set; } = HumanActionType.Stand;
    public int MotionStep { get; set; } = 0;
    public float ActionTimer { get; set; } = 0f;

    // 浮动伤害提示 (文字、颜色、剩余生命秒数)
    public string? DamageText { get; set; }
    public float DamageTextTimer { get; set; }

    public MapEntity(int recogId, string name, EntityType type, Vector2 tilePos, int appr = 0, int maxHp = 100)
    {
        RecogId = recogId;
        Name = name;
        Type = type;
        TilePos = tilePos;
        Appr = appr;
        MaxHp = maxHp;
        Hp = maxHp;
    }

    /// <summary>
    /// 触发单次动作
    /// </summary>
    public void TriggerAction(HumanActionType action)
    {
        CurrentAction = action;
        MotionStep = 0;
        ActionTimer = 0f;
    }

    /// <summary>
    /// 受到伤害
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (IsDead) return;
        Hp = Math.Max(0, Hp - damage);
        DamageText = $"-{damage}";
        DamageTextTimer = 1.0f; // 飘字持续 1 秒

        if (Hp <= 0)
        {
            TriggerAction(HumanActionType.Die);
        }
        else
        {
            TriggerAction(HumanActionType.Struck);
        }
    }

    /// <summary>
    /// 推进实体动画时钟
    /// </summary>
    public void UpdateAnimation(float dt)
    {
        if (DamageTextTimer > 0f)
        {
            DamageTextTimer -= dt;
            if (DamageTextTimer <= 0f) DamageText = null;
        }

        var actionInfo = HumanAction.GetActionInfo(CurrentAction);
        ActionTimer += dt * 1000f;
        if (ActionTimer >= actionInfo.FTime)
        {
            ActionTimer -= actionInfo.FTime;

            bool isOneShot = CurrentAction switch
            {
                HumanActionType.Attack or HumanActionType.HeavyHit or HumanActionType.BigHit or
                HumanActionType.Spell or HumanActionType.Struck or HumanActionType.Die => true,
                _ => false
            };

            if (isOneShot)
            {
                if (MotionStep + 1 >= actionInfo.Frame)
                {
                    if (CurrentAction == HumanActionType.Die)
                    {
                        MotionStep = actionInfo.Frame - 1; // 死亡停留在倒地最后一帧
                    }
                    else
                    {
                        // 攻击/受击完成，恢复待机
                        CurrentAction = HumanActionType.Stand;
                        MotionStep = 0;
                    }
                }
                else
                {
                    MotionStep++;
                }
            }
            else
            {
                MotionStep = (MotionStep + 1) % actionInfo.Frame;
            }
        }
    }

    /// <summary>
    /// 计算前方 1 格的攻击落点瓦片坐标
    /// </summary>
    public static Vector2 GetFrontTile(Vector2 origin, int direction)
    {
        int dx = 0, dy = 0;
        switch (direction)
        {
            case 0: dy = -1; break;
            case 1: dx = 1; dy = -1; break;
            case 2: dx = 1; break;
            case 3: dx = 1; dy = 1; break;
            case 4: dy = 1; break;
            case 5: dx = -1; dy = 1; break;
            case 6: dx = -1; break;
            case 7: dx = -1; dy = -1; break;
        }
        return new Vector2((float)Math.Round(origin.X) + dx, (float)Math.Round(origin.Y) + dy);
    }
}
