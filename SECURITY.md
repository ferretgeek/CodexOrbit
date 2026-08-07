# 安全策略

[English](SECURITY.en.md)

## 支持版本

安全修复面向最新已发布版本。提交问题前请先升级，以排除已经修复的缺陷。

## 报告漏洞

请优先使用 GitHub 仓库的 **Security → Report a vulnerability** 私下报告。
如果仓库尚未启用 Security Advisories，请创建不包含敏感信息的普通 Issue，请求
维护者提供私密联系方式。

报告中建议包括：

- 受影响版本；
- Windows 和 Codex 运行环境；
- 可稳定复现的最小步骤；
- 预期行为与实际行为；
- 风险和可能的修复方向。

请勿提交真实 Token、API Key、Cookie、完整会话日志、用户名路径或账户截图。
如需日志，请先删除提示词、回复、账户信息和本机路径。

## 安全边界

- Codex Orbit 不应读取 Codex 凭据文件。
- 与 `codex app-server` 的通信应保持在本机标准输入/输出。
- 发布包不得包含缓存运行时、用户设置、会话日志或构建机器路径。
- GitHub Release 的 EXE 与 ZIP 应同时发布 SHA-256。

依赖上游 Codex 协议造成的兼容性问题不一定是安全漏洞，但欢迎报告。
