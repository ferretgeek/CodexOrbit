# 参与贡献

[English](CONTRIBUTING.en.md)

感谢帮助改进 Codex Orbit。

## 开发环境

- Windows 10/11 x64
- Visual Studio 2022 或可用的 MSBuild
- .NET Framework 4.8 Developer Pack
- PowerShell 7 或 Windows PowerShell 5.1

构建和确定性测试：

```powershell
.\scripts\test.ps1
```

真实账户集成测试：

```powershell
.\scripts\test.ps1 -Live
```

真实账户测试只读取额度，不应在 CI 中运行。

## 提交前检查

1. Release x64 构建零警告。
2. `--notification-test` 通过。
3. 修改 UI 时分别检查迷你窗和圆环，不出现裁切、遮挡或模糊。
4. 搜索并移除用户名、绝对路径、日志、Token、API Key 和设置文件。
5. 不提交 `bin/`、`obj/`、EXE、PDB、缓存运行时或截图中的真实账户信息。
6. 行为变化同步更新 README、隐私说明和 CHANGELOG。
7. 用户可见内容同时更新简体中文与英文文档。

## UI 预览

构建后可以生成不含真实账户数据的预览图：

```powershell
.\src\CodexOrbit\bin\x64\Release\CodexOrbit.exe `
  --render-mini-preview "$env:TEMP\codex-orbit-mini.png" `
  --theme midnight

.\src\CodexOrbit\bin\x64\Release\CodexOrbit.exe `
  --render-preview "$env:TEMP\codex-orbit-ring.png" `
  --theme midnight
```

可用主题以源码 `ThemeManager.Presets` 为准。

## 仅用于诊断的环境变量

| 变量 | 用途 |
| --- | --- |
| `CODEX_ORBIT_CODEX_PATH` | 指定一个 Codex 可执行文件 |
| `CODEX_ORBIT_ONLY_RUNTIME` | 限制运行时来源以便测试 |
| `CODEX_ORBIT_DISABLE_PACKAGED_RUNTIME=1` | 禁用 Windows 安装包运行时 |
| `CODEX_ORBIT_DISABLE_WSL=1` | 禁用 WSL 检测 |

`CODEX_ORBIT_ONLY_RUNTIME` 支持 `override`、`running`、`app`、`local`、
`cli`、`path`、`extension`、`package` 和 `wsl`。

这些变量不是普通用户必需配置，文档和错误报告中不要填写凭据。

## 代码范围

保持项目无遥测、无账户密钥存储、无不必要第三方依赖。协议兼容改动应同时补充
确定性测试，避免只能依赖真实账户验证。

参与本项目即表示同意遵守[行为准则](CODE_OF_CONDUCT.md)。
