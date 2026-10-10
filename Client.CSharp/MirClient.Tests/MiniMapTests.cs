using System.Numerics;
using MirClient.Protocol.Resources;
using Xunit;

namespace MirClient.Tests;

public class MiniMapTests
{
    [Fact]
    public void TestWorldToMiniMap_And_ReverseProjection()
    {
        // 假设比奇城 1000 x 1000 大地图，小地图放在 (600, 10, 160, 160)
        MiniMapProjection proj = new(1000, 1000, 600f, 10f, 160f, 160f);

        // 测试原点 (0, 0)
        Vector2 originScreen = proj.WorldToMiniMap(Vector2.Zero);
        Assert.Equal(600f, originScreen.X);
        Assert.Equal(10f, originScreen.Y);

        // 测试中心点 (500, 500)
        Vector2 centerScreen = proj.WorldToMiniMap(new Vector2(500, 500));
        Assert.Equal(680f, centerScreen.X);
        Assert.Equal(90f, centerScreen.Y);

        // 测试反向投影: 点击中心 (680, 90) -> 还原到世界坐标 (500, 500)
        Vector2 worldPos = proj.MiniMapToWorld(new Vector2(680, 90));
        Assert.Equal(500f, worldPos.X);
        Assert.Equal(500f, worldPos.Y);
    }

    [Fact]
    public void TestBoundaryProtection_Clamp()
    {
        MiniMapProjection proj = new(800, 600, 600f, 20f, 180f, 140f);

        // 点击负值超界
        Vector2 negWorld = proj.MiniMapToWorld(new Vector2(100f, -50f));
        Assert.Equal(0f, negWorld.X);
        Assert.Equal(0f, negWorld.Y);

        // 点击极大值超界
        Vector2 maxWorld = proj.MiniMapToWorld(new Vector2(1920f, 1080f));
        Assert.Equal(799f, maxWorld.X);
        Assert.Equal(599f, maxWorld.Y);
    }

    [Fact]
    public void TestIsInside_HitTest()
    {
        MiniMapProjection proj = new(1000, 1000, 600f, 20f, 180f, 140f);

        Assert.True(proj.IsInside(new Vector2(650, 50)));
        Assert.True(proj.IsInside(new Vector2(600, 20)));
        Assert.True(proj.IsInside(new Vector2(780, 160)));

        Assert.False(proj.IsInside(new Vector2(590, 50)));
        Assert.False(proj.IsInside(new Vector2(800, 50)));
        Assert.False(proj.IsInside(new Vector2(650, 10)));
        Assert.False(proj.IsInside(new Vector2(650, 170)));
    }
}
