using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class HumanActionTests
{
    [Fact]
    public void TestStandFrameCalculation()
    {
        // 站立动作 8 方向起始帧测试
        for (int dir = 0; dir < 8; dir++)
        {
            int expectedStart = dir * 8; // Start: 0, frame: 4, skip: 4 => stride = 8
            int actualStart = HumanAction.CalculateFrameIndex(HumanActionType.Stand, dir, 0, dress: 0);
            Assert.Equal(expectedStart, actualStart);

            // 每方向 4 帧有效帧
            for (int step = 0; step < 4; step++)
            {
                int frame = HumanAction.CalculateFrameIndex(HumanActionType.Stand, dir, step, dress: 0);
                Assert.Equal(expectedStart + step, frame);
            }
        }
    }

    [Fact]
    public void TestWalkFrameCalculation()
    {
        // 行走动作 8 方向起始帧测试 (Start = 64)
        for (int dir = 0; dir < 8; dir++)
        {
            int expectedStart = 64 + dir * 8; // Start: 64, frame: 6, skip: 2 => stride = 8
            int actualStart = HumanAction.CalculateFrameIndex(HumanActionType.Walk, dir, 0, dress: 0);
            Assert.Equal(expectedStart, actualStart);

            // 每方向 6 帧有效帧
            for (int step = 0; step < 6; step++)
            {
                int frame = HumanAction.CalculateFrameIndex(HumanActionType.Walk, dir, step, dress: 0);
                Assert.Equal(expectedStart + step, frame);
            }
        }
    }

    [Fact]
    public void TestRunFrameCalculation()
    {
        // 奔跑动作 8 方向起始帧测试 (Start = 128)
        for (int dir = 0; dir < 8; dir++)
        {
            int expectedStart = 128 + dir * 8; // Start: 128, frame: 6, skip: 2 => stride = 8
            int actualStart = HumanAction.CalculateFrameIndex(HumanActionType.Run, dir, 0, dress: 0);
            Assert.Equal(expectedStart, actualStart);
        }
    }

    [Fact]
    public void TestAttackFrameCalculation()
    {
        // 普通挥砍攻击动作 8 方向测试 (Start = 200, 6帧, skip 2 => 步长 8)
        for (int dir = 0; dir < 8; dir++)
        {
            int expectedStart = 200 + dir * 8;
            int actualStart = HumanAction.CalculateFrameIndex(HumanActionType.Attack, dir, 0, dress: 0);
            Assert.Equal(expectedStart, actualStart);

            // 每方向 6 帧有效帧
            for (int step = 0; step < 6; step++)
            {
                int frame = HumanAction.CalculateFrameIndex(HumanActionType.Attack, dir, step, dress: 0);
                Assert.Equal(expectedStart + step, frame);
            }
        }
    }

    [Fact]
    public void TestSpellFrameCalculation()
    {
        // 施法动作 8 方向测试 (Start = 392, 6帧, skip 2 => 步长 8)
        for (int dir = 0; dir < 8; dir++)
        {
            int expectedStart = 392 + dir * 8;
            int actualStart = HumanAction.CalculateFrameIndex(HumanActionType.Spell, dir, 0, dress: 0);
            Assert.Equal(expectedStart, actualStart);
        }
    }

    [Fact]
    public void TestStruckAndDieFrameCalculation()
    {
        // 受击 (Start = 472, 3帧, skip 5 => 步长 8)
        int struckFrame = HumanAction.CalculateFrameIndex(HumanActionType.Struck, direction: 4, motionStep: 2, dress: 0);
        Assert.Equal(472 + 4 * 8 + 2, struckFrame);

        // 死亡倒地 (Start = 536, 4帧, skip 4 => 步长 8)
        int dieFrame = HumanAction.CalculateFrameIndex(HumanActionType.Die, direction: 4, motionStep: 3, dress: 0);
        Assert.Equal(536 + 4 * 8 + 3, dieFrame);
    }

    [Fact]
    public void TestDressOffsetCalculation()
    {
        // 服装偏移 HUMANFRAME = 600
        int dress1Stand = HumanAction.CalculateFrameIndex(HumanActionType.Stand, direction: 4, motionStep: 0, dress: 1);
        Assert.Equal(600 + 4 * 8, dress1Stand);
    }

    [Fact]
    public void TestHairFrameCalculation()
    {
        // 光头 (hairId = 0) 不生成贴图
        Assert.Equal(-1, HumanAction.CalculateHairFrameIndex(HumanActionType.Stand, direction: 0, motionStep: 0, hairId: 0, sex: 0));

        // 经典短发 (hairId = 1): 男 1200, 女 1800
        int maleHairStand = HumanAction.CalculateHairFrameIndex(HumanActionType.Stand, direction: 4, motionStep: 0, hairId: 1, sex: 0);
        Assert.Equal(1200 + 4 * 8, maleHairStand);

        int femaleHairStand = HumanAction.CalculateHairFrameIndex(HumanActionType.Stand, direction: 4, motionStep: 0, hairId: 1, sex: 1);
        Assert.Equal(1800 + 4 * 8, femaleHairStand);
    }

    [Fact]
    public void TestWeaponFrameCalculation()
    {
        // 空手 (weaponId = 0) 不生成贴图
        Assert.Equal(-1, HumanAction.CalculateWeaponFrameIndex(HumanActionType.Stand, direction: 0, motionStep: 0, weaponId: 0, sex: 0));

        // 木剑 (weaponId = 1): offset = 600
        int woodenSwordStand = HumanAction.CalculateWeaponFrameIndex(HumanActionType.Stand, direction: 4, motionStep: 0, weaponId: 1, sex: 0);
        Assert.Equal(600 + 4 * 8, woodenSwordStand);

        // 裁决之杖 (weaponId = 5): offset = 3000
        int judgeStaffAttack = HumanAction.CalculateWeaponFrameIndex(HumanActionType.Attack, direction: 4, motionStep: 2, weaponId: 5, sex: 0);
        Assert.Equal(3000 + 200 + 4 * 8 + 2, judgeStaffAttack);
    }

    [Fact]
    public void TestWeaponOrderTable()
    {
        // 面北站立待机 (dir = 0, baseFrame = 0): 武器在身后 (WORDER = 0)
        int northOrder = HumanAction.GetWeaponOrder(sex: 0, baseFrame: 0);
        Assert.Equal(0, northOrder);

        // 面东站立待机 (dir = 2, baseFrame = 16): 武器在身前持握 (WORDER = 1)
        int eastOrder = HumanAction.GetWeaponOrder(sex: 0, baseFrame: 16);
        Assert.Equal(1, eastOrder);

        // 面南站立待机 (dir = 4, baseFrame = 32): 盛大原版木剑垂于腰侧后方 (WORDER = 0)
        int southOrder = HumanAction.GetWeaponOrder(sex: 0, baseFrame: 32);
        Assert.Equal(0, southOrder);
    }
}
