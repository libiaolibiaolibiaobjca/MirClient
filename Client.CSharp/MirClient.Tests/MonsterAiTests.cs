using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MonsterAiTests
{
    [Fact]
    public void TestMonster_AggroDetection_EnterChase()
    {
        MapEntity cat = new(101, "钉耙猫", EntityType.Monster, new Vector2(330, 268), 0, 80);
        MonsterAiController ai = new(cat);
        Assert.Equal(MonsterAiState.Idle, ai.State);

        // 玩家距离 8 格 (超出 AggroRadius 6格) -> 保持 Idle
        ai.Update(0.1f, new Vector2(338, 268), (_, _) => false, out _);
        Assert.Equal(MonsterAiState.Idle, ai.State);

        // 玩家进入 4 格 (<= 6格) -> 转入 Chase
        ai.Update(0.1f, new Vector2(334, 268), (_, _) => false, out _);
        Assert.Equal(MonsterAiState.Chase, ai.State);
    }

    [Fact]
    public void TestMonster_Damaged_Enrage()
    {
        MapEntity pig = new(102, "白野猪", EntityType.Monster, new Vector2(350, 268), 0, 500);
        MonsterAiController ai = new(pig);
        Assert.Equal(MonsterAiState.Idle, ai.State);

        // 远程雷电术打击，激怒怪物
        ai.OnDamaged(60);
        Assert.Equal(MonsterAiState.Chase, ai.State);
    }

    [Fact]
    public void TestMonster_ApproachAndAttack()
    {
        MapEntity cat = new(103, "钉耙猫", EntityType.Monster, new Vector2(330, 268), 0, 80);
        MonsterAiController ai = new(cat);

        // 怪物与玩家只有 1.0 格 (在攻击距离内)
        Vector2 playerPos = new(331, 268);
        ai.Update(0.1f, playerPos, (_, _) => false, out _);
        Assert.Equal(MonsterAiState.Attack, ai.State);

        // 推进时间使其超过攻击冷却 (1.2秒)
        bool attacked = ai.Update(1.5f, playerPos, (_, _) => false, out int damage);
        Assert.True(attacked);
        Assert.InRange(damage, ai.DamageMin, ai.DamageMax);
    }

    [Fact]
    public void TestMonster_Deaggro_OutOfRange()
    {
        MapEntity scarecrow = new(104, "稻草人", EntityType.Monster, new Vector2(330, 268), 0, 100);
        MonsterAiController ai = new(scarecrow);

        // 先激怒进入追逐
        ai.OnDamaged(20);
        Assert.Equal(MonsterAiState.Chase, ai.State);

        // 玩家使用随机传送石飞到 20 格外 (超出 DeaggroRadius 12格)
        ai.Update(0.1f, new Vector2(360, 268), (_, _) => false, out _);
        Assert.Equal(MonsterAiState.Idle, ai.State);
    }

    [Fact]
    public void TestMonster_DeadAndRespawn()
    {
        MapEntity pig = new(105, "白野猪", EntityType.Monster, new Vector2(330, 268), 0, 500);
        MonsterAiController ai = new(pig);

        pig.TakeDamage(600);
        Assert.True(pig.IsDead);

        ai.Update(0.1f, new Vector2(330, 268), (_, _) => false, out _);
        Assert.Equal(MonsterAiState.Dead, ai.State);

        // 复活重置
        ai.Respawn();
        Assert.False(pig.IsDead);
        Assert.Equal(500, pig.Hp);
        Assert.Equal(MonsterAiState.Idle, ai.State);
    }
}
