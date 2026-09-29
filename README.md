# Win7 Book Management

一个面向小型书店的简易、现代化、完全离线进销存桌面软件。

## 目标环境

- Windows 7 SP1（32 位或 64 位）
- .NET Framework 4.8
- 完全离线运行
- SQLite 单机数据库
- 程序按 x86 构建，以同时兼容 32 位和 64 位 Win7

> Windows 7 已停止微软官方支持。本项目仍以 Win7 SP1 兼容为硬约束。最终发布的离线安装包会把应用运行 DLL、SQLite 原生 DLL 和 .NET Framework 4.8 离线运行时一起封装，目标电脑安装时不需要联网下载依赖。

## 技术栈

- C# / WinForms
- .NET Framework 4.8
- System.Data.SQLite.Core 1.0.118
- NPOI 2.6.2
- SQLite
- 自定义 Win7-safe UI theme
- NSIS 3.13 离线 installer

运行时不依赖网络服务，也不要求安装 Microsoft Excel。

## 已实现功能

- 现代化主框架：深色导航、明确选中态、浅色工作区、统一按钮/表格/输入框视觉
- 经营概览：今日净销售、本月净销售、当前库存、低库存、最近销售
- 首页“第一次使用”详细操作卡片，可展开/收起
- 首次启动自动新手引导：遮罩层 + 高亮框 + 箭头逐步指向关键功能
- 独立“使用帮助”页面，可随时重新播放新手引导
- 图书资料与多字段搜索
- 供应商资料
- 采购入库
- 销售开单（支持扫码枪作为键盘输入 ISBN）
- 单据中心：销售单、采购单、销售退货单、采购退货单统一查看
- 销售退货 / 采购退货及防超量退货
- 当前库存查询、低库存筛选、库存调整与完整库存流水
- 销售、采购、退货、库存变动按日期查询
- 指定日期历史库存快照
- 六类报表导出为 .xlsx
- SQLite 数据库备份与恢复
- DPI-aware manifest

## 新手使用路线

第一次使用只需要记住：

1. **图书资料**：先建立图书和 ISBN。
2. **供应商**：建立常用供货方。
3. **采购入库**：进货后库存自动增加。
4. **销售开单**：扫码结账后库存自动减少。
5. **单据中心**：查历史单据；正常退货必须从原单发起。
6. **报表与导出**：按日期查询并导出 Excel。
7. **备份与恢复**：建议每天关店前创建备份。

第一次启动会自动出现交互式引导；以后可在首页或“使用帮助”中重新播放。

## 完全离线安装包

最终安装包名称：

`installer\output\Win7BookManagement-Offline-Setup.exe`

它会封装：

- `Win7BookManagement.exe`
- 程序 Release 目录中的全部托管 DLL
- `System.Data.SQLite.dll`
- x86 `SQLite.Interop.dll`
- NPOI 及其运行依赖
- Microsoft .NET Framework 4.8 离线运行时

安装程序要求 **Windows 7 SP1 或更高**。如果检测到电脑还没有 .NET Framework 4.8，会直接运行安装包内部附带的微软离线安装程序，不需要联网下载。

如果 .NET Framework 4.8 是本次安装中新装的，安装完成后不会强行立即启动 BOOK DESK；部分 Win7 机器可能需要先重启一次。

## 本地构建依赖

开发电脑不再要求单独安装 .NET Framework 4.8 Targeting Pack。项目通过 NuGet 固定引用 `Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3`，因此只要本机具有可用的 Visual Studio/MSBuild 和 NuGet 访问能力，就可以还原 .NET Framework 4.8 编译参考程序集。目标 Win7 运行时仍由最终离线 installer 内置的 .NET Framework 4.8 runtime 负责。

## 一条命令生成 installer

在仓库根目录打开 CMD 或 PowerShell，执行：

```bat
build-installer.cmd
```

脚本会自动：

1. 查找 MSBuild；
2. 还原 NuGet；
3. 编译 `Release | x86`；
4. 运行应用自检；
5. 检查关键 SQLite 运行 DLL；
6. 准备 .NET Framework 4.8 官方离线运行时；
7. 准备 NSIS 3.13 编译器（本机未安装时自动使用官方便携版）；
8. 生成最终离线安装包。

成功后直接得到：

```text
installer\output\Win7BookManagement-Offline-Setup.exe
```

本地生成 installer 需要开发机安装 Visual Studio / MSBuild。首次构建若本机没有缓存 .NET 4.8 离线安装程序或 NSIS 编译器，**构建过程**需要网络；生成出来的最终 installer 在目标 Win7 上不需要网络。


## GitHub 自动构建

GitHub Actions 在 push / pull request 时会：

- 编译 Release x86；
- 运行数据库、进销存、退货、报表、Excel、备份自检；
- 生成离线 installer；
- 上传两个 artifact：
  - `win7-book-management-x86`
  - `win7-book-management-offline-installer`

因此即使本机不编 installer，也可以从成功的 Actions 构建直接取得安装包。

## 数据位置

程序优先使用：

`C:\ProgramData\Win7BookManagement\`

如果当前用户无法写入该目录，会自动回退到：

`%LOCALAPPDATA%\Win7BookManagement\`

其中：

- `data\bookstore.db`：主数据库
- `backup\`：建议的备份目录
- `exports\`：建议的 Excel 导出目录

## 开发约束

修改代码前请先阅读根目录的 [AGENTS.md](AGENTS.md)。Win7 / .NET Framework 4.8 / 离线运行属于硬约束。


## 已验证的离线 installer

GitHub Actions 已实际完成离线 installer 构建，生成文件：

`Win7BookManagement-Offline-Setup.exe`

当前验证构建中的 installer 约 **125.6 MB**，构建脚本会在生成后检查其体积，若异常偏小则直接判定失败，以避免漏打包 .NET Framework 4.8 离线运行时。

目标 Win7 SP1 电脑安装时不需要联网寻找 DLL，也不需要单独安装 Microsoft Excel、SQLite、NPOI 或 Visual C++ Redistributable。安装程序会携带应用 Release 目录中的全部运行文件，并在检测到缺少 .NET Framework 4.8 时使用内置的微软离线运行时安装。
