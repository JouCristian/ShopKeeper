# 小铺掌柜

“小铺掌柜”是面向家庭小卖铺的本地离线桌面软件。当前仓库已完成第四阶段：项目骨架、SQLite 初始化脚本、主窗口导航、登录与配置、商品管理、进货入库、库存批次和移动加权平均成本。

## 技术栈

- C# WinForms
- .NET Framework 4.8
- SQLite 本地数据库
- 单机离线运行，不使用 Electron、Tauri、WebView、联网服务或云端账号

## 运行方式

1. 在开发电脑安装 Visual Studio 2019/2022 或 Build Tools，并安装 .NET Framework 4.8 Developer Pack。
2. 打开 `XiaoPuZhangGui.sln`。
3. 还原 NuGet 包。
4. 编译并运行 `XiaoPuZhangGui` 项目。

也可以在命令行使用 MSBuild：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" XiaoPuZhangGui.sln /restore /p:Configuration=Debug
```

## 运行时数据目录

Debug 开发模式下，运行数据统一放在项目根目录的 `.runtime`：

```text
.runtime/
  app.config.xml
  database/
    shop.db
  backups/
  exports/
```

Release 模式下，运行数据放在程序运行目录的 `data`：

```text
data/
  app.config.xml
  database/
    shop.db
  backups/
  exports/
```

`.runtime`、`data`、`shop.db`、`app.config.xml`、`backups`、`exports`、`bin`、`obj` 都不应提交到 GitHub。`shop.db` 是本地业务数据库，包含真实经营数据时尤其不能提交。若开发阶段登录异常，可以先备份或删除 `.runtime\app.config.xml`，再重新启动程序完成初始化。

首次使用新的 Debug 运行目录时，程序会尝试把旧 `bin\Debug\app.config.xml` 和 `bin\Debug\database\shop.db` 复制到 `.runtime`，但不会覆盖 `.runtime` 中已有的新数据。

## 已完成范围

- 第一阶段：项目骨架、WinForms 主窗口、SQLite 初始化、默认分类、基础配置、备份目录。
- 第二阶段：首次初始化、6 位 PIN 登录、恢复密钥重置 PIN、系统设置页。
- 第三阶段：商品分类管理、商品新增/编辑/停用/启用、商品搜索筛选。
- 第四阶段：进货入库、入库明细、库存批次、移动加权平均成本。
- 第五阶段：销售记账、销售明细、应收/成本/毛利润计算、商品库存扣减、库存批次扣减。

## 下一步建议

下一阶段建议进入库存盘点基础能力：盘点单、库存修正、盈亏原因和报废登记。赊账管理、日报月报和 Excel 导出仍建议继续分阶段推进。
