# MirClient.CSharp (OpenMir2 跨平台客户端)

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Spec-Driven](https://img.shields.io/badge/Standard-OpenSpec-brightgreen.svg)](specs/)

基于 **.NET 8 (C#)** 重构的现代化《热血传奇》跨平台游戏客户端。  
本项目采用 **OpenSpec（规范驱动开发）** 模式，以原版 Delphi 客户端源码为权威蓝本进行分层移植，实现对 **[OpenMir2](https://github.com/mirbeta/OpenMir2)** 服务端与**盛大十周年客户端素材包**的 100% 二进制兼容。

---

## 目录索引

- [一、 项目背景与架构设计](#一-项目背景与架构设计)
- [二、 目录结构体系](#二-目录结构体系)
- [三、 OpenSpec 规范体系](#三-openspec-规范体系)
- [四、 容器化开发与构建指南](#四-容器化开发与构建指南)
- [五、 跨平台独立二进制发布 (Windows / Mac)](#五-跨平台独立二进制发布-windows--mac)
- [六、 联调配置指南](#六-联调配置指南)

---

## 一、 项目背景与架构设计

### 1. 为什么重构？
原版传奇客户端（Delphi XE 开发，26.5 万行代码）存在以下痛点：
- 强绑定 Windows GDI、Delphi VCL 窗体与 x86 内嵌汇编。
- 依赖十多个历史遗留第三方组件包，环境搭建极其繁琐，无法跨平台维护。

### 2. 核心架构（分层精准移植）

```
┌────────────────────────────────────────────────────────┐
│               MirClient.Runner / GUI (呈现层)           │
│   - 控制台联调测试器 (MVP)                              │
│   - 跨平台 2D 游戏视口 (Raylib / 独立桌面窗口)           │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────┴─────────────────────────────┐
│             MirClient.Protocol (协议与数据层)           │
│   - EDCode: 严格对应原版 6-Bit 编解码算法 (GBK 代码页 936)│
│   - DefaultMessage: 12 字节 packed 包头内存模型         │
│   - ProtocolConsts: 严格对齐 Grobal2.pas 命令字         │
│   - Network/MirConnection: 异步 Socket 高性能客户端引擎  │
│   - Resources/WilLoader: 盛大十周年 WIL 序列帧解析器    │
│   - Resources/MapLoader: 盛大十周年 MAP 瓦片网格解析器  │
└────────────────────────────────────────────────────────┘
```

---

## 二、 目录结构体系

```text
├── specs/                   # OpenSpec 标准规范文档中心
│   ├── 01-edcode-protocol.md# 6-Bit 编码与封包帧规范
│   ├── 02-resource-formats.md# 十周年 WIL 素材与 MAP 地图二进制规范
│   └── 03-input-and-gameflow.md# 业务生命周期与操作输入快捷键规范
├── Client.CSharp/           # .NET 8 客户端主工程源码
│   ├── MirClient.sln        # Visual Studio / Rider 解决方案
│   ├── MirClient.Protocol/  # 核心协议、编解码与素材资源类库
│   ├── MirClient.Tests/     # 自动化测试工程 (xUnit，覆盖编解码与素材加载)
│   └── MirClient.Runner/    # 控制台/客户端启动入口
├── build.sh                 # 跨平台容器化一键构建、测试与发布脚本
└── README.md                # 本文档
```

---

## 三、 OpenSpec 规范体系

本项目坚持 **“规范先行，代码对齐”** 的原则，所有模块均先在 `specs/` 目录下确立技术规范：

- [OpenSpec 01: 6-Bit 编码与网络封包规范](specs/01-edcode-protocol.md)
  - 异或种子 `0xAC`、ASCII 偏移 `0x3C`、4:3 字节扩展算法。
  - 全局强制采用 **GBK (代码页 936)** 字符集。
  - `#<16B EncodedMsg><Payload>!` 封包帧格式与分包机制。
- [OpenSpec 02: 素材与地图二进制规范](specs/02-resource-formats.md)
  - 十周年 `.wil` 图像头（56B）、调色板（1024B）与序列帧像素解码。
  - 十周年 `.map` 地图头（52B）、12B/14B 瓦片网格与障碍物碰撞检测。
- [OpenSpec 03: 业务生命周期与操作输入规范](specs/03-input-and-gameflow.md)
  - 登录握手与角色查询状态机。
  - 经典 `F1`~`F12`、鼠标右键跑动、`Shift` 强攻等全量快捷键映射。

---

## 四、 容器化开发与构建指南

本项目推行 **宿主机零污染（Container-First）** 开发模式。宿主机（macOS / Linux）无需安装 .NET SDK，仅需 `podman` 或 `docker` 即可完成全部工作。

### 1. 编译项目
```bash
./build.sh build
```

### 2. 运行自动化测试
```bash
./build.sh test
```

### 3. 运行网络联调测试
```bash
./build.sh run
```

---

## 五、 跨平台独立二进制发布 (Windows / Mac)

借助 .NET 8 的跨平台交叉编译能力，容器可在数十秒内直接输出**自带运行时、无需在目标机器上安装任何依赖的单文件免安装程序**：

### 1. 发布 Windows 独立运行程序 (.exe)
适合拷到 Windows 电脑上直接双击运行：
```bash
./build.sh publish-win
```
- **输出产物**：`bin/win-x64/MirClient.Runner.exe`
- **特性**：单文件打包，目标 Windows 系统**无需安装任何 .NET 运行时**。

### 2. 发布 macOS 原生独立运行程序
```bash
./build.sh publish-osx
```
- **输出产物**：`bin/osx-x64/MirClient.Runner`

---

## 六、 联调配置指南

默认目标服务端预设配置：
- **服务端主机 IP**：`192.168.0.103`
- **登录网关端口**：`7000` (LoginGate)

启动程序时如需指定其他服务器或端口，直接传入参数即可：
```bash
# 格式: MirClient.Runner <ServerIP> <ServerPort>
./MirClient.Runner.exe 192.168.0.103 7000
```
