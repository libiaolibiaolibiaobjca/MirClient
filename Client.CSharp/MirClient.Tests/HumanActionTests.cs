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
    public void TestDressOffsetCalculation()
    {
        // 服装偏移 HUMANFRAME = 600
        int dress1Stand = HumanAction.CalculateFrameIndex(HumanActionType.Stand, direction: 4, motionStep: 0, dress: 1);
        Assert.Equal(600 + 4 * 8, dress1Stand);
    }
}
