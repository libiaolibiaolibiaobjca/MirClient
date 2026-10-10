using System.Numerics;
using Raylib_cs;
using MirClient.Protocol;
using MirClient.Protocol.Network;
using MirClient.Runner.Graphics;
using MirClient.Runner.Platform;

namespace MirClient.Runner.Scenes;

/// <summary>
/// 盛大十周年原版登录场景：
/// 1. 100% 贴图 ChrSel.wil/wzl Index 22 (古朴石门与火把全景)；
/// 2. 账号密码输入完成后，播放 Index 103 ~ 122 原版推开石门动画；
/// 3. 支持在页面直接维护服务器 IP/Port 与十周年客户端目录，并自动双向持久化同步 config.ini；
/// 4. 严苛 Fail-Fast：未找到十周年资源时严禁假画面糊弄，支持非阻塞 STA 线程原生鼠标选择。
/// </summary>
public class LoginScene : IScene
{
    private readonly GameEngine _engine;
    private readonly ClientConfig _config;

    // 登录输入状态
    private string _username = "mir2_hero";
    private string _password = "123456";
    private int _activeField = 0; // 0: 账号, 1: 密码, 2: 设置-IP, 3: 设置-端口, 4: 设置-客户端路径
    private float _cursorTimer = 0f;
    private bool _cursorVisible = true;
    private float _backspaceTimer = 0f;

    // 推开石门动画状态 (对应原版 IntroScn.pas: m_nCurFrame 103..122)
    private bool _isDoorOpening = false;
    private int _doorFrame = 103;
    private const int DOOR_START_FRAME = 103;
    private const int DOOR_END_FRAME = 122;
    private float _doorTimer = 0f;

    // 服务器与目录设置弹窗状态
    private bool _showConfigModal = false;
    private string _editServerIp = "192.168.0.103";
    private string _editServerPort = "7000";
    private string _editClientPath = "";

    // 异步文件夹选择状态 (防止主窗口未响应卡死)
    private volatile bool _isBrowsingFolder = false;
    private string? _pendingPickedFolder = null;

    public LoginScene(GameEngine engine, ClientConfig config)
    {
        _engine = engine;
        _config = config;
        SyncConfigToInputs();
    }

    private void SyncConfigToInputs()
    {
        _editServerIp = string.IsNullOrWhiteSpace(_config.ServerAddr) ? "192.168.0.103" : _config.ServerAddr;
        _editServerPort = _config.ServerPort <= 0 ? "7000" : _config.ServerPort.ToString();
        _editClientPath = _config.ClientPath ?? "";
    }

    public void Init()
    {
        _isDoorOpening = false;
        _doorFrame = DOOR_START_FRAME;
        _doorTimer = 0f;
        _showConfigModal = false;
        _isBrowsingFolder = false;
        _pendingPickedFolder = null;

        SyncConfigToInputs();

        // 尝试初次校验十周年资产
        AssetManager.Initialize(_config, allowPromptDialog: false);
    }

    public void Update(float dt)
    {
        // 1. 检查后台 STA 线程传回的选中文件夹路径
        if (_pendingPickedFolder != null)
        {
            string picked = _pendingPickedFolder;
            _pendingPickedFolder = null;
            _isBrowsingFolder = false;

            if (!string.IsNullOrWhiteSpace(picked))
            {
                _editClientPath = picked;
                _config.ClientPath = picked;
                bool ok = AssetManager.Initialize(_config, allowPromptDialog: false);
                if (ok)
                {
                    _config.Save(); // 自动持久化！
                    Console.WriteLine($"[保存配置] 十周年客户端目录已自动写入 config.ini: {picked}");
                    _showConfigModal = false;
                }
            }
        }

        // 2. 光标闪烁
        _cursorTimer += dt;
        if (_cursorTimer >= 0.5f)
        {
            _cursorVisible = !_cursorVisible;
            _cursorTimer = 0f;
        }

        // 3. 如果正在播放推开石门动画 (原版每帧约 40ms)
        if (_isDoorOpening)
        {
            _doorTimer += dt;
            if (_doorTimer >= 0.045f)
            {
                _doorTimer = 0f;
                _doorFrame++;
                if (_doorFrame > DOOR_END_FRAME)
                {
                    _isDoorOpening = false;
                    _engine.SwitchToGameScene();
                    return;
                }
            }
            return;
        }

        // 4. 处理用户交互输入
        HandleInput(dt);
    }

