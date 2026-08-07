# 支持

[English](SUPPORT.en.md)

## 提交 Issue 前

1. 安装最新 Release。
2. 确认 Codex App 或 CLI 可以打开，并已使用 ChatGPT 账户登录。
3. 从托盘菜单选择“立即刷新”。
4. 查看 [README](README.md) 的故障排查。
5. 仅在源码构建场景运行 `.\scripts\test.ps1`。

## 反馈渠道

- 可复现缺陷：使用 Bug Issue 表单。
- 产品建议：使用功能建议表单。
- 可能的安全漏洞：遵循 [SECURITY.md](SECURITY.md)，不要公开敏感细节。
- Codex 账户、账单或套餐问题：联系 OpenAI 官方支持；本社区项目无法查看或更改
  账户。

Issue 可使用简体中文或英文。

## 安全诊断信息

请提供 Codex Orbit 版本、Windows 版本、运行时类型、界面状态文案和最小复现
步骤。不要提供 Token、API Key、Cookie、完整会话日志、真实账户截图或未脱敏的
用户路径。

发生未处理错误后，可检查：

```text
%LOCALAPPDATA%\CodexQuota\error.log
```

程序会限制其大小并脱敏常见路径和密钥格式，但无法保证移除所有业务或个人上下文。
不要直接上传完整日志；只复制与问题相关的最小片段，并在分享前人工删除提示词、
账户信息和其他可识别内容。
