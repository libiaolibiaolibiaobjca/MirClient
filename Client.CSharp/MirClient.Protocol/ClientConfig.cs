using System.Text;

namespace MirClient.Protocol;

/// <summary>
/// 客户端本地配置 (严格对应 TMirStartupInfo 核心连接与显示字段，零冗余)
/// </summary>
public class ClientConfig
{
    public string ServerAddr { get; set; } = "192.168.0.103";
    public int ServerPort { get; set; } = 7000;
    public string ServerName { get; set; } = "热血传奇";
    public string ClientPath { get; set; } = "";
    public int ScreenWidth { get; set; } = 1024;
    public int ScreenHeight { get; set; } = 768;
    public bool FullScreen { get; set; } = false;

    /// <summary>
    /// 加载或自动创建同目录下的 config.ini
    /// </summary>
    public static ClientConfig LoadOrCreate(string filePath = "config.ini")
    {
        ClientConfig config = new();

        if (!File.Exists(filePath))
        {
            config.Save(filePath);
            return config;
        }

        try
        {
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#') || line.StartsWith('['))
                    continue;

                int eqIdx = line.IndexOf('=');
                if (eqIdx <= 0) continue;

                string key = line[..eqIdx].Trim();
                string val = line[(eqIdx + 1)..].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "serveraddr":
                    case "ip":
                        config.ServerAddr = val;
                        break;
                    case "serverport":
                    case "port":
                        if (int.TryParse(val, out int port)) config.ServerPort = port;
                        break;
                    case "servername":
                    case "name":
                        config.ServerName = val;
                        break;
                    case "clientpath":
                    case "clientdir":
                    case "path":
                        config.ClientPath = val;
                        break;
                    case "screenwidth":
                    case "width":
                        if (int.TryParse(val, out int w)) config.ScreenWidth = w;
                        break;
                    case "screenheight":
                    case "height":
                        if (int.TryParse(val, out int h)) config.ScreenHeight = h;
                        break;
                    case "fullscreen":
                        if (bool.TryParse(val, out bool fs)) config.FullScreen = fs;
                        break;
                }
            }
        }
        catch
        {
            // 读取异常时使用默认值
        }

        return config;
    }

    /// <summary>
    /// 保存配置文件
    /// </summary>
    public void Save(string filePath = "config.ini")
    {
        StringBuilder sb = new();
        sb.AppendLine("; MirClient.CSharp 客户端配置文件");
        sb.AppendLine("[Server]");
        sb.AppendLine($"ServerAddr={ServerAddr}");
        sb.AppendLine($"ServerPort={ServerPort}");
        sb.AppendLine($"ServerName={ServerName}");
        sb.AppendLine();
        sb.AppendLine("[Graphics]");
        sb.AppendLine($"ScreenWidth={ScreenWidth}");
        sb.AppendLine($"ScreenHeight={ScreenHeight}");
        sb.AppendLine($"FullScreen={FullScreen.ToString().ToLowerInvariant()}");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}
