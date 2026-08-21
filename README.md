[![Codex 额度悬浮窗](assets/social-preview.png)](https://github.com/ferretgeek/codex-quota-widget/releases/latest)

# Codex 额度悬浮窗

中文 · [English](README.en.md)

[![最新版本](https://img.shields.io/github/v/release/ferretgeek/codex-quota-widget?display_name=tag&label=%E7%89%88%E6%9C%AC)](https://github.com/ferretgeek/codex-quota-widget/releases/latest)
[![许可](https://img.shields.io/github/license/ferretgeek/codex-quota-widget?label=%E8%AE%B8%E5%8F%AF)](LICENSE)
[![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?logo=windows&logoColor=white)](#安装)

> 桌面上一个小窗，随时告诉你 Codex 还剩多少、几点回满。

## 为什么会需要它

用 Codex 写代码，最难受的是写到一半额度没了——上一句还在改逻辑，下一句就被打断。

想知道还剩多少，得打开 Codex 的界面翻一下。翻一次不麻烦，一天翻十次就很烦。

所以做了这个：一个一直挂在桌面角落的小窗，显示 ChatGPT 套餐里 Codex 主额度的剩余百分比和"距离重置还有几天几小时"。快用完了会弹通知，回满了也会弹通知。

它**不要你复制任何 Token，不读你的凭据文件，也不上传任何东西**。

## 界面

可以做成迷你条、圆环，或者两个都显示。六套全局主题（含樱粉和薄荷两套浅色），能调透明度、置顶、鼠标穿透和全屏自动隐藏。

| 操作 | 结果 |
| --- | --- |
| 左键拖动 | 移动悬浮窗 |
| 右键悬浮窗 | 展开额度详情，3 秒后自动收起 |
| 再次右键 | 立刻收起详情 |
| `Ctrl` + 右键 | 打开设置菜单 |
| 托盘图标右键 | 打开设置菜单 |
| 双击托盘图标 | 把悬浮窗叫回来 |

详情里会显示：主额度剩余百分比、距离重置还有几天几小时、当前套餐标识，以及**这个数字是实时同步的还是本地快照**——这一点很重要，你至少知道自己在看什么。

> 开了"鼠标穿透"之后，悬浮窗就点不到了，请从托盘菜单关掉它。

## 安装

### 用现成的（推荐）

1. 打开[最新 Release](https://github.com/ferretgeek/codex-quota-widget/releases/latest)。
2. 下载 `CodexOrbit-<版本>-windows-x64.exe`；想要离线双语文档就下 ZIP。
3. 用 `SHA256SUMS.txt` 核对一下。
4. 放到一个固定目录，双击运行。
5. 想开机自启，在托盘菜单里勾上。

Release 会附带构建来源证明。装了 GitHub CLI 的话可以进一步验证这个文件确实是本仓库的 Actions 构建出来的：

```powershell
gh attestation verify .\CodexOrbit-<版本>-windows-x64.exe --repo ferretgeek/codex-quota-widget
```

> 移动过 EXE 之后要重新勾一次开机自启。
>
> Windows SmartScreen 可能提示"未知发布者"——这是因为社区构建没有商业代码签名证书。请先核对 SHA-256 和来源证明；**来源证明不等于 Authenticode 签名，也不会让 SmartScreen 的提示消失**。

### 自己编译

用 PowerShell 7 或 Windows PowerShell 打开仓库目录：

```powershell
.\scripts\build.ps1
```

产物在 `src\CodexOrbit\bin\x64\Release\CodexOrbit.exe`。

也可以用 Visual Studio 2022 打开 `CodexOrbit.sln`，选 `Release | x64` 构建。项目只用 .NET Framework 自带程序集，没有 NuGet 运行时依赖。

## 需要什么

- Windows 10/11 x64、.NET Framework 4.8。
- 机器上装了任意一种 Codex 运行时（不需要它正在运行）：
  - Codex Windows App
  - Codex CLI
  - 带 Codex 的 VS Code / Cursor / Windsurf 扩展
  - WSL 里的 Codex CLI
- **Codex 必须是用 ChatGPT 账户登录的。** 用 API Key 登录能调 API，但那不是套餐额度，拿不到这个数字——界面会明确告诉你认证方式不匹配，而不是显示 0。

| 你的情况 | 能用吗 | 说明 |
| --- | --- | --- |
| 只装了 Codex App，没打开 | ✅ | 自动找到 App 自带的运行时 |
| Codex App 正在运行 | ✅ | 优先复用同来源的可执行文件，另起一个独立服务 |
| 只装了 Codex CLI，没运行 | ✅ | 从 npm 或 PATH 里定位 |
| 只有 IDE 扩展 | ✅ | 扫常见扩展目录 |
| 只有 WSL 里的 Codex CLI | ✅ | WSL 里同样要用 ChatGPT 登录 |
| 完全没装过 Codex，也没本地缓存 | ❌ | 没有可连接的账户服务 |
| 用 API Key 登录 | ❌ 拿不到套餐额度 | 界面会提示认证方式不匹配 |

## 技术上值得一提的地方

**它不碰你的凭据。** 实时模式下，程序按需拉起一个隐藏的本机 `codex app-server` 子进程，只通过标准输入输出通信。身份验证、Token 刷新和所有远程请求都由 Codex 自己完成——`.codex\auth.json` 从头到尾没被读过。

**稀疏通知的安全合并。** 上游发的 `account/rateLimits/updated` 是增量、稀疏的，直接覆盖会丢字段。程序按当前协议逐字段合并，而不是拿最后一条替换全部。

**实时不可用时有兜底，而且会告诉你。** 服务端通知拿不到时，退回只读扫描最近 Codex 会话日志尾部的 `rate_limits` 记录。它不保存也不上传对话内容，并且界面上会明确标出"这是本地快照"，不会把旧数据伪装成实时。

**六种运行时自动发现。** 正在运行的 App、已安装的 App、Codex CLI、`PATH`、IDE 扩展目录、Windows 安装包、WSL——按优先级依次探测，装了任意一种就能用，不需要你手动指路径。

**上游协议在变，所以留了余地。** 当前版本按 `codex-cli 0.147.0` 生成的协议 Schema 验证过，同时保留本地日志兜底以应对临时的兼容问题。

Codex 的登录方式与 `app-server` 协议见 [OpenAI 身份验证文档](https://learn.chatgpt.com/docs/auth)和 [Codex App Server 文档](https://learn.chatgpt.com/docs/app-server)，额度规则见 [Codex 定价与用量文档](https://learn.chatgpt.com/docs/pricing)。

## 隐私

本地目录可能出现这些内容：

```text
%LOCALAPPDATA%\CodexQuota\
├─ settings.json   界面与提醒偏好
├─ runtime\        从已安装的 Windows App 准备的运行时缓存
└─ error.log       只在发生未处理错误时才创建的本地诊断日志
```

诊断日志不上传，单条内容有长度上限，会脱敏用户目录和常见 Token 格式——但它仍然可能包含异常上下文，**分享之前请自己看一眼**。以上内容都不会进入源码仓库或发布压缩包。

完整范围见 [PRIVACY.md](PRIVACY.md)。

## 它不做什么

- 不显示 OpenAI API 的余额或 API rate limit——那是另一套东西。
- 不绕过、不放宽、不刷新任何额度限制。
- 不读你的凭据文件，不要求你粘贴 Token 或 API Key。
- 没有遥测，没有"匿名统计"。

## 排查

**一直显示"未找到可用的 Codex"。** 先确认 Codex App 或 CLI 装好了并能正常打开。CLI 用户可以在终端跑 `codex --version`，然后从托盘菜单选"重新检测运行时"。

更多情况见 [SUPPORT.md](SUPPORT.md)。

## 更多文档

[架构说明](docs/ARCHITECTURE.md) · [运维与发布](docs/OPERATIONS.md) · [隐私](PRIVACY.md) · [版本变更](CHANGELOG.md) · [参与开发](CONTRIBUTING.md) · [安全策略](SECURITY.md) · [获取支持](SUPPORT.md) · [第三方声明](THIRD_PARTY_NOTICES.md)

## 许可与声明

见 [LICENSE](LICENSE)（[中文译本](LICENSE.zh-CN.md)）。

这是社区维护的非官方项目，与 OpenAI 没有隶属、授权或背书关系。它显示的是 Codex 的 ChatGPT 套餐额度，不是 OpenAI API 的余额或 rate limit，也不会绕过任何额度限制。相关商标归其权利人所有。