    private void HandleInput(float dt)
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // 弹窗处于激活状态时，优先处理设置弹窗
        if (_showConfigModal)
        {
            HandleConfigModalInput(mousePos, dt);
            return;
        }

        // 若十周年素材未就绪，只处理资源选择与配置按钮
        if (!AssetManager.IsReady)
        {
            HandleMissingAssetsInput(mousePos, screenW, screenH);
            return;
        }

        // 正常登录界面输入处理
        int inputW = 240;
        int inputH = 34;
        int inputX = (screenW - inputW) / 2 + 10;
        int accY = screenH / 2 + 30;
        int pwdY = accY + 44;

        Rectangle accRect = new(inputX, accY, inputW, inputH);
        Rectangle pwdRect = new(inputX, pwdY, inputW, inputH);
        Rectangle btnEnter = new((screenW - 180) / 2, pwdY + 54, 180, 42);
        Rectangle btnConfig = new(screenW - 130, 16, 114, 30);

        bool hoverAcc = Raylib.CheckCollisionPointRec(mousePos, accRect);
        bool hoverPwd = Raylib.CheckCollisionPointRec(mousePos, pwdRect);
        bool hoverEnter = Raylib.CheckCollisionPointRec(mousePos, btnEnter);
        bool hoverConfig = Raylib.CheckCollisionPointRec(mousePos, btnConfig);

