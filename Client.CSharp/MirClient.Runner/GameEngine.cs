using System.Collections.Concurrent;
using Raylib_cs;
using MirClient.Protocol;
using MirClient.Protocol.Network;
using MirClient.Runner.Graphics;
using MirClient.Runner.Scenes;

namespace MirClient.Runner;

/// <summary>
/// 客户端主游戏引擎：调度窗口、生命周期、多场景切换与后台网络封包线程通信
/// </summary>
public class GameEngine : IDisposable
{
    private readonly ClientConfig _config;
    private readonly MirConnection _connection = new();
    private IScene _currentScene;
    private readonly LoginScene _loginScene;
    private readonly GameScene _gameScene;

    private readonly ConcurrentQueue<string> _networkEvents = new();
    private bool _isRunning = false;

    public bool IsConnected => _connection.IsConnected;

    public GameEngine(ClientConfig config)
    {
        _config = config;
        _loginScene = new LoginScene(this, _config);
        _gameScene = new GameScene(this, _config);
        _currentScene = _loginScene;
    }

    public void Run()
    {
        // 1. 初始化 Raylib 窗口与渲染配置
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(_config.ScreenWidth, _config.ScreenHeight, $"热血传奇 - MirClient.CSharp ({_config.ServerName})");
        Raylib.SetTargetFPS(60);

        // 初始化中文字体系统 (消除豆腐块乱码)
        FontManager.Initialize();

        _isRunning = true;
        _currentScene.Init();

        // 2. 启动后台异步网络连接
        StartNetworkWorker();

        // 3. 游戏主渲染与逻辑循环
        while (!Raylib.WindowShouldClose() && _isRunning)
        {
            float dt = Raylib.GetFrameTime();

            // 处理后台网络事件
            while (_networkEvents.TryDequeue(out string? evt))
            {
                // 可推送至场景日志
            }

            // 更新与渲染当前场景
            _currentScene.Update(dt);

            Raylib.BeginDrawing();
            _currentScene.Render();
            Raylib.EndDrawing();
        }

        // 4. 清理与关闭
        Shutdown();
    }

    public void SwitchToGameScene()
    {
        _currentScene = _gameScene;
        _currentScene.Init();
    }

    public void SwitchToLoginScene()
    {
        _currentScene = _loginScene;
        _currentScene.Init();
    }

    /// <summary>
    /// 当在界面中修改 IP / 端口后，热重连服务器
    /// </summary>
    public void ReconnectServer()
    {
        try
        {
            _connection.Disconnect();
        }
        catch { }
        StartNetworkWorker();
    }

    private void StartNetworkWorker()
    {
        _connection.OnConnected += (s, e) =>
        {
            _networkEvents.Enqueue($"[网络] 成功连通服务端 {_config.ServerAddr}:{_config.ServerPort}");
        };

        _connection.OnDisconnected += (s, reason) =>
        {
            _networkEvents.Enqueue($"[网络] 连接断开: {reason}");
        };

        _connection.OnPacketReceived += (s, e) =>
        {
            _networkEvents.Enqueue($"[收到响应] Ident={e.Header.Ident}, Recog={e.Header.Recog}");
        };

        // 异步尝试连网
        _ = Task.Run(async () =>
        {
            while (_isRunning && !_connection.IsConnected)
            {
                try
                {
                    await _connection.ConnectAsync(_config.ServerAddr, _config.ServerPort, timeoutMs: 3000);
                    // 连接成功后发送探针握手
                    DefaultMessage probeMsg = new(
                        ident: ProtocolConsts.CM_QUERYCHR,
                        recog: 8888,
                        param: 100,
                        tag: 200,
                        series: 1
                    );
                    await _connection.SendPacketAsync(probeMsg, "guest/probe");
                    break;
                }
                catch
                {
                    // 失败后等待 3 秒自动重连
                    await Task.Delay(3000);
                }
            }
        });
    }

    private void Shutdown()
    {
        _isRunning = false;
        _connection.Dispose();
        TextureManager.UnloadAll();
        FontManager.Dispose();
        Raylib.CloseWindow();
    }

    public void Dispose()
    {
        Shutdown();
        _loginScene.Dispose();
        _gameScene.Dispose();
    }
}
