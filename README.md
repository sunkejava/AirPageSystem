# AirPageSystem

基于 **.NET 10 + Vue 3** 的 AirPage 墨水屏快照管理与定时推送系统。系统采集行情、服务器状态或自定义 HTTP JSON 数据，生成适合 528×792 四级灰度屏的 PNG 预览和固件兼容 2-bit BMP，并推送到 AirPage。

当前版本：**v0.0.1**。可在 [Releases](https://github.com/sunkejava/AirPageSystem/releases) 下载 Windows、Linux、macOS 自包含版本，无需预装 .NET。

## 功能

- Vue 管理端：总览、模板、数据源、定时任务、设备和推送历史。
- **最新行情面板**：三大指数、上涨/下跌/平盘、涨跌停及异动股票。
- **服务端状态面板**：系统信息、应用内存、磁盘、运行时间、网络流量和高内存进程。
- 自定义面板：绑定 GET/POST JSON 数据源，通过声明式路径配置标题、指标和列表。
- 标准五段 Cron、独立时区、任务启停和执行结果。
- AirPage 设备 ID 使用 ASP.NET Core Data Protection 加密存储；接口和日志不回显凭据。
- 严格四调色板、2-bit、bottom-up BMP 编码，并检查 512 KiB 限制。
- SQLite 持久化、Docker 部署和 GitHub Actions 构建。

## Docker 启动

~~~bash
docker compose up -d --build
~~~

也可直接使用多架构镜像：

~~~bash
docker run -d --name airpage-system -p 5088:8080 -v airpage-data:/app/data ghcr.io/sunkejava/airpagesystem:0.0.1
~~~

浏览器访问 http://localhost:5088：

1. 在“设备”页粘贴 AirPage 设备链接并设为默认设备。
2. 在“面板模板”中预览“最新行情面板”或“服务端状态面板”。
3. 确认排版后立即推送，或在“定时推送”中创建任务。

运行数据保存在根目录 data/，不会提交到 Git。

## 本地开发

需要 .NET 10 SDK、Node.js 24+ 和中文字体。

~~~bash
cd src/airpage-web
npm install
npm run build
cd ../AirPageSystem.Api
dotnet run --urls http://localhost:5088
~~~

前端热更新：

~~~bash
cd src/airpage-web
npm run dev
~~~

## Cron 示例

| 用途 | Cron |
|---|---|
| 工作日 09:30 | 30 9 * * 1-5 |
| 工作日 10:30 | 30 10 * * 1-5 |
| 每小时整点 | 0 * * * * |
| 每天 08:00 | 0 8 * * * |

默认时区为 Asia/Shanghai。首版调度器按 Cron 执行；行情模板若需严格排除法定休市日，可继续增加交易日历策略。

## 自定义 JSON 映射

先添加 HTTP JSON 数据源，再创建 custom 模板：

~~~json
{
  "titlePath": "$.title",
  "metrics": [
    { "label": "状态", "path": "$.status" },
    { "label": "在线数", "path": "$.online" }
  ],
  "itemsPath": "$.items",
  "columns": [
    { "label": "名称", "path": "$.name" },
    { "label": "数值", "path": "$.value" }
  ]
}
~~~

当前支持对象属性路径，例如 $.data.status。首版刻意不支持脚本表达式与远程代码执行。

## 安全

- 只接受受信任 AirPage 域名的 HTTPS 设备地址。
- 设备 ID 加密后存入 SQLite；请持久化 data/ 中的数据库和 Data Protection 密钥。
- 自定义数据源默认禁止访问环回及 RFC1918 私网地址以减少 SSRF 风险。局域网监控需显式设置 DataSources__AllowPrivateNetworks=true。
- 生产环境建议增加登录、RBAC、Secret Manager/Vault 和反向代理 TLS。
- 不要提交真实设备 URL、API 令牌、数据库或 appsettings.Production.json。

## 项目结构

~~~text
src/
├─ AirPageSystem.Api/  .NET 10 API、调度、渲染和推送
└─ airpage-web/        Vue 3 + Vite 管理端
~~~

## API

- GET/POST /api/devices
- GET/POST/PUT /api/templates
- GET/POST /api/data-sources
- POST /api/data-sources/{id}/test
- GET/POST /api/schedules
- POST /api/panels/execute
- GET /api/dashboard
- GET /api/history

## 当前边界

- 公开行情源应在生产环境增加缓存、重试、熔断和授权数据源。
- CPU 百分比需跨采样窗口计算，当前服务状态面板优先展示稳定的内存、磁盘、流量与进程信息。
- 首版使用 EnsureCreated 快速初始化；持续迭代时建议改为 EF Core migrations。
