# AirPageSystem v0.0.3

新增内置面板 **3xui代理监控**。

- 自动检测本机 3x-ui 与 Xray 进程状态、版本和运行时长。
- 只读采集 3x-ui SQLite 数据库中的上传、下载、总用量、入站、客户端及客户端 IP 数量。
- 按入站端口统计 TCP 活动连接和 UDP 监听。
- 展示主机 IPv4 地址及高流量入站排行。
- 不读取或展示客户端 UUID、密码、订阅 ID 等凭据。
- 原生部署自动探测常见数据库路径；Docker 支持只读挂载数据库。

---

# AirPageSystem v0.0.2

Windows/SQLite 兼容性修复版本。

## 修复

- 修复 SQLite 无法翻译 `DateTimeOffset` 排序导致 `/api/history` 返回 500。
- 修复 SQLite 无法翻译动态日期比较导致 `/api/dashboard` 返回 500。
- 修复定时调度器查询 `NextRunAt` 时持续报错、任务无法触发。
- 前端各区域独立加载，防止统计接口异常时将已经成功的设备新增误报为失败。
- 添加成功后立即清空设备凭据输入框。

兼容 v0.0.1 已生成的数据库，升级时无需删除 `data/airpage.db`。

---

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
