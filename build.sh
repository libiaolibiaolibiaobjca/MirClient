#!/usr/bin/env bash
set -e

# ==============================================================================
# OpenMir2 C# 客户端容器化构建与运行脚本
# 镜像要求: mcr.microsoft.com/dotnet/sdk:8.0
# ==============================================================================

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOTNET_SDK_IMG="mcr.microsoft.com/dotnet/sdk:8.0"

# 检测容器引擎 (优先 podman，备选 docker)
if command -v podman &>/dev/null; then
  CONTAINER_CLI="podman"
elif command -v docker &>/dev/null; then
  CONTAINER_CLI="docker"
else
  echo "[错误] 未找到 podman 或 docker，请确保已安装容器引擎。"
  exit 1
fi

ACTION="${1:-build}"

case "$ACTION" in
  init)
    echo "[容器化] 初始化 .NET 8 工程解决方案..."
    $CONTAINER_CLI run --rm \
      -v "$PROJECT_ROOT":/workspace \
      -w /workspace/Client.CSharp \
      "$DOTNET_SDK_IMG" \
      bash -c "
        dotnet new sln -n MirClient --force
        dotnet new classlib -n MirClient.Protocol -f net8.0 --force
        dotnet new xunit -n MirClient.Tests -f net8.0 --force
        dotnet new console -n MirClient.Runner -f net8.0 --force
        dotnet sln add MirClient.Protocol/MirClient.Protocol.csproj
        dotnet sln add MirClient.Tests/MirClient.Tests.csproj
        dotnet sln add MirClient.Runner/MirClient.Runner.csproj
        dotnet add MirClient.Tests/MirClient.Tests.csproj reference MirClient.Protocol/MirClient.Protocol.csproj
        dotnet add MirClient.Runner/MirClient.Runner.csproj reference MirClient.Protocol/MirClient.Protocol.csproj
      "
    echo "[完成] .NET 8 工程初始化成功！"
    ;;

  build)
    echo "[容器化] 编译项目..."
    $CONTAINER_CLI run --rm \
      -v "$PROJECT_ROOT":/workspace \
      -w /workspace/Client.CSharp \
      "$DOTNET_SDK_IMG" \
      dotnet build MirClient.sln -c Release
    ;;

  test)
    echo "[容器化] 运行单元测试..."
    $CONTAINER_CLI run --rm \
      -v "$PROJECT_ROOT":/workspace \
      -w /workspace/Client.CSharp \
      "$DOTNET_SDK_IMG" \
      dotnet test MirClient.Tests/MirClient.Tests.csproj -c Release --logger "console;verbosity=normal"
    ;;

  run)
    echo "[容器化] 运行网络联调测试程序..."
    $CONTAINER_CLI run --rm -it \
      -v "$PROJECT_ROOT":/workspace \
      -w /workspace/Client.CSharp \
      "$DOTNET_SDK_IMG" \
      dotnet run --project MirClient.Runner/MirClient.Runner.csproj -c Release
    ;;

  publish-osx)
    echo "[容器化] 跨平台交叉编译为 macOS 原生独立免安装应用 (Self-Contained)..."
    $CONTAINER_CLI run --rm \
      -v "$PROJECT_ROOT":/workspace \
      -w /workspace/Client.CSharp \
      "$DOTNET_SDK_IMG" \
      dotnet publish MirClient.Runner/MirClient.Runner.csproj \
        -c Release \
        -r osx-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -o /workspace/bin/osx-x64
    echo "[完成] 已生成 macOS 原生可执行文件位于: bin/osx-x64/MirClient.Runner"
    ;;

  publish-win)
    echo "[容器化] 跨平台交叉编译为 Windows 独立免安装可执行程序 (.exe)..."
    $CONTAINER_CLI run --rm \
      -v "$PROJECT_ROOT":/workspace \
      -w /workspace/Client.CSharp \
      "$DOTNET_SDK_IMG" \
      dotnet publish MirClient.Runner/MirClient.Runner.csproj \
        -c Release \
        -r win-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:EnableCompressionInSingleFile=true \
        -o /workspace/bin/win-x64
    echo "[完成] 已生成 Windows 独立可执行文件位于: bin/win-x64/MirClient.Runner.exe"
    ;;

  *)
    echo "用法: $0 {init|build|test|run|publish-osx|publish-win}"
    exit 1
    ;;
esac
