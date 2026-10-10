using System.Collections.Concurrent;
using Raylib_cs;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using MirClient.Runner.Platform;

namespace MirClient.Runner.Graphics;

/// <summary>
/// 盛大十周年客户端原生核心资产管理器：
/// 1. 严格践行“没有就不对”原则：启动强校验，缺失则引导原生鼠标选择，决不伪造假画面；
/// 2. 1:1 映射原版 WIL/WZL 资产库，提供 GPU 显存级纹理按需加载与缓存。
/// </summary>
public static class AssetManager
{
    public static bool IsReady { get; private set; } = false;
    public static string ClientRootPath { get; private set; } = "";
    public static string MissingReason { get; private set; } = "";

    // 原版核心图库
    public static WilLoader ChrSelLib { get; } = new();
    public static WilLoader PrguseLib { get; } = new();
    public static WilLoader Prguse3Lib { get; } = new();
    public static WilLoader TilesLib { get; } = new();
    public static WilLoader SmTilesLib { get; } = new();
    public static WilLoader ObjectsLib { get; } = new();
    public static WilLoader HumLib { get; } = new();    // 角色身体与服装原版图库 (Data/Hum)
    public static WilLoader HairLib { get; } = new();   // 角色发型原版图库 (Data/Hair)
    public static WilLoader WeaponLib { get; } = new(); // 角色武器原版图库 (Data/Weapon)
    public static WilLoader NpcLib { get; } = new();    // NPC原版图库 (Data/Npc)
    public static WilLoader Mon1Lib { get; } = new();   // 经典怪物原版图库 (Data/Mon1 或 Data/Mon)

    // GPU 纹理显存缓存池 (键为图库标识+帧序号)
    private static readonly ConcurrentDictionary<string, Texture2D> _textureCache = new();

    /// <summary>
    /// 初始化并校验盛大十周年客户端核心资产
    /// </summary>
    public static bool Initialize(ClientConfig config, bool allowPromptDialog = true)
    {
        ClearTextures();
        IsReady = false;
        MissingReason = "";

        // 1. 确定客户端目录 (优先 config.ini, 其次当前运行目录, 再次常见盘符)
        string? resolvedDir = ResolveClientDirectory(config.ClientPath);

        // 2. 如果未找到有效目录且允许弹出对话框，则唤起原生鼠标文件夹选择窗口
        if (resolvedDir == null && allowPromptDialog)
        {
            Console.WriteLine("\n[资产提示] 未在默认位置检测到盛大十周年客户端，准备弹出原生文件夹选择器...");
            string? picked = NativeFileDialog.SelectFolder("请用鼠标选择盛大热血传奇十周年客户端根目录 (包含 Data 文件夹)");
            if (!string.IsNullOrWhiteSpace(picked))
            {
                resolvedDir = ResolveClientDirectory(picked);
                if (resolvedDir != null)
                {
                    // 校验成功，自动将路径写回 config.ini，下次启动无需再选！
                    config.ClientPath = resolvedDir;
                    config.Save();
                    Console.WriteLine($"[配置更新] 已将十周年客户端路径自动持久化至 config.ini: {resolvedDir}");
                }
            }
        }

        if (resolvedDir == null)
        {
            MissingReason = "未找到盛大热血传奇十周年客户端根目录！\n“没有就不对” —— 本客户端拒绝任何假素材糊弄。\n请点击界面上的【选择十周年客户端目录】按钮，或在 config.ini 中配置 ClientPath。";
            Console.WriteLine($"\n[资产致命错误] {MissingReason}");
            return false;
        }

        ClientRootPath = resolvedDir;
        string dataDir = Path.Combine(resolvedDir, "Data");
        string mapDir = Path.Combine(resolvedDir, "Map");

        // 3. 强断言检查关键文件 (没有就不对)
        List<string> missingFiles = new();
        if (!ChrSelLib.TryLoadAuto(Path.Combine(dataDir, "ChrSel"))) missingFiles.Add("Data/ChrSel (登录石门与火把)");
        if (!PrguseLib.TryLoadAuto(Path.Combine(dataDir, "Prguse"))) missingFiles.Add("Data/Prguse (界面底栏与血魔球)");
        if (!Prguse3Lib.TryLoadAuto(Path.Combine(dataDir, "Prguse3"))) missingFiles.Add("Data/Prguse3 (经典控制台底板)");
        if (!TilesLib.TryLoadAuto(Path.Combine(dataDir, "Tiles"))) missingFiles.Add("Data/Tiles (地图主地貌瓦片)");
        if (!HumLib.TryLoadAuto(Path.Combine(dataDir, "Hum"))) missingFiles.Add("Data/Hum (角色身体与服装)");
        SmTilesLib.TryLoadAuto(Path.Combine(dataDir, "SmTiles"));
        ObjectsLib.TryLoadAuto(Path.Combine(dataDir, "Objects"));
        HairLib.TryLoadAuto(Path.Combine(dataDir, "Hair"));
        WeaponLib.TryLoadAuto(Path.Combine(dataDir, "Weapon"));
        NpcLib.TryLoadAuto(Path.Combine(dataDir, "Npc"));
        if (!Mon1Lib.TryLoadAuto(Path.Combine(dataDir, "Mon1")))
        {
            Mon1Lib.TryLoadAuto(Path.Combine(dataDir, "Mon"));
        }

        string map0 = Path.Combine(mapDir, "0.map");
        if (!File.Exists(map0)) missingFiles.Add("Map/0.map (比奇城地图)");

        if (missingFiles.Count > 0)
        {
            MissingReason = $"十周年客户端缺少以下关键核心资产:\n- " + string.Join("\n- ", missingFiles);
            Console.WriteLine($"\n[资产校验失败] {MissingReason}");
            return false;
        }

        Console.WriteLine($"[资产装配成功] 已 100% 挂载盛大十周年原生资产库: {resolvedDir}");
        Console.WriteLine($"  ChrSel: {ChrSelLib.ImageCount}, Prguse: {PrguseLib.ImageCount}, Prguse3: {Prguse3Lib.ImageCount}");
        Console.WriteLine($"  Hum: {HumLib.ImageCount}, Hair: {HairLib.ImageCount}, Weapon: {WeaponLib.ImageCount}");
        Console.WriteLine($"  Npc: {NpcLib.ImageCount}, Mon1: {Mon1Lib.ImageCount}");
        IsReady = true;
        return true;
    }

