# OpenSpec 02: 盛大十周年素材 (WIL) 与地图 (MAP) 二进制规范

## 1. 规范定位
本规范定义客户端对盛大《热血传奇》十周年客户端中 `.wil` 图像素材文件与 `.map` 地图瓦片文件的解析标准，保证 C# 客户端能够直接加载十周年原版 `Data/` 和 `Map/` 目录中的全部资源。

---

## 2. WIL 图像素材文件二进制结构

每个 `.wil` 文件通常配对一个同名的 `.wix` 索引文件（或内部自带偏移表）：

### 2.1 文件头结构 (`TWMImageHeader`, 56 字节)

| 偏移量 | 字段名 | 类型 | 长度 (Bytes) | 含义与说明 |
| :---: | :--- | :--- | :---: | :--- |
| `0x00` | `Title` | `string[40]` (定长字符) | 41 | 格式标识字符串（如 `"ILIB v1.0-WEMADE Entertainment inc."`） |
| `0x29` | `ImageCount` | `int32` | 4 | 文件中包含的图片帧总数 |
| `0x2D` | `ColorCount` | `int32` | 4 | 调色板颜色数 (通常为 256) |
| `0x31` | `PaletteSize`| `int32` | 4 | 调色板字节大小 (通常为 256 * 4 = 1024) |
| `0x35` | `VerFlag` | `int32` | 4 | 版本标志位 |

### 2.2 调色板 (`Palette`, 1024 字节)
紧随文件头之后，为 256 色 RGBQUAD 数组（每项为 B, G, R, Reserved 4 字节）。

### 2.3 索引偏移表 (`IndexTable`)
- 若存在 `.wix` 文件：由 4 字节整数数组组成，记录每帧图片在 `.wil` 文件中的绝对字节偏移量。
- 若单 `.wil` 文件：紧跟调色板之后为 `ImageCount * 4` 字节的偏移表。

### 2.4 单帧图像信息结构 (`TWMImageInfo`, 8 字节 + 图像数据)

| 偏移量 | 字段名 | 类型 | 长度 (Bytes) | 含义与说明 |
| :---: | :--- | :--- | :---: | :--- |
| `0x00` | `Width` | `uint16` | 2 | 图像宽度（像素） |
| `0x02` | `Height` | `uint16` | 2 | 图像高度（像素） |
| `0x04` | `Px` | `int16` | 2 | 渲染锚点 X 偏移 (Pivot X) |
| `0x06` | `Py` | `int16` | 2 | 渲染锚点 Y 偏移 (Pivot Y) |

紧接着为像素数据：
- **8位色**：大小为 `Width * Height` 字节，每个字节对应调色板索引（0 为透明色）。
  - **16位色 (RGB565，1:1 溯源 Delphi 原版 `Source/SceneUI/uCommon.pas:505-516` `Pixel565To32`)**：盛大十周年核心 16 位格式采用标准 RGB565（高 5 位 bits 11..15 为 Red，中间 6 位 bits 5..10 为 Green，低 5 位 bits 0..4 为 Blue）。
  - **自底向上翻转 (MirrorY = True)**：盛大十周年位图行按 Windows DIB/BMP 标准自底向上存储，目标行映射为 `targetY = height - 1 - y`（严格对齐 Delphi `uGameEngine.pas:1355`），以确保建筑物、树木与路灯正立。
  - **显卡原生 RGBA 排布**：内存字节序列对齐 `[Byte 0: Red, Byte 1: Green, Byte 2: Blue, Byte 3: Alpha]`：
  ```csharp
  // 严格遵循 Delphi 原版 RGB565 二进制规范与 Raylib UncompressedR8G8B8A8 物理内存排布
  uint r = (uint)(((color >> 11) & 0x1F) * 255 / 31);
  uint g = (uint)(((color >> 5) & 0x3F) * 255 / 63);
  uint b = (uint)((color & 0x1F) * 255 / 31);
  // 小端序内存排布 [Byte 0: r, Byte 1: g, Byte 2: b, Byte 3: a]
  rgba[dstRowOffset + x] = ((uint)alpha << 24) | (b << 16) | (g << 8) | r;
  ```

