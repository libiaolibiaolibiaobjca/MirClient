using System;
using System.Collections.Generic;

namespace MirClient.Protocol.Resources;

/// <summary>
/// 仓库中保管的物品实体快照 (对齐 OpenMir2 StorageItemList)
/// </summary>
public record StorageItemRecord(
    int MakeIndex,
    string Name,
    int Dura = 1000,
    int DuraMax = 1000,
    int Weight = 1,
    int Price = 0);

/// <summary>
/// 仓库存取操作回执结果
/// </summary>
public record StorageActionResult(
    bool Success,
    int MakeIndex,
    string Message,
    ushort StatusIdent);

/// <summary>
/// OpenMir2 经典传奇仓库与保管员网络存取协议管理器 (对齐 PlayObject.Operate.cs 与 Messages.cs)
/// </summary>
public static class StorageProtocolManager
{
    /// <summary>
    /// 将服务端返回的仓库错误消息状态转为可读中文信息
    /// </summary>
    public static string GetStorageFailReason(ushort ident)
    {
        return ident switch
        {
            ProtocolConsts.SM_STORAGE_FULL => "您的个人仓库已经满了，不能再保管任何东西了",
            ProtocolConsts.SM_STORAGE_FAIL => "无法保管该物品，请确认是否为可保管物品或仓库已加锁",
            ProtocolConsts.SM_TAKEBACKSTORAGEITEM_FULLBAG => "您的包裹已满或负重超限，无法容纳该物品",
            ProtocolConsts.SM_TAKEBACKSTORAGEITEM_FAIL => "取回物品失败，可能密码尚未解锁或物品不存在",
            _ => $"仓库操作失败 (状态码: {ident})"
        };
    }

    /// <summary>
    /// 构造请求存入物品至仓库数据包 (CM_USERSTORAGEITEM = 1031)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateSaveStorageItemPacket(int merchantId, int makeIndex, string itemName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_USERSTORAGEITEM,
            recog: merchantId,
            param: (ushort)(makeIndex & 0xFFFF),
            tag: (ushort)((makeIndex >> 16) & 0xFFFF),
            series: 0);

        string payload = EDCode.EncodeString(itemName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 构造请求从仓库取回物品数据包 (CM_USERTAKEBACKSTORAGEITEM = 1032)
    /// </summary>
    public static (DefaultMessage Header, string EncodedPayload) CreateTakeBackStorageItemPacket(int merchantId, int makeIndex, string itemName)
    {
        DefaultMessage header = new(
            ident: ProtocolConsts.CM_USERTAKEBACKSTORAGEITEM,
            recog: merchantId,
            param: (ushort)(makeIndex & 0xFFFF),
            tag: (ushort)((makeIndex >> 16) & 0xFFFF),
            series: 0);

        string payload = EDCode.EncodeString(itemName ?? string.Empty);
        return (header, payload);
    }

    /// <summary>
    /// 解析服务端下发的仓库物品列表封包 (SM_SAVEITEMLIST = 704)
    /// 支持格式:
    /// 1. 结构化文本分割格式: ItemName/MakeIndex/Dura/DuraMax/
    /// 2. 单项名称序列格式: Name1/Name2/.../
    /// 完美兼容明文输入与 EDCode 编码输入
    /// </summary>
    public static IReadOnlyList<StorageItemRecord> ParseStorageItemList(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return Array.Empty<StorageItemRecord>();
        }

        string text = rawBody;
        // 如果全是 ASCII 字符且不含中文字符，尝试进行 EDCode 解码
        if (!rawBody.Any(c => c > 127))
        {
            string decoded = EDCode.DecodeText(rawBody);
            if (!string.IsNullOrWhiteSpace(decoded) && decoded.Any(c => c > 127))
            {
                text = decoded;
            }
        }

        List<StorageItemRecord> items = new();
        string[] tokens = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return items;

        // 如果是 4 的倍数且第 2 项是数字，按结构化方式解析 [Name, MakeIndex, Dura, DuraMax]
        if (tokens.Length >= 4 && int.TryParse(tokens[1], out _))
        {
            for (int i = 0; i + 3 < tokens.Length; i += 4)
            {
                string name = tokens[i];
                int.TryParse(tokens[i + 1], out int makeIndex);
                int.TryParse(tokens[i + 2], out int dura);
                int.TryParse(tokens[i + 3], out int duraMax);

                items.Add(new StorageItemRecord(
                    MakeIndex: makeIndex,
                    Name: name,
                    Dura: dura <= 0 ? 1000 : dura,
                    DuraMax: duraMax <= 0 ? 1000 : duraMax
                ));
            }
        }
        else
        {
            // 单独名字列表形式 (以索引自增补全)
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrWhiteSpace(token))
                {
                    items.Add(new StorageItemRecord(
                        MakeIndex: 100001 + i,
                        Name: token,
                        Dura: 1000,
                        DuraMax: 1000
                    ));
                }
            }
        }

        return items;
    }

    /// <summary>
    /// 解析存入/取回结果
    /// </summary>
    public static StorageActionResult ParseActionResult(DefaultMessage header)
    {
        bool success = header.Ident is ProtocolConsts.SM_STORAGE_OK or ProtocolConsts.SM_TAKEBACKSTORAGEITEM_OK;
        int makeIndex = header.Recog;
        string msg = success
            ? (header.Ident == ProtocolConsts.SM_STORAGE_OK ? "物品已成功存入个人仓库。" : "物品已成功从仓库取回。")
            : GetStorageFailReason(header.Ident);

        return new StorageActionResult(success, makeIndex, msg, header.Ident);
    }
}
