using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace MirClient.Runner.Graphics;

/// <summary>
/// 纹理资源管理器：负责将 RGBA 字节流与十周年 WIL 图像帧转换为 GPU 纹理显存，并管理生命周期。
/// </summary>
public static class TextureManager
{
    private static readonly Dictionary<string, Texture2D> _cache = new();

    /// <summary>
    /// 将 RGBA32 格式的原始字节流转换为 Raylib Texture2D
    /// </summary>
    public static Texture2D CreateTextureFromRgba(byte[] rgbaData, int width, int height)
    {
        if (rgbaData == null || rgbaData.Length < width * height * 4)
        {
            return CreateFallbackTexture(width, height, Color.Magenta);
        }

        unsafe
        {
            fixed (byte* ptr = rgbaData)
            {
                Image image = new()
                {
                    Data = (void*)ptr,
                    Width = width,
                    Height = height,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };
                return Raylib.LoadTextureFromImage(image);
            }
        }
    }

    /// <summary>
    /// 创建纯色或带边框的高保真复古砖石纹理
    /// </summary>
    public static Texture2D CreateProceduralTile(int width, int height, Color baseColor, Color borderColor)
    {
        string key = $"tile_{width}_{height}_{baseColor.R}_{baseColor.G}_{baseColor.B}";
        if (_cache.TryGetValue(key, out Texture2D cached)) return cached;

        Image img = Raylib.GenImageColor(width, height, baseColor);
        // 绘制复古像素边框
        for (int x = 0; x < width; x++)
        {
            Raylib.ImageDrawPixel(ref img, x, 0, borderColor);
            Raylib.ImageDrawPixel(ref img, x, height - 1, borderColor);
        }
        for (int y = 0; y < height; y++)
        {
            Raylib.ImageDrawPixel(ref img, 0, y, borderColor);
            Raylib.ImageDrawPixel(ref img, width - 1, y, borderColor);
        }

        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        _cache[key] = tex;
        return tex;
    }

    /// <summary>
    /// 生成降级占位纹理
    /// </summary>
    public static Texture2D CreateFallbackTexture(int width, int height, Color color)
    {
        Image img = Raylib.GenImageColor(width, height, color);
        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        return tex;
    }

    /// <summary>
    /// 释放全部缓存纹理
    /// </summary>
    public static void UnloadAll()
    {
        foreach (var tex in _cache.Values)
        {
            Raylib.UnloadTexture(tex);
        }
        _cache.Clear();
    }
}
