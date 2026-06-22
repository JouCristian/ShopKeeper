# 小铺掌柜

“小铺掌柜”是面向家庭小卖铺的本地离线桌面软件，使用 C# WinForms + .NET Framework 4.8 + SQLite 实现。软件面向 Windows 7 SP1 及以上环境，不依赖 Office，不使用 Electron、Tauri、WebView、联网服务或云端账号。

## 功能范围

- 首次初始化、6 位 PIN 登录、恢复密钥重置 PIN。
- 商品分类、商品新增/编辑/停用/启用。
- 进货入库、库存批次、移动加权平均成本。
- 销售记账、库存扣减、销售毛利润。
- 库存盘点、报废登记、报废损失。
- 赊账销售、部分还款、多次还款、结清状态。
- 首页看板、日报/月报/自定义范围经营统计。
- Excel/WPS `.xlsx` 导出。
- 数据备份、恢复前备份、从备份包恢复。

## 开发运行

1. 安装 Visual Studio 2019/2022 或 Build Tools。
2. 安装 .NET Framework 4.8 Developer Pack。
3. 打开 `XiaoPuZhangGui.sln`，还原 NuGet 包。
4. 编译并运行 `XiaoPuZhangGui` 项目。

命令行编译示例：

```powershell
& "D:\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" .\XiaoPuZhangGui.sln /restore /p:Configuration=Debug
```

## 运行时目录

Debug 开发模式下，运行数据统一放在项目根目录 `.runtime`：

```text
.runtime/
  app.config.xml
  database/
    shop.db
  backups/
  exports/
```

Release 正式运行时，运行数据放在程序运行目录 `data`：

```text
data/
  app.config.xml
  database/
    shop.db
  backups/
  exports/
```

`.runtime`、`data`、`shop.db`、`app.config.xml`、`backups`、`exports`、`bin`、`obj` 都不应提交到 GitHub。`shop.db` 是本地业务数据库，包含真实经营数据时尤其不能提交。

## Release 部署

1. 使用 `Configuration=Release` 编译。
2. 将 `src\XiaoPuZhangGui\bin\Release` 目录复制到目标电脑。
3. 目标电脑需安装 .NET Framework 4.8。
4. 第一次运行 `XiaoPuZhangGui.exe` 时，程序会在运行目录下创建 `data`。
5. 如果 `data\app.config.xml` 不存在，会进入首次初始化。
6. 如果 `data\database\shop.db` 不存在，会创建数据库并写入默认分类。
7. 不要把开发目录 `.runtime` 复制到正式 Release 目录。

Release 目录至少应包含：

- `XiaoPuZhangGui.exe`
- `XiaoPuZhangGui.exe.config`
- `Database\schema.sql`
- `System.Data.SQLite.dll`
- `x86\SQLite.Interop.dll`
- `x64\SQLite.Interop.dll`
- `NPOI.dll`
- `NPOI.OOXML.dll`
- `NPOI.OpenXml4Net.dll`
- `NPOI.OpenXmlFormats.dll`
- `ICSharpCode.SharpZipLib.dll`
- `BouncyCastle.Crypto.dll`

## 备份与恢复

备份包为 zip 文件，默认写入运行时 `backups` 目录。命名格式：

```text
小铺掌柜_备份_备份类型_YYYYMMDD_HHmmss.zip
```

备份包内容：

```text
database/shop.db
app.config.xml
backup_info.txt
```

`backup_info.txt` 记录软件名称、备份时间、备份类型、数据库路径、配置路径。

自动备份规则：

- 每天首次启动自动备份一次。
- 每次退出软件自动备份一次。
- 自动备份失败不会阻止启动或退出。
- 自动备份默认保留最近 60 个。
- 清理旧备份时只清理自动备份，不删除手动备份。

手动备份：

- 在“系统设置”中点击“立即备份”，保存到默认备份目录。
- 点击“备份到其他位置”，可选择 U 盘或其他目录。
- 手动备份不会被自动清理。

从备份恢复：

- 在“系统设置”中点击“从备份恢复”。
- 恢复前会提示确认，并先生成“恢复前备份”。
- 支持从 zip 备份包恢复。
- 支持选择单独 `.db` 文件，仅恢复数据库。
- 恢复完成后请重启软件。
- 如果数据库被占用导致无法替换，程序会提示失败并保留当前数据。

## 正式试用前验收清单

1. 首次初始化：新 Release 目录首次启动，能设置店铺名称和 6 位 PIN。
2. PIN 登录：关闭后重新打开，使用同一 PIN 能登录。
3. 商品新增：能新增商品、分类、条码、售价、最低库存。
4. 商品编辑：能修改商品信息，停用/启用状态正确。
5. 进货入库：能登记进货单、批次、数量、进价、到期日期。
6. 销售记账：能完成全款销售，销售单和明细正确保存。
7. 库存扣减：销售后商品库存和批次剩余数量正确扣减。
8. 库存盘点：能登记盘盈/盘亏，库存修正正确。
9. 报废登记：能登记报废数量、原因和损失金额。
10. 赊账销售：实收小于应收时能生成赊账记录。
11. 部分还款：能登记小于剩余欠款的还款。
12. 赊账结清：还清后状态变为已结清。
13. 首页看板：能显示今日指标、库存提醒、临期提醒、赊账提醒和快捷入口。
14. 经营报表：日报、月报、自定义范围统计结果正确。
15. Excel 导出：经营报表、库存清单、赊账清单、临期清单能导出并被 WPS/Excel 打开。
16. 手动备份：系统设置页“立即备份”能生成 zip 备份包。
17. 从备份恢复：恢复前生成恢复前备份，恢复后重启能读取数据。
18. Release 首次启动：不依赖源码目录，不依赖 `.runtime`。
19. Release 重新启动：继续使用同一个 `data` 目录，数据不丢失。
20. Win7 目标机运行检查：目标机安装 .NET Framework 4.8 后，程序能启动、登录、读写 SQLite、导出 Excel。