        if (hoverAcc || hoverPwd)
            Raylib.SetMouseCursor(MouseCursor.IBeam);
        else if (hoverEnter || hoverConfig)
            Raylib.SetMouseCursor(MouseCursor.PointingHand);
        else
            Raylib.SetMouseCursor(MouseCursor.Default);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (hoverAcc) { _activeField = 0; _cursorVisible = true; }
            else if (hoverPwd) { _activeField = 1; _cursorVisible = true; }
            else if (hoverEnter) { StartDoorOpening(); }
            else if (hoverConfig) { SyncConfigToInputs(); _showConfigModal = true; }
        }

        // 字符输入
        int ch = Raylib.GetCharPressed();
        while (ch > 0)
        {
            if (ch >= 32 && ch <= 126)
            {
                if (_activeField == 0 && _username.Length < 16) _username += (char)ch;
                else if (_activeField == 1 && _password.Length < 16) _password += (char)ch;
            }
            ch = Raylib.GetCharPressed();
        }

        // 退格键
        HandleBackspace(dt, () => _activeField == 0 ? _username : _password, s => { if (_activeField == 0) _username = s; else _password = s; });

        // Tab 切换
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
            _activeField = (_activeField + 1) % 2;

        // 回车直接开启石门进入
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
            StartDoorOpening();
    }

    private void StartDoorOpening()
    {
        _isDoorOpening = true;
        _doorFrame = DOOR_START_FRAME;
        _doorTimer = 0f;
    }

    private void HandleBackspace(float dt, Func<string> getter, Action<string> setter)
    {
        bool down = Raylib.IsKeyDown(KeyboardKey.Backspace);
        bool pressed = Raylib.IsKeyPressed(KeyboardKey.Backspace);

        if (pressed || (down && _backspaceTimer > 0.45f))
        {
            string s = getter();
            if (s.Length > 0)
            {
                setter(s[..^1]);
                _cursorVisible = true;
                _cursorTimer = 0f;
            }
            if (down && _backspaceTimer > 0.45f) _backspaceTimer = 0.4f;
        }

        if (down) _backspaceTimer += dt;
        else _backspaceTimer = 0f;
    }

    private void HandleMissingAssetsInput(Vector2 mousePos, int screenW, int screenH)
    {
        Rectangle btnPickFolder = new((screenW - 320) / 2, screenH / 2 + 30, 320, 46);
        Rectangle btnServerSet = new((screenW - 320) / 2, screenH / 2 + 90, 320, 46);

        bool hoverPick = Raylib.CheckCollisionPointRec(mousePos, btnPickFolder);
        bool hoverSet = Raylib.CheckCollisionPointRec(mousePos, btnServerSet);

        if (hoverPick || hoverSet) Raylib.SetMouseCursor(MouseCursor.PointingHand);
        else Raylib.SetMouseCursor(MouseCursor.Default);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !_isBrowsingFolder)
        {
            if (hoverPick)
            {
                // 异步 STA 线程唤起，防止卡死
                _isBrowsingFolder = true;
                NativeFileDialog.SelectFolderAsync("请用鼠标选择盛大热血传奇十周年客户端根目录 (包含 Data 和 Map 文件夹)", path =>
                {
                    _pendingPickedFolder = path ?? "";
                });
            }
            else if (hoverSet)
            {
                SyncConfigToInputs();
                _showConfigModal = true;
            }
        }
    }

    private void HandleConfigModalInput(Vector2 mousePos, float dt)
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        int modalW = 520;
        int modalH = 340;
        int modalX = (screenW - modalW) / 2;
        int modalY = (screenH - modalH) / 2;

        Rectangle ipRect = new(modalX + 130, modalY + 76, 350, 34);
        Rectangle portRect = new(modalX + 130, modalY + 124, 350, 34);
        Rectangle pathRect = new(modalX + 130, modalY + 172, 260, 34);
        Rectangle btnBrowse = new(modalX + 400, modalY + 172, 80, 34);

        Rectangle btnSave = new(modalX + 100, modalY + 254, 140, 40);
        Rectangle btnCancel = new(modalX + 280, modalY + 254, 140, 40);

        bool hoverIp = Raylib.CheckCollisionPointRec(mousePos, ipRect);
        bool hoverPort = Raylib.CheckCollisionPointRec(mousePos, portRect);
        bool hoverPath = Raylib.CheckCollisionPointRec(mousePos, pathRect);
        bool hoverBrowse = Raylib.CheckCollisionPointRec(mousePos, btnBrowse);
        bool hoverSave = Raylib.CheckCollisionPointRec(mousePos, btnSave);
        bool hoverCancel = Raylib.CheckCollisionPointRec(mousePos, btnCancel);

        if (hoverIp || hoverPort || hoverPath) Raylib.SetMouseCursor(MouseCursor.IBeam);
        else if (hoverBrowse || hoverSave || hoverCancel) Raylib.SetMouseCursor(MouseCursor.PointingHand);
        else Raylib.SetMouseCursor(MouseCursor.Default);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !_isBrowsingFolder)
        {
            if (hoverIp) { _activeField = 2; _cursorVisible = true; }
            else if (hoverPort) { _activeField = 3; _cursorVisible = true; }
            else if (hoverPath) { _activeField = 4; _cursorVisible = true; }
            else if (hoverBrowse)
            {
                // 异步专用 STA 线程弹出文件夹选择器
                _isBrowsingFolder = true;
                NativeFileDialog.SelectFolderAsync("请用鼠标选择盛大热血传奇十周年客户端根目录", path =>
                {
                    _pendingPickedFolder = path ?? "";
                });
            }
            else if (hoverSave)
            {
                // 保存并持久化到 config.ini，并热重连
                _config.ServerAddr = string.IsNullOrWhiteSpace(_editServerIp) ? "192.168.0.103" : _editServerIp.Trim();
                if (int.TryParse(_editServerPort.Trim(), out int p) && p > 0) _config.ServerPort = p;
                if (!string.IsNullOrWhiteSpace(_editClientPath))
                {
                    _config.ClientPath = _editClientPath.Trim();
                    AssetManager.Initialize(_config, allowPromptDialog: false);
                }
                _config.Save();
                _engine.ReconnectServer();
                _showConfigModal = false;
                Console.WriteLine($"[配置热同步] 已保存新服务配置 {_config.ServerAddr}:{_config.ServerPort} (路径: {_config.ClientPath}) 至 config.ini");
            }
            else if (hoverCancel)
            {
                _showConfigModal = false;
            }
        }

        // 字符输入
        int ch = Raylib.GetCharPressed();
        while (ch > 0)
        {
            if (_activeField == 2 && _editServerIp.Length < 32)
            {
                if ((ch >= '0' && ch <= '9') || ch == '.' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || ch == '-')
                    _editServerIp += (char)ch;
            }
            else if (_activeField == 3 && _editServerPort.Length < 6)
            {
                if (ch >= '0' && ch <= '9') _editServerPort += (char)ch;
            }
            else if (_activeField == 4 && _editClientPath.Length < 128)
            {
                if (ch >= 32) _editClientPath += (char)ch;
            }
            ch = Raylib.GetCharPressed();
        }

        // 退格键
        if (_activeField == 2) HandleBackspace(dt, () => _editServerIp, s => _editServerIp = s);
        else if (_activeField == 3) HandleBackspace(dt, () => _editServerPort, s => _editServerPort = s);
        else if (_activeField == 4) HandleBackspace(dt, () => _editClientPath, s => _editClientPath = s);

        // Tab 键切换输入焦点
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _activeField = _activeField switch
            {
                2 => 3,
                3 => 4,
                4 => 2,
                _ => 2
            };
            _cursorVisible = true;
        }
    }

    public void Render()
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        Raylib.ClearBackground(Color.Black);

        // 1. 若十周年素材未就绪，渲染醒目的“没有就不对”严正提示面板
        if (!AssetManager.IsReady)
        {
            RenderMissingAssetsView(screenW, screenH);
            if (_showConfigModal) RenderConfigModal(screenW, screenH);
            return;
        }

        // 2. 素材就绪：100% 贴图盛大十周年原版 ChrSel 资源
        if (_isDoorOpening)
        {
            // 播放石门推开动画 (原版 Index 103..122)
            Texture2D? doorTex = AssetManager.GetTexture(AssetManager.ChrSelLib, "ChrSel", _doorFrame);
            if (doorTex.HasValue)
            {
                int dx = (screenW - doorTex.Value.Width) / 2;
                int dy = (screenH - doorTex.Value.Height) / 2;
                Raylib.DrawTexture(doorTex.Value, dx, dy, Color.White);
            }
        }
        else
        {
            // 贴图经典石门与火把背景 (原版 ChrSel.wil/wzl Index 22)
            Texture2D? bgTex = AssetManager.GetTexture(AssetManager.ChrSelLib, "ChrSel", 22);
            if (bgTex.HasValue)
            {
                int bx = (screenW - bgTex.Value.Width) / 2;
                int by = (screenH - bgTex.Value.Height) / 2;
                Raylib.DrawTexture(bgTex.Value, bx, by, Color.White);
            }

            RenderLoginControls(screenW, screenH);
        }

        RenderTopBar(screenW);

        if (_showConfigModal) RenderConfigModal(screenW, screenH);
    }

    private void RenderLoginControls(int screenW, int screenH)
    {
        int inputW = 240;
        int inputH = 34;
        int inputX = (screenW - inputW) / 2 + 10;
        int accY = screenH / 2 + 30;
        int pwdY = accY + 44;

        // 账号框
        FontManager.DrawTextWithShadow("账 号:", inputX - 58, accY + 7, 18, Color.Gold, Color.Black);
        Raylib.DrawRectangle(inputX, accY, inputW, inputH, new Color(15, 15, 15, 240));
        Raylib.DrawRectangleLinesEx(new Rectangle(inputX, accY, inputW, inputH), _activeField == 0 ? 2 : 1, _activeField == 0 ? Color.Gold : Color.DarkGray);
        FontManager.DrawText(_username, inputX + 8, accY + 7, 18, Color.White);
        if (_activeField == 0 && _cursorVisible)
        {
            int curX = inputX + 8 + FontManager.MeasureText(_username, 18);
            Raylib.DrawRectangle(curX + 2, accY + 6, 2, 22, Color.Gold);
        }

        // 密码框
        FontManager.DrawTextWithShadow("密 码:", inputX - 58, pwdY + 7, 18, Color.Gold, Color.Black);
        Raylib.DrawRectangle(inputX, pwdY, inputW, inputH, new Color(15, 15, 15, 240));
        Raylib.DrawRectangleLinesEx(new Rectangle(inputX, pwdY, inputW, inputH), _activeField == 1 ? 2 : 1, _activeField == 1 ? Color.Gold : Color.DarkGray);
        string masked = new('*', _password.Length);
        FontManager.DrawText(masked, inputX + 8, pwdY + 7, 18, Color.White);
        if (_activeField == 1 && _cursorVisible)
        {
            int curX = inputX + 8 + FontManager.MeasureText(masked, 18);
            Raylib.DrawRectangle(curX + 2, pwdY + 6, 2, 22, Color.Gold);
        }

        // 进入玛法大陆按钮
        int btnW = 180;
        int btnH = 42;
        int btnX = (screenW - btnW) / 2;
        int btnY = pwdY + 54;
        Rectangle btnRect = new(btnX, btnY, btnW, btnH);
        bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnRect);

        Raylib.DrawRectangleRec(btnRect, hover ? new Color(130, 95, 35, 255) : new Color(55, 42, 30, 255));
        Raylib.DrawRectangleLinesEx(btnRect, 2, hover ? Color.Yellow : Color.Gold);
        string enterText = "进入玛法大陆";
        int tW = FontManager.MeasureText(enterText, 18);
        FontManager.DrawTextWithShadow(enterText, btnX + (btnW - tW) / 2, btnY + 11, 18, Color.White, Color.Black);
    }

    private void RenderTopBar(int screenW)
    {
        bool online = _engine.IsConnected;
        Raylib.DrawCircle(20, 24, 6, online ? Color.Lime : Color.Red);
        string netText = online ? $"网络在线: {_config.ServerAddr}:{_config.ServerPort} (通畅)" : $"正在连接: {_config.ServerAddr}:{_config.ServerPort}...";
        FontManager.DrawTextWithShadow(netText, 34, 15, 16, online ? Color.Lime : Color.LightGray, Color.Black);

        // 右上角【服务器设置】按钮
        Rectangle btnCfg = new(screenW - 130, 12, 114, 28);
        bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnCfg);
        Raylib.DrawRectangleRec(btnCfg, hover ? new Color(90, 65, 35, 230) : new Color(35, 30, 25, 210));
        Raylib.DrawRectangleLinesEx(btnCfg, 1, hover ? Color.Yellow : Color.Gold);
        FontManager.DrawTextWithShadow("⚙ 服务器设置", screenW - 124, 17, 14, Color.Gold, Color.Black);
    }

    private void RenderMissingAssetsView(int screenW, int screenH)
    {
        Raylib.ClearBackground(new Color(15, 12, 16, 255));

        string title = "【盛大热血传奇十周年原生客户端素材未就绪】";
        int titleW = FontManager.MeasureText(title, 26);
        FontManager.DrawTextWithShadow(title, (screenW - titleW) / 2, screenH / 2 - 130, 26, Color.Red, Color.Black);

        string reason = "“没有就不对” —— 本客户端拒绝任何几何假图形糊弄，必须依托原汁原味的官方资产运行。\n请用鼠标选择您电脑上盛大十周年客户端的根目录 (程序会自动写入 config.ini，下次无需再选)。";
        FontManager.DrawText(reason, (screenW - 680) / 2, screenH / 2 - 70, 16, Color.LightGray);

        if (_isBrowsingFolder)
        {
            string waiting = "【正在打开系统选择窗口，请在弹出的系统对话框中点选十周年客户端目录...】";
            int ww = FontManager.MeasureText(waiting, 18);
            FontManager.DrawTextWithShadow(waiting, (screenW - ww) / 2, screenH / 2 + 30, 18, Color.Yellow, Color.Black);
        }
        else
        {
            Rectangle btnPick = new((screenW - 340) / 2, screenH / 2 + 25, 340, 48);
            bool hoverPick = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnPick);
            Raylib.DrawRectangleRec(btnPick, hoverPick ? new Color(140, 95, 35, 255) : new Color(65, 48, 28, 255));
            Raylib.DrawRectangleLinesEx(btnPick, 2, hoverPick ? Color.Yellow : Color.Gold);
            string pickTxt = "📁 鼠标选择十周年客户端目录";
            int ptw = FontManager.MeasureText(pickTxt, 18);
            FontManager.DrawTextWithShadow(pickTxt, (int)btnPick.X + (340 - ptw) / 2, (int)btnPick.Y + 14, 18, Color.Gold, Color.Black);

            Rectangle btnSet = new((screenW - 340) / 2, screenH / 2 + 90, 340, 44);
            bool hoverSet = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnSet);
            Raylib.DrawRectangleRec(btnSet, hoverSet ? new Color(75, 65, 55, 255) : new Color(40, 34, 28, 255));
            Raylib.DrawRectangleLinesEx(btnSet, 1, hoverSet ? Color.White : Color.Gray);
            string setTxt = "⚙ 服务器地址与端口配置";
            int stw = FontManager.MeasureText(setTxt, 16);
            FontManager.DrawText(setTxt, (int)btnSet.X + (340 - stw) / 2, (int)btnSet.Y + 13, 16, Color.White);
        }
    }

    private void RenderConfigModal(int screenW, int screenH)
    {
        // 遮罩
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(0, 0, 0, 200));

        int modalW = 520;
        int modalH = 340;
        int modalX = (screenW - modalW) / 2;
        int modalY = (screenH - modalH) / 2;

        // 弹窗底板
        Rectangle mRect = new(modalX, modalY, modalW, modalH);
        Raylib.DrawRectangleRec(mRect, new Color(20, 18, 16, 255));
        Raylib.DrawRectangleLinesEx(mRect, 2, Color.Gold);

        FontManager.DrawTextWithShadow("服务器配置与客户端路径 (自动同步 config.ini)", modalX + 35, modalY + 22, 18, Color.Gold, Color.Black);

        // 1. IP 输入框 (默认值清晰展现，纯黑底白字高对比度)
        FontManager.DrawTextWithShadow("服务 IP:", modalX + 35, modalY + 82, 18, Color.White, Color.Black);
        Rectangle ipRect = new(modalX + 130, modalY + 76, 350, 34);
        Raylib.DrawRectangleRec(ipRect, new Color(10, 10, 10, 255));
        Raylib.DrawRectangleLinesEx(ipRect, _activeField == 2 ? 2 : 1, _activeField == 2 ? Color.Gold : new Color(80, 80, 80, 255));
        FontManager.DrawText(_editServerIp, (int)ipRect.X + 10, (int)ipRect.Y + 8, 18, Color.White);
        if (_activeField == 2 && _cursorVisible)
            Raylib.DrawRectangle((int)ipRect.X + 10 + FontManager.MeasureText(_editServerIp, 18) + 2, (int)ipRect.Y + 6, 2, 22, Color.Gold);

        // 2. Port 输入框
        FontManager.DrawTextWithShadow("网关端口:", modalX + 35, modalY + 130, 18, Color.White, Color.Black);
        Rectangle portRect = new(modalX + 130, modalY + 124, 350, 34);
        Raylib.DrawRectangleRec(portRect, new Color(10, 10, 10, 255));
        Raylib.DrawRectangleLinesEx(portRect, _activeField == 3 ? 2 : 1, _activeField == 3 ? Color.Gold : new Color(80, 80, 80, 255));
        FontManager.DrawText(_editServerPort, (int)portRect.X + 10, (int)portRect.Y + 8, 18, Color.White);
        if (_activeField == 3 && _cursorVisible)
            Raylib.DrawRectangle((int)portRect.X + 10 + FontManager.MeasureText(_editServerPort, 18) + 2, (int)portRect.Y + 6, 2, 22, Color.Gold);

        // 3. 客户端路径输入框 + 【浏览】按钮 (支持直接键盘打字输入，也支持鼠标点选)
        FontManager.DrawTextWithShadow("十周年目录:", modalX + 25, modalY + 178, 18, Color.White, Color.Black);
        Rectangle pathRect = new(modalX + 130, modalY + 172, 260, 34);
        Rectangle btnBrowse = new(modalX + 400, modalY + 172, 80, 34);
        bool hoverBrowse = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnBrowse);

        Raylib.DrawRectangleRec(pathRect, new Color(10, 10, 10, 255));
        Raylib.DrawRectangleLinesEx(pathRect, _activeField == 4 ? 2 : 1, _activeField == 4 ? Color.Gold : new Color(80, 80, 80, 255));

        if (string.IsNullOrEmpty(_editClientPath))
            FontManager.DrawText("例如: D:\\盛大热血传奇", (int)pathRect.X + 8, (int)pathRect.Y + 8, 16, Color.Gray);
        else
            FontManager.DrawText(_editClientPath, (int)pathRect.X + 8, (int)pathRect.Y + 8, 16, Color.White);

        if (_activeField == 4 && _cursorVisible)
            Raylib.DrawRectangle((int)pathRect.X + 8 + FontManager.MeasureText(_editClientPath, 16) + 2, (int)pathRect.Y + 6, 2, 22, Color.Gold);

        // 浏览按钮
        Raylib.DrawRectangleRec(btnBrowse, hoverBrowse ? new Color(90, 70, 40, 255) : new Color(45, 38, 30, 255));
        Raylib.DrawRectangleLinesEx(btnBrowse, 1, hoverBrowse ? Color.Yellow : Color.Gold);
        FontManager.DrawText("浏览...", (int)btnBrowse.X + 16, (int)btnBrowse.Y + 8, 16, Color.Gold);

        if (_isBrowsingFolder)
        {
            FontManager.DrawText("正在打开选择窗口，请在系统对话框中选择...", modalX + 130, modalY + 215, 14, Color.Yellow);
        }

        // 4. 保存并热重连 与 取消 按钮
        Rectangle btnSave = new(modalX + 100, modalY + 254, 140, 40);
        Rectangle btnCancel = new(modalX + 280, modalY + 254, 140, 40);
        bool hoverSave = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnSave);
        bool hoverCancel = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), btnCancel);

        Raylib.DrawRectangleRec(btnSave, hoverSave ? new Color(130, 95, 35, 255) : new Color(60, 45, 25, 255));
        Raylib.DrawRectangleLinesEx(btnSave, 1, hoverSave ? Color.Yellow : Color.Gold);
        FontManager.DrawText("保存并热重连", (int)btnSave.X + 22, (int)btnSave.Y + 11, 16, Color.White);

        Raylib.DrawRectangleRec(btnCancel, hoverCancel ? new Color(80, 80, 80, 255) : new Color(40, 40, 40, 255));
        Raylib.DrawRectangleLinesEx(btnCancel, 1, hoverCancel ? Color.White : Color.Gray);
        FontManager.DrawText("取消", (int)btnCancel.X + 54, (int)btnCancel.Y + 11, 16, Color.White);
    }

    public void OnResize(int width, int height) { }

    public void Dispose()
    {
    }
}
