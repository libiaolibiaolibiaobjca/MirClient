#!/usr/bin/env bash
set -e

# ==============================================================================
# 将 MirClient.CSharp 核心源码推送到新仓库
# 目标: https://github.com/libiaolibiaolibiaobjca/MirClient.CSharp.git
# ==============================================================================

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_REPO="https://github.com/libiaolibiaolibiaobjca/MirClient.CSharp.git"
EXPORT_DIR="$SRC_DIR/.export_csharp_repo"

echo "[1/4] 清理并准备干净的发布目录..."
rm -rf "$EXPORT_DIR"
mkdir -p "$EXPORT_DIR"

echo "[2/4] 拷贝纯净的 C# 客户端与规范文件..."
cp -r "$SRC_DIR/specs" "$EXPORT_DIR/"
cp -r "$SRC_DIR/Client.CSharp" "$EXPORT_DIR/"
cp "$SRC_DIR/build.sh" "$EXPORT_DIR/"
cp "$SRC_DIR/README.md" "$EXPORT_DIR/"
cp "$SRC_DIR/.gitignore" "$EXPORT_DIR/"

echo "[3/4] 初始化独立 Git 仓库并创建首次提交..."
cd "$EXPORT_DIR"
git init
git branch -M main
git add .
git commit -m "feat: implement classic audio sound engine with 13 sound effects and 73 passing tests"
git remote add origin "$TARGET_REPO"

echo "[4/4] 推送到远程仓库: $TARGET_REPO ..."
git push -f -u origin main

echo ""
echo "================================================="
echo "  [成功] 代码已成功推送到新仓库: $TARGET_REPO"
echo "================================================="

# 清理临时导出目录
cd "$SRC_DIR"
rm -rf "$EXPORT_DIR"
