# 贡献与构建

先阅读 [AGENTS.md](AGENTS.md)：目标始终是 Win7 SP1 / net48 / WinForms / x86 / 完全离线。金额用整数分，库存变动必须与流水同事务，历史单据不可改写。

## 本地验证

```bat
msbuild Win7BookManagement.sln -restore /m /p:Configuration=Debug /p:Platform=x86
msbuild Win7BookManagement.sln -restore /m /p:Configuration=Release /p:Platform=x86
dotnet test tests/Win7BookManagement.Tests/Win7BookManagement.Tests.csproj -c Release -p:Platform=x86 --no-build
src\Win7BookManagement\bin\x86\Release\Win7BookManagement.exe --self-test
tools\Win7BookManagement.UiTools\bin\x86\Release\Win7BookManagement.UiTools.exe --self-test
build-installer.cmd
```

 legacy 应用项目的每个 `.cs` 必须显式列入 csproj；提交前运行 `scripts/validate-csproj-sources.ps1`。测试项目也显式列出测试源文件。普通构建不生成图标；确需重绘时显式运行 `scripts/generate-app-icon.ps1` 并审查资源差异。

## 版本与发布

只修改根目录 `VERSION`。EXE 程序集版本、文件版本和安装器版本由构建注入；关于信息读取程序集。发布标签必须为 `v<VERSION>`。CI 生成安装包与 SHA256SUMS。

构建依赖哈希固定在 `scripts/build-installer.ps1`，下载和缓存都会校验；不要以“重新下载”绕过哈希失败。签名通过仓库 secrets `BOOKDESK_SIGNING_CERT`（Base64 PFX）和 `BOOKDESK_SIGNING_PASSWORD` 配置，使用 SHA-256 + RFC3161 时间戳。没有证书时产物明确未签名，不得宣称已签名。

## 验收

Windows CI 不能替代真实 Win7。发布前按 [Win7 烟测清单](docs/WIN7_SMOKE_TEST.md) 记录系统、补丁、DPI、耗时、打印机与 Office/WPS 版本。性能目标与测量方法见 [PERFORMANCE.md](docs/PERFORMANCE.md)。
