using System.Numerics;
using Raylib_cs;
using MirClient.Protocol;
using MirClient.Protocol.Resources;
using MirClient.Runner.Graphics;

namespace MirClient.Runner.Scenes;

/// <summary>
/// 盛大十周年原版主游戏视口场景：
/// 1. 100% 依托 Map/0.map 与 Data/Tiles.*、SmTiles.*、Objects.* 渲染真实比奇城地貌；
/// 2. 100% 贴图 Prguse3 Index 371 原版控制台底板与 Prguse Index 4 矩形裁剪玻璃血魔球；
/// 3. 原生 8 方向操作走跑、摄像机跟随与聊天窗口。
/// </summary>
public class GameScene : IScene
{
    private readonly GameEngine _engine;
    private readonly ClientConfig _config;

    // 0.map 地图加载器
    private readonly MapLoader _mapLoader = new();
    private bool _mapLoaded = false;

    // 玩家世界坐标 (以瓦片为单位，比奇省老兵坐标 330, 268)
    private Vector2 _playerTilePos = new(330, 268);
    private Vector2 _targetTilePos = new(330, 268);
    private int _direction = 4; // 初始面向南方 (0: 上, 1: 右上, 2: 右, 3: 右下, 4: 下, 5: 左下, 6: 左, 7: 左上)
    private bool _isRunning = false;
    private float _moveSpeed = 4.0f; // 瓦片/秒

    // 原版角色动作与动画状态机
    private HumanActionType _currentAction = HumanActionType.Stand;
    private float _actionTimer = 0f;
    private int _motionStep = 0;

    // 玩家外观与纸娃娃属性 (1:1 盛大原版体系)
    private string _playerName = "逐鹿天下";
    private int _gender = 0;   // 0: 男, 1: 女
    private int _hairId = 1;   // 1: 经典短发, 0: 光头
    private int _weaponId = 1; // 1: 新手木剑, 0: 空手
    private int _dressId = 0;  // 0: 布衣

    // 场景多实体管理 (NPC、怪物、其他玩家)
    private readonly List<MapEntity> _entities = new();

    private static bool IsOneShotAction(HumanActionType action) => action switch
    {
        HumanActionType.Attack or HumanActionType.HeavyHit or HumanActionType.BigHit or 
        HumanActionType.Spell or HumanActionType.Struck or HumanActionType.Die => true,
        _ => false
    };

    // 摄像机平滑像素位置
    private Vector2 _cameraPixelPos;

    // 瓦片尺寸 (经典传奇: 48x32 像素网格)
    private const int TILE_WIDTH = 48;
    private const int TILE_HEIGHT = 32;

    // 生命与魔法值
    private float _hp = 650;
    private const float MAX_HP = 650;
    private float _mp = 320;
    private const float MAX_MP = 320;

    // 聊天消息队列
    private readonly List<string> _chatLogs = new();

    public GameScene(GameEngine engine, ClientConfig config)
    {
        _engine = engine;
        _config = config;
        _cameraPixelPos = _playerTilePos * new Vector2(TILE_WIDTH, TILE_HEIGHT);

        // 订阅真实 7200 游戏网关广播
        _engine.NetworkSession.OnGamePacket += HandleServerGamePacket;
        _engine.NetworkSession.OnLog += msg => _chatLogs.Add(msg);
    }

    public void Init()
    {
        _chatLogs.Clear();
        _chatLogs.Add("[系统] 欢迎回到热血传奇！已连接至盛大十周年原生资产引擎。");

        // 若网络会话已完成选角，同步主角真实属性
        if (!string.IsNullOrWhiteSpace(_engine.NetworkSession.CharacterName))
        {
            _playerName = _engine.NetworkSession.CharacterName;
            _gender = _engine.NetworkSession.CharacterGender;
            _hairId = _engine.NetworkSession.CharacterHair;
            _chatLogs.Add($"[联机成功] 已通过 7200 游戏网关进入世界，当前角色:【{_playerName}】！");
        }
        else
        {
            _chatLogs.Add($"[离线模式] 当前以本地体验模式运行 (区服: {_config.ServerName})。");
        }

        _chatLogs.Add("[系统] 操作指引: 鼠标左键行走，右键奔跑，WASD 漫游，空格挥砍攻击。");
        _chatLogs.Add("[系统] 调试功能: [F11] 实时切换色彩解码模式；[F8/F9] 动态换发型与武器！");

        // 写入资源诊断日志 client_diagnostic.log
        try
        {
            List<string> diagLines = new()
            {
                $"=== 盛大十周年核心资产诊断报告 ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===",
                $"客户端路径: {AssetManager.ClientRootPath}",
                $"Tiles: 文件={AssetManager.TilesLib.LoadedFilePath}, WZL={AssetManager.TilesLib.IsWzl}, 总数={AssetManager.TilesLib.ImageCount}, 帧0={AssetManager.TilesLib.GetImageMeta(0)}",
                $"SmTiles: 文件={AssetManager.SmTilesLib.LoadedFilePath}, WZL={AssetManager.SmTilesLib.IsWzl}, 总数={AssetManager.SmTilesLib.ImageCount}",
                $"Objects: 文件={AssetManager.ObjectsLib.LoadedFilePath}, WZL={AssetManager.ObjectsLib.IsWzl}, 总数={AssetManager.ObjectsLib.ImageCount}",
                $"Prguse: 文件={AssetManager.PrguseLib.LoadedFilePath}, WZL={AssetManager.PrguseLib.IsWzl}, 总数={AssetManager.PrguseLib.ImageCount}, 帧4={AssetManager.PrguseLib.GetImageMeta(4)}",
                $"Prguse3: 文件={AssetManager.Prguse3Lib.LoadedFilePath}, WZL={AssetManager.Prguse3Lib.IsWzl}, 总数={AssetManager.Prguse3Lib.ImageCount}, 帧371={AssetManager.Prguse3Lib.GetImageMeta(371)}",
                $"Hum: 文件={AssetManager.HumLib.LoadedFilePath}, WZL={AssetManager.HumLib.IsWzl}, 总数={AssetManager.HumLib.ImageCount}, 帧0={AssetManager.HumLib.GetImageMeta(0)}",
                $"当前色彩模式: {WilLoader.ActiveColorMode}"
            };
            File.WriteAllLines("client_diagnostic.log", diagLines);
        }
        catch { }

        // 加载比奇城 0.map
        string mapPath = Path.Combine(AssetManager.ClientRootPath, "Map", "0.map");
        if (File.Exists(mapPath))
        {
            try
            {
                _mapLoader.Load(mapPath);
                _mapLoaded = true;
                Console.WriteLine($"[地图装载成功] 比奇城 0.map ({_mapLoader.Width} x {_mapLoader.Height}) 已就绪！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[地图错误] 加载 0.map 失败: {ex.Message}");
                _mapLoaded = false;
            }
        }

        // 初始化场景实体 (比奇老兵 NPC 与野外怪物)
        lock (_entities)
        {
            _entities.Clear();
            if (!_engine.NetworkSession.IsConnected)
            {
                _entities.Add(new MapEntity(1001, "比奇老兵", EntityType.Npc, new Vector2(330, 268), appr: 0, maxHp: 1000));
                _entities.Add(new MapEntity(2001, "稻草人", EntityType.Monster, new Vector2(332, 268), appr: 0, maxHp: 80));
                _entities.Add(new MapEntity(2002, "钉耙猫", EntityType.Monster, new Vector2(328, 267), appr: 1, maxHp: 100));
                _entities.Add(new MapEntity(2003, "半兽人", EntityType.Monster, new Vector2(331, 266), appr: 2, maxHp: 150));
                _entities.Add(new MapEntity(2004, "森林雪人", EntityType.Monster, new Vector2(329, 270), appr: 3, maxHp: 120));
                _chatLogs.Add("[世界] [离线沙盒] 比奇省老兵已就位，城外刷新野怪【稻草人】、【钉耙猫】、【半兽人】！");
            }
            else
            {
                _chatLogs.Add("[世界] [联机在线] 已接入 7200 游戏网关，正在监听并渲染服务端动态世界实体...");
            }
        }
    }

