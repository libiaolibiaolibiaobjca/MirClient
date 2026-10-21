using System.Collections.Concurrent;
using Raylib_cs;
using MirClient.Protocol;
using MirClient.Protocol.Network;
using MirClient.Runner.Audio;
using MirClient.Runner.Graphics;
using MirClient.Runner.Scenes;

namespace MirClient.Runner;

/// <summary>
/// 客户端主游戏引擎：调度窗口、生命周期、多场景切换与后台网络封包线程通信
/// </summary>
public class GameEngine : IDisposable
{
    private readonly ClientConfig _config;
    public NetworkSession NetworkSession { get; } = new();
    private IScene _currentScene;
    private readonly LoginScene _loginScene;
    private readonly GameScene _gameScene;

    private readonly ConcurrentQueue<string> _networkEvents = new();
    private bool _isRunning = false;

    public bool IsConnected => NetworkSession.IsConnected;

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

        // 初始化原生硬件音频桥接设备
        AudioBridge.Initialize();

        // 若配置中指定全屏，则切入全屏模式
        if (_config.FullScreen && !Raylib.IsWindowFullscreen())
        {
            Raylib.ToggleFullscreen();
        }

        _isRunning = true;
        _currentScene.Init();

        // 2. 启动后台异步网络连接
        StartNetworkWorker();

        // 3. 游戏主渲染与逻辑循环
        while (!Raylib.WindowShouldClose() && _isRunning)
        {
            float dt = Raylib.GetFrameTime();

            // 响应全屏切换热键: Alt + Enter (Windows 官方标准)
            bool isAltPressed = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);
            if (isAltPressed && Raylib.IsKeyPressed(KeyboardKey.Enter))
            {
                ToggleFullscreen();
            }

            // 监听窗口缩放或全屏切换带来的视口分辨率变化
            if (Raylib.IsWindowResized())
            {
                _currentScene.OnResize(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            }

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

    /// <summary>
    /// 切换窗口化与全屏显示模式 (支持 Alt + Enter 快捷键触发)
    /// </summary>
    public void ToggleFullscreen()
    {
        Raylib.ToggleFullscreen();
        _config.FullScreen = Raylib.IsWindowFullscreen();
        int curW = Raylib.GetScreenWidth();
        int curH = Raylib.GetScreenHeight();
        _currentScene.OnResize(curW, curH);
    }

    public void SwitchToLoginScene()
    {
        _currentScene = _loginScene;
        _currentScene.Init();
    }

    /// <summary>
    /// 当在界面中修改 IP / 端口后重置网络状态
    /// </summary>
    public void ReconnectServer()
    {
        NetworkSession.Disconnect();
    }

    private void StartNetworkWorker()
    {
        // 网络由 NetworkSession 统一驱动
    }

    private void Shutdown()
    {
        _isRunning = false;
        NetworkSession.Dispose();
        TextureManager.UnloadAll();
        FontManager.Dispose();
        AudioBridge.Dispose();
        if (Raylib.IsAudioDeviceReady())
        {
            Raylib.CloseAudioDevice();
        }
        Raylib.CloseWindow();
    }

    public void Dispose()
    {
        Shutdown();
        _loginScene.Dispose();
        _gameScene.Dispose();
    }
}
