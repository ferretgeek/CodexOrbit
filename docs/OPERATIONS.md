# 安装、升级与运维

[English](OPERATIONS.en.md)

## 架构与服务器边界

Codex Orbit 是 Windows 10/11 x64 的 WPF 桌面悬浮窗。它在当前交互式用户会话中寻找本机 Codex 运行时，按需启动隐藏的 `codex app-server` 子进程，并只通过重定向标准输入/输出通信；程序不监听端口，也不读取或复制 `.codex\auth.json`。

项目不提供 Linux 服务、Windows Service 或远程 Web 面板。这不是缺失的部署脚本：额度来自当前用户已登录的本机 Codex，会话窗口、托盘、通知、置顶与全屏检测也依赖交互式 Windows 桌面。把认证或额度重新发布到网络会新增凭据代理、鉴权、TLS、限流和多用户隔离风险，却不能替代每个用户自己的 Codex 登录。需要在远程 Windows 主机使用时，可以在受控 RDP/虚拟桌面会话中按普通桌面应用运行，但数据只属于该会话用户，断开会话后的 UI/通知行为取决于主机策略；这不等于服务器模式。

## 安装

1. 需要 Windows 10/11 x64、.NET Framework 4.8，以及至少一个已安装并用 ChatGPT 账户登录的 Codex App、CLI、支持的 IDE 扩展或 WSL CLI。
2. 从 GitHub Release 下载独立 EXE 或 ZIP，同时下载 `SHA256SUMS.txt`；核对 SHA-256。有 GitHub CLI 时再执行 README 中的 `gh attestation verify`。
3. 社区二进制默认没有商业 Authenticode 签名；来源证明也不能消除 SmartScreen。无法独立验证来源时，从源码运行 `scripts\build.ps1`。
4. 将 EXE 放到固定、仅当前用户可写的目录后运行。需要开机自启时通过托盘菜单启用；程序不会擅自创建自启项。

## 升级与回退

1. 从托盘退出 Codex Orbit，确认任务管理器中没有残留进程。
2. 复制 `%LOCALAPPDATA%\CodexQuota\settings.json` 到受控位置；它只包含界面与提醒偏好，不包含 Token。`runtime\` 是可再生缓存，`error.log` 是诊断信息，不应作为设置备份。
3. 验证新版校验值与来源证明后，用新版 EXE 替换旧文件。若启用了开机自启且路径发生变化，关闭后重新启用一次。
4. 启动后检查主题、显示模式、窗口位置、托盘、手动刷新和实时标识。设置格式会按白名单与范围规范化。
5. 回退时退出程序，恢复旧 EXE 与升级前的 `settings.json`。上游 App Server 协议持续演进，旧版本可能不兼容新的 Codex 运行时；优先升级 Codex Orbit，而不是复制旧认证文件。

## 备份与恢复

- Codex Orbit 不保存账户凭据或额度数据库，唯一不可再生状态是 `%LOCALAPPDATA%\CodexQuota\settings.json`。退出程序后复制该文件即可备份。
- 恢复前退出程序，把已验证的 `settings.json` 放回同一路径，再启动并检查所有设置。损坏或未来版本字段会被安全默认值和白名单规范化，不能靠手工加入未知字段扩权。
- `runtime\` 可删除后重新发现；`error.log` 可在完成私密诊断后删除。不要备份或分发 `.codex\auth.json`、会话日志或整个用户目录。

## 健康检查

- `codex --version` 或 Codex App 正常；Codex 使用 ChatGPT 登录而非仅 API Key。
- 托盘图标与悬浮窗可见，“立即刷新”后显示实时主额度、套餐和重置倒计时；服务不可用时界面明确标记“非实时”。
- 迷你条、圆环、六套主题、透明度、置顶、穿透、全屏隐藏和多显示器恢复符合设置。
- 开发/发布候选执行 `scripts\test.ps1`；真实账户只在明确授权的本机用 `-Live` 做只读验证。发布包还需执行 `scripts\package.ps1` 和 [`PUBLISHING.md`](PUBLISHING.md) 的验收。

## 故障排查

- **找不到 Codex：** 确认 App/CLI/IDE 扩展/WSL 至少一个可执行，再从托盘立即刷新。不要把第三方 `codex.exe` 塞进缓存目录规避来源检查。
- **尚未登录 ChatGPT：** 在 Codex App 或 CLI 完成 ChatGPT 登录。只设置 `OPENAI_API_KEY` 不能提供 ChatGPT 套餐额度。
- **非实时或不刷新：** 等待 15 秒退避，确认上游运行时未被安全软件阻止，然后手动刷新。旧快照不会触发低额度或重置通知。
- **设置异常：** 退出后先备份，再删除 `settings.json` 恢复默认值；不要在运行中编辑。
- **运行时缓存异常：** 退出后删除 `%LOCALAPPDATA%\CodexQuota\runtime\`，重启让程序重新发现并校验运行时。
- **需要报告：** 查看有界 `error.log`，人工删除提示词、账户、路径和身份上下文后只提交最小片段；安全问题走私密漏洞入口。

## 卸载

从托盘退出；若启用过开机自启，先在托盘菜单关闭。删除固定目录中的 Codex Orbit EXE/ZIP 解压文件，再按需删除 `%LOCALAPPDATA%\CodexQuota\`。后者会永久删除界面设置、缓存和本地诊断日志，但不会注销 Codex、删除 Codex 凭据或影响 Codex App/CLI。确认自启路径已经移除后再删除或移动 EXE。
