using System.Numerics;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大传奇小地图雷达坐标双向投影与寻路映射器
/// </summary>
public class MiniMapProjection
{
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }

    public float RectX { get; set; }
    public float RectY { get; set; }
    public float RectW { get; set; }
    public float RectH { get; set; }

    public MiniMapProjection(int mapWidth, int mapHeight, float rectX, float rectY, float rectW, float rectH)
    {
        MapWidth = Math.Max(1, mapWidth);
        MapHeight = Math.Max(1, mapHeight);
        RectX = rectX;
        RectY = rectY;
        RectW = Math.Max(1f, rectW);
        RectH = Math.Max(1f, rectH);
    }

    /// <summary>
    /// 世界瓦片坐标 -> 小地图像素坐标
    /// </summary>
    public Vector2 WorldToMiniMap(Vector2 worldTile)
    {
        float ratioX = Math.Clamp(worldTile.X / MapWidth, 0f, 1f);
        float ratioY = Math.Clamp(worldTile.Y / MapHeight, 0f, 1f);

        return new Vector2(
            RectX + ratioX * RectW,
            RectY + ratioY * RectH
        );
    }

    /// <summary>
    /// 小地图屏幕点击像素 -> 世界瓦片坐标 (自动边界裁剪)
    /// </summary>
    public Vector2 MiniMapToWorld(Vector2 screenPos)
    {
        float localX = Math.Clamp(screenPos.X - RectX, 0f, RectW);
        float localY = Math.Clamp(screenPos.Y - RectY, 0f, RectH);

        float tileX = Math.Clamp((localX / RectW) * MapWidth, 0f, MapWidth - 1);
        float tileY = Math.Clamp((localY / RectH) * MapHeight, 0f, MapHeight - 1);

        return new Vector2(MathF.Round(tileX), MathF.Round(tileY));
    }

    /// <summary>
    /// 判定点击位置是否在小地图内部
    /// </summary>
    public bool IsInside(Vector2 screenPos)
    {
        return screenPos.X >= RectX && screenPos.X <= RectX + RectW &&
               screenPos.Y >= RectY && screenPos.Y <= RectY + RectH;
    }
}
