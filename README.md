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
- AntdUI 2.4.12（原生 Ant Design 浅色主题）+ Win7/DPI-safe 布局层
- Inno Setup 6.7.3 离线 installer

运行时不依赖网络服务，也不要求安装 Microsoft Excel。

## 已实现功能

- 现代化主框架：AntdUI.Menu 侧边导航、Ant Design 默认浅色主题、统一按钮/输入/选择/日期/表格交互
- 经营概览：今日净销售、本月净销售、当前库存、低库存、最近销售
- 首页“第一次使用”详细操作卡片，可展开/收起
- 首次启动自动新手引导：遮罩层 + 高亮框 + 箭头逐步指向关键功能
- 独立“使用帮助”页面，可随时重新播放新手引导
- 图书资料与多字段搜索：店内编码、ISBN、书名、作者、出版社、分类、出版年、版次、装帧、默认货架位、定价、默认进价、售价、备注
- 供应商资料
- 采购入库（供应商可选且默认“不区分”；支持按完整/部分 ISBN 或书名模糊搜索已有图书资料）
- 销售开单（支持扫码枪作为键盘输入 ISBN）
- 单据中心：销售单、采购单、销售退货单、采购退货单统一查看
- 销售退货 / 采购退货及防超量退货
- 当前库存查询、低库存筛选、库存调整与完整库存流水
- 销售、采购、退货、库存变动按日期查询
- 指定日期历史库存快照
- 六类报表导出为 .xlsx
- SQLite 数据库备份与恢复
- DPI-aware manifest
- 4K / 高 DPI：WinForms 使用 DPI 自动缩放与高 DPI 自动重排；主要工具栏、表格列和弹窗采用响应式布局

## 自出版物与商品编码

- ISBN 现在是可选业务标识，不再承担店内商品编码职责。
- 新建图书若没有店内编码，系统自动生成 `BK-000001` 形式的唯一编码。
- 采购入库与销售开单都可按店内编码、ISBN、书名或作者查找，因此无 ISBN 的自出版物可以完整走入库、销售和库存流程。
- 图书编辑页只展示一个“销售价格”，保存时同步写入旧库中的定价 / 零售价字段以保持历史兼容。
- 出版社、出版年、版次和装帧均为可选整理字段，书名仍是唯一必填的核心资料。

## 图书资料增强

轻量版只吸收完整版 Flutter 系统中对小型书店最有价值的字段：店内编码、出版年、版次、装帧、默认货架位、默认进价和备注。默认进价用于采购入库预填，但正式采购单仍保存当次实际进价。旧数据库会自动补齐这些字段并升级 schema，不需要删库重建。

## UI 重构标准与第一阶段

项目现在以 [docs/UI_DESIGN_STANDARD.md](docs/UI_DESIGN_STANDARD.md) 作为 UI 单一验收标准。本轮开始引入 **AntdUI 2.4.12**，用于替换核心流程中的输入框、下拉框、数值输入、按钮与高频数据表格，同时仅用内部 UiTheme 约束字体回退、DPI 安全、间距和响应式布局；AntdUI 控件的颜色、圆角、hover / active / selected 状态优先使用组件库原生主题。选择固定版本是为了继续保持 Win7 SP1 / .NET Framework 4.8 / x86 / 完全离线部署边界。

第一阶段已优先重构：

- **零售开单**：动作工具栏、商品输入区、购物车大表格、结算备注、底部数量 / 应收摘要与确认结账；窄窗口自动隐藏次要表格列。
- **图书资料**：查询卡、统一动作区、结果 / 低库存摘要、主表格、宽屏右侧详情面板；小窗口自动折叠详情面板。
- **图书编辑**：字段按基础信息、分类与出版、价格信息、经营备注重新分组；使用内容驱动的字段高度和两列表单 Grid，Body 可滚动、Footer 固定。
- **全局主题策略**：AntdUI 使用默认 Ant Design Light（默认主色 #1677FF）；UiTheme 只提供 Win7-safe 字体回退、字体度量、最小高度和响应式断点。

这轮的重点是先把信息结构、Grid 排列、尺寸系统和响应式规则做好，再逐页迁移采购入库、库存、单据中心、报表、供应商等页面。

第二阶段继续按同一标准完成：

- **采购入库**：改造成与零售开单一致的工作流结构，补充店内编码、货架位、当前库存、小计、入库册数与采购金额汇总；供应商明确为可选并默认“不区分”。
- **库存管理**：增加结果 / 低库存 / 库存总册数摘要，宽屏右侧库存详情，窄屏自动折叠；库存调整改为“增加 / 减少 + 数量 + 调整后库存预览”，不再要求用户手工理解正负数。
- **共享图书选择器**：统一销售和采购中的搜索、表格字段、响应式列隐藏和 Dialog 尺寸行为。

第三阶段继续重构单据与退货工作流：

- **单据中心**：统一单据类型 / 日期 / 关键词查询区，增加单据数量与金额汇总；单据列表和书目明细按窗口高度响应式分区，并补齐 Empty State 和动态列格式。
- **销售 / 采购退货**：明确原单号和库存影响，补充“一键最大可退 / 清零”、实时项目数 / 册数 / 金额汇总，以及提交前库存影响确认；采购退货仍同时受原单可退数量与当前库存约束。

第四阶段继续统一报表和供应商管理：

