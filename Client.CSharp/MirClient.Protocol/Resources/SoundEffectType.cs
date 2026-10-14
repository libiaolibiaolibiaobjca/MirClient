namespace MirClient.Protocol.Resources;

/// <summary>
/// 盛大热血传奇经典音效类型枚举
/// </summary>
public enum SoundEffectType
{
    AttackSwing = 1,   // 挥刀破风声
    Thunder = 2,       // 雷电术轰鸣
    Fireball = 3,      // 大火球呼啸爆裂
    FireSword = 4,     // 烈火剑法烈焰呼啸
    Heal = 5,          // 神圣治愈术圣光
    MonsterStruck = 6, // 怪物受击吼叫
    MonsterDie = 7,    // 怪物阵亡哀鸣 (白野猪惨嚎)
    PlayerStruck = 8,  // 玩家受击闷哼
    PickupGold = 9,    // 拾取金币清脆叮当声
    PickupItem = 10,   // 拾取装备物品咔哒声
    UsePotion = 11,    // 喝药水咕嘟声
    LevelUp = 12,      // 突破境界升级号角
    ButtonClick = 13   // 界面圆钮点击声
}
