# 架构说明

[English](ARCHITECTURE.en.md)

## 数据流

```text
Codex App / CLI / IDE / WSL
            │
            │ 本机 stdio JSONL
            ▼
      codex app-server
            │
            ▼
   CodexAppServerClient
            │
            ├─ account/rateLimits/updated ──► 稀疏合并
            └─ account/rateLimits/read ─────► 5 秒安全轮询
            │
            ▼
     CodexUsageReader
            │
            ├─ 主额度筛选
            ├─ 本地日志兜底
            └─ 变化去重
            │
            ▼
 MainWindow / MiniStatusWindow / Tray
```

## 主要模块

- `CodexAppServerClient`：发现运行时、管理子进程、发送请求和接收通知。
- `CodexUsageReader`：合并实时数据与本地快照，只选择主 Codex 额度。
- `MainWindow`：圆环窗口、托盘菜单、通知和显示模式协调。
- `MiniStatusWindow`：紧凑的主额度与重置倒计时视图。
- `AppSettings`：只保存界面和提醒偏好。
- `UsageProbe`：假 app-server、通知时延测试和可选真实账户测试。

## 运行时发现顺序

1. `CODEX_ORBIT_CODEX_PATH` 显式覆盖。
2. 正在运行的 `codex.exe`。
3. Codex Windows App 的本地运行时。
4. Codex Orbit 已准备的本机缓存。
5. npm 安装的 Codex CLI 与 `%USERPROFILE%\.codex\bin`。
6. PATH 中的 `codex.exe`、`codex.cmd` 或 `codex.bat`。
7. VS Code、Cursor、Windsurf 等扩展目录。
8. Windows 安装包。
9. WSL 中的 Codex CLI。

候选运行时按顺序尝试；认证不匹配或启动失败时继续尝试下一个。

## 实时与兜底

连接成功后，`account/rateLimits/read` 建立完整快照。当前
`account/rateLimits/updated` 通知可能只携带变化字段；程序会将非空字段递归
合并到最近完整响应，再生成新快照。通知缺失时每 5 秒读取一次额度；实时读取
失败后退避 15 秒，连续短暂失败时保留最近 30 秒快照并明确标注非实时。

全部实时来源不可用时，本地解析器只扫描会话日志尾部的 `rate_limits` 记录。
此时 UI 必须明确标注“非实时”，且不会根据旧快照发送低额度或重置通知。

## 安全约束

- 不读取 `auth.json`。
- 不接收用户提供的 Token。
- app-server 使用本机重定向标准流，不开放监听端口。
- 缓存的 Windows App 运行时使用 SHA-256 验证复制完整性。
- 用户设置使用临时文件替换，且不创建额外备份。
- 用户设置在应用前会经过白名单与数值范围校验。

App Server 是仍在演进的上游接口。每次兼容层改动必须同步更新确定性协议夹具。
