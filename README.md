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
- 经营概览：今日净销售、本月净销售、当前库存、低库存、最近销售
- 图书资料与多字段搜索
- 供应商资料
- 采购入库
- 销售开单（支持扫码枪作为键盘输入 ISBN）
- 单据中心：销售单、采购单、销售退货单、采购退货单统一查看
- 单据明细：直接查看每张原单的原数量、已退数量、可退数量
- 销售退货：从原销售单发起，按原售价退款并自动加回库存
- 采购退货：从原采购单发起，按原进价退给供应商并自动扣减库存
- 防超量退货：同一原单明细累计退货不能超过原数量
- 防负库存：采购退货不能让库存变成负数
- 离开未提交采购/销售单时二次确认
- 当前库存查询与低库存筛选
- 库存调整与完整库存流水
- 销售、采购、销售退货、采购退货、库存变动按日期查询
- 指定日期历史库存快照
- 六类报表导出为 .xlsx
- SQLite 数据库备份与恢复
- 系统 DPI-aware manifest

## 单据与退货设计

原销售单和采购单一旦生成就保持历史不变。退货不会修改原单，而是生成新的退货单并关联原单。

销售退货会：
1. 检查原销售明细剩余可退数量；
2. 使用原销售价格生成退货快照；
3. 增加当前库存；
4. 写入 SALE_RETURN 库存流水；
5. 影响退货发生日期的净销售额。

采购退货会：
1. 检查原采购明细剩余可退数量；
2. 检查当前库存足够；
3. 使用原采购进价生成退货快照；
4. 扣减当前库存；
5. 写入 PURCHASE_RETURN 库存流水。

上述步骤都在单个 SQLite 事务中执行，失败时整体回滚。

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

建议在 Windows 10/11 开发机使用 Visual Studio 2022 构建，生成目标仍然是 .NET Framework 4.8 / x86，可运行于 Windows 7 SP1。

1. 安装 Visual Studio，并启用“.NET 桌面开发”。
2. 打开 `Win7BookManagement.sln`。
3. 还原 NuGet 包。
4. 选择 `Release | x86`。
5. 构建解决方案。

输出目录：

`src\Win7BookManagement\bin\x86\Release\`

部署时复制整个 Release 目录，不要只复制 EXE。

## 自动构建

GitHub Actions 会在 push / pull request 时：
- 还原依赖；
- 编译 Release x86；
- 运行数据库、采购、销售、退货、库存回滚、单据查询、报表、Excel、备份自检；
- 上传完整运行目录作为构建产物。

## Win7 部署

目标电脑需要 Windows 7 SP1 和 .NET Framework 4.8。

可以直接复制 GitHub Actions 生成的完整目录运行，也可以使用 `installer/win7-book-management.iss` 通过 Inno Setup 生成安装包。

首次启动时会自动创建 SQLite 数据库；旧数据库会自动向前升级到当前 schema。

## 开发约束

修改代码前请先阅读根目录的 [AGENTS.md](AGENTS.md)。Win7 / .NET Framework 4.8 / 离线运行属于硬约束。
