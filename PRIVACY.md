# 隐私说明

[English](PRIVACY.en.md)

Codex Orbit 的设计目标是本地优先、最小读取和零遥测。

## 不会收集的内容

- 不包含遥测、分析、广告或崩溃报告 SDK。
- 不向项目维护者发送任何数据。
- 不读取或保存 Codex 的访问令牌、刷新令牌、API Key 或
  `%USERPROFILE%\.codex\auth.json`。
- 不要求用户粘贴密码、Cookie 或 Token。

## 实时额度模式

程序在本机启动 `codex app-server`，通过重定向的标准输入/输出交换 JSONL
消息。Codex 运行时负责身份验证、Token 刷新以及与 OpenAI 服务的通信。
Codex Orbit 请求协议要求的账户状态和额度窗口。当前 `account/read` 响应可能
包含账户邮箱；程序在 JSON 反序列化后立即丢弃邮箱及其他无关账户字段，只在内存
中保留认证要求、账户类型和套餐类型。邮箱不会被读取用于业务逻辑、写入磁盘或
写入诊断日志。

Codex Orbit 不传入 `--analytics-default-enabled`。App Server 自身的分析设置由用户
安装的 Codex 运行时及其配置控制，具体以上游 Codex 官方文档为准。

程序没有自己的远程服务器、统计端点或更新检查。

## 本地快照兜底

当实时账户服务不可用时，程序会读取：

```text
%USERPROFILE%\.codex\sessions
```

读取范围受到以下限制：

- 最多检查最近 10 天、160 个会话文件；
- 每个文件只读取末尾最多 256 KiB；
- 只定位包含 `rate_limits` 的记录；
- 只提取额度窗口、百分比、重置时间和套餐类型；
- 不上传、不复制、不持久化提示词、回复或完整日志。

本地快照会在内存中缓存，程序退出后消失。

## 本地持久化

界面设置保存在：

```text
%LOCALAPPDATA%\CodexQuota\settings.json
```

内容仅包括主题、窗口位置、透明度、显示模式、提醒阈值和套餐显示覆盖，不含账户
标识或凭据。

如果启用“开机自启”，程序会写入当前用户注册表：

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CodexOrbit
```

如果需要从 Windows App 安装包准备可执行的本机运行时，文件会复制到：

```text
%LOCALAPPDATA%\CodexQuota\runtime
```

该目录只包含 Codex 可执行运行时，不包含会话、设置或账户凭据。

发生未处理错误时，程序可能创建：

```text
%LOCALAPPDATA%\CodexQuota\error.log
```

该文件只保存在本机，不会自动上传。单条诊断最多约 32 KiB；文件超过约
512 KiB 后会在下次写入前轮换。程序会脱敏 `%USERPROFILE%`、
`%LOCALAPPDATA%` 的实际路径，以及常见 GitHub Token 和 API Key 格式。
诊断仍可能包含异常消息或调用上下文，分享前必须人工检查并删除提示词、账户信息
或其他可识别个人身份的内容。

## 删除本地数据

1. 从托盘菜单退出 Codex Orbit。
2. 在菜单中关闭“开机自启”，或删除上面的注册表值。
3. 删除 `%LOCALAPPDATA%\CodexQuota`。

Codex Orbit 不维护云端账户，因此没有需要向本项目申请删除的远程数据。
