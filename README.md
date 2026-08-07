# Codex Orbit

[English](README.en.md)

Codex Orbit 是一个轻量的 Windows 桌面额度悬浮窗。它通过本机 Codex 的
`app-server` 获取 ChatGPT 套餐中的 Codex 主额度，并在服务端通知不可用时以
5 秒轮询和本地会话快照兜底。

> [!IMPORTANT]
> 这是社区维护的非官方项目，与 OpenAI 没有隶属、授权或背书关系。
> 它显示的是 Codex 的 ChatGPT 套餐额度，不是 OpenAI API 的余额或 API
> rate limit，也不会绕过任何额度限制。

![Codex Orbit 迷你窗与圆环预览](assets/screenshots/overview-midnight.png)

## 功能

- 主额度实时同步，按当前协议安全合并稀疏的
  `account/rateLimits/updated` 通知。
- 自动识别正在运行或已安装的 Codex App、Codex CLI、PATH、IDE 扩展、
  Windows 安装包和 WSL 运行时。
- App 或 CLI 不需要预先运行；Codex Orbit 会按需启动隐藏的本地
  `codex app-server` 子进程。
- 迷你条、圆环或同时显示；支持多主题、透明度、置顶、鼠标穿透和全屏隐藏。
- 显示主额度剩余百分比及“距离重置还有几天几小时”。
- Spark 等模型专用额度不会占用主界面。
- 低额度与额度重置系统通知。
- 不读取 Codex 凭据，不要求用户复制 Token 或 API Key，没有遥测。

## 系统要求

- Windows 10/11 x64。
- .NET Framework 4.8。
- 至少存在一种可用的 Codex 运行时：
  - Codex Windows App；
  - Codex CLI；
  - 带 Codex 的 VS Code/Cursor/Windsurf 扩展；
  - WSL 中的 Codex CLI。
- Codex 使用 ChatGPT 账户登录。API Key 登录可调用 API，但不能提供
  ChatGPT 套餐额度。