    public void Update(float dt)
    {
        HandleInput(dt);

        // F11 实时切换 16 位颜色解码模式
        if (Raylib.IsKeyPressed(KeyboardKey.F11))
        {
            WilLoader.ActiveColorMode = (ColorDecodeMode)(((int)WilLoader.ActiveColorMode + 1) % 4);
            AssetManager.ClearTextures();
            string modeDesc = WilLoader.ActiveColorMode switch
            {
                ColorDecodeMode.Bgr565 => "模式 0: 盛大原版官方正统 (实测 100% 正确真值)",
                ColorDecodeMode.Rgb565 => "模式 1: 备用 RGB565 (高5位 Red, 低5位 Blue)",
                ColorDecodeMode.BigEndianRgb => "模式 2: 备用大端序 RGB565 (字节对调, 高5位 Red)",
                ColorDecodeMode.BigEndianBgr => "模式 3: 备用大端序 BGR565 (字节对调, 高5位 Blue)",
                _ => ""
            };
            _chatLogs.Add($"[色彩切换] 已切换至 -> {modeDesc}");
            Console.WriteLine($"[色彩切换] {modeDesc}");
        }

        // 战斗交互与热键
        if (Raylib.IsKeyPressed(KeyboardKey.Space) || (Raylib.IsKeyDown(KeyboardKey.LeftShift) && Raylib.IsMouseButtonPressed(MouseButton.Left)))
        {
            TriggerAttack();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.F1))
        {
            TriggerSpell();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.H))
        {
            TriggerStruck();
        }

        // F7: 切换性别 (男/女)
        if (Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            _gender = (_gender + 1) % 2;
            _chatLogs.Add($"[外观切换] 性别已切换至: {(_gender == 0 ? "男" : "女")}");
        }

        // F8: 切换发型
        if (Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            _hairId = (_hairId + 1) % 4; // 0:光头, 1:短发, 2:束发, 3:长发
            string hairDesc = _hairId switch { 0 => "光头", 1 => "经典短发", 2 => "束发", 3 => "长发", _ => $"发型{_hairId}" };
            _chatLogs.Add($"[外观切换] 发型已切换至: {hairDesc}");
        }

        // F9: 切换武器
        if (Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            _weaponId = (_weaponId + 1) % 6; // 0:空手, 1:木剑, 2:乌木剑, 3:青铜剑, 4:八荒, 5:裁决之杖
            string weaponDesc = _weaponId switch { 0 => "空手赤拳", 1 => "新手木剑", 2 => "乌木剑", 3 => "青铜剑", 4 => "八荒刀", 5 => "裁决之杖", _ => $"武器{_weaponId}" };
            _chatLogs.Add($"[外观切换] 武器已切换至: {weaponDesc}");
        }

        bool isOneShot = IsOneShotAction(_currentAction);

        // 非单次动作时处理移动插值与动作切换
        if (!isOneShot)
        {
            Vector2 diff = _targetTilePos - _playerTilePos;
            float dist = diff.Length();
            HumanActionType targetAction = HumanActionType.Stand;

            if (dist > 0.05f)
            {
                targetAction = _isRunning ? HumanActionType.Run : HumanActionType.Walk;
                float speed = _isRunning ? _moveSpeed * 1.8f : _moveSpeed;
                Vector2 step = Vector2.Normalize(diff) * speed * dt;
                if (step.Length() > dist)
                    _playerTilePos = _targetTilePos;
                else
                    _playerTilePos += step;

                UpdateDirection(diff);
            }
            else
            {
                _playerTilePos = _targetTilePos;
                _isRunning = false;
                targetAction = HumanActionType.Stand;
            }

            // 状态切换时重置步进
            if (targetAction != _currentAction)
            {
                _currentAction = targetAction;
                _motionStep = 0;
                _actionTimer = 0f;
            }
        }

        // 动画帧时钟推进
        var actionInfo = HumanAction.GetActionInfo(_currentAction);
        _actionTimer += dt * 1000f;
        if (_actionTimer >= actionInfo.FTime)
        {
            _actionTimer -= actionInfo.FTime;
            if (isOneShot)
            {
                if (_motionStep + 1 >= actionInfo.Frame)
                {
                    if (_currentAction == HumanActionType.Die)
                    {
                        _motionStep = actionInfo.Frame - 1; // 死亡停留在倒地最后一帧
                    }
                    else
                    {
                        // 攻击/施法/受击单次动作播放完毕，平滑恢复站立
                        _currentAction = HumanActionType.Stand;
                        _motionStep = 0;
                    }
                }
                else
                {
                    _motionStep++;
                }
            }
            else
            {
                _motionStep = (_motionStep + 1) % actionInfo.Frame;
            }
        }

        // 推进所有场景实体逻辑与动画
        lock (_entities)
        {
            foreach (var entity in _entities)
            {
                entity.UpdateAnimation(dt);
            }
        }

        // 摄像机平滑跟随
        Vector2 targetPixel = _playerTilePos * new Vector2(TILE_WIDTH, TILE_HEIGHT);
        _cameraPixelPos = Vector2.Lerp(_cameraPixelPos, targetPixel, 10f * dt);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.F2))
        {
            _engine.SwitchToLoginScene();
        }
    }

    private void HandleInput(float dt)
    {
        // 若当前处于攻击、施法、受击等单次动作中，锁定移动以保持打击姿态
        if (IsOneShotAction(_currentAction)) return;

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // 键盘 WASD 控制
        Vector2 moveInput = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) moveInput.Y -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) moveInput.Y += 1;
        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) moveInput.X -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) moveInput.X += 1;

        if (moveInput != Vector2.Zero)
        {
            Vector2 next = _playerTilePos + Vector2.Normalize(moveInput) * 1.5f;
            if (!IsCellBlocked((int)next.X, (int)next.Y))
            {
                _targetTilePos = next;
                _isRunning = Raylib.IsKeyDown(KeyboardKey.LeftShift);
                SendMovePacket(next, _isRunning);
            }
            return;
        }

        // 鼠标右键：长按奔跑
        if (Raylib.IsMouseButtonDown(MouseButton.Right))
        {
            Vector2 mousePos = Raylib.GetMousePosition();
            Vector2 screenCenter = new(screenW / 2f, (screenH - 140) / 2f);
            Vector2 mouseDir = mousePos - screenCenter;
            if (mouseDir.Length() > 30f)
            {
                Vector2 next = _playerTilePos + Vector2.Normalize(mouseDir) * 2.0f;
                if (!IsCellBlocked((int)next.X, (int)next.Y))
                {
                    _targetTilePos = next;
                    _isRunning = true;
                    SendMovePacket(next, true);
                }
            }
        }
        // 鼠标左键：点击地面走动
        else if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mousePos = Raylib.GetMousePosition();
            if (mousePos.Y < screenH - 150)
            {
                Vector2 screenCenter = new(screenW / 2f, (screenH - 140) / 2f);
                Vector2 offset = mousePos - screenCenter;
                Vector2 next = _playerTilePos + new Vector2(offset.X / TILE_WIDTH, offset.Y / TILE_HEIGHT);
                if (!IsCellBlocked((int)next.X, (int)next.Y))
                {
                    _targetTilePos = next;
                    _isRunning = false;
                    SendMovePacket(next, false);
                }
            }
        }
    }

    private void SendMovePacket(Vector2 targetPos, bool isRunning)
    {
        if (_engine.NetworkSession.IsConnected)
        {
            DefaultMessage moveMsg = new(
                ident: isRunning ? ProtocolConsts.CM_RUN : ProtocolConsts.CM_WALK,
                recog: 0,
                param: (ushort)targetPos.X,
                tag: (ushort)targetPos.Y,
                series: (ushort)_direction
            );
            _ = _engine.NetworkSession.SendGatePacketAsync(moveMsg);
        }
    }

    private bool IsCellBlocked(int x, int y)
    {
        if (!_mapLoaded) return false;
        if (x < 0 || x >= _mapLoader.Width || y < 0 || y >= _mapLoader.Height) return true;
        return _mapLoader.Cells[x, y].IsBlocked;
    }

    private void UpdateDirection(Vector2 dir)
    {
        float angle = MathF.Atan2(dir.Y, dir.X) * (180.0f / MathF.PI);
        if (angle < 0) angle += 360f;

        _direction = angle switch
        {
            >= 247.5f and < 292.5f => 0, // 上
            >= 292.5f and < 337.5f => 1, // 右上
            >= 337.5f or < 22.5f   => 2, // 右
            >= 22.5f and < 67.5f   => 3, // 右下
            >= 67.5f and < 112.5f  => 4, // 下
            >= 112.5f and < 157.5f => 5, // 左下
            >= 157.5f and < 202.5f => 6, // 左
            _                      => 7  // 左上
        };
    }

    public void Render()
    {
        // 确保全帧缓冲区纯黑底色，杜绝任何未清除缓冲区的白板或撕裂
        Raylib.ClearBackground(Color.Black);

        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();

        // 1. 渲染比奇城地图视口 (严格依托 0.map 与三趟独立 Pass 管线)
        RenderMapViewport(screenW, screenH - 140);

        // 2. 渲染十周年原版主界面 HUD (Prguse3 底板 + Prguse 玻璃血魔球 + 嵌入式聊天视窗 + 原生圆钮交互)
        RenderBottomHud(screenW, screenH);

        // 3. 渲染右上角小地图
        RenderMiniMap(screenW);

        // 4. 左上角显示当前色彩解码模式及 [F11] 切换指引 (带半透明背景框，防树木遮挡)
        string modeStr = $"[F11: 切换色彩模式] 当前: {(int)WilLoader.ActiveColorMode} - {WilLoader.ActiveColorMode}";
        Raylib.DrawRectangle(8, 8, 380, 24, new Color(0, 0, 0, 200));
        FontManager.DrawTextWithShadow(modeStr, 14, 13, 12, Color.Gold, Color.Black);
    }

    private void RenderMapViewport(int viewportW, int viewportH)
    {
        Vector2 screenCenter = new(viewportW / 2f, viewportH / 2f);

        int centerTileX = (int)MathF.Round(_playerTilePos.X);
        int centerTileY = (int)MathF.Round(_playerTilePos.Y);

        int viewRadiusX = (viewportW / TILE_WIDTH) / 2 + 3;
        int viewRadiusY = (viewportH / TILE_HEIGHT) / 2 + 3;

        int startY = centerTileY - viewRadiusY;
        int endY = centerTileY + viewRadiusY;
        int startX = centerTileX - viewRadiusX;
        int endX = centerTileX + viewRadiusX;

        if (!_mapLoaded) return;

        // 【Pass 1: 底层大瓦片 (Tiles / BkImg)】
        // 跨度 2x2 网格 (96x64 像素)，严格先全量铺平底层地砖，杜绝盖碎建筑基座
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (x < 0 || x >= _mapLoader.Width || y < 0 || y >= _mapLoader.Height) continue;
                if ((x % 2 != 0) || (y % 2 != 0)) continue;

                var cell = _mapLoader.Cells[x, y];
                int bkIdx = cell.CleanBkIndex - 1;
                if (bkIdx >= 0)
                {
                    float screenX = screenCenter.X + (x * TILE_WIDTH - _cameraPixelPos.X);
                    float screenY = screenCenter.Y + (y * TILE_HEIGHT - _cameraPixelPos.Y);
                    Texture2D? tileTex = AssetManager.GetTexture(AssetManager.TilesLib, "Tiles", bkIdx);
                    if (tileTex.HasValue)
                    {
                        Raylib.DrawTexture(tileTex.Value, (int)screenX, (int)screenY, Color.White);
                    }
                }
            }
        }

        // 【Pass 2: 中层微地貌与道路瓦片 (SmTiles / MidImg)】
        // 逐格扫描并覆盖在大瓦片之上
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (x < 0 || x >= _mapLoader.Width || y < 0 || y >= _mapLoader.Height) continue;

                var cell = _mapLoader.Cells[x, y];
                int midIdx = cell.CleanMidIndex - 1;
                if (midIdx >= 0)
                {
                    float screenX = screenCenter.X + (x * TILE_WIDTH - _cameraPixelPos.X);
                    float screenY = screenCenter.Y + (y * TILE_HEIGHT - _cameraPixelPos.Y);
                    Texture2D? smTileTex = AssetManager.GetTexture(AssetManager.SmTilesLib, "SmTiles", midIdx);
                    if (smTileTex.HasValue)
                    {
                        Raylib.DrawTexture(smTileTex.Value, (int)screenX, (int)screenY, Color.White);
                    }
                }
            }
        }

        // 【Pass 3: 前景物体建筑/城墙与光晕 (Objects / FrImg)】
        // 严格遵循 Y 轴自上而下逐行扫描，还原自然景深与遮挡 (Y-Sorting)
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (x < 0 || x >= _mapLoader.Width || y < 0 || y >= _mapLoader.Height) continue;

                var cell = _mapLoader.Cells[x, y];
                int frIdx = cell.CleanFrIndex - 1;
                if (frIdx >= 0)
                {
                    float screenX = screenCenter.X + (x * TILE_WIDTH - _cameraPixelPos.X);
                    float screenY = screenCenter.Y + (y * TILE_HEIGHT - _cameraPixelPos.Y);
                    Texture2D? objTex = AssetManager.GetTexture(AssetManager.ObjectsLib, "Objects", frIdx);
                    if (objTex.HasValue)
                    {
                        int drawX = (int)screenX;
                        int drawY = (int)screenY - (objTex.Value.Height - TILE_HEIGHT);

                        // 盛大原版光照混合: 当 (AniFrame & 0x80) != 0 时为路灯/火把光晕，走加法混合
                        bool isBlend = (cell.AniFrame & 0x80) != 0;
                        if (isBlend)
                        {
                            Raylib.BeginBlendMode(BlendMode.Additive);
                            Raylib.DrawTexture(objTex.Value, drawX, drawY, Color.White);
                            Raylib.EndBlendMode();
                        }
                        else
                        {
                            Raylib.DrawTexture(objTex.Value, drawX, drawY, Color.White);
                        }
                    }
                }
            }
        }

        // 2.5D 视口实体深度排序 (Y-Sorting): 脚底 Y 越小越先画，近景遮挡远景
        var renderItems = new List<(float Y, Action Draw)>();

        // 收集所有可见场景实体 (NPC、怪物)
        lock (_entities)
        {
            foreach (var entity in _entities)
            {
                Vector2 rel = entity.TilePos - _playerTilePos;
                Vector2 entityPixel = screenCenter + rel * new Vector2(TILE_WIDTH, TILE_HEIGHT);
                // 视口粗裁剪 (超出屏幕边缘则跳过)
                if (entityPixel.X < -120 || entityPixel.X > viewportW + 120 || entityPixel.Y < -120 || entityPixel.Y > viewportH + 120)
                    continue;

                renderItems.Add((entity.TilePos.Y, () => DrawEntity(entity, entityPixel)));
            }
        }

        // 添加玩家自身
        renderItems.Add((_playerTilePos.Y, () => DrawPlayer(screenCenter)));

        // 严格按照脚底 Y 升序排列并依次绘制
        renderItems.Sort((a, b) => a.Y.CompareTo(b.Y));
        foreach (var item in renderItems)
        {
            item.Draw();
        }
    }

    private void TriggerAttack()
    {
        int screenW = Raylib.GetScreenWidth();
        int screenH = Raylib.GetScreenHeight();
        Vector2 screenCenter = new(screenW / 2f, (screenH - 140) / 2f);
        Vector2 mouseDir = Raylib.GetMousePosition() - screenCenter;
        if (mouseDir.Length() > 20f)
        {
            UpdateDirection(mouseDir);
        }

        _targetTilePos = _playerTilePos; // 挥砍打断移动
        _currentAction = HumanActionType.Attack;
        _motionStep = 0;
        _actionTimer = 0f;

        // 若处于联机状态，向网关上报攻击指令 CM_HIT
        if (_engine.NetworkSession.IsConnected)
        {
            DefaultMessage hitMsg = new(
                ident: ProtocolConsts.CM_HIT,
                recog: 0,
                param: (ushort)_playerTilePos.X,
                tag: (ushort)_playerTilePos.Y,
                series: (ushort)_direction
            );
            _ = _engine.NetworkSession.SendGatePacketAsync(hitMsg);
        }

        // 计算前方攻击落点瓦片 (正前方 1 格)
        Vector2 frontTile = MapEntity.GetFrontTile(_playerTilePos, _direction);

        // 查找命中目标实体
        MapEntity? target = null;
        lock (_entities)
        {
            target = _entities.FirstOrDefault(e => !e.IsDead && Vector2.Distance(e.TilePos, frontTile) < 0.9f);
        }
        if (target != null)
        {
            if (target.Type == EntityType.Monster)
            {
                int damage = Random.Shared.Next(18, 32); // 挥砍基础物理伤害
                target.TakeDamage(damage);
                _chatLogs.Add($"[战斗] 挥砍命中【{target.Name}】造成 {damage} 点伤害！目标生命: {target.Hp}/{target.MaxHp}");
                if (target.IsDead)
                {
                    _chatLogs.Add($"[击杀] 成功消灭【{target.Name}】！获得经验值 +100");
                }
            }
            else if (target.Type == EntityType.Npc)
            {
                _chatLogs.Add($"[NPC] 【{target.Name}】对您说：“年轻人，比奇城外多有妖邪出没，切莫轻敌！”");
            }
        }
    }

    private void TriggerSpell()
    {
        _targetTilePos = _playerTilePos;
        _currentAction = HumanActionType.Spell;
        _motionStep = 0;
        _actionTimer = 0f;
    }

    private void TriggerStruck()
    {
        _targetTilePos = _playerTilePos;
        _currentAction = HumanActionType.Struck;
        _motionStep = 0;
        _actionTimer = 0f;
    }

    private void DrawPlayer(Vector2 center)
    {
        // 1. 盛大十周年原生角色模型复合渲染 (Hum + Hair + Weapon)
        int baseFrame = HumanAction.CalculateBaseFrame(_currentAction, _direction, _motionStep);
        int bodyFrame = _dressId * HumanAction.HUMANFRAME + baseFrame;
        int hairFrame = HumanAction.CalculateHairFrameIndex(_currentAction, _direction, _motionStep, _hairId, _gender);
        int weaponFrame = HumanAction.CalculateWeaponFrameIndex(_currentAction, _direction, _motionStep, _weaponId, _gender);

        int weaponOrder = HumanAction.GetWeaponOrder(_gender, baseFrame);

        var (bodyTex, bpx, bpy) = AssetManager.GetFrameWithOffset(AssetManager.HumLib, "Hum", bodyFrame);
        var (hairTex, hpx, hpy) = _hairId > 0 ? AssetManager.GetFrameWithOffset(AssetManager.HairLib, "Hair", hairFrame) : (null, (short)0, (short)0);
        var (weaponTex, wpx, wpy) = _weaponId > 0 ? AssetManager.GetFrameWithOffset(AssetManager.WeaponLib, "Weapon", weaponFrame) : (null, (short)0, (short)0);

        void DrawWeaponLayer()
        {
            if (weaponTex.HasValue)
            {
                Raylib.DrawTexture(weaponTex.Value, (int)(center.X + wpx), (int)(center.Y + wpy), Color.White);
            }
        }

        void DrawBodyAndHairLayers()
        {
            if (bodyTex.HasValue)
            {
                Raylib.DrawTexture(bodyTex.Value, (int)(center.X + bpx), (int)(center.Y + bpy), Color.White);
            }
            if (hairTex.HasValue)
            {
                Raylib.DrawTexture(hairTex.Value, (int)(center.X + hpx), (int)(center.Y + hpy), Color.White);
            }
        }

        // 严格遵循盛大原版 1:1 WORDER 遮挡次序决策
        if (weaponOrder == 0)
        {
            // 武器在身后 (北向背负或身侧挥斩后收): Weapon -> Body -> Hair
            DrawWeaponLayer();
            DrawBodyAndHairLayers();
        }
        else
        {
            // 武器在身前 (南向正面握持或前挥斩): Body -> Hair -> Weapon
            DrawBodyAndHairLayers();
            DrawWeaponLayer();
        }

        // 2. 头顶原版经典血条与角色名称 (定位于角色头部上方)
        int headY = (int)(center.Y - 64);
        int hpBarW = 46;
        int hpBarH = 4;
        int hpBarX = (int)(center.X - hpBarW / 2f);
        int hpBarY = headY;

        Raylib.DrawRectangle(hpBarX, hpBarY, hpBarW, hpBarH, Color.Black);
        float hpRate = Math.Clamp(_hp / MAX_HP, 0f, 1f);
        Raylib.DrawRectangle(hpBarX, hpBarY, (int)(hpBarW * hpRate), hpBarH, Color.Green);
        Raylib.DrawRectangleLines(hpBarX, hpBarY, hpBarW, hpBarH, Color.Gold);

        string name = _playerName;
        int nameW = FontManager.MeasureText(name, 12);
        FontManager.DrawTextWithShadow(name, (int)(center.X - nameW / 2f), headY - 16, 12, Color.Yellow, Color.Black);

        // 显示外观说明
        string hairDesc = _hairId switch { 0 => "光头", 1 => "短发", 2 => "束发", 3 => "长发", _ => $"发型{_hairId}" };
        string weaponDesc = _weaponId switch { 0 => "空手", 1 => "木剑", 2 => "乌木剑", 3 => "青铜剑", 4 => "八荒", 5 => "裁决", _ => $"武器{_weaponId}" };
        string lookInfo = $"[{(_gender == 0 ? "男" : "女")} | {weaponDesc} | {hairDesc}]";
        int infoW = FontManager.MeasureText(lookInfo, 10);
        FontManager.DrawTextWithShadow(lookInfo, (int)(center.X - infoW / 2f), headY - 28, 10, Color.LightGray, Color.Black);
    }

    private void DrawEntity(MapEntity entity, Vector2 center)
    {
        if (entity.Type == EntityType.Npc)
        {
            // NPC 贴图绘制 (NpcLib: Data/Npc)
            int npcFrame = entity.Appr * 60 + (entity.MotionStep % 4);
            var (npcTex, px, py) = AssetManager.GetFrameWithOffset(AssetManager.NpcLib, "Npc", npcFrame);
            if (npcTex.HasValue)
            {
                Raylib.DrawTexture(npcTex.Value, (int)(center.X + px), (int)(center.Y + py), Color.White);
            }
            else
            {
                // 容错表现：以 Hum 基础帧呈现老者装束
                var (humTex, hx, hy) = AssetManager.GetFrameWithOffset(AssetManager.HumLib, "Hum", 0);
                if (humTex.HasValue)
                {
                    Raylib.DrawTexture(humTex.Value, (int)(center.X + hx), (int)(center.Y + hy), new Color(170, 210, 255, 255));
                }
            }

            // NPC 头顶金色尊称
            int nameW = FontManager.MeasureText(entity.Name, 12);
            FontManager.DrawTextWithShadow(entity.Name, (int)(center.X - nameW / 2f), (int)center.Y - 60, 12, Color.Gold, Color.Black);
        }
        else if (entity.Type == EntityType.Monster)
        {
            // 怪物模型绘制 (Mon1Lib: Data/Mon1)
            // 盛大原版 Mon1 中每只怪物通常 8 方向展开，每个动作 6~8 帧
            int baseMotionFrame = entity.CurrentAction switch
            {
                HumanActionType.Stand => entity.Direction * 10 + (entity.MotionStep % 4),
                HumanActionType.Walk  => 80 + entity.Direction * 10 + (entity.MotionStep % 6),
                HumanActionType.Attack => 160 + entity.Direction * 10 + (entity.MotionStep % 6),
                HumanActionType.Struck => 240 + entity.Direction * 10 + (entity.MotionStep % 2),
                HumanActionType.Die   => 260 + entity.Direction * 10 + (entity.MotionStep % 4),
                _ => entity.Direction * 10
            };
            int monFrame = entity.Appr * 360 + baseMotionFrame;

            var (monTex, mx, my) = AssetManager.GetFrameWithOffset(AssetManager.Mon1Lib, "Mon1", monFrame);
            if (monTex.HasValue)
            {
                Color tint = entity.IsDead ? new Color(110, 110, 110, 170) : Color.White;
                Raylib.DrawTexture(monTex.Value, (int)(center.X + mx), (int)(center.Y + my), tint);
            }
            else
            {
                // 容错表现：若怪物图库未解压或当前帧超出，使用原版 Hum 染红作为假想怪演示
                int humBase = HumanAction.CalculateBaseFrame(entity.CurrentAction, entity.Direction, entity.MotionStep);
                var (humTex, hx, hy) = AssetManager.GetFrameWithOffset(AssetManager.HumLib, "Hum", humBase);
                if (humTex.HasValue)
                {
                    Color monTint = entity.IsDead ? new Color(80, 50, 50, 160) : new Color(255, 120, 120, 255);
                    Raylib.DrawTexture(humTex.Value, (int)(center.X + hx), (int)(center.Y + hy), monTint);
                }
            }

            // 活着的怪物显示血条与名称
            if (!entity.IsDead)
            {
                int barW = 36;
                int barH = 3;
                int barX = (int)(center.X - barW / 2f);
                int barY = (int)(center.Y - 50);
                Raylib.DrawRectangle(barX, barY, barW, barH, Color.Black);
                float hpRate = Math.Clamp((float)entity.Hp / entity.MaxHp, 0f, 1f);
                Raylib.DrawRectangle(barX, barY, (int)(barW * hpRate), barH, Color.Red);

                int mNameW = FontManager.MeasureText(entity.Name, 11);
                FontManager.DrawTextWithShadow(entity.Name, (int)(center.X - mNameW / 2f), barY - 14, 11, new Color(255, 180, 180, 255), Color.Black);
            }

            // 浮动伤害跳字提示
            if (entity.DamageText != null)
            {
                float floatOffset = (1.0f - entity.DamageTextTimer) * 20f;
                int floatY = (int)(center.Y - 60 - floatOffset);
                int dmgW = FontManager.MeasureText(entity.DamageText, 14);
                FontManager.DrawTextWithShadow(entity.DamageText, (int)(center.X - dmgW / 2f), floatY, 14, Color.Yellow, Color.Red);
            }
        }
    }

    private void RenderBottomHud(int screenW, int screenH)
    {
        // 1. 贴图十周年原版控制台金属雕花底板 (Prguse3 Index 371 或 Prguse Index 0)
        Texture2D? bottomTex = AssetManager.GetTexture(AssetManager.Prguse3Lib, "Prguse3", 371) 
                            ?? AssetManager.GetTexture(AssetManager.PrguseLib, "Prguse", 0);

        int boardW = bottomTex.HasValue ? bottomTex.Value.Width : 800;
        int boardH = bottomTex.HasValue ? bottomTex.Value.Height : 140;

        // 宽屏居中局部锚点系统 (严格对齐 Delphi DBottom.Left := (SCREENWIDTH - DBottom.Width) div 2)
        int boardX = (screenW - boardW) / 2;
        int boardY = screenH - boardH;

        // 【关键】：在底板全域（包括中央镂空的聊天窗口背面）铺垫深墨黑底色，彻底消灭聊天视窗白板穿透
        Raylib.DrawRectangle(boardX, boardY, boardW, boardH, new Color(14, 12, 10, 255));

        if (boardX > 0)
        {
            Raylib.DrawRectangle(0, boardY, boardX, boardH, new Color(18, 16, 14, 255));
            Raylib.DrawRectangle(boardX + boardW, boardY, screenW - (boardX + boardW), boardH, new Color(18, 16, 14, 255));
        }

        if (bottomTex.HasValue)
        {
            Rectangle srcRect = new(0, 0, bottomTex.Value.Width, bottomTex.Value.Height);
            Rectangle destRect = new(boardX, boardY, bottomTex.Value.Width, bottomTex.Value.Height);
            Raylib.DrawTexturePro(bottomTex.Value, srcRect, destRect, Vector2.Zero, 0f, Color.White);
        }
        else
        {
            Raylib.DrawRectangle(boardX, boardY, boardW, boardH, new Color(22, 20, 18, 255));
            Raylib.DrawLine(boardX, boardY, boardX + boardW, boardY, Color.Gold);
        }

        // 2. 贴图十周年原版双色玻璃血魔球 (Prguse Index 4) - 严格相对 boardX, boardY 局部锚定
        Texture2D? ballTex = AssetManager.GetTexture(AssetManager.PrguseLib, "Prguse", 4);
        if (ballTex.HasValue)
        {
            int bw = ballTex.Value.Width;
            int bh = ballTex.Value.Height;

            // 原版 Delphi FState.pas:8824: 血球相对底板 (Left + 38, Top + 91 精准同心嵌合左侧龙纹凹槽)
            int ballX = boardX + 38;
            int ballY = boardH >= 200 ? boardY + 91 : boardY + (boardH - bh) / 2;

            // 左半球 HP (生命纯红)：根据 (MAX_HP - HP)/MAX_HP 动态计算顶部裁剪
            float hpEmptyRate = (MAX_HP - _hp) / MAX_HP;
            int hpCropTop = (int)(bh * Math.Clamp(hpEmptyRate, 0f, 1f));
            Rectangle hpSrc = new(0, hpCropTop, bw / 2, bh - hpCropTop);
            Rectangle hpDest = new(ballX, ballY + hpCropTop, bw / 2, bh - hpCropTop);
            Raylib.DrawTexturePro(ballTex.Value, hpSrc, hpDest, Vector2.Zero, 0f, Color.White);

            // 右半球 MP (魔法湛蓝)：根据 (MAX_MP - MP)/MAX_MP 动态计算顶部裁剪
            float mpEmptyRate = (MAX_MP - _mp) / MAX_MP;
            int mpCropTop = (int)(bh * Math.Clamp(mpEmptyRate, 0f, 1f));
            Rectangle mpSrc = new(bw / 2, mpCropTop, bw / 2, bh - mpCropTop);
            Rectangle mpDest = new(ballX + bw / 2, ballY + mpCropTop, bw / 2, bh - mpCropTop);
            Raylib.DrawTexturePro(ballTex.Value, mpSrc, mpDest, Vector2.Zero, 0f, Color.White);

            // 血球数值文字
            string hpStr = $"{(int)_hp}";
            string mpStr = $"{(int)_mp}";
            FontManager.DrawTextWithShadow(hpStr, ballX + 10, ballY + bh / 2 - 8, 12, Color.White, Color.Black);
            FontManager.DrawTextWithShadow(mpStr, ballX + bw / 2 + 8, ballY + bh / 2 - 8, 12, Color.White, Color.Black);
        }

        // 3. 底板中央聊天视窗 (深黑曜石遮罩覆盖原生刺眼白块，文字清晰雅致)
        RenderChatBox(boardX, boardY);

        // 4. 右侧 F1 ~ F8 快捷技能槽与原生红宝石圆钮交互 (避开聊天框，精准居于右侧凹槽)
        RenderSkillAndFuncButtons(boardX, boardY);
    }

    private void RenderSkillAndFuncButtons(int boardX, int boardY)
    {
        // 1. F1 ~ F8 技能快捷栏：精准嵌合右上方雕花凹槽 (StartX 对齐 502，彻底避开聊天框)
        int slotSize = 32;
        int slotSpacing = 3;
        int slotsStartX = boardX + 502;
        int slotsY = boardY + 12;
        string[] skillNames = { "烈火", "刺杀", "逐日", "野蛮", "半月", "攻杀", "开天", "随机" };

        for (int i = 0; i < 8; i++)
        {
            int slotX = slotsStartX + i * (slotSize + slotSpacing);
            Rectangle slotRect = new(slotX, slotsY, slotSize, slotSize);
            Raylib.DrawRectangleRec(slotRect, new Color(22, 18, 14, 210));
            Raylib.DrawRectangleLinesEx(slotRect, 1, new Color(110, 90, 45, 220));

            FontManager.DrawText($"F{i + 1}", slotX + 2, slotsY + 2, 10, Color.Orange);
            int skillW = FontManager.MeasureText(skillNames[i], 11);
            FontManager.DrawText(skillNames[i], slotX + (slotSize - skillW) / 2, slotsY + 15, 11, Color.Yellow);
        }

        // 2. 底板右下角原生雕花红宝石大圆钮交互（移除突兀的纯黑矩形，100% 依托原生贴图浮雕）
        // 4 个经典大圆钮依次为：包裹、状态、行会、退出
        (string name, int centerX, int centerY, Action action)[] nativeButtons =
        {
            ("包裹", boardX + 718, boardY + 115, () => _chatLogs.Add("[系统] 包裹窗口快捷键: F9")),
            ("状态", boardX + 750, boardY + 115, () => _chatLogs.Add("[系统] 角色状态快捷键: F10")),
            ("行会", boardX + 782, boardY + 115, () => _chatLogs.Add("[系统] 行会窗口快捷键: G")),
            ("退出", boardX + 814, boardY + 115, () => _engine.SwitchToLoginScene())
        };

        Vector2 mousePos = Raylib.GetMousePosition();
        int btnRadius = 14;

        foreach (var btn in nativeButtons)
        {
            Vector2 btnCenter = new(btn.centerX, btn.centerY);
            bool hover = Vector2.Distance(mousePos, btnCenter) <= btnRadius;

            if (hover)
            {
                // 鼠标悬停原生红宝石按钮：高亮金色光环与提示
                Raylib.DrawCircleLines(btn.centerX, btn.centerY, btnRadius, Color.Gold);
                Raylib.DrawCircleLines(btn.centerX, btn.centerY, btnRadius - 1, Color.Yellow);
                FontManager.DrawTextWithShadow(btn.name, btn.centerX - 11, btn.centerY - 24, 11, Color.Yellow, Color.Black);

                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    btn.action();
                }
            }
        }
    }

    private void RenderChatBox(int boardX, int boardY)
    {
        // 针对十周年底板中央聊天区，铺设典雅黑曜石底板，文字清晰锐利
        int chatBoxX = boardX + 192;
        int chatBoxY = boardY + 10;
        int chatBoxW = 310;
        int chatBoxH = 228;

        Raylib.DrawRectangle(chatBoxX, chatBoxY, chatBoxW, chatBoxH, new Color(16, 14, 12, 235));
        Raylib.DrawRectangleLines(chatBoxX, chatBoxY, chatBoxW, chatBoxH, new Color(75, 62, 40, 160));

        int chatTextX = chatBoxX + 6;
        int chatTextY = chatBoxY + 4;
        int lineY = chatTextY;
        int startIdx = Math.Max(0, _chatLogs.Count - 5);
        for (int i = startIdx; i < _chatLogs.Count; i++)
        {
            FontManager.DrawText(_chatLogs[i], chatTextX, lineY, 12, i == _chatLogs.Count - 1 ? Color.Lime : Color.LightGray);
            lineY += 17;
        }
    }

    private void RenderMiniMap(int screenW)
    {
        int mapW = 150;
        int mapH = 120;
        int mapX = screenW - mapW - 12;
        int mapY = 12;

        Raylib.DrawRectangle(mapX, mapY, mapW, mapH, new Color(18, 16, 20, 220));
        Raylib.DrawRectangleLines(mapX, mapY, mapW, mapH, Color.Gold);

        string mapTitle = $"比奇城 [{(int)_playerTilePos.X}, {(int)_playerTilePos.Y}]";
        FontManager.DrawText(mapTitle, mapX + 8, mapY + 6, 12, Color.Yellow);
        Raylib.DrawRectangleLines(mapX + 10, mapY + 24, mapW - 20, mapH - 32, new Color(50, 50, 50, 255));
        Raylib.DrawCircle(mapX + mapW / 2, mapY + 24 + (mapH - 32) / 2, 3, Color.Yellow);
    }

    private void HandleServerGamePacket(DefaultMessage header, string payload)
    {
        switch (header.Ident)
        {
            case ProtocolConsts.SM_LOGON:
                _chatLogs.Add($"[世界] 收到服务端 SM_LOGON 确认！生命值上限: {header.Recog}");
                break;

            case ProtocolConsts.SM_CHANGEMAP:
            {
                string mapName = EDCode.DecodeText(payload);
                _chatLogs.Add($"[世界] 服务端指示切换地图: {mapName}");
                break;
            }

            case ProtocolConsts.SM_SPURN:
            case ProtocolConsts.SM_NOWACTOR:
            {
                int entityId = header.Recog;
                int x = header.Param;
                int y = header.Tag;
                int dir = header.Series;
                string decPayload = EDCode.DecodeText(payload);
                var parts = decPayload.Split('/');
                string entityName = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) 
                    ? parts[0] 
                    : (header.Ident == ProtocolConsts.SM_NOWACTOR ? "行者" : "异兽");
                int appr = parts.Length > 1 && int.TryParse(parts[1], out int a) ? a : 0;
                int maxHp = parts.Length > 3 && int.TryParse(parts[3], out int hp) ? hp : 100;

                EntityType type = header.Ident == ProtocolConsts.SM_NOWACTOR 
                    ? EntityType.Player 
                    : (appr == 0 && entityName.Contains("老兵") ? EntityType.Npc : EntityType.Monster);

                lock (_entities)
                {
                    var existing = _entities.FirstOrDefault(e => e.RecogId == entityId);
                    if (existing == null)
                    {
                        var newEntity = new MapEntity(entityId, entityName, type, new Vector2(x, y), appr, maxHp)
                        {
                            Direction = dir
                        };
                        _entities.Add(newEntity);
                        _chatLogs.Add($"[世界广播] 实体【{entityName}】现身于 [{x}, {y}]！");
                    }
                    else
                    {
                        existing.TilePos = new Vector2(x, y);
                        existing.Direction = dir;
                    }
                }
                break;
            }

            case ProtocolConsts.SM_TURN:
            {
                lock (_entities)
                {
                    var entity = _entities.FirstOrDefault(e => e.RecogId == header.Recog);
                    if (entity != null)
                    {
                        entity.Direction = header.Series;
                        entity.CurrentAction = HumanActionType.Stand;
                    }
                }
                break;
            }

            case ProtocolConsts.SM_WALK:
            case ProtocolConsts.SM_RUN:
            {
                lock (_entities)
                {
                    var entity = _entities.FirstOrDefault(e => e.RecogId == header.Recog);
                    if (entity != null)
                    {
                        entity.TilePos = new Vector2(header.Param, header.Tag);
                        entity.Direction = header.Series;
                        entity.CurrentAction = header.Ident == ProtocolConsts.SM_RUN ? HumanActionType.Run : HumanActionType.Walk;
                    }
                }
                break;
            }

            case ProtocolConsts.SM_HIT:
            case ProtocolConsts.SM_POWERHIT:
            {
                lock (_entities)
                {
                    var entity = _entities.FirstOrDefault(e => e.RecogId == header.Recog);
                    if (entity != null)
                    {
                        entity.Direction = header.Series;
                        entity.TriggerAction(HumanActionType.Attack);
                    }
                }
                break;
            }

            case ProtocolConsts.SM_STRUCK:
            {
                lock (_entities)
                {
                    var entity = _entities.FirstOrDefault(e => e.RecogId == header.Recog);
                    if (entity != null)
                    {
                        int curHp = header.Param;
                        int maxHp = header.Tag;
                        int dmg = header.Series;
                        entity.Hp = curHp;
                        if (maxHp > 0) entity.MaxHp = maxHp;
                        entity.TakeDamage(dmg);
                    }
                    else if (header.Recog == 0) // 本地玩家自身受击
                    {
                        _hp = Math.Max(0f, (float)header.Param);
                        TriggerStruck();
                    }
                }
                break;
            }

            case ProtocolConsts.SM_DEATH:
            {
                lock (_entities)
                {
                    var entity = _entities.FirstOrDefault(e => e.RecogId == header.Recog);
                    if (entity != null)
                    {
                        entity.TakeDamage(99999);
                        _chatLogs.Add($"[世界] 【{entity.Name}】倒地阵亡！");
                    }
                }
                break;
            }

            case ProtocolConsts.SM_DISAPPEAR:
            case ProtocolConsts.SM_SKELETON:
            {
                lock (_entities)
                {
                    _entities.RemoveAll(e => e.RecogId == header.Recog);
                }
                break;
            }

            case ProtocolConsts.SM_HEAR:
            {
                string msg = EDCode.DecodeText(payload);
                _chatLogs.Add($"[世界传音] {msg}");
                break;
            }

            default:
                break;
        }
    }

    public void OnResize(int width, int height) { }

    public void Dispose()
    {
        _chatLogs.Clear();
    }
}
