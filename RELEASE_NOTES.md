# AirPageSystem v0.0.1

首个公开版本，提供完整的 AirPage 墨水屏快照管理、面板生成和定时推送能力。

## 主要功能

- Vue 3 管理端与 .NET 10 后端。
- 最新 A 股行情、服务端状态两个内置面板。
- 自定义 HTTP JSON 数据源及面板字段映射。
- 528×792 PNG 预览与固件兼容 2-bit 四级灰度 BMP。
- AirPage 设备、Cron 定时任务及推送历史管理。
- 设备 ID 和数据源敏感请求头加密保存。

## 下载选择

- `win-x64` / `win-arm64`：Windows 自包含版本。
- `linux-x64` / `linux-arm64`：Linux 自包含版本。
- `osx-x64` / `osx-arm64`：macOS Intel / Apple Silicon 自包含版本。
- `docker.tar.gz`：Docker Compose 部署文件。

所有系统压缩包都包含 .NET 运行时及中文字体，不需要另行安装 .NET。解压后直接运行 `AirPageSystem.Api`（Windows 为 `AirPageSystem.Api.exe`），访问 `http://localhost:5088`。

Docker 镜像：`ghcr.io/sunkejava/airpagesystem:0.0.1`。
