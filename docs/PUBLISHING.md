# GitHub 发布指南

[English](PUBLISHING.en.md)

本目录就是仓库根目录，不要再嵌套到另一个同名目录中。

## 仓库设置

建议并在正式发布前核对：

- 公开仓库，默认分支为 `main`；
- Issues、Discussions 和 Private vulnerability reporting 已启用；
- Actions 仅允许 GitHub 官方 Actions 与
  `microsoft/setup-msbuild`，并要求引用完整提交 SHA；
- `main` 分支保护要求 `build` 的 `windows` 与 CodeQL 的
  `analyze-csharp` 检查通过；
- Dependency graph、Dependabot 与 Secret scanning 可用时保持启用；
- 仓库主题包含 `codex`、`chatgpt`、`windows`、`wpf`、
  `dotnet-framework`、`quota-monitor`。

工作流使用 `windows-2022` 和 Visual Studio 2022 工具链范围。所有 Actions
引用固定到完整提交 SHA，并由按月分组的 Dependabot PR 提醒更新。

## 本地发布构建

运行：

```powershell
.\scripts\package.ps1
```

脚本会先构建和测试，再写入：

```text
artifacts\CodexOrbit-<版本>-windows-x64.exe
artifacts\CodexOrbit-<版本>-windows-x64.zip
artifacts\SHA256SUMS.txt
```

ZIP 包含 EXE、完整中英双语文档、预览图和包内 EXE 校验值。打包器固定 ZIP
条目排序，并以当前提交时间作为统一条目时间戳；在源码、内容和编译工具链一致时，
重复打包应产生相同结果。这不是跨 Visual Studio、MSBuild 或编译器版本的
位级一致承诺，发布页上的 SHA-256 才是对应资产的权威校验值。

## 标签驱动发布

1. 更新 `src\CodexOrbit\Properties\AssemblyInfo.cs` 中的
   `AssemblyVersion`、`AssemblyFileVersion` 和
   `AssemblyInformationalVersion`。
2. 更新两份 CHANGELOG，并新增 `docs/releases/v<版本>.md` 双语说明。
3. 运行完整确定性测试、脱敏真实账户检查、UI 预览 QA 和两次打包一致性检查。
4. 确认本地源码无未预期改动后提交并推送。
5. 等待 `windows` 和 `analyze-csharp` 检查成功。
6. 创建并推送匹配的注释标签，例如 `v3.2.1`。

`release` 工作流会从标签重新构建和测试，核对标签与二进制版本，发布 EXE、ZIP
和校验文件，并为三项资产生成 GitHub 构建来源证明。存在双语 Release Notes
文件时会直接使用。

## 发布前隐私与安全检查

在仓库根目录运行：

```powershell
rg -n -i --hidden `
  --glob '!bin/**' `
  --glob '!obj/**' `
  --glob '!artifacts/**' `
  'C:\\Users|token|password|secret|api[_-]?key|auth\.json|rollout-.*\.jsonl'
```

逐项确认命中只是文档、安全扫描规则或协议术语，而不是真实值。还应确认：

- 未跟踪 `bin/`、`obj/`、`artifacts/`、EXE、PDB、日志、转储、设置、凭据或
  会话文件；
- 协议夹具没有真实邮箱、账户标识或凭据；
- 截图由预览模式生成，不含真实账户数据；
- 发布 EXE 与 ZIP 不包含构建机绝对路径、用户数据或运行时缓存；
- `SHA256SUMS.txt` 能验证两个下载资产；
- 本地 `error.log` 不进入仓库或发布包；
- `build`、`codeql` 和标签 `release` 工作流均成功。

## 发布后验证

从 Release 页面重新下载资产，而不是复用本地文件：

```powershell
Get-FileHash .\CodexOrbit-3.2.1-windows-x64.exe -Algorithm SHA256
gh attestation verify .\CodexOrbit-3.2.1-windows-x64.exe `
  --repo ferretgeek/CodexOrbit
```

同时验证 ZIP 和 `SHA256SUMS.txt`，再在 Windows 上启动 EXE，检查托盘、迷你窗、
圆环、刷新和退出。来源证明只能确认资产由指定 GitHub 仓库工作流生成，不等同于
商业 Authenticode 签名，也不会自动消除 SmartScreen 的未知发布者提示。
