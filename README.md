# 小铺掌柜

“小铺掌柜”是面向家庭小卖铺的本地离线桌面软件。当前仓库处于第一阶段：已搭建 C# WinForms / .NET Framework 4.8 项目骨架、SQLite 初始化脚本、主窗口导航和基础占位页面。

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

## 下一步建议

第二阶段建议先实现登录与设置初始化：店铺名称、6 位 PIN、恢复密钥，以及设置页中的数据库路径和备份路径查看。随后再进入商品管理与销售记账的核心业务。
