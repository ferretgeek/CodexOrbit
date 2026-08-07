# GitHub 发布指南

[English](PUBLISHING.en.md)

这份目录可以直接作为仓库根目录使用。不要把它再嵌套到另一个同名目录中。

## 首次创建仓库

1. 在 GitHub 创建一个空仓库，建议名称为 `CodexOrbit`。
2. 不要在 GitHub 页面额外生成 README、许可证或 `.gitignore`，本目录已经包含。
3. 将本目录内容上传或用本机 Git 推送。
4. 在仓库 **Settings → General → Features** 中启用 Issues 与 Discussions。
5. 在 **Settings → Code security and analysis** 中启用 Private vulnerability
   reporting。
6. 确认 Actions 中的 `build` 工作流成功；建议为 `main` 设置分支保护并要求该
   检查通过。

项目没有写死维护者用户名或仓库 URL，因此首次发布前不需要替换个人占位符。

## 发布 Release

在本机运行：

```powershell
.\scripts\package.ps1
```

脚本会先构建和测试，然后在 `artifacts` 中生成：

```text
CodexOrbit-<版本>-windows-x64.exe
CodexOrbit-<版本>-windows-x64.zip
SHA256SUMS.txt
```

ZIP 内包含 EXE、MIT 许可证、完整中英双语文档、预览图，以及包内 EXE 的
SHA-256。

创建 GitHub Release 时：

1. Tag 使用 `v3.1.2` 这类格式。
2. 标题与 Tag 保持一致。
3. 在 `docs/releases/v<版本>.md` 准备中英双语 Release Notes。
4. 上传独立 EXE、ZIP 与 `SHA256SUMS.txt`，不要上传 PDB、`obj` 或运行时缓存。
5. 发布后在一台未参与构建的 Windows 机器上下载并验证一次。

推送匹配的 `v*` 标签后，`release` 工作流会重新构建、测试、核对标签与二进制
版本，并自动创建或更新 Release。

## 发布前隐私检查

在仓库根目录运行：

```powershell
rg -n -i --hidden `
  --glob '!bin/**' `
  --glob '!obj/**' `
  'C:\\Users|token|password|secret|api[_-]?key|auth\.json|rollout-.*\.jsonl'
```

逐项确认命中只是文档中的安全说明或协议字段，而不是真实值。还应检查：

- 仓库中没有 `bin/`、`obj/`、`artifacts/`、EXE、PDB、日志和含真实账户
  数据的截图；
- 没有 `.env`、`settings.json`、`auth.json` 或会话 JSONL；
- Release EXE 中没有构建机路径；
- 截图使用预览模式生成，不含真实额度或套餐。

## 版本更新

版本号位于：

```text
src\CodexOrbit\Properties\AssemblyInfo.cs
```

同时更新 `AssemblyVersion`、`AssemblyFileVersion`、
`AssemblyInformationalVersion`、两份 CHANGELOG 和双语 Release Notes。完成真实
构建、确定性测试、脱敏真实账户检查和 UI 预览后再创建 Tag。
