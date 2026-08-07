# 第三方与商标说明

[English](THIRD_PARTY_NOTICES.en.md)

Codex Orbit 的应用代码仅引用 Windows 和 .NET Framework 4.8 提供的系统程序集，
发布包不捆绑 NuGet 第三方运行库。

项目会调用用户自行安装的 Codex App、Codex CLI 或 IDE 扩展中的
`codex app-server`。这些 Codex 文件不属于本仓库，也不会被提交或重新分发；
其许可、服务条款与隐私政策由相应提供方负责。

GitHub Actions 工作流使用 GitHub 的 `actions/*` 和 Microsoft 的
`microsoft/setup-msbuild`，这些工具只在 CI 中运行，不进入应用发布包。

“OpenAI”“ChatGPT”和“Codex”是其各自权利人的商标。本项目与 OpenAI 没有
隶属、授权、赞助或背书关系。
