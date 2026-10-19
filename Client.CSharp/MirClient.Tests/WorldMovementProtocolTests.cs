using System;
using MirClient.Protocol;
using Xunit;

namespace MirClient.Tests;

/// <summary>
/// 验证完全对齐 OpenMir2 的世界移动、朝向与视野协议常数及封包解析
/// </summary>
public class WorldMovementProtocolTests
{
    [Fact]
    public void Test_ProtocolConsts_AlignWithOpenMir2Messages()
    {
        // 核心移动与动作指令完全对齐 OpenMir2 Messages.cs
        Assert.Equal(10, ProtocolConsts.SM_TURN);
        Assert.Equal(11, ProtocolConsts.SM_WALK);
        Assert.Equal(12, ProtocolConsts.SM_SITDOWN);
        Assert.Equal(13, ProtocolConsts.SM_RUN);
        Assert.Equal(14, ProtocolConsts.SM_HIT);
        Assert.Equal(30, ProtocolConsts.SM_DISAPPEAR);
        Assert.Equal(50, ProtocolConsts.SM_LOGON);
        Assert.Equal(51, ProtocolConsts.SM_NEWMAP);
        Assert.Equal(634, ProtocolConsts.SM_CHANGEMAP);

        // 客户端上行控制指令
        Assert.Equal(3010, ProtocolConsts.CM_TURN);
        Assert.Equal(3011, ProtocolConsts.CM_WALK);
        Assert.Equal(3013, ProtocolConsts.CM_RUN);
        Assert.Equal(3014, ProtocolConsts.CM_HIT);
        Assert.Equal(3012, ProtocolConsts.CM_SITDOWN);
    }

    [Fact]
    public void Test_EncodeAndDecode_MovePacket()
    {
        int targetX = 330;
        int targetY = 268;
        int dir = 4;

        // 模拟生成 CM_WALK / CM_RUN 包
        DefaultMessage walkMsg = new(
            ident: ProtocolConsts.CM_WALK,
            recog: 0,
            param: (ushort)targetX,
            tag: (ushort)targetY,
            series: (ushort)dir
        );

        string encoded = EDCode.EncodeMessage(walkMsg);
        Assert.NotEmpty(encoded);

        DefaultMessage decoded = EDCode.DecodeMessage(encoded);
        Assert.Equal(ProtocolConsts.CM_WALK, decoded.Ident);
        Assert.Equal((ushort)targetX, decoded.Param);
        Assert.Equal((ushort)targetY, decoded.Tag);
        Assert.Equal((ushort)dir, decoded.Series);
    }

    [Fact]
    public void Test_WorldBroadcast_EntityTurnAndAppear()
    {
        // 模拟服务端下发 SM_TURN 广播（OpenMir2 视野生成机制：实体初次入视野时广播 SM_TURN）
        int entityActorId = 998877;
        int posX = 335;
        int posY = 270;
        int dir = 2;
        string entityName = "半兽勇士";

        DefaultMessage turnMsg = new(
            ident: ProtocolConsts.SM_TURN,
            recog: entityActorId,
            param: (ushort)posX,
            tag: (ushort)posY,
            series: (ushort)dir
        );

        string rawPayload = EDCode.EncodeString(entityName);
        string packetStr = EDCode.MakePacket(turnMsg, rawPayload);

        string content = packetStr.Trim('#', '!');
        DefaultMessage parsedHeader = EDCode.DecodeMessage(content[..16]);
        string parsedPayload = content[16..];

        Assert.Equal(ProtocolConsts.SM_TURN, parsedHeader.Ident);
        Assert.Equal(entityActorId, parsedHeader.Recog);
        Assert.Equal(posX, parsedHeader.Param);
        Assert.Equal(posY, parsedHeader.Tag);
        Assert.Equal(dir, parsedHeader.Series & 0x07);

        string decodedName = EDCode.DecodeText(parsedPayload);
        Assert.Equal(entityName, decodedName);
    }

    [Fact]
    public void Test_WorldBroadcast_Disappear()
    {
        // 模拟实体离开视野广播 SM_DISAPPEAR
        int entityActorId = 998877;
        DefaultMessage disMsg = new(
            ident: ProtocolConsts.SM_DISAPPEAR,
            recog: entityActorId,
            param: 0,
            tag: 0,
            series: 0
        );

        string packetStr = EDCode.MakePacket(disMsg, "");
        string content = packetStr.Trim('#', '!');
        DefaultMessage parsedHeader = EDCode.DecodeMessage(content[..16]);
        Assert.Equal(ProtocolConsts.SM_DISAPPEAR, parsedHeader.Ident);
        Assert.Equal(entityActorId, parsedHeader.Recog);
    }

    [Fact]
    public void Test_WorldBroadcast_MapDescriptionAndAbility()
    {
        // 1. 验证地图描述信息 SM_MAPDESCRIPTION (54)
        string mapName = "盟重省";
        DefaultMessage mapDescMsg = new(
            ident: ProtocolConsts.SM_MAPDESCRIPTION,
            recog: -1,
            param: 0,
            tag: 0,
            series: 0
        );
        string mapPacket = EDCode.MakePacket(mapDescMsg, EDCode.EncodeString(mapName));
        string mapContent = mapPacket.Trim('#', '!');
        DefaultMessage parsedMapHdr = EDCode.DecodeMessage(mapContent[..16]);
        string decodedMapName = EDCode.DecodeText(mapContent[16..]);
        Assert.Equal(ProtocolConsts.SM_MAPDESCRIPTION, parsedMapHdr.Ident);
        Assert.Equal(mapName, decodedMapName);

        // 2. 验证玩家属性下发 SM_ABILITY (52)
        int gold = 88888;
        int job = 0; // 战士
        DefaultMessage abilMsg = new(
            ident: ProtocolConsts.SM_ABILITY,
            recog: gold,
            param: (ushort)job,
            tag: 0,
            series: 0
        );
        string abilPacket = EDCode.MakePacket(abilMsg, "");
        string abilContent = abilPacket.Trim('#', '!');
        DefaultMessage parsedAbilHdr = EDCode.DecodeMessage(abilContent[..16]);
        Assert.Equal(ProtocolConsts.SM_ABILITY, parsedAbilHdr.Ident);
        Assert.Equal(gold, parsedAbilHdr.Recog);
        Assert.Equal(job, parsedAbilHdr.Param & 0xFF);

        // 3. 验证角色名称下发 SM_USERNAME (42)
        string chrName = "gm01";
        int chrActorId = 1234567;
        DefaultMessage nameMsg = new(
            ident: ProtocolConsts.SM_USERNAME,
            recog: chrActorId,
            param: 255, // 名字颜色
            tag: 0,
            series: 0
        );
        string namePacket = EDCode.MakePacket(nameMsg, EDCode.EncodeString(chrName));
        string nameContent = namePacket.Trim('#', '!');
        DefaultMessage parsedNameHdr = EDCode.DecodeMessage(nameContent[..16]);
        string decodedChrName = EDCode.DecodeText(nameContent[16..]);
        Assert.Equal(ProtocolConsts.SM_USERNAME, parsedNameHdr.Ident);
        Assert.Equal(chrActorId, parsedNameHdr.Recog);
        Assert.Equal(chrName, decodedChrName);
    }
}

