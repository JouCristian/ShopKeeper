# 小铺掌柜

“小铺掌柜”是面向家庭小卖铺的本地离线桌面软件。当前仓库已完成第三阶段：项目骨架、SQLite 初始化脚本、主窗口导航、登录与配置、商品分类管理、商品新增/编辑/停用/启用、搜索与筛选。

## 技术栈

- C# WinForms
- .NET Framework 4.8
- SQLite 本地数据库
- 单机离线运行，不使用 Electron、Tauri、WebView、联网服务或云端账号

## 目录结构

```text
XiaoPuZhangGui.sln
src/
  XiaoPuZhangGui/
    Forms/
    Models/
    Services/
    Repositories/
    Database/
    Utils/
    Exports/
    Backups/
```

## 运行方式

1. 在开发电脑安装 Visual Studio 2019/2022 或 Build Tools，并安装 .NET Framework 4.8 Developer Pack。
2. 打开 `XiaoPuZhangGui.sln`。
3. 还原 NuGet 包。
4. 编译并运行 `XiaoPuZhangGui` 项目。

也可以在命令行使用 MSBuild：

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" XiaoPuZhangGui.sln /restore /p:Configuration=Debug
```

首次启动时，程序会在运行目录下自动生成：

- `app.config.xml`：保存店铺名称、数据库路径、备份路径。
- `database\shop.db`：SQLite 数据库文件。
- `backups\`：本地备份目录。

## 第一阶段已完成

- 创建解决方案 `XiaoPuZhangGui.sln`。
- 创建 WinForms 主项目 `XiaoPuZhangGui`。
- 创建基础目录 `Forms`、`Models`、`Services`、`Repositories`、`Database`、`Utils`、`Exports`、`Backups`。
- 编写 SQLite 初始化脚本 `Database/schema.sql`。
- 实现首次启动自动创建 `shop.db`。
- 初始化默认商品分类：烟酒、饮料、零食、方便食品、日用品、调味品、冷冻食品、其他。
- 实现主窗口左侧导航和基础页面占位。
- 实现 `AppConfig` 配置保存。
- 启动时检查并创建 `backups` 目录。

## 第二阶段已完成

- 首次启动显示初始化向导，填写店铺名称、6 位 PIN、确认 PIN。
- 初始化后生成恢复密钥，恢复密钥只显示一次。
- 已初始化后启动显示登录页，正确 PIN 才能进入主界面。
- 登录页提供“忘记 PIN”，可用恢复密钥重置 PIN。
- PIN 和恢复密钥仅保存带 salt 的 PBKDF2 哈希，不保存明文。
- 系统设置页可查看店铺名称、数据库路径、备份路径、初始化状态和程序版本。
- 系统设置页支持保存店铺名称、打开备份目录、重新生成恢复密钥。

## 第三阶段已完成

- 商品管理页替换占位页面，支持名称/条码搜索、分类筛选、状态筛选。
- DataGridView 展示商品名称、分类、条码、规格、默认售价、库存、均价、最低库存、保质期、到期日期、状态和操作。
- 新增/编辑商品，支持默认售价、当前库存、库存均价、最低库存预警、是否启用保质期、到期日期和备注。
- 商品停用/重新启用，不做物理删除。
- 分类管理支持查看、新增、改名、停用/启用。
- 兼容已有 SQLite 数据库，启动时通过迁移补齐商品表新字段。

## 下一步建议

第四阶段建议进入进货入库基础能力：入库主单/明细、批次记录、移动加权平均成本更新。销售、盘点、赊账和报表仍建议继续分阶段推进。