Codex 的登录方式与 `app-server` 协议可参考
[OpenAI 身份验证文档](https://learn.chatgpt.com/docs/auth)和
[Codex App Server 文档](https://learn.chatgpt.com/docs/app-server)，额度规则见
[Codex 定价与用量文档](https://learn.chatgpt.com/docs/pricing)。

上游 App Server 仍在演进。Codex Orbit v3.2.1 已按 `codex-cli 0.147.0`
生成的当前协议 Schema 验证，并保留本地日志兜底以应对临时兼容问题。

## 安装

### 从 Release 安装

1. 打开仓库的[最新 Release](../../releases/latest)。
2. 下载 `CodexOrbit-<版本>-windows-x64.exe`；需要离线双语文档时下载 ZIP。
3. 使用 `SHA256SUMS.txt` 核对下载文件。
4. 将程序放入固定目录后双击运行。
5. 需要随系统启动时，在托盘菜单中启用“开机自启”。

从 v3.2.1 起，GitHub Release 还会生成构建来源证明。已安装 GitHub CLI 时可
进一步验证下载文件确由本仓库的 GitHub Actions 生成：

```powershell
gh attestation verify .\CodexOrbit-<版本>-windows-x64.exe `
  --repo ferretgeek/CodexOrbit
```

移动 EXE 后需要重新启用一次开机自启。若 Windows SmartScreen 提示未知发布者，
请先核对 SHA-256 和来源证明；社区构建默认没有商业代码签名证书，来源证明不等同
于 Authenticode 签名，也不会自动消除 SmartScreen 提示。

### 从源码构建

以 PowerShell 7 或 Windows PowerShell 打开仓库目录：

```powershell
.\scripts\build.ps1
```

产物位于：

```text
src\CodexOrbit\bin\x64\Release\CodexOrbit.exe
```

也可以用 Visual Studio 2022 打开 `CodexOrbit.sln`，选择 `Release | x64`
后构建。项目只使用 .NET Framework 自带程序集，没有 NuGet 运行时依赖。

## 使用

| 操作 | 结果 |
| --- | --- |
| 左键拖动 | 移动悬浮窗 |
| 右键悬浮窗 | 显示额度详情，3 秒后自动关闭 |
| 再次右键 | 立即关闭详情 |
| `Ctrl + 右键` | 打开设置菜单 |
| 托盘图标右键 | 打开设置菜单 |
| 双击托盘图标 | 重新显示悬浮窗 |

详情中会显示：

- 主额度剩余百分比；
- 距离重置还有几天、几小时；
- 当前套餐标识；
- 数据是实时同步还是本地快照。

“鼠标穿透”开启后，请通过托盘菜单关闭它。

## 各种运行环境

| 环境 | 是否支持 | 说明 |
| --- | --- | --- |
| 只安装 Codex App，App 未运行 | 支持 | 自动找到 App 自带运行时 |
| Codex App 正在运行 | 支持 | 优先复用同来源的可执行文件启动独立服务 |
| 只安装 Codex CLI，CLI 未运行 | 支持 | 自动从 npm 或 PATH 定位 |
| 只有 IDE 扩展 | 支持 | 扫描常见扩展目录 |
| 只有 WSL Codex CLI | 支持 | WSL 中也必须使用 ChatGPT 登录 |
| App 与 CLI 都未运行 | 支持 | 只要本机仍安装了任一运行时 |
| 完全没有 Codex 运行时或本地缓存 | 不支持 | 没有可连接的账户服务 |
| API Key 登录 | 不支持套餐额度 | UI 会明确提示认证方式不匹配 |

## 数据来源与隐私

实时模式只通过标准输入/输出与本机 `codex app-server` 子进程通信。身份验证、
Token 刷新和远程请求均由 Codex 自己处理，Codex Orbit 不读取
`.codex\auth.json`。

实时服务不可用时，程序会只读扫描最近 Codex 会话日志尾部的
`rate_limits` 记录。它不会保存或上传对话内容。详细范围见
[PRIVACY.md](PRIVACY.md)。

本地目录可能包含以下内容：

```text
%LOCALAPPDATA%\CodexQuota\
├─ settings.json   界面与提醒偏好
├─ runtime\         从已安装 Windows App 准备的运行时缓存
└─ error.log        仅发生未处理错误时创建的本地诊断日志
```

诊断日志不会上传，单条内容会限制长度，并会脱敏用户目录和常见 Token 格式；
它仍可能包含异常上下文，分享前应人工检查。以上本地内容都不会包含在源码仓库或
发布压缩包中。

## 故障排查

### 一直显示“未找到可用的 Codex”

先确认 Codex App 或 CLI 已安装，并能正常打开。CLI 用户可在终端运行：

```powershell
codex --version
```

随后从托盘菜单选择“立即刷新”。

### 显示“尚未登录 ChatGPT”

在 Codex App 或 CLI 中完成 ChatGPT 登录，再刷新。只配置
`OPENAI_API_KEY` 不等于 ChatGPT 套餐登录。

### WSL 无法显示额度

确认 WSL 中的 `codex` 可执行，并且该 WSL 实例使用 ChatGPT 登录。
若 WSL 使用 API Key，Codex Orbit 会跳过它并尝试其他运行时。

### 数据暂时不刷新

服务端通知会立即更新；5 秒安全定时器负责自动纠偏。实时连接失败后会退避
15 秒，避免反复启动不可用的运行时。也可以从托盘菜单手动强制刷新。若实时服务
不可用，界面会明确标注“非实时”并显示最近的本地快照。

### 查看本地诊断日志

程序遇到未处理错误时可能写入：

```text
%LOCALAPPDATA%\CodexQuota\error.log
```

日志只保存在本机，不会自动上传；其大小会受到限制，并会脱敏常见路径和密钥格式。
如需提交 Issue，请先人工检查并删除提示词、账户信息和其他仍可能识别个人身份的
上下文，不要直接附上未经检查的完整日志。

### 重置所有界面设置

退出程序后删除：

```text
%LOCALAPPDATA%\CodexQuota\settings.json
```

重新运行即可恢复默认设置。

## 开发与测试

```powershell
# 构建并运行不需要真实账户的完整确定性测试
.\scripts\test.ps1

# 额外验证当前机器的真实 Codex 账户
.\scripts\test.ps1 -Live

# 生成用于 GitHub Release 的独立 EXE、ZIP 与 SHA-256
.\scripts\package.ps1
```

确定性测试会启动本地假 `app-server`，覆盖稀疏通知合并、账户响应最小化、
诊断日志脱敏、本地会话日志解析、额度阈值、套餐映射、重置提醒和窗口定位，
不访问真实账户。

## 文档

| 主题 | 简体中文 | English |
| --- | --- | --- |
| 参与贡献 | [CONTRIBUTING.md](CONTRIBUTING.md) | [CONTRIBUTING.en.md](CONTRIBUTING.en.md) |
| 隐私 | [PRIVACY.md](PRIVACY.md) | [PRIVACY.en.md](PRIVACY.en.md) |
| 安全 | [SECURITY.md](SECURITY.md) | [SECURITY.en.md](SECURITY.en.md) |
| 支持 | [SUPPORT.md](SUPPORT.md) | [SUPPORT.en.md](SUPPORT.en.md) |
| 架构 | [ARCHITECTURE.md](docs/ARCHITECTURE.md) | [ARCHITECTURE.en.md](docs/ARCHITECTURE.en.md) |
| 发布 | [PUBLISHING.md](docs/PUBLISHING.md) | [PUBLISHING.en.md](docs/PUBLISHING.en.md) |
| 更新记录 | [CHANGELOG.md](CHANGELOG.md) | [CHANGELOG.en.md](CHANGELOG.en.md) |
| 行为准则 | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) | [CODE_OF_CONDUCT.en.md](CODE_OF_CONDUCT.en.md) |

## 许可证

[MIT](LICENSE) · [中文参考译文](LICENSE.zh-CN.md)

“OpenAI”“ChatGPT”和“Codex”是其各自权利人的商标。本项目名称中的 Codex
仅用于说明兼容对象。