    /// <summary>
    /// 寻找包含有效 Data 和 Map 的盛大十周年客户端根目录
    /// </summary>
    private static string? ResolveClientDirectory(string? candidate)
    {
        List<string> tryDirs = new();
        if (!string.IsNullOrWhiteSpace(candidate)) tryDirs.Add(candidate.Trim());

        string appBase = AppDomain.CurrentDomain.BaseDirectory;
        tryDirs.Add(appBase);
        tryDirs.Add(Path.Combine(appBase, ".."));
        tryDirs.Add(@"D:\盛大网络\热血传奇");
        tryDirs.Add(@"D:\热血传奇");
        tryDirs.Add(@"D:\Mir2");
        tryDirs.Add(@"C:\盛大网络\热血传奇");
        tryDirs.Add(@"E:\Mir2");
        tryDirs.Add(@"E:\盛大网络\热血传奇");

        foreach (var dir in tryDirs)
        {
            if (Directory.Exists(dir))
            {
                string data = Path.Combine(dir, "Data");
                string map = Path.Combine(dir, "Map");
                if (Directory.Exists(data) || Directory.Exists(Path.Combine(dir, "data")))
                {
                    return Path.GetFullPath(dir);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 获取指定图库的 GPU 纹理 (自动上传显存并按需缓存)
    /// </summary>
    public static Texture2D? GetTexture(WilLoader loader, string libKey, int index)
    {
        if (index < 0 || !IsReady) return null;
        string key = $"{libKey}_{index}";

        if (_textureCache.TryGetValue(key, out Texture2D cached))
        {
            return cached;
        }

        WilImage? wilImg = loader.GetImage(index);
        if (wilImg == null || wilImg.Width == 0 || wilImg.Height == 0) return null;

        unsafe
        {
            fixed (uint* ptr = wilImg.RgbaPixels)
            {
                Image img = new()
                {
                    Data = (void*)ptr,
                    Width = wilImg.Width,
                    Height = wilImg.Height,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };
                Texture2D texture = Raylib.LoadTextureFromImage(img);
                Raylib.SetTextureFilter(texture, TextureFilter.Point);
                _textureCache[key] = texture;
                return texture;
            }
        }
    }

    /// <summary>
    /// 获取带原生锚点偏移 (Px, Py) 的帧纹理
    /// </summary>
    public static (Texture2D? Texture, short Px, short Py) GetFrameWithOffset(WilLoader loader, string libKey, int index)
    {
        WilImage? wilImg = loader.GetImage(index);
        if (wilImg == null) return (null, 0, 0);

        Texture2D? tex = GetTexture(loader, libKey, index);
        return (tex, wilImg.Px, wilImg.Py);
    }

    /// <summary>
    /// 释放全部显存纹理
    /// </summary>
    public static void ClearTextures()
    {
        foreach (var kvp in _textureCache)
        {
            Raylib.UnloadTexture(kvp.Value);
        }
        _textureCache.Clear();
    }
}
