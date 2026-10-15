using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class WorldBossTests
{
    public WorldBossTests()
    {
        WorldBossManager.Instance.Reset();
    }

    [Fact]
    public void Test_WorldBoss_Spawn_AllTypes()
    {
        var boss = WorldBossManager.Instance;

        // 1. 沃玛教主
        boss.SpawnBoss(WorldBossType.WomaKing, new Vector2(100, 100));
        Assert.Equal("沃玛教主", boss.BossName);
        Assert.Equal(8000, boss.MaxHp);
        Assert.Equal(8000, boss.CurrentHp);
        Assert.Equal(WorldBossPhase.Alive, boss.Phase);

        // 2. 牛魔王
        boss.SpawnBoss(WorldBossType.BullDemonKing, new Vector2(200, 200));
        Assert.Equal("牛魔王", boss.BossName);
        Assert.Equal(10000, boss.MaxHp);

        // 3. 祖玛教主
        boss.SpawnBoss(WorldBossType.ZumaKing, new Vector2(300, 300));
        Assert.Equal("祖玛教主", boss.BossName);
        Assert.Equal(12000, boss.MaxHp);

        // 4. 赤月恶魔
        boss.SpawnBoss(WorldBossType.MoonDevil, new Vector2(400, 400));
        Assert.Equal("赤月恶魔", boss.BossName);
        Assert.Equal(15000, boss.MaxHp);
    }

    [Fact]
    public void Test_WorldBoss_Damage_And_Enrage()
    {
        var boss = WorldBossManager.Instance;
        boss.SpawnBoss(WorldBossType.ZumaKing, new Vector2(330, 268));

        // 受到伤害，生命高于 50%
        boss.ApplyDamage("无双战神", 4000, out bool enrage1, out bool killed1);
        Assert.False(enrage1);
        Assert.False(killed1);
        Assert.Equal(8000, boss.CurrentHp);
        Assert.Equal(WorldBossPhase.Alive, boss.Phase);

        // 继续受到伤害，跌破 50% (<= 6000)，触发嗜血狂暴
        boss.ApplyDamage("无双战神", 2500, out bool enrage2, out bool killed2);
        Assert.True(enrage2);
        Assert.False(killed2);
        Assert.Equal(5500, boss.CurrentHp);
        Assert.True(boss.IsEnraged);
        Assert.Equal(WorldBossPhase.Enraged, boss.Phase);
        Assert.Contains(boss.WorldBroadcasts, bc => bc.Contains("嗜血狂暴"));
    }

    [Fact]
    public void Test_WorldBoss_DamageMeter_And_HighestDealer()
    {
        var boss = WorldBossManager.Instance;
        boss.SpawnBoss(WorldBossType.ZumaKing, new Vector2(330, 268));

        boss.ApplyDamage("昔日法神", 3000, out _, out _);
        boss.ApplyDamage("烈火战士", 5000, out _, out _);
        boss.ApplyDamage("无极道人", 2000, out _, out _);

        Assert.Equal("昔日法神", boss.FirstAttacker);
        Assert.Equal("烈火战士", boss.HighestDamageDealer);
        Assert.Equal(5000, boss.DamageMeter["烈火战士"]);
        Assert.Equal(3000, boss.DamageMeter["昔日法神"]);
        Assert.Equal(2000, boss.DamageMeter["无极道人"]);
    }

    [Fact]
    public void Test_WorldBoss_SkillDefinition()
    {
        var boss = WorldBossManager.Instance;
        boss.SpawnBoss(WorldBossType.MoonDevil, new Vector2(100, 100));

        // 常态技能
        var normalSkill = boss.GetSkillDefinition();
        Assert.Equal("九幽地刺穿心", normalSkill.Name);
        Assert.Equal(320, normalSkill.Damage);

        // 狂暴态技能
        boss.ApplyDamage("主宰者", 10000, out bool enrage, out _);
        Assert.True(enrage);
        var enragedSkill = boss.GetSkillDefinition();
        Assert.Equal(500, enragedSkill.Damage); // 狂暴后大招伤害大幅提升
    }

    [Fact]
    public void Test_RollRareAffix_Quality()
    {
        // 1. 精品 (+2)
        var w1 = Item.CreateJudgmentMace();
        var (i1, r1, b1) = WorldBossManager.RollRareAffix(w1, 2);
        Assert.Equal(ItemRarity.Fine, r1);
        Assert.Equal(2, b1);
        Assert.Equal(2, i1.ExtraDc);

        // 2. 黄金极品 (+4)
        var w2 = Item.CreateJudgmentMace();
        var (i2, r2, b2) = WorldBossManager.RollRareAffix(w2, 4);
        Assert.Equal(ItemRarity.Rare, r2);
        Assert.Equal(4, b2);
        Assert.Equal(4, i2.ExtraDc);

        // 3. 神圣绝品 (+5)
        var w3 = Item.CreateJudgmentMace();
        var (i3, r3, b3) = WorldBossManager.RollRareAffix(w3, 5);
        Assert.Equal(ItemRarity.Epic, r3);
        Assert.Equal(5, b3);
        Assert.Equal(5, i3.ExtraDc);
    }

    [Fact]
    public void Test_BigExplosion_Loot_And_Ownership()
    {
        var boss = WorldBossManager.Instance;
        boss.SpawnBoss(WorldBossType.ZumaKing, new Vector2(330, 268));

        boss.ApplyDamage("风云法圣", 4000, out _, out _);
        boss.ApplyDamage("傲世刀狂", 8000, out _, out bool killed);
        Assert.True(killed);

        // 触发天女散花大爆
        var drops = boss.TriggerBigExplosion(new Vector2(330, 268), "傲世刀狂", (x, y) => false);
        Assert.NotEmpty(drops);
        Assert.True(drops.Count >= 14); // 包含多堆金币、多捆太阳水与教主神装

        // 最高伤害者（傲世刀狂）应享有所有掉落物的 60 秒归属保护权
        foreach (var drop in drops)
        {
            Assert.Equal("傲世刀狂", drop.OwnerName);
            Assert.True(drop.ProtectSeconds > 0f);
            Assert.True(drop.IsProtected);
        }

        // 广播队列包含击杀通告
        Assert.Contains(boss.WorldBroadcasts, bc => bc.Contains("傲世刀狂") && bc.Contains("祖玛教主"));
    }

    [Fact]
    public void Test_Pickup_Protection_And_Expiration()
    {
        var boss = WorldBossManager.Instance;
        boss.SpawnBoss(WorldBossType.WomaKing, new Vector2(200, 200));
        boss.ApplyDamage("刀锋战士", 8000, out _, out _);

        var drops = boss.TriggerBigExplosion(new Vector2(200, 200), "刀锋战士", (x, y) => false);
        var targetLoot = drops.First(d => d.Item != null);

        var invOther = new InventoryManager();
        int goldOther = 0;

        // 1. 他人（路人甲）在 60 秒保护期内拾取失败
        bool pickedByOther = boss.TryPickupBossLoot(targetLoot.TilePos, "路人甲", invOther, ref goldOther, out string msgOther);
        Assert.False(pickedByOther);
        Assert.Contains("归属保护", msgOther);

        // 2. 归属者（刀锋战士）在保护期内成功拾取
        var invOwner = new InventoryManager();
        int goldOwner = 0;
        bool pickedByOwner = boss.TryPickupBossLoot(targetLoot.TilePos, "刀锋战士", invOwner, ref goldOwner, out string msgOwner);
        Assert.True(pickedByOwner);
        Assert.Contains("成功缴获", msgOwner);

        // 3. 时间流逝 61 秒，保护期过期
        boss.Update(61f, new Vector2(200, 200), out _);
        var freeLoot = boss.ActiveLoots.First(d => d.Item != null);
        Assert.False(freeLoot.IsProtected);

        // 此时他人可自由捡漏夺宝
        bool pickedExpired = boss.TryPickupBossLoot(freeLoot.TilePos, "路人甲", invOther, ref goldOther, out string msgExpired);
        Assert.True(pickedExpired);
        Assert.Contains("成功缴获", msgExpired);
    }

    [Fact]
    public void Test_CommandParser_Boss_And_Drop()
    {
        // 1. @boss spawn
        var cmd1 = CommandParser.Parse("@boss spawn 赤月恶魔");
        Assert.Equal(ParsedCommandType.Boss, cmd1.Type);
        Assert.Equal("spawn", cmd1.SubAction);
        Assert.Equal("赤月恶魔", cmd1.TargetName);

        // 2. @boss kill
        var cmd2 = CommandParser.Parse("@boss kill");
        Assert.Equal(ParsedCommandType.Boss, cmd2.Type);
        Assert.Equal("kill", cmd2.SubAction);

        // 3. @boss status
        var cmd3 = CommandParser.Parse("@boss status");
        Assert.Equal(ParsedCommandType.Boss, cmd3.Type);
        Assert.Equal("status", cmd3.SubAction);

        // 4. @drop
        var cmd4 = CommandParser.Parse("@drop 裁决之杖 5");
        Assert.Equal(ParsedCommandType.Drop, cmd4.Type);
        Assert.Equal("裁决之杖", cmd4.TargetName);
        Assert.Equal(5, cmd4.Amount);
    }
}
