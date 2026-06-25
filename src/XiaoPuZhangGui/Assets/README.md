# 小铺掌柜本地视觉资源

本目录只放本地离线资源，不允许运行时联网加载图片或图标。程序运行使用 PNG；`svg` 目录只作为设计源文件备份，程序不依赖 SVG 渲染库。

## 资源优先级

程序读取资源时按以下顺序查找：

1. 自定义资源目录中的同名 PNG。
2. 程序目录 `Assets/` 中的内置默认 PNG。
3. GDI+ 生成的简洁占位图。

Debug 自定义目录：项目根目录 `.runtime/assets/`

Release 自定义目录：程序目录 `data/assets/`

自定义资源可以直接放在 `assets/` 下，也可以放在：

- `assets/icons/png/`
- `assets/illustrations/png/`
- `assets/illustrations/png/headers/`
- `assets/illustrations/png/empty/`
- `assets/illustrations/png/dashboard/`
- `assets/illustrations/png/report/`

## 图标

内置图标路径：`Assets/icons/png/{name}.png`

建议尺寸：

- 导航和小按钮：24x24 或 32x32 PNG
- 空状态和提醒：32x32 PNG
- 单个图标建议小于 20KB

导航图标：

- `nav_dashboard.png`
- `nav_sales.png`
- `nav_product.png`
- `nav_purchase.png`
- `nav_inventory.png`
- `nav_credit.png`
- `nav_report.png`
- `nav_settings.png`

操作图标：

- `action_add.png`
- `action_search.png`
- `action_save.png`
- `action_export.png`
- `action_backup.png`
- `action_restore.png`
- `action_refresh.png`

空状态图标：

- `empty_product.png`
- `empty_sales.png`
- `empty_purchase.png`
- `empty_inventory.png`
- `empty_credit.png`
- `empty_report.png`

分类图标：

- `category_smoke.png`
- `category_drink.png`
- `category_snack.png`
- `category_food.png`
- `category_daily.png`
- `category_frozen.png`
- `category_other.png`

## 插图

内置插图路径：`Assets/illustrations/png/{name}.png`

建议尺寸：480x200 PNG。插图建议简洁、不要包含大量文字，单张建议小于 300KB。

当前插图：

- `dashboard_hero.png`
- `shop_hero.png`
- `login_hero.png`
- `first_run_hero.png`
- `recovery_key_hero.png`

分区插图：

- `headers/product.png`：商品管理页头
- `headers/sales.png`：销售记账页头
- `headers/purchase.png`：进货入库页头
- `headers/inventory.png`：库存盘点页头
- `headers/credit.png`：赊账管理页头
- `dashboard/advice.png`：首页掌柜提示
- `report/header.png`：经营报表筛选区右侧插图
- `empty/general.png`：通用空状态
- `empty/product.png`：商品管理空状态
- `empty/sales_cart.png`：当前销售单空状态
- `empty/sales_orders.png`：今日销售单空状态
- `empty/purchase.png`：进货入库空状态
- `empty/credit.png`：赊账管理空状态
- `empty/report.png`：经营报表空状态
- `empty/dashboard.png`：首页提醒和排行空状态

## 替换方法

打开“系统设置 -> 界面资源 -> 打开自定义资源目录”，放入同名 PNG 后点击“刷新界面资源”。切换页面后会重新读取资源。

不要把 `.runtime/`、`data/`、导出的 Excel、备份包或数据库文件提交到 Git。
