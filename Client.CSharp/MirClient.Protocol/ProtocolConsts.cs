namespace MirClient.Protocol;

/// <summary>
/// 严格对应 Grobal2.pas 与 OpenMir2 Messages.cs 中的经典传奇通信命令代码 (CM: Client->Server, SM: Server->Client)
/// </summary>
public static class ProtocolConsts
{
    // 客户端发往服务端的命令 (CM_)
    public const ushort CM_QUERYCHR      = 100;  // 查询角色列表
    public const ushort CM_NEWCHR        = 101;  // 创建新角色
    public const ushort CM_DELCHR        = 102;  // 删除角色
    public const ushort CM_SELCHR        = 103;  // 选择角色进入游戏
    public const ushort CM_SELECTSERVER  = 104;  // 选择游戏区服
    public const ushort CM_TURN          = 1003; // 角色原地转身
    public const ushort CM_WALK          = 1004; // 角色走步 (单格)
    public const ushort CM_RUN           = 1005; // 角色奔跑 (跨格)
    public const ushort CM_HIT           = 1006; // 角色物理攻击
    public const ushort CM_SPELL         = 3017; // 角色主动施放技能法术
    public const ushort CM_SAY           = 1007; // 玩家发言广播 (经典网关兼容)
    public const ushort CM_SAY_OPENMIR2  = 3030; // 玩家发言广播 (OpenMir2 标准命令)
    public const ushort CM_DROPITEM      = 1000; // 丢弃物品
    public const ushort CM_PICKUP        = 1001; // 拾取地面物品
    public const ushort CM_TAKEONITEM    = 1003; // 穿戴装备
    public const ushort CM_TAKEOFFITEM   = 1004; // 脱下装备
    public const ushort CM_EAT           = 1006; // 消耗药水或物品
    public const ushort CM_BUTCH         = 1007; // 挖肉/搜寻
    public const ushort CM_CLICKNPC      = 1010; // 点击 NPC 对话
    public const ushort CM_MERCHANTDLGSELECT = 1011; // 选择 NPC 对话脚本标签
    public const ushort CM_USERSELLITEM  = 1013; // 出售物品给 NPC
    public const ushort CM_USERBUYITEM   = 1014; // 从 NPC 购买物品
    public const ushort CM_USERREPAIRITEM= 1023; // NPC 修理装备
    public const ushort CM_GROUPMODE     = 1019; // 允许/禁止组队模式开关 (Param: 1=允许, 0=禁止)
    public const ushort CM_CREATEGROUP   = 1020; // 创建队伍并邀请首位成员
    public const ushort CM_ADDGROUPMEMBER= 1021; // 邀请添加队伍成员
    public const ushort CM_DELGROUPMEMBER= 1022; // 移出队员或主动退队
    public const ushort CM_DEALTRY       = 1025; // 玩家发起面对面交易
    public const ushort CM_DEALADDITEM   = 1026; // 放入交易物品 (Param: MakeIndex)
    public const ushort CM_DEALDELITEM   = 1027; // 撤回交易物品 (Param: MakeIndex)
    public const ushort CM_DEALCANCEL    = 1028; // 取消交易
    public const ushort CM_DEALCHGGOLD   = 1029; // 调整交易金币数 (Param: 金币数)
    public const ushort CM_DEALEND       = 1030; // 锁定交易并确认成交
    public const ushort CM_USERSTORAGEITEM = 1031; // 请求存入物品至仓库 (Recog: NPC Id, Param/Tag: MakeIndex 低高字, Payload: 物品名)
    public const ushort CM_USERTAKEBACKSTORAGEITEM = 1032; // 请求从仓库取回物品 (Recog: NPC Id, Param/Tag: MakeIndex 低高字, Payload: 物品名)
    public const ushort CM_OPENGUILDDLG  = 1035; // 请求打开行会主面板
    public const ushort CM_GUILDHOME     = 1036; // 查询行会主页信息
    public const ushort CM_GUILDMEMBERLIST = 1037; // 请求行会成员名册与职务列表
    public const ushort CM_GUILDADDMEMBER= 1038; // 招募新成员入会 (Payload: 目标人名)
    public const ushort CM_GUILDDELMEMBER= 1039; // 逐出成员或退出行会 (Payload: 目标人名)
    public const ushort CM_GUILDUPDATENOTICE = 1040; // 修改行会公告 (Payload: 公告内容)
    public const ushort CM_GUILDUPDATERANKINFO = 1041; // 更新行会职务头衔设置
    public const ushort CM_GUILDWAR      = 1042; // 申请开启沙巴克攻城战 / 行会宣战请求 (Payload: 目标行会名)
    public const ushort CM_CASTLEWITHDRAWGOLD = 1043; // 沙城城主提取城池税金 (Recog: 提款金额)
    public const ushort CM_IDPASSWORD    = 2001; // 账号密码登录 (LoginGate 7000)
    public const ushort CM_ADDNEWUSER    = 2002; // 账号注册
    public const ushort CM_USERLOGON     = 2003; // 用户登录确认