---

## 3. MAP 地图文件二进制结构

盛大传奇地图格式是一个规则的二维网格，逻辑尺寸由文件头指定。

### 3.1 地图文件头 (`TMapHeader`, 52 字节 packed)

| 偏移量 | 字段名 | 类型 | 长度 (Bytes) | 含义与说明 |
| :---: | :--- | :--- | :--- | :--- |
| `0x00` | `Width` | `uint16` | 2 | 地图总宽度（瓦片格点数） |
| `0x02` | `Height` | `uint16` | 2 | 地图总高度（瓦片格点数） |
| `0x04` | `Title` | `char[16]` | 16 | 地图标识/标题 |
| `0x14` | `UpdateDate` | `double` (TDateTime)| 8 | Delphi TDateTime 日期时间戳 |
| `0x1C` | `Reserved` | `byte[24]` | 24 | 保留字段 |

### 3.2 单元格瓦片结构 (`MapCell`)

地图主体为由 `Width * Height` 个单元格组成的连续数组，按 **先 X 后 Y**（逐列或逐行）排列。

#### 经典 1.76 / 1.80 老地图单元格 (12 字节 packed):

| 偏移量 | 字段名 | 类型 | 长度 | 关键掩码与规则 |
| :---: | :--- | :--- | :---: | :--- |
| `0x00` | `BkImg` | `uint16` | 2 | 背景大瓦片编号。**最高位 `0x8000` 置 1 表示不能行走（障碍物）** |
| `0x02` | `MidImg`| `uint16` | 2 | 中间层瓦片编号。最高位 `0x8000` 置 1 表示有障碍物 |
| `0x04` | `FrImg` | `uint16` | 2 | 前景物体建筑编号。最高位 `0x8000` 置 1 表示遮挡 |
| `0x06` | `DoorIndex` | `byte` | 1 | 门对象索引 |
| `0x07` | `DoorOffset`| `byte` | 1 | 门状态偏移 (打开/关闭) |
| `0x08` | `AniFrame` | `byte` | 1 | 动画帧数 (水流、火把等动态瓦片)。**最高位 `0x80` 表示发光混合图** |
| `0x09` | `AniTick` | `byte` | 1 | 动画跳帧间隔 |
| `0x0A` | `Area` | `byte` | 1 | 区域类型 |
| `0x0B` | `Light` | `byte` | 1 | 光照等级 (0~15) |

#### 扩展新地图单元格 (14 字节 packed):
在 12 字节基础上，末尾增加 2 个字节：
- `+0x0C`：`TileIndex` (`byte`) —— 背景瓦片所属的扩展补丁文件编号（Tiles2, Tiles3 等）。
- `+0x0D`：`SmTileIndex` (`byte`) —— 前景/中景所属的扩展补丁文件编号（SmTiles2, Objects2 等）。

---

## 4. 坐标系与瓦片渲染数学规则

- **坐标系**：等轴测斜 45 度投影。
- **大瓦片 (Tiles)**：每个瓦片跨度为 `2 x 2` 逻辑格点，像素尺寸通常为 `96 x 68` 或 `48 x 32`。
- **1-based 索引规范**：Delphi 原版中 `BkImg`、`MidImg`、`FrImg` 存储的是 1-based 序号，获取图库纹理前必须执行 `- 1`：
  ```csharp
  int tileIndex = cleanBkIndex - 1;
  int smTileIndex = cleanMidIndex - 1;
  int objIndex = cleanFrIndex - 1;
  ```
- **光照与加法混合规范**：
  若单元格标记 `(cell.AniFrame & 0x80) != 0`，代表该贴图为路灯/火把/光晕特效，必须使用 `BlendMode.BLEND_ADDITIVE` 加法混合渲染，避免黑底光晕掩盖地貌。
- **障碍物碰撞检测**：
  判断坐标 `(x, y)` 能否通过：
  ```csharp
  bool isBlocked = (cell.BkImg & 0x8000) != 0 || (cell.MidImg & 0x8000) != 0;
  ```
