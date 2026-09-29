# Win7 Book Management

一个面向小型书店的简易、现代化、完全离线进销存桌面软件。

## 目标环境

- Windows 7 SP1（32 位或 64 位）
- .NET Framework 4.8
- 完全离线运行
- SQLite 单机数据库
- 程序按 x86 构建，以同时兼容 32 位和 64 位 Win7

> Windows 7 已停止微软官方支持。本项目的目标是兼容仍在使用的 Win7 SP1 设备，因此运行时保持保守；UI 则通过统一主题、布局和交互设计尽量避免传统 WinForms 的陈旧感。

## 技术栈

- C# / WinForms
- .NET Framework 4.8
- System.Data.SQLite.Core 1.0.118
- NPOI 2.6.2
- SQLite
- 自定义 Win7-safe UI theme
- 可选 Inno Setup 安装包

运行时不依赖网络服务，也不要求安装 Microsoft Excel。

## 已实现功能

- 现代化主框架：深色导航、明确选中态、浅色工作区、统一按钮/表格/输入框视觉
- 响应式主导航和百分比伸缩的数据卡片，优先适应较窄桌面窗口
- 经营概览：首页显示今日销售、本月销售、当前库存、低库存提醒、最近销售
- 可配置低库存阈值
- 图书资料：ISBN、书名、作者、出版社、分类、定价、售价、启用状态
- 图书搜索支持 ISBN、书名、作者、出版社、分类
- 供应商资料
- 采购入库
- 销售开单（支持扫码枪作为键盘输入 ISBN）
- 离开未提交采购/销售单时二次确认，避免误丢数据
- 当前库存查询与“仅看低库存”
- 库存调整
- 完整库存流水
- 按日期查询销售明细
- 按日期查询采购明细
- 按日期查询库存变动
- 指定日期的历史库存快照
- 四类报表导出为 .xlsx
- SQLite 数据库备份与恢复
- 系统 DPI-aware manifest

## UI 方向

UI 不依赖 Windows 10/11 专属 API。当前方案优先用原生 WinForms + 自定义视觉系统，确保 Win7 上行为稳定。

后续若引入第三方控件，要求同时满足：
- 明确支持 .NET Framework 4.8；
- Win7 实机 smoke test 通过；
- 不把业务逻辑绑定到 UI 厂商；
- 对可读性、输入效率或对话框体验有明确提升。

当前调研中，Krypton Toolkit 与 ReaLTaiizor 都提供 net48 构建，可作为后续局部控件升级候选；现阶段不为了“换皮”强行引入依赖。

## 库存设计

库存不是只保存一个最终数字。每次采购、销售和人工调整都会在同一个 SQLite 事务中：

1. 写入业务单据；
2. 更新当前库存；
3. 写入 `inventory_transactions` 库存流水；
4. 成功时一起提交，失败时一起回滚。

因此可以根据流水重建某个历史日期结束时的库存数量。

## 数据位置

程序优先使用：

`C:\ProgramData\Win7BookManagement\`

如果当前用户无法写入该目录，会自动回退到：

`%LOCALAPPDATA%\Win7BookManagement\`

其中：
- `data\bookstore.db`：主数据库
- `backup\`：建议的备份目录
- `exports\`：建议的 Excel 导出目录

## 本地构建

建议在 Windows 10/11 开发机使用 Visual Studio 2022 构建，生成的目标程序仍然是 .NET Framework 4.8 / x86，可运行于 Windows 7 SP1。

1. 安装 Visual Studio，并启用“.NET 桌面开发”。
2. 打开 `Win7BookManagement.sln`。
3. 还原 NuGet 包。
4. 选择 `Release | x86`。
5. 构建解决方案。

输出目录：

`src\Win7BookManagement\bin\x86\Release\`

把整个目录复制到目标电脑，不要只复制 EXE；SQLite 的本机 DLL 和其他依赖 DLL 也必须一起保留。

## 自动构建

GitHub Actions 会在 push / pull request 时：
- 还原依赖；
- 编译全部 UI 与业务代码（Release x86）；
- 运行数据库、设置、经营概览查询、库存事务、报表、Excel 和备份的 `--self-test`；
- 上传完整运行目录作为构建产物。

WinForms 的最终视觉/交互 smoke test 应在 Windows 7 SP1 实机完成，因为 CI runner 并不是 Win7 桌面会话。

## Win7 部署

目标电脑需要 Windows 7 SP1 和 .NET Framework 4.8。

可以直接复制 GitHub Actions 生成的完整目录运行，也可以使用 `installer/win7-book-management.iss` 通过 Inno Setup 生成安装包。

首次启动时会自动创建 SQLite 数据库和表结构；旧数据库启动时会以幂等方式升级到当前 schema。

## 开发约束

修改代码前请先阅读根目录的 [AGENTS.md](AGENTS.md)。Win7 / .NET Framework 4.8 / 离线运行属于硬约束。