    // 服务端发往客户端的响应与世界广播 (SM_)
    public const ushort SM_SPELL                = 17;   // 实体施放技能法术广播 (Recog=ActorId, Param=TargetX, Tag=TargetY, Series=MagicId)
    public const ushort SM_LOGON                = 50;   // 登录进入世界 (下发玩家属性)
    public const ushort SM_TURN                 = 51;   // 实体转身
    public const ushort SM_WALK                 = 52;   // 实体走步
    public const ushort SM_RUN                  = 53;   // 实体奔跑
    public const ushort SM_HIT                  = 54;   // 实体攻击
    public const ushort SM_POWERHIT             = 55;   // 强力攻击
    public const ushort SM_STRUCK               = 56;   // 实体受击硬直与掉血
    public const ushort SM_DEATH                = 57;   // 实体死亡倒地
    public const ushort SM_SKELETON             = 58;   // 实体遗骸化
    public const ushort SM_DISAPPEAR            = 59;   // 实体离开视野或消失
    public const ushort SM_DROPITEM_SUCCESS     = 600;  // 丢弃物品成功确认
    public const ushort SM_DROPITEM_FAIL        = 601;  // 丢弃物品失败
    public const ushort SM_ITEMSHOW             = 610;  // 地面生成物品广播 (坐标/造型/名称)
    public const ushort SM_ITEMHIDE             = 611;  // 地面物品消失/被拾取 (RecogId)
    public const ushort SM_DROPITEM             = 610;  // 兼容别名
    public const ushort SM_ITEMDISAPPEAR        = 611;  // 兼容别名
    public const ushort SM_FEATURECHANGED      = 41;   // 角色纸娃娃外观发生改变
    public const ushort SM_SPURN                = 510;  // 刷新怪物或NPC实体
    public const ushort SM_NOWACTOR             = 511;  // 刷新其他玩家角色实体
    public const ushort SM_ADDITEM              = 200;  // 背包新增单件物品
    public const ushort SM_BAGITEMS             = 201;  // 下发背包物品清单
    public const ushort SM_DELITEM              = 202;  // 背包删除单件物品 (Recog=MakeIndex)
    public const ushort SM_UPDATEITEM           = 203;  // 背包物品更新 (持久度/数量)
    public const ushort SM_TAKEON_OK            = 615;  // 穿戴装备成功
    public const ushort SM_TAKEON_FAIL          = 616;  // 穿戴装备失败
    public const ushort SM_TAKEOFF_OK           = 619;  // 脱下装备成功
    public const ushort SM_TAKEOFF_FAIL         = 620;  // 脱下装备失败
    public const ushort SM_SENDUSEITEMS         = 621;  // 下发身上已穿戴装备列表
    public const ushort SM_WEIGHTCHANGED        = 622;  // 负重/背包重量改变
    public const ushort SM_EAT_OK               = 635;  // 物品使用成功
    public const ushort SM_EAT_FAIL             = 636;  // 物品使用失败
    public const ushort SM_MAGICFIRE            = 638;  // 法术命中与伤害结算 (Recog=TargetId, Param=Damage, Tag=MagicId)
    public const ushort SM_MAGICFIREFAIL        = 639;  // 法术释放失败
    public const ushort SM_ADDMAGIC             = 210;  // 习得新技能下发
    public const ushort SM_SENDMYMAGIC          = 211;  // 下发角色已掌握法术列表
    public const ushort SM_MERCHANTSAY          = 643;  // NPC 对话正文与脚本选项下发
    public const ushort SM_MERCHANTDLGCLOSE     = 644;  // 关闭 NPC 对话框
    public const ushort SM_SENDGOODSLIST        = 645;  // NPC 商品货架列表
    public const ushort SM_SENDUSERSELL         = 646;  // NPC 回购物品视窗
    public const ushort SM_SENDUSERREPAIR       = 668;  // NPC 特修/普修视窗
    public const ushort SM_QUERYCHR             = 520;  // 下发角色列表
    public const ushort SM_NEWCHR_SUCCESS       = 521;  // 创建角色成功
    public const ushort SM_NEWCHR_FAIL          = 522;  // 创建角色失败
    public const ushort SM_STARTPLAY            = 525;  // 选角成功，开始进入游戏网关
    public const ushort SM_QUERYCHR_FAIL        = 527;  // 查询角色失败
    public const ushort SM_PASSOK_SELECTSERVER  = 529;  // 密码验证通过，请求选择服务器
    public const ushort SM_HEAR                 = 40;   // 收到普通聊天发言广播 (Recog=ActorId)
    public const ushort SM_HEAR_LEGACY          = 530;  // 兼容老网关发言命令
    public const ushort SM_SYSMESSAGE           = 100;  // 系统广播/公告/提示信息
    public const ushort SM_GROUPMESSAGE         = 101;  // 组队频道聊天消息
    public const ushort SM_CRY                  = 102;  // 喊话频道广播
    public const ushort SM_WHISPER              = 103;  // 私聊/密语消息
    public const ushort SM_GUILDMESSAGE         = 104;  // 行会频道聊天消息
    public const ushort SM_GROUPMODECHANGED     = 659;  // 组队模式切换通知 (Param: 1=允许, 0=禁止)
    public const ushort SM_CREATEGROUP_OK       = 660;  // 创建队伍成功
    public const ushort SM_CREATEGROUP_FAIL     = 661;  // 创建队伍失败 (Recog: 错误代码)
    public const ushort SM_GROUPADDMEM_OK       = 662;  // 添加队伍成员成功
    public const ushort SM_GROUPDELMEM_OK       = 663;  // 删除/离开队伍成功
    public const ushort SM_GROUPADDMEM_FAIL     = 664;  // 添加队伍成员失败
    public const ushort SM_GROUPDELMEM_FAIL     = 665;  // 删除队伍成员失败
    public const ushort SM_GROUPCANCEL          = 666;  // 队伍解散
    public const ushort SM_GROUPMEMBERS         = 667;  // 队伍全员名单同步 (Payload: Name1/Name2/.../)
    public const ushort SM_DEALMENU             = 673;  // 打开交易双栏视窗 (Payload: 对方角色名)
    public const ushort SM_DEALTRY_FAIL         = 674;  // 发起交易失败 (距离太远/对方正忙/未开启交易)
    public const ushort SM_DEALADDITEM_OK       = 675;  // 本地放入物品成功
    public const ushort SM_DEALADDITEM_FAIL     = 676;  // 本地放入物品失败
    public const ushort SM_DEALDELITEM_OK       = 677;  // 本地撤回物品成功
    public const ushort SM_DEALDELITEM_FAIL     = 678;  // 本地撤回物品失败
    public const ushort SM_DEALCANCEL           = 681;  // 交易取消中断 (物品原样返还)
    public const ushort SM_DEALREMOTEADDITEM    = 682;  // 对方放入物品同步
    public const ushort SM_DEALREMOTEDELITEM    = 683;  // 对方撤回物品同步
    public const ushort SM_DEALCHGGOLD_OK       = 684;  // 本地金币调整成功 (Recog: 交易金币数)
    public const ushort SM_DEALCHGGOLD_FAIL     = 685;  // 本地金币调整失败
    public const ushort SM_DEALREMOTECHGGOLD    = 686;  // 对方金币调整同步 (Recog: 对方金币数)
    public const ushort SM_DEALSUCCESS          = 687;  // 交易锁定并成交
    public const ushort SM_SENDUSERSTORAGEITEM       = 700;  // 打开仓库保管存入界面 (Recog: NPC Id)
    public const ushort SM_STORAGE_OK                = 701;  // 物品存入仓库成功
    public const ushort SM_STORAGE_FULL              = 702;  // 仓库已满提示
    public const ushort SM_STORAGE_FAIL              = 703;  // 物品存入仓库失败
    public const ushort SM_SAVEITEMLIST              = 704;  // 下发仓库物品名册 (Payload 为多个 ClientItem)
    public const ushort SM_TAKEBACKSTORAGEITEM_OK    = 705;  // 取回物品成功 (Recog: MakeIndex)
    public const ushort SM_TAKEBACKSTORAGEITEM_FAIL  = 706;  // 取回物品失败
    public const ushort SM_TAKEBACKSTORAGEITEM_FULLBAG = 707; // 取回失败：包裹已满或超重
    public const ushort SM_OPENGUILDDLG         = 753;  // 打开行会面板成功 (Payload: 行会名与会长权限等)
    public const ushort SM_OPENGUILDDLG_FAIL    = 754;  // 打开行会失败 (未加入任何行会)
    public const ushort SM_SENDGUILDMEMBERLIST  = 756;  // 下发全行会职务层级与成员名册 (Payload: #RankNo/*RankName/Member1/.../)
    public const ushort SM_GUILDADDMEMBER_OK    = 757;  // 招募成员入会成功
    public const ushort SM_GUILDADDMEMBER_FAIL  = 758;  // 招募成员失败 (Recog: 错误代码)
    public const ushort SM_GUILDDELMEMBER_OK    = 759;  // 移出或退出行会成功
    public const ushort SM_GUILDDELMEMBER_FAIL  = 760;  // 移出或退出行会失败
    public const ushort SM_GUILDRANKUPDATE_FAIL = 761;  // 职务信息更新失败
    public const ushort SM_CASTLE_GOLD          = 762;  // 下发沙巴克城池当前金币与税收总额 (Recog: 金币数)
    public const ushort SM_SABUK_WALLSTATUS     = 763;  // 沙巴克城门与工事防御状态广播 (Recog: 破损等级, Param: 残余HP%, Tag: 1=开启/0=关闭)
    public const ushort SM_CASTLE_OWNER         = 764;  // 沙巴克霸主行会与掌门人全服广播 (Payload: 行会名/掌门人名/占领日期)
    public const ushort SM_WARSTATUS            = 765;  // 沙巴克攻城战阶段状态全服同步 (Recog: 阶段代码 0~3, Param: 剩余时长秒数)
    public const ushort SM_CHANGEMAP            = 634;  // 切换地图 (下发地图名与坐标)
}
