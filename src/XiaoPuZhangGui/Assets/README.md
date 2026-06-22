# 小铺掌柜本地视觉资源

本目录只放本地资源，不允许运行时联网加载图片或图标。WinForms 运行时优先读取 `png` 目录中的图片；`svg` 目录只作为设计源文件，程序不依赖 SVG 渲染库。

## 图标

运行时 PNG 路径：`Assets/icons/png/{name}.png`

设计源文件路径：`Assets/icons/svg/{name}.svg`

建议尺寸：

- 导航和小按钮：20x20 或 24x24 PNG
- 空状态和提醒：32x32 PNG
- 图片体积尽量小，单个图标建议小于 20KB

当前图标资源：

- `home`：首页看板
- `sales`：销售记账
- `product`：商品管理
- `purchase`：进货入库
- `inventory`：库存盘点
- `credit`：赊账管理
- `report`：经营报表
- `settings`：系统设置
- `empty_box`：暂无商品 / 暂无数据
- `empty_cart`：暂无销售
- `empty_credit`：暂无赊账
- `warning_stock`：低库存
- `warning_expiry`：临期 / 已过期
- `backup`：备份
- `export_excel`：Excel 导出

## 首页插图

运行时 PNG 路径：`Assets/illustrations/png/shop_hero.png`

设计源文件路径：`Assets/illustrations/svg/shop_hero.svg`

建议尺寸：360x160 或 480x200，PNG/JPG 均可。插图建议保持简洁，不包含大量文字，文件体积建议小于 300KB。

## 替换规则

如果要替换资源，只需要保持文件名不变并替换对应 PNG。Release 包中也可以直接替换 `Assets/icons/png` 或 `Assets/illustrations/png` 下的 PNG 文件。资源加载失败时，程序会降级为代码生成的简洁占位图，不会影响页面打开。