- **报表与导出**：统一报表类型 / 日期范围 / 查询 / 导出结构，增加明细行、数量 / 库存和金额摘要；空结果明确提示并禁止导出空业务报表，表格列按窗口宽度响应式隐藏。
- **供应商管理**：增加综合搜索、启用 / 停用统计、宽屏右侧详情和统一新增 / 编辑 Dialog；停用只影响新的采购选择，不删除历史采购单中的供应商快照。

第五阶段优先处理实际运行暴露出的高 DPI / 字体裁切问题，而不是继续堆视觉装饰：

- **DPI-safe 基础层**：Label、AntdUI 按钮、输入、日期、数值框和 Table / 兼容 DataGridView 按实际字体高度计算安全最小尺寸；过小的 TableLayout 固定文本行自动改为 AutoSize 或扩高。
- **输入框基线**：不再把原生单行 TextBox 强行拉成固定高控件，避免文字贴顶和“只显示一半”；高度感由外层 Grid / Margin 提供。
- **主框架 / Dashboard**：主导航使用 AntdUI.Menu 原生 Inline 模式与选中/hover 状态；首页只读表格也使用 AntdUI.Table，指标卡支持 4 列 ↔ 2×2 响应式布局。
- **遗留页面扫尾**：设置、备份、帮助、新手引导和资料编辑表单全部移除容易裁字的 18–34px 文本固定高度，并统一垂直对齐。

第六阶段继续做全项目布局一致性扫尾：

- **清理剩余固定文本行**：销售、采购、图书资料、库存、单据、退货、报表、供应商、图书选择器和编辑 Dialog 不再使用容易和字体/DPI 冲突的小型 Absolute RowStyle。
- **详情与表头对齐**：列表标题、详情标题、详情字段和值统一使用内容驱动高度 + 最小尺寸，避免靠顶部 Padding 人工“推位置”。
- **自动 UI 合约自检**：--self-test 现在还会实例化主要页面，在 1024×768 基线检查 Label 裁切、固定高度单行 TextBox、过矮按钮和小型绝对 TableLayout 行，阻止这些问题再次进入构建产物。

第七阶段开始从“能正确显示”转向“看起来像正式商业软件”：

- **去系统边框感**：业务卡片和分区改为依靠 Surface / Background / 留白建立层级，不再大量使用 WinForms FixedSingle 边框。
- **按钮层级**：主操作保留强调色和粗体，次级操作使用正常字重，降低工具栏噪音。
- **零售 / 采购工作区**：补齐 ISBN 字段标签、销售/入库明细标题、可编辑字段提示和更清楚的空状态。
- **图书资料**：统一为“查询结果 / 图书详情”等面向用户的文案，并收紧表格、详情与查询区的视觉层级。



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

安装程序要求 **Windows 7 SP1 或更高**。Inno Setup 脚本明确设置 `MinVersion=6.1sp1`，因此未安装 SP1 的 Windows 7 会在安装前被阻止。如果检测到电脑还没有 .NET Framework 4.8，会直接运行安装包内部附带的微软离线安装程序，不需要联网下载。

如果 .NET Framework 4.8 是本次安装中新装的，安装完成后不会强行立即启动 BOOK DESK；部分 Win7 机器可能需要先重启一次。

## 本地构建依赖

开发电脑不再要求单独安装 .NET Framework 4.8 Targeting Pack。项目通过 NuGet 固定引用 `Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3`，因此只要本机具有可用的 Visual Studio/MSBuild 和 NuGet 访问能力，就可以还原 .NET Framework 4.8 编译参考程序集。目标 Win7 运行时仍由最终离线 installer 内置的 .NET Framework 4.8 runtime 负责。

## Visual Studio 本地生成与 NuGet 还原

仓库根目录现在提供 `NuGet.Config`，显式允许 Visual Studio 在生成前自动还原缺失的 NuGet 包，并保证 `nuget.org` 可作为包源。这样在拉取包含新依赖的提交（例如 AntdUI）后，不会因为本机尚未缓存新包而直接出现 `CS0246` “找不到 AntdUI” 一类编译错误。

如果 Visual Studio 在执行 `git pull` 时一直处于打开状态，建议在拉取后重新加载解决方案；也可以右键解决方案选择“还原 NuGet 程序包”。命令行可使用：

```bat
msbuild Win7BookManagement.sln -restore /m /p:Configuration=Release /p:Platform=x86
```

如果生成失败后 Visual Studio 询问“是否运行上次成功的生成”，选择“是”只会启动上一次成功生成的旧 EXE，不包含本次尚未成功编译的代码改动。

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
7. 准备 Inno Setup 6.7.3 编译器（本机未安装时自动从官方 GitHub Release 获取）；
8. 生成最终离线安装包。

成功后直接得到：

```text
installer\output\Win7BookManagement-Offline-Setup.exe
```

本地生成 installer 需要开发机安装 Visual Studio / MSBuild。Inno Setup 6.7.3 会自动准备到项目的 `.tools` 目录；若用于符合 Inno Setup 官方定义的商业用途，请按其当前许可要求购买商业许可证。首次构建若本机没有缓存 .NET 4.8 离线安装程序或 Inno Setup 6.7.3 编译器，**构建过程**需要网络；生成出来的最终 installer 在目标 Win7 上不需要网络。


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
