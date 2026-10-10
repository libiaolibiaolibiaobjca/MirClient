using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class EntityTests
{
    [Fact]
    public void TestGetFrontTile_AllEightDirections()
    {
        Vector2 origin = new(330, 268);

        // 0: 上 (0, -1) => (330, 267)
        Assert.Equal(new Vector2(330, 267), MapEntity.GetFrontTile(origin, 0));

        // 1: 右上 (1, -1) => (331, 267)
        Assert.Equal(new Vector2(331, 267), MapEntity.GetFrontTile(origin, 1));

        // 2: 右 (1, 0) => (331, 268)
        Assert.Equal(new Vector2(331, 268), MapEntity.GetFrontTile(origin, 2));

        // 3: 右下 (1, 1) => (331, 269)
        Assert.Equal(new Vector2(331, 269), MapEntity.GetFrontTile(origin, 3));

        // 4: 下 (0, 1) => (330, 269)
        Assert.Equal(new Vector2(330, 269), MapEntity.GetFrontTile(origin, 4));

        // 5: 左下 (-1, 1) => (329, 269)
        Assert.Equal(new Vector2(329, 269), MapEntity.GetFrontTile(origin, 5));

        // 6: 左 (-1, 0) => (329, 268)
        Assert.Equal(new Vector2(329, 268), MapEntity.GetFrontTile(origin, 6));

        // 7: 左上 (-1, -1) => (329, 267)
        Assert.Equal(new Vector2(329, 267), MapEntity.GetFrontTile(origin, 7));
    }

    [Fact]
    public void TestMonster_TakeDamageAndDie()
    {
        var monster = new MapEntity(2001, "稻草人", EntityType.Monster, new Vector2(332, 268), appr: 0, maxHp: 100);

        Assert.False(monster.IsDead);
        Assert.Equal(100, monster.Hp);

        // 受到 30 点伤害 -> 触发 Struck
        monster.TakeDamage(30);
        Assert.Equal(70, monster.Hp);
        Assert.Equal("-30", monster.DamageText);
        Assert.Equal(HumanActionType.Struck, monster.CurrentAction);
        Assert.False(monster.IsDead);

        // 受到 80 点致命伤害 -> 触发 Die
        monster.TakeDamage(80);
        Assert.Equal(0, monster.Hp);
        Assert.True(monster.IsDead);
        Assert.Equal(HumanActionType.Die, monster.CurrentAction);

        // 死亡后再次受击不产生额外扣血
        monster.TakeDamage(50);
        Assert.Equal(0, monster.Hp);
    }

    [Fact]
    public void TestMonster_AnimationClock()
    {
        var monster = new MapEntity(2001, "稻草人", EntityType.Monster, new Vector2(332, 268), appr: 0, maxHp: 100);
        monster.TriggerAction(HumanActionType.Attack); // Attack 85ms 一帧

        Assert.Equal(0, monster.MotionStep);

        // 推进 0.09 秒 (90ms) -> 应步进到第 1 帧
        monster.UpdateAnimation(0.09f);
        Assert.Equal(1, monster.MotionStep);
    }

    [Fact]
    public void TestYSorting_OrderCorrectness()
    {
        // 模拟 3 个实体不同的脚底 Y 坐标
        var e1 = new MapEntity(1, "远方怪", EntityType.Monster, new Vector2(330, 260)); // Y=260 (远处)
        var e2 = new MapEntity(2, "中间玩家", EntityType.Player, new Vector2(330, 265)); // Y=265
        var e3 = new MapEntity(3, "近景NPC", EntityType.Npc, new Vector2(330, 270));    // Y=270 (近处)

        var list = new List<MapEntity> { e3, e1, e2 };
        list.Sort((a, b) => a.TilePos.Y.CompareTo(b.TilePos.Y));

        Assert.Equal("远方怪", list[0].Name);
        Assert.Equal("中间玩家", list[1].Name);
        Assert.Equal("近景NPC", list[2].Name);
    }
}
