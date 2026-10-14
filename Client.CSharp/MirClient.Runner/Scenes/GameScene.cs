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

    // 背包与装备管理器
    private readonly InventoryManager _inv = new();
    private bool _showBag = false;
    private bool _showEquip = false;

    // 鼠标双击检测时钟
    private double _lastBagClickTime = 0;
    private int _lastBagClickIndex = -1;
    private double _lastEquipClickTime = 0;
    private int _lastEquipClickSlot = -1;

    // 地表掉落物结构
    private record GroundItem(Item Item, Vector2 TilePos, float SpawnTime);
    private readonly List<GroundItem> _groundItems = new();

    // 全屏技能法术与粒子光效系统
    private readonly SkillEffectManager _skillFx = new();

    // 右上角雷达小地图显隐 (TAB 键切换)
    private bool _showMiniMap = true;

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

    // 随身金币经济系统 (初始 50,000 金币)
    private int _gold = 50000;

    // NPC 对话与商店状态
    private enum NpcDialogMode { Dialog, Shop, Sell }
    private MapEntity? _activeNpc = null;
    private NpcProfile? _activeNpcProfile = null;
    private NpcDialogMode _npcDialogMode = NpcDialogMode.Dialog;

    // 怪物自主巡逻与仇恨反击 AI 控制器
    private readonly Dictionary<int, MonsterAiController> _monsterAis = new();
    private readonly Dictionary<int, double> _monsterDeathTimes = new();

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

        SyncEquippedAppearance();
        _chatLogs.Add("[系统] 操作指引: 鼠标左键行走，右键奔跑，WASD 漫游，空格挥砍攻击。");
        _chatLogs.Add("[系统] 技能法术: [1/F1] 雷电术；[2/F2] 烈火剑法；[3/F3] 治愈术；[4/F4] 火球术 (支持点击底板快捷槽施法)。");
        _chatLogs.Add("[系统] 快捷面板: [TAB] 开关小地图雷达；[F9] 包裹背包；[F10] 角色装备；[F11] 色彩解码模式。");
        _chatLogs.Add($"[系统] NPC交互: 鼠标左键点击比奇城内 NPC (铁匠/药铺/老兵) 开启对话与货架买卖；随身金币: {_gold:N0}。");

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

        // 初始化场景实体 (比奇老兵/铁匠/药铺 NPC 与野外怪物)
        lock (_entities)
        {
            _entities.Clear();
            _monsterAis.Clear();
            _monsterDeathTimes.Clear();

            if (!_engine.NetworkSession.IsConnected)
            {
                // 城镇关键功能 NPC
                _entities.Add(new MapEntity(1001, "比奇老兵", EntityType.Npc, new Vector2(330, 268), appr: 0, maxHp: 1000));
                _entities.Add(new MapEntity(1002, "比奇城铁匠", EntityType.Npc, new Vector2(331, 269), appr: 0, maxHp: 1000));
                _entities.Add(new MapEntity(1003, "回春堂药铺老板", EntityType.Npc, new Vector2(329, 267), appr: 0, maxHp: 1000));

                // 野外经典怪物 (具备自主巡逻与仇恨反击 AI)
                var scarecrow = new MapEntity(2001, "稻草人", EntityType.Monster, new Vector2(333, 268), appr: 0, maxHp: 80);
                var cat = new MapEntity(2002, "钉耙猫", EntityType.Monster, new Vector2(326, 267), appr: 1, maxHp: 100);
                var orc = new MapEntity(2003, "半兽人", EntityType.Monster, new Vector2(331, 264), appr: 2, maxHp: 150);
                var yeti = new MapEntity(2004, "森林雪人", EntityType.Monster, new Vector2(327, 270), appr: 3, maxHp: 120);
                var whitePig = new MapEntity(2005, "白野猪", EntityType.Monster, new Vector2(335, 271), appr: 4, maxHp: 500);

                _entities.Add(scarecrow);
                _entities.Add(cat);
                _entities.Add(orc);
                _entities.Add(yeti);
                _entities.Add(whitePig);

                _monsterAis[scarecrow.RecogId] = new MonsterAiController(scarecrow);
                _monsterAis[cat.RecogId] = new MonsterAiController(cat);
                _monsterAis[orc.RecogId] = new MonsterAiController(orc);
                _monsterAis[yeti.RecogId] = new MonsterAiController(yeti);
                _monsterAis[whitePig.RecogId] = new MonsterAiController(whitePig);

                _chatLogs.Add("[世界] [离线沙盒] 比奇老兵、铁匠、药铺已就位；城外刷新【稻草人】、【钉耙猫】、【白野猪】！");
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

        // 战斗交互与热键 (1~4 与 F1~F4 技能法术)
        if (Raylib.IsKeyPressed(KeyboardKey.Space) || (Raylib.IsKeyDown(KeyboardKey.LeftShift) && Raylib.IsMouseButtonPressed(MouseButton.Left)))
        {
            TriggerAttack();
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.F1))
        {
            CastSkill(SkillType.ThunderBolt);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.F2))
        {
            CastSkill(SkillType.FireSword);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Three) || Raylib.IsKeyPressed(KeyboardKey.F3))
        {
            CastSkill(SkillType.Healing);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Four) || Raylib.IsKeyPressed(KeyboardKey.F4))
        {
            CastSkill(SkillType.Fireball);
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

        // F9: 开关包裹背包
        if (Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            _showBag = !_showBag;
        }

        // F10: 开关角色状态与装备
        if (Raylib.IsKeyPressed(KeyboardKey.F10))
        {
            _showEquip = !_showEquip;
        }

        // TAB: 开关小地图雷达
        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
        {
            _showMiniMap = !_showMiniMap;
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

        // 推进野外怪物的自主巡逻与仇恨反击 AI
        double nowTime = Raylib.GetTime();
        lock (_entities)
        {
            foreach (var entity in _entities)
            {
                if (entity.Type != EntityType.Monster) continue;

                if (!_monsterAis.TryGetValue(entity.RecogId, out var ai))
                {
                    ai = new MonsterAiController(entity);
                    _monsterAis[entity.RecogId] = ai;
                }

                // 若怪物已阵亡，记录死亡时间，满 15 秒在出生点复活刷新
                if (entity.IsDead)
                {
                    if (!_monsterDeathTimes.ContainsKey(entity.RecogId))
                    {
                        _monsterDeathTimes[entity.RecogId] = nowTime;
                    }
                    else if (nowTime - _monsterDeathTimes[entity.RecogId] > 15.0)
                    {
                        _monsterDeathTimes.Remove(entity.RecogId);
                        ai.Respawn();
                        _chatLogs.Add($"[世界] 【{entity.Name}】在荒野中重新刷新凝聚！");
                    }
                    continue;
                }

                // 玩家存活时驱动 AI 决策并检测反击
                if (_hp > 0 && ai.Update(dt, _playerTilePos, IsCellBlocked, out int dmg))
                {
                    _hp = Math.Max(0, _hp - dmg);
                    TriggerStruck();
                    _chatLogs.Add($"[受到攻击] 【{entity.Name}】发动反击，对您造成 {dmg} 点物理伤害！(当前生命: {(int)_hp}/{(int)MAX_HP})");

                    if (_hp <= 0)
                    {
                        _currentAction = HumanActionType.Die;
                        _motionStep = 0;
                        _chatLogs.Add($"[阵亡] 您被【{entity.Name}】击倒在地！点击屏幕中央【安全区满血复活】重返玛法。");
                    }
                }
            }
        }

        // 摄像机平滑跟随
        Vector2 targetPixel = _playerTilePos * new Vector2(TILE_WIDTH, TILE_HEIGHT);
        _cameraPixelPos = Vector2.Lerp(_cameraPixelPos, targetPixel, 10f * dt);

        // 自动拾取脚下地面战利品
        lock (_groundItems)
        {
            for (int i = _groundItems.Count - 1; i >= 0; i--)
            {
                var gi = _groundItems[i];
                if (Vector2.Distance(_playerTilePos, gi.TilePos) < 0.75f)
                {
                    if (_inv.AddItem(gi.Item))
                    {
                        _chatLogs.Add($"[拾取] 获得【{gi.Item.Name}】！已存入包裹。");
                        _groundItems.RemoveAt(i);
                    }
                }
            }
        }

        // 推进全屏法术光效与粒子生命周期
        _skillFx.Update(dt);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_activeNpc != null)
            {
                _activeNpc = null;
                _activeNpcProfile = null;
            }
            else if (_showBag || _showEquip)
            {
                _showBag = false;
                _showEquip = false;
            }
            else
            {
                _engine.SwitchToLoginScene();
            }
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
        // 鼠标左键：点击地面走动或小地图寻路
        else if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mousePos = Raylib.GetMousePosition();

            // 若点击在小地图内部，触发自动寻路奔跑导航
            if (HandleMiniMapClick(mousePos))
            {
                return;
            }

            if (mousePos.Y < screenH - 150)
            {
                // 如果当前正在与 NPC 对话，且点击在对话框内，则不处理地面行走
                if (_activeNpc != null && CheckNpcDialogCollision(mousePos, screenW, screenH))
                {
                    return;
                }

                Vector2 screenCenter = new(screenW / 2f, (screenH - 140) / 2f);
                Vector2 offset = mousePos - screenCenter;
                Vector2 clickedTile = _playerTilePos + new Vector2(offset.X / TILE_WIDTH, offset.Y / TILE_HEIGHT);

                // 检测是否点击了场景中的 NPC 实体
                MapEntity? clickedNpc = null;
                lock (_entities)
                {
                    clickedNpc = _entities.FirstOrDefault(e => !e.IsDead && e.Type == EntityType.Npc && Vector2.Distance(e.TilePos, clickedTile) < 1.5f);
                }
                if (clickedNpc != null)
                {
                    OpenNpcDialog(clickedNpc);
                    return;
                }

                if (!IsCellBlocked((int)clickedTile.X, (int)clickedTile.Y))
                {
                    _targetTilePos = clickedTile;
                    _isRunning = false;
                    SendMovePacket(clickedTile, false);
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

        // 5. 渲染 F9 背包物品栏与 F10 角色装备状态窗口
        if (_showBag) RenderInventoryWindow(screenW, screenH);
        if (_showEquip) RenderEquipmentWindow(screenW, screenH);

        // 6. 渲染 NPC 经典木纹羊皮纸对话框与商店货架
        if (_activeNpc != null && _activeNpcProfile != null)
        {
            RenderNpcDialogWindow(screenW, screenH);
        }

        // 7. 玩家阵亡界面 (黑白暗红覆屏与安全区复活按钮)
        if (_hp <= 0)
        {
            RenderDeathScreen(screenW, screenH);
        }
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

        // 【Pass 4: 地面战利品与光芒 (Ground Items)】
        lock (_groundItems)
        {
            foreach (var gi in _groundItems)
            {
                Vector2 rel = gi.TilePos - _playerTilePos;
                Vector2 itemPixel = screenCenter + rel * new Vector2(TILE_WIDTH, TILE_HEIGHT);
                if (itemPixel.X < -60 || itemPixel.X > viewportW + 60 || itemPixel.Y < -60 || itemPixel.Y > viewportH + 60)
                    continue;

                // 闪烁金色光芒与物品品名
                float time = (float)Raylib.GetTime();
                float glow = 0.6f + 0.4f * MathF.Sin(time * 5f);
                Color glowColor = new(255, (int)(215 * glow), 0, 255);
                Raylib.DrawCircleLines((int)itemPixel.X, (int)itemPixel.Y, 8, glowColor);
                FontManager.DrawTextWithShadow(gi.Item.Name, (int)itemPixel.X - 16, (int)itemPixel.Y - 14, 11, Color.Gold, Color.Black);
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

        // 【Pass 5: 全屏技能法术发光光效层 (Additive Blending)】
        _skillFx.DrawAll(screenCenter, _playerTilePos, TILE_WIDTH, TILE_HEIGHT);
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
                if (_monsterAis.TryGetValue(target.RecogId, out var ai))
                {
                    ai.OnDamaged(damage);
                }
                _chatLogs.Add($"[战斗] 挥砍命中【{target.Name}】造成 {damage} 点伤害！目标生命: {target.Hp}/{target.MaxHp}");
                if (target.IsDead)
                {
                    _chatLogs.Add($"[击杀] 成功消灭【{target.Name}】！获得经验值 +100");

                    // 掉落地面物品
                    Item drop = Random.Shared.Next(4) switch
                    {
                        0 => Item.CreateEbonySword(),
                        1 => Item.CreateHeavyArmor(),
                        2 => Item.CreateJudgmentMace(),
                        _ => Item.CreateRedPotion(3)
                    };
                    lock (_groundItems)
                    {
                        _groundItems.Add(new GroundItem(drop, target.TilePos, (float)Raylib.GetTime()));
                    }
                    _chatLogs.Add($"[掉落] 地面出现战利品【{drop.Name}】！走近即可拾取。");
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

    private void CastSkill(SkillType type)
    {
        var skill = Skill.GetSkill(type);
        if (_mp < skill.RequiredMp)
        {
            _chatLogs.Add($"[技能] 魔法值不足！施展【{skill.Name}】需要 {skill.RequiredMp} 点 MP (当前: {(int)_mp})");
            return;
        }

        _mp = Math.Clamp(_mp - skill.RequiredMp, 0, MAX_MP);
        _targetTilePos = _playerTilePos;
        _currentAction = (type == SkillType.FireSword) ? HumanActionType.Attack : HumanActionType.Spell;
        _motionStep = 0;
        _actionTimer = 0f;

        Vector2 frontTile = MapEntity.GetFrontTile(_playerTilePos, _direction);

        switch (type)
        {
            case SkillType.ThunderBolt:
            {
                MapEntity? target = FindNearestMonster(frontTile, 6f);
                Vector2 strikePos = target != null ? target.TilePos : _playerTilePos + (_direction switch
                {
                    0 => new Vector2(0, -2),
                    1 => new Vector2(2, -2),
                    2 => new Vector2(2, 0),
                    3 => new Vector2(2, 2),
                    4 => new Vector2(0, 2),
                    5 => new Vector2(-2, 2),
                    6 => new Vector2(-2, 0),
                    _ => new Vector2(-2, -2)
                });

                _skillFx.SpawnThunder(strikePos);
                _chatLogs.Add($"[法术] 念动咒语，召唤九天神雷轰击【{strikePos.X:F0},{strikePos.Y:F0}】！");

                if (target != null && Vector2.Distance(target.TilePos, strikePos) < 1.0f)
                {
                    int dmg = Random.Shared.Next(skill.MinPower, skill.MaxPower + 1);
                    ApplyMonsterDamage(target, dmg, skill.Name);
                }
                break;
            }

            case SkillType.FireSword:
            {
                _skillFx.SpawnFireSword(frontTile, _direction);
                _chatLogs.Add($"[武技] 挥出烈火剑法，极度炙热的烈焰剑气撕裂空气！");

                MapEntity? target = null;
                lock (_entities)
                {
                    target = _entities.FirstOrDefault(e => !e.IsDead && Vector2.Distance(e.TilePos, frontTile) < 1.1f);
                }
                if (target != null)
                {
                    int dmg = (int)(Random.Shared.Next(skill.MinPower, skill.MaxPower + 1) + (_inv.TotalMaxDc * 1.5f));
                    ApplyMonsterDamage(target, dmg, skill.Name);
                }
                break;
            }

            case SkillType.Healing:
            {
                _skillFx.SpawnHealing(_playerTilePos);
                int heal = 60;
                _hp = Math.Clamp(_hp + heal, 0, MAX_HP);
                _chatLogs.Add($"[道术] 施展神圣治愈术，圣洁辉光笼罩全身，生命值 +{heal}！(HP: {(int)_hp}/{(int)MAX_HP})");
                break;
            }

            case SkillType.Fireball:
            {
                MapEntity? target = FindNearestMonster(frontTile, 6f);
                Vector2 targetPos = target != null ? target.TilePos : _playerTilePos + (_direction switch
                {
                    0 => new Vector2(0, -3),
                    1 => new Vector2(3, -3),
                    2 => new Vector2(3, 0),
                    3 => new Vector2(3, 3),
                    4 => new Vector2(0, 3),
                    5 => new Vector2(-3, 3),
                    6 => new Vector2(-3, 0),
                    _ => new Vector2(-3, -3)
                });

                _chatLogs.Add($"[法术] 凝聚炙热大火球射向目标！");
                _skillFx.SpawnFireball(_playerTilePos, targetPos, () =>
                {
                    if (target != null && !target.IsDead && Vector2.Distance(target.TilePos, targetPos) < 1.2f)
                    {
                        int dmg = Random.Shared.Next(skill.MinPower, skill.MaxPower + 1);
                        ApplyMonsterDamage(target, dmg, skill.Name);
                    }
                });
                break;
            }
        }
    }

    private MapEntity? FindNearestMonster(Vector2 nearPos, float maxDist)
    {
        lock (_entities)
        {
            return _entities
                .Where(e => !e.IsDead && e.Type == EntityType.Monster && Vector2.Distance(e.TilePos, nearPos) <= maxDist)
                .OrderBy(e => Vector2.Distance(e.TilePos, nearPos))
                .FirstOrDefault();
        }
    }

    private void ApplyMonsterDamage(MapEntity target, int damage, string skillName)
    {
        target.TakeDamage(damage);
        if (_monsterAis.TryGetValue(target.RecogId, out var ai))
        {
            ai.OnDamaged(damage);
        }
        _chatLogs.Add($"[战斗] 【{skillName}】重创【{target.Name}】造成 {damage} 点伤害！剩余生命: {target.Hp}/{target.MaxHp}");
        if (target.IsDead)
        {
            _chatLogs.Add($"[击杀] 成功消灭【{target.Name}】！获得经验值 +150");

            Item drop = Random.Shared.Next(4) switch
            {
                0 => Item.CreateEbonySword(),
                1 => Item.CreateHeavyArmor(),
                2 => Item.CreateJudgmentMace(),
                _ => Item.CreateRedPotion(3)
            };
            lock (_groundItems)
            {
                _groundItems.Add(new GroundItem(drop, target.TilePos, (float)Raylib.GetTime()));
            }
            _chatLogs.Add($"[掉落] 地面出现战利品【{drop.Name}】！走近即可拾取。");
        }
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
        string[] skillNames = { "雷电", "烈火", "治愈", "火球", "半月", "刺杀", "逐日", "随机" };
        SkillType?[] slotSkills = { SkillType.ThunderBolt, SkillType.FireSword, SkillType.Healing, SkillType.Fireball, null, null, null, null };

        Vector2 mousePos = Raylib.GetMousePosition();

        for (int i = 0; i < 8; i++)
        {
            int slotX = slotsStartX + i * (slotSize + slotSpacing);
            Rectangle slotRect = new(slotX, slotsY, slotSize, slotSize);
            bool isHover = Raylib.CheckCollisionPointRec(mousePos, slotRect);

            Raylib.DrawRectangleRec(slotRect, isHover ? new Color(45, 38, 28, 240) : new Color(22, 18, 14, 210));
            Raylib.DrawRectangleLinesEx(slotRect, 1, isHover ? Color.Gold : new Color(110, 90, 45, 220));

            FontManager.DrawText($"F{i + 1}", slotX + 2, slotsY + 2, 10, isHover ? Color.Yellow : Color.Orange);
            int skillW = FontManager.MeasureText(skillNames[i], 11);
            FontManager.DrawText(skillNames[i], slotX + (slotSize - skillW) / 2, slotsY + 15, 11, isHover ? Color.White : Color.Yellow);

            // 支持鼠标左键点击快捷栏施法
            if (isHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (slotSkills[i].HasValue)
                {
                    CastSkill(slotSkills[i]!.Value);
                }
                else
                {
                    _chatLogs.Add($"[技能] 快捷槽 F{i + 1} 暂未装配技能。");
                }
            }
        }

        // 2. 底板右下角原生雕花红宝石大圆钮交互（移除突兀的纯黑矩形，100% 依托原生贴图浮雕）
        // 4 个经典大圆钮依次为：包裹、状态、行会、退出
        (string name, int centerX, int centerY, Action action)[] nativeButtons =
        {
            ("包裹", boardX + 718, boardY + 115, () => _showBag = !_showBag),
            ("状态", boardX + 750, boardY + 115, () => _showEquip = !_showEquip),
            ("行会", boardX + 782, boardY + 115, () => _chatLogs.Add("[系统] 行会窗口快捷键: G")),
            ("退出", boardX + 814, boardY + 115, () => _engine.SwitchToLoginScene())
        };

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
        int mapW = 168;
        int mapH = 142;
        int mapX = screenW - mapW - 12;
        int mapY = 12;

        if (!_showMiniMap)
        {
            // 折叠状态：右上角显示精美迷你唤醒按钮
            Rectangle minRect = new(screenW - 90, 12, 78, 24);
            bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), minRect);
            Raylib.DrawRectangleRec(minRect, new Color(18, 14, 12, 230));
            Raylib.DrawRectangleLinesEx(minRect, 1, hover ? Color.Gold : new Color(120, 95, 45, 220));
            FontManager.DrawText("TAB 雷达", screenW - 80, 17, 11, hover ? Color.Yellow : Color.LightGray);
            if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                _showMiniMap = true;
            }
            return;
        }

        // 1. 展开状态：盛大原版风格深曜石底框与古典金线浮雕
        Rectangle fullRect = new(mapX, mapY, mapW, mapH);
        Raylib.DrawRectangleRec(fullRect, new Color(12, 10, 8, 235));
        Raylib.DrawRectangleLinesEx(fullRect, 1, Color.Gold);

        // 2. 标题栏与实时坐标：例如 "[比奇省] 330, 268"
        string mapTitle = $"[比奇省] {(int)_playerTilePos.X}, {(int)_playerTilePos.Y}";
        FontManager.DrawTextWithShadow(mapTitle, mapX + 8, mapY + 5, 11, Color.Lime, Color.Black);

        // 折叠按键提示
        FontManager.DrawText("[TAB]", mapX + mapW - 38, mapY + 5, 10, Color.DarkGray);

        // 3. 计算坐标投影器
        int mapCols = _mapLoader.Width > 0 ? _mapLoader.Width : 800;
        int mapRows = _mapLoader.Height > 0 ? _mapLoader.Height : 600;

        float viewX = mapX + 6;
        float viewY = mapY + 22;
        float viewW = mapW - 12;
        float viewH = mapH - 28;

        MiniMapProjection proj = new(mapCols, mapRows, viewX, viewY, viewW, viewH);

        // 4. 地图底图层：尝试从 MMapLib 获取原版缩略图，若无则使用墨绿拓扑网格兜底
        Texture2D? mmapTex = AssetManager.GetTexture(AssetManager.MMapLib, "mmap", 0);
        if (mmapTex.HasValue)
        {
            Rectangle src = new(0, 0, mmapTex.Value.Width, mmapTex.Value.Height);
            Rectangle dest = new(viewX, viewY, viewW, viewH);
            Raylib.DrawTexturePro(mmapTex.Value, src, dest, Vector2.Zero, 0f, new Color(255, 255, 255, 220));
        }
        else
        {
            Raylib.DrawRectangle((int)viewX, (int)viewY, (int)viewW, (int)viewH, new Color(15, 26, 18, 240));
        }

        // 内视口细金边框
        Raylib.DrawRectangleLines((int)viewX, (int)viewY, (int)viewW, (int)viewH, new Color(75, 60, 35, 220));

        // 5. 动态实体图例 (NPC 黄点, 怪物红点)
        lock (_entities)
        {
            foreach (var ent in _entities)
            {
                if (ent.IsDead) continue;
                Vector2 entPx = proj.WorldToMiniMap(ent.TilePos);
                if (ent.Type == EntityType.Npc)
                {
                    Raylib.DrawCircle((int)entPx.X, (int)entPx.Y, 2.5f, Color.Gold);
                }
                else if (ent.Type == EntityType.Monster)
                {
                    Raylib.DrawCircle((int)entPx.X, (int)entPx.Y, 2.0f, Color.Red);
                }
            }
        }

        // 6. 玩家自身位置：动态呼吸发光红点 + 视向波束
        Vector2 playerPx = proj.WorldToMiniMap(_playerTilePos);
        float pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 8f);
        Raylib.DrawCircle((int)playerPx.X, (int)playerPx.Y, 3.5f + pulse * 2f, new Color(255, 60, 60, 150));
        Raylib.DrawCircle((int)playerPx.X, (int)playerPx.Y, 2.5f, Color.White);

        // 视向朝向扫描波束
        float angle = _direction * 45f;
        float rad = angle * (MathF.PI / 180f);
        Vector2 dirVec = new(MathF.Cos(rad), MathF.Sin(rad));
        Raylib.DrawLine((int)playerPx.X, (int)playerPx.Y, (int)(playerPx.X + dirVec.X * 8f), (int)(playerPx.Y + dirVec.Y * 8f), Color.Yellow);
    }

    private bool HandleMiniMapClick(Vector2 mousePos)
    {
        if (!_showMiniMap) return false;

        int screenW = Raylib.GetScreenWidth();
        int mapW = 168;
        int mapH = 142;
        int mapX = screenW - mapW - 12;
        int mapY = 12;

        int mapCols = _mapLoader.Width > 0 ? _mapLoader.Width : 800;
        int mapRows = _mapLoader.Height > 0 ? _mapLoader.Height : 600;

        MiniMapProjection proj = new(mapCols, mapRows, mapX + 6, mapY + 22, mapW - 12, mapH - 28);

        if (proj.IsInside(mousePos))
        {
            Vector2 targetTile = proj.MiniMapToWorld(mousePos);
            _targetTilePos = targetTile;
            _isRunning = true;
            SendMovePacket(targetTile, true);
            _chatLogs.Add($"[雷达导航] 自动寻路前往坐标: [{(int)targetTile.X}, {(int)targetTile.Y}]");
            return true;
        }

        return false;
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

    private void SyncEquippedAppearance()
    {
        _weaponId = _inv.EquippedWeaponShape;
        _dressId = _inv.EquippedDressShape;
    }

    private void RenderInventoryWindow(int screenW, int screenH)
    {
        int winW = 270;
        int winH = 350;
        int winX = screenW - winW - 20;
        int winY = screenH - 140 - winH - 10;
        Vector2 mousePos = Raylib.GetMousePosition();
        double now = Raylib.GetTime();

        // 窗口背景与金色浮雕框
        Raylib.DrawRectangle(winX, winY, winW, winH, new Color(16, 14, 12, 245));
        Raylib.DrawRectangleLines(winX, winY, winW, winH, Color.Gold);

        // 标题栏
        Raylib.DrawRectangle(winX, winY, winW, 26, new Color(42, 34, 22, 255));
        FontManager.DrawTextWithShadow("【 随身包裹 (F9) 】", winX + 10, winY + 6, 12, Color.Gold, Color.Black);

        // 关闭按钮 [X]
        Rectangle closeRect = new(winX + winW - 22, winY + 4, 18, 18);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
        Raylib.DrawRectangleRec(closeRect, closeHover ? Color.Maroon : new Color(28, 20, 15, 255));
        FontManager.DrawText("X", winX + winW - 17, winY + 6, 12, closeHover ? Color.White : Color.Gray);
        if (closeHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _showBag = false;
            return;
        }

        // 40 格背包网格 (6 列 x 7 行)
        int cols = 6;
        int slotSize = 38;
        int spacing = 3;
        int gridStartX = winX + 13;
        int gridStartY = winY + 34;

        Item? hoveredItem = null;

        for (int i = 0; i < InventoryManager.BAG_CAPACITY; i++)
        {
            int col = i % cols;
            int row = i / cols;
            int slotX = gridStartX + col * (slotSize + spacing);
            int slotY = gridStartY + row * (slotSize + spacing);
            Rectangle slotRect = new(slotX, slotY, slotSize, slotSize);

            bool isHover = Raylib.CheckCollisionPointRec(mousePos, slotRect);
            Item? item = _inv.BagItems[i];

            // 槽位底框
            Raylib.DrawRectangleRec(slotRect, new Color(24, 20, 16, 220));
            Raylib.DrawRectangleLinesEx(slotRect, 1, isHover ? Color.Yellow : new Color(80, 68, 45, 180));

            if (item != null)
            {
                // 物品展示：色泽区分类型
                Color nameColor = item.Type switch
                {
                    ItemType.Weapon => Color.Gold,
                    ItemType.Dress => Color.SkyBlue,
                    ItemType.Potion => Color.Pink,
                    _ => Color.White
                };

                // 绘制物品名称简写
                string shortName = item.Name.Length > 2 ? item.Name.Substring(0, 2) : item.Name;
                FontManager.DrawText(shortName, slotX + 7, slotY + 11, 12, nameColor);

                // 数量角标
                if (item.Count > 1)
                {
                    string countStr = $"{item.Count}";
                    FontManager.DrawText(countStr, slotX + slotSize - 12, slotY + slotSize - 12, 10, Color.Lime);
                }

                if (isHover)
                {
                    hoveredItem = item;

                    // 鼠标点击判定 (双击穿戴或使用)
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (now - _lastBagClickTime < 0.35 && _lastBagClickIndex == i)
                        {
                            // 若当前处于 NPC 商店出售模式，双击直接折价出售给 NPC
                            if (_activeNpc != null && _npcDialogMode == NpcDialogMode.Sell)
                            {
                                if (NpcShop.TrySellItem(_inv, ref _gold, i, out string soldName, out int gain, out string err))
                                {
                                    _chatLogs.Add($"[出售] 成功将【{soldName}】售予【{_activeNpc.Name}】，获得 {gain:N0} 金币！当前金币: {_gold:N0}");
                                }
                                else
                                {
                                    _chatLogs.Add($"[出售失败] {err}");
                                }
                            }
                            else if (item.Type == ItemType.Potion)
                            {
                                if (_inv.UsePotion(i, out int hp, out int mp))
                                {
                                    _hp = Math.Min(MAX_HP, _hp + hp);
                                    _mp = Math.Min(MAX_MP, _mp + mp);
                                    _chatLogs.Add($"[使用] 服用【{item.Name}】，生命恢复+{hp}，魔法恢复+{mp}！");
                                }
                            }
                            else
                            {
                                // 穿戴装备
                                string eqName = item.Name;
                                if (_inv.EquipItem(i))
                                {
                                    SyncEquippedAppearance();
                                    _chatLogs.Add($"[换装] 穿戴了【{eqName}】！纸娃娃模型实时更新。");
                                }
                            }
                            _lastBagClickIndex = -1;
                        }
                        else
                        {
                            _lastBagClickTime = now;
                            _lastBagClickIndex = i;
                        }
                    }
                }
            }
        }

        // 底部金币与随身负重汇总
        int bottomBarY = winY + winH - 24;
        Raylib.DrawRectangle(winX + 4, bottomBarY - 2, winW - 8, 22, new Color(24, 20, 16, 230));
        Raylib.DrawRectangleLines(winX + 4, bottomBarY - 2, winW - 8, 22, new Color(75, 60, 40, 200));
        FontManager.DrawText($"金币: {_gold:N0}", winX + 8, bottomBarY + 2, 11, Color.Gold);
        FontManager.DrawText($"负重: {_inv.TotalWeight}/280", winX + winW - 88, bottomBarY + 2, 11, Color.LightGray);

        // 浮动 Tips 渲染在最上层
        if (hoveredItem != null)
        {
            RenderItemTooltip(hoveredItem, (int)mousePos.X, (int)mousePos.Y);
        }
    }

    private void RenderEquipmentWindow(int screenW, int screenH)
    {
        int winW = 250;
        int winH = 350;
        int winX = 20;
        int winY = screenH - 140 - winH - 10;
        Vector2 mousePos = Raylib.GetMousePosition();
        double now = Raylib.GetTime();

        // 窗口背景与金色浮雕框
        Raylib.DrawRectangle(winX, winY, winW, winH, new Color(16, 14, 12, 245));
        Raylib.DrawRectangleLines(winX, winY, winW, winH, Color.Gold);

        // 标题栏
        Raylib.DrawRectangle(winX, winY, winW, 26, new Color(42, 34, 22, 255));
        FontManager.DrawTextWithShadow("【 角色状态 (F10) 】", winX + 10, winY + 6, 12, Color.Gold, Color.Black);

        // 关闭按钮 [X]
        Rectangle closeRect = new(winX + winW - 22, winY + 4, 18, 18);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
        Raylib.DrawRectangleRec(closeRect, closeHover ? Color.Maroon : new Color(28, 20, 15, 255));
        FontManager.DrawText("X", winX + winW - 17, winY + 6, 12, closeHover ? Color.White : Color.Gray);
        if (closeHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _showEquip = false;
            return;
        }

        // 角色基本信息
        FontManager.DrawTextWithShadow($"角色: {_playerName} (战士)", winX + 12, winY + 34, 12, Color.Yellow, Color.Black);

        // 8 大装备槽位布局 (2 列 x 4 行)
        (EquipSlot slot, string label, int x, int y)[] slots =
        {
            (EquipSlot.Dress, "衣服", winX + 15, winY + 56),
            (EquipSlot.Weapon, "武器", winX + 130, winY + 56),
            (EquipSlot.Helmet, "头盔", winX + 15, winY + 98),
            (EquipSlot.Necklace, "项链", winX + 130, winY + 98),
            (EquipSlot.ArmringL, "左手镯", winX + 15, winY + 140),
            (EquipSlot.ArmringR, "右手镯", winX + 130, winY + 140),
            (EquipSlot.RingL, "左戒指", winX + 15, winY + 182),
            (EquipSlot.RingR, "右戒指", winX + 130, winY + 182),
        };

        Item? hoveredItem = null;

        foreach (var (slot, label, sx, sy) in slots)
        {
            Rectangle sRect = new(sx, sy, 100, 36);
            bool isHover = Raylib.CheckCollisionPointRec(mousePos, sRect);
            Item? eq = _inv.EquippedItems[(int)slot];

            Raylib.DrawRectangleRec(sRect, new Color(24, 20, 16, 220));
            Raylib.DrawRectangleLinesEx(sRect, 1, isHover ? Color.Yellow : new Color(80, 68, 45, 180));

            FontManager.DrawText(label, sx + 4, sy + 3, 10, Color.DarkGray);
            if (eq != null)
            {
                FontManager.DrawText(eq.Name, sx + 4, sy + 16, 11, Color.Gold);
                if (isHover)
                {
                    hoveredItem = eq;
                    // 双击卸下
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (now - _lastEquipClickTime < 0.35 && _lastEquipClickSlot == (int)slot)
                        {
                            string eqName = eq.Name;
                            if (_inv.UnequipItem(slot))
                            {
                                SyncEquippedAppearance();
                                _chatLogs.Add($"[卸下] 脱下了【{eqName}】并收回包裹。");
                            }
                            _lastEquipClickSlot = -1;
                        }
                        else
                        {
                            _lastEquipClickTime = now;
                            _lastEquipClickSlot = (int)slot;
                        }
                    }
                }
            }
            else
            {
                FontManager.DrawText("无", sx + 4, sy + 16, 11, Color.Gray);
            }
        }

        // 下部动态属性汇总
        int attrY = winY + 228;
        Raylib.DrawLine(winX + 10, attrY, winX + winW - 10, attrY, new Color(75, 62, 40, 200));

        FontManager.DrawText($"物理攻击 (DC): {_inv.TotalMinDc} - {_inv.TotalMaxDc}", winX + 15, attrY + 8, 12, Color.White);
        FontManager.DrawText($"物理防御 (AC): {_inv.TotalMinAc} - {_inv.TotalMaxAc}", winX + 15, attrY + 28, 12, Color.White);
        FontManager.DrawText($"生命数值 (HP): {(int)_hp} / {(int)MAX_HP}", winX + 15, attrY + 48, 12, Color.Lime);
        FontManager.DrawText($"魔法数值 (MP): {(int)_mp} / {(int)MAX_MP}", winX + 15, attrY + 68, 12, Color.SkyBlue);
        FontManager.DrawText($"随身负重: {_inv.TotalWeight} / 280", winX + 15, attrY + 88, 12, Color.LightGray);

        if (hoveredItem != null)
        {
            RenderItemTooltip(hoveredItem, (int)mousePos.X, (int)mousePos.Y);
        }
    }

    private void RenderItemTooltip(Item item, int mouseX, int mouseY)
    {
        int tipW = 160;
        int tipH = 96;
        int tipX = Math.Min(Raylib.GetScreenWidth() - tipW - 10, mouseX + 12);
        int tipY = Math.Min(Raylib.GetScreenHeight() - tipH - 10, mouseY + 12);

        Raylib.DrawRectangle(tipX, tipY, tipW, tipH, new Color(10, 8, 6, 245));
        Raylib.DrawRectangleLines(tipX, tipY, tipW, tipH, Color.Gold);

        FontManager.DrawTextWithShadow(item.Name, tipX + 8, tipY + 6, 12, Color.Yellow, Color.Black);
        FontManager.DrawText($"类型: {item.Type}", tipX + 8, tipY + 24, 11, Color.LightGray);

        if (item.Type == ItemType.Weapon)
            FontManager.DrawText($"攻击力: {item.MinDc} - {item.MaxDc}", tipX + 8, tipY + 40, 11, Color.Lime);
        else if (item.Type == ItemType.Dress)
            FontManager.DrawText($"防御力: {item.MinAc} - {item.MaxAc}", tipX + 8, tipY + 40, 11, Color.SkyBlue);
        else if (item.Type == ItemType.Potion)
            FontManager.DrawText($"恢复: HP+{item.HpRestore} MP+{item.MpRestore}", tipX + 8, tipY + 40, 11, Color.Pink);

        FontManager.DrawText($"重量: {item.Weight}  持久: {item.Dura}/{item.MaxDura}", tipX + 8, tipY + 56, 10, Color.Gray);
        FontManager.DrawText("[鼠标双击穿戴/使用/卸下]", tipX + 8, tipY + 74, 10, Color.Gold);
    }

    private bool CheckNpcDialogCollision(Vector2 mousePos, int screenW, int screenH)
    {
        int winW = 380;
        int winH = 280;
        int winX = (screenW - winW) / 2;
        int winY = (screenH - 140 - winH) / 2 - 20;
        return Raylib.CheckCollisionPointRec(mousePos, new Rectangle(winX, winY, winW, winH));
    }

    private void OpenNpcDialog(MapEntity npc)
    {
        _activeNpc = npc;
        _activeNpcProfile = NpcShop.ResolveProfile(npc.Name);
        _npcDialogMode = NpcDialogMode.Dialog;
        _targetTilePos = _playerTilePos; // 停步与 NPC 对话
        _chatLogs.Add($"[NPC] 正在与【{_activeNpcProfile.Name}】对话...");
    }

    private void RenderNpcDialogWindow(int screenW, int screenH)
    {
        if (_activeNpc == null || _activeNpcProfile == null) return;

        int winW = 380;
        int winH = 280;
        int winX = (screenW - winW) / 2;
        int winY = (screenH - 140 - winH) / 2 - 20;
        Vector2 mousePos = Raylib.GetMousePosition();

        // 1. 经典复古羊皮木纹外框与金线浮雕
        Raylib.DrawRectangle(winX, winY, winW, winH, new Color(20, 16, 12, 245));
        Raylib.DrawRectangleLines(winX, winY, winW, winH, Color.Gold);
        Raylib.DrawRectangleLines(winX + 2, winY + 2, winW - 4, winH - 4, new Color(90, 75, 45, 200));

        // 2. 标题栏
        Raylib.DrawRectangle(winX, winY, winW, 28, new Color(48, 38, 24, 255));
        string title = $"【 {_activeNpcProfile.Name} 】  {_activeNpcProfile.Title}";
        FontManager.DrawTextWithShadow(title, winX + 12, winY + 7, 12, Color.Gold, Color.Black);

        // 关闭叉号 [X]
        Rectangle closeRect = new(winX + winW - 24, winY + 5, 18, 18);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
        Raylib.DrawRectangleRec(closeRect, closeHover ? Color.Maroon : new Color(30, 22, 16, 255));
        FontManager.DrawText("X", winX + winW - 19, winY + 7, 12, closeHover ? Color.White : Color.Gray);
        if (closeHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            _activeNpc = null;
            _activeNpcProfile = null;
            return;
        }

        // 3. NPC 状态模式渲染
        if (_npcDialogMode == NpcDialogMode.Dialog)
        {
            // 问候台词
            FontManager.DrawTextWithShadow(_activeNpcProfile.Greeting, winX + 16, winY + 42, 12, Color.LightGray, Color.Black);

            // 分割金线
            Raylib.DrawLine(winX + 12, winY + 110, winX + winW - 12, winY + 110, new Color(85, 70, 40, 200));

            // 选项列表
            int optY = winY + 124;
            for (int i = 0; i < _activeNpcProfile.Options.Count; i++)
            {
                var opt = _activeNpcProfile.Options[i];
                Rectangle optRect = new(winX + 20, optY, winW - 40, 24);
                bool optHover = Raylib.CheckCollisionPointRec(mousePos, optRect);

                if (optHover)
                {
                    Raylib.DrawRectangleRec(optRect, new Color(60, 48, 30, 180));
                    Raylib.DrawRectangleLinesEx(optRect, 1, Color.Gold);
                }

                Color optColor = optHover ? Color.Yellow : new Color(130, 200, 255, 255);
                FontManager.DrawText(opt.Label, winX + 26, optY + 5, 12, optColor);

                if (optHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    HandleNpcAction(opt.ActionCode);
                }

                optY += 28;
            }
        }
        else if (_npcDialogMode == NpcDialogMode.Shop)
        {
            // 货架列表
            FontManager.DrawTextWithShadow($"【 随身金币: {_gold:N0} 】 点击商品即可购买", winX + 16, winY + 36, 12, Color.Lime, Color.Black);

            int goodsY = winY + 62;
            for (int i = 0; i < _activeNpcProfile.Goods.Count; i++)
            {
                var g = _activeNpcProfile.Goods[i];
                Rectangle itemRect = new(winX + 16, goodsY, winW - 32, 42);
                bool gHover = Raylib.CheckCollisionPointRec(mousePos, itemRect);

                Raylib.DrawRectangleRec(itemRect, gHover ? new Color(45, 36, 24, 220) : new Color(24, 20, 16, 200));
                Raylib.DrawRectangleLinesEx(itemRect, 1, gHover ? Color.Gold : new Color(80, 65, 40, 200));

                FontManager.DrawText(g.Item.Name, winX + 24, goodsY + 6, 12, Color.Gold);
                string desc = g.Item.Type == ItemType.Weapon ? $"攻击: {g.Item.MinDc}-{g.Item.MaxDc}  重量: {g.Item.Weight}" :
                              g.Item.Type == ItemType.Dress ? $"防御: {g.Item.MinAc}-{g.Item.MaxAc}  重量: {g.Item.Weight}" :
                              $"恢复: HP+{g.Item.HpRestore} MP+{g.Item.MpRestore}";
                FontManager.DrawText(desc, winX + 24, goodsY + 24, 10, Color.LightGray);

                // 购买按钮
                Rectangle buyBtn = new(winX + winW - 100, goodsY + 8, 76, 26);
                bool buyHover = Raylib.CheckCollisionPointRec(mousePos, buyBtn);
                Raylib.DrawRectangleRec(buyBtn, buyHover ? Color.DarkGreen : new Color(30, 48, 30, 240));
                Raylib.DrawRectangleLinesEx(buyBtn, 1, buyHover ? Color.Lime : Color.Green);
                FontManager.DrawText($"{g.Price:N0}金", winX + winW - 95, goodsY + 14, 11, Color.White);

                if (buyHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (NpcShop.TryBuyItem(_inv, ref _gold, g, out string err))
                    {
                        _chatLogs.Add($"[交易] 成功购买【{g.Item.Name}】！花费 {g.Price:N0} 金币。(剩余金币: {_gold:N0})");
                    }
                    else
                    {
                        _chatLogs.Add($"[购买失败] {err}");
                    }
                }

                goodsY += 48;
            }

            // 返回按钮
            Rectangle backBtn = new(winX + 16, winY + winH - 32, 60, 22);
            bool backHover = Raylib.CheckCollisionPointRec(mousePos, backBtn);
            Raylib.DrawRectangleRec(backBtn, backHover ? new Color(50, 40, 30, 240) : new Color(30, 24, 18, 200));
            Raylib.DrawRectangleLinesEx(backBtn, 1, backHover ? Color.Gold : Color.Gray);
            FontManager.DrawText("< 返回", winX + 24, winY + winH - 28, 11, backHover ? Color.Yellow : Color.White);
            if (backHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                _npcDialogMode = NpcDialogMode.Dialog;
            }
        }
    }

    private void HandleNpcAction(string actionCode)
    {
        switch (actionCode)
        {
            case "@buy":
                _npcDialogMode = NpcDialogMode.Shop;
                break;
            case "@sell":
                _npcDialogMode = NpcDialogMode.Sell;
                _showBag = true; // 自动呼出包裹
                _chatLogs.Add($"[NPC] 【{_activeNpc?.Name}】：“双击包裹中的闲置装备或药水即可直接折价出售！”");
                break;
            case "@repair":
                _chatLogs.Add($"[NPC] 【{_activeNpc?.Name}】：“少侠的神兵完好如初，无需特修！”");
                break;
            case "@tele_mengzhong":
                _playerTilePos = new Vector2(330, 330);
                _targetTilePos = _playerTilePos;
                _activeNpc = null;
                _activeNpcProfile = null;
                _chatLogs.Add("[空间法阵] 空间波动涌现，您已被传送至【盟重省土城 (330, 330)】！");
                break;
            case "@tele_snake":
                _playerTilePos = new Vector2(150, 150);
                _targetTilePos = _playerTilePos;
                _activeNpc = null;
                _activeNpcProfile = null;
                _chatLogs.Add("[空间法阵] 穿过比奇关隘，您已抵达【毒蛇山谷隘口 (150, 150)】！");
                break;
            case "@legend":
                _chatLogs.Add("[传说] 比奇老兵：“相传唯有攻陷沙巴克皇宫并立于城主雕像之巅的行会霸主，方能号令玛法！”");
                break;
            case "@close":
            default:
                _activeNpc = null;
                _activeNpcProfile = null;
                break;
        }
    }

    private void RenderDeathScreen(int screenW, int screenH)
    {
        // 1. 全屏半透明猩红暗夜滤镜 (还原传奇经典死亡体验)
        Raylib.DrawRectangle(0, 0, screenW, screenH, new Color(40, 10, 10, 190));

        int centerX = screenW / 2;
        int centerY = screenH / 2 - 30;

        // 2. 居中显目阵亡提示
        string deathTitle = "【 您 已 战 败 阵 亡 】";
        int titleW = FontManager.MeasureText(deathTitle, 22);
        FontManager.DrawTextWithShadow(deathTitle, centerX - titleW / 2, centerY - 40, 22, Color.Red, Color.Black);

        string hint = "胜败乃兵家常事，少侠请回安全区重整旗鼓再战玛法！";
        int hintW = FontManager.MeasureText(hint, 13);
        FontManager.DrawTextWithShadow(hint, centerX - hintW / 2, centerY, 13, Color.LightGray, Color.Black);

        // 3. 【安全区复活】按钮
        Rectangle reviveBtn = new(centerX - 80, centerY + 36, 160, 36);
        Vector2 mousePos = Raylib.GetMousePosition();
        bool isHover = Raylib.CheckCollisionPointRec(mousePos, reviveBtn);

        Raylib.DrawRectangleRec(reviveBtn, isHover ? new Color(160, 30, 30, 255) : new Color(90, 20, 20, 240));
        Raylib.DrawRectangleLinesEx(reviveBtn, 2, isHover ? Color.Gold : Color.Red);

        string btnText = "安全区满血复活";
        int btnW = FontManager.MeasureText(btnText, 14);
        FontManager.DrawTextWithShadow(btnText, centerX - btnW / 2, centerY + 46, 14, isHover ? Color.Yellow : Color.White, Color.Black);

        if (isHover && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ReviveAtSafeZone();
        }
    }

    private void ReviveAtSafeZone()
    {
        _playerTilePos = new Vector2(330, 268); // 比奇城安全区中心
        _targetTilePos = _playerTilePos;
        _hp = MAX_HP;
        _mp = MAX_MP;
        _currentAction = HumanActionType.Stand;
        _motionStep = 0;
        _chatLogs.Add("[复活] 祈祷神光自虚空降临，您已在【比奇城安全区】满血复活！");
    }
}
