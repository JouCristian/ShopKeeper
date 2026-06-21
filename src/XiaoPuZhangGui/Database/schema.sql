PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    store_name TEXT NOT NULL DEFAULT '小铺掌柜',
    pin_hash TEXT NULL,
    pin_salt TEXT NULL,
    recovery_key_hash TEXT NULL,
    recovery_key_salt TEXT NULL,
    is_initialized INTEGER NOT NULL DEFAULT 0,
    database_path TEXT NULL,
    backup_path TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE IF NOT EXISTS categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    is_active INTEGER NOT NULL DEFAULT 1,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    category_id INTEGER NOT NULL,
    barcode TEXT NULL,
    specification TEXT NULL,
    default_price NUMERIC NOT NULL DEFAULT 0,
    current_stock NUMERIC NOT NULL DEFAULT 0,
    average_cost NUMERIC NOT NULL DEFAULT 0,
    min_stock_alert NUMERIC NOT NULL DEFAULT 0,
    requires_expiry INTEGER NOT NULL DEFAULT 1,
    expiry_date TEXT NULL,
    status TEXT NOT NULL DEFAULT '在售',
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (category_id) REFERENCES categories(id)
);

CREATE TABLE IF NOT EXISTS stock_batches (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    purchase_item_id INTEGER NULL,
    batch_code TEXT NULL,
    source_type TEXT NULL,
    source_id INTEGER NULL,
    quantity_in NUMERIC NOT NULL DEFAULT 0,
    quantity_remaining NUMERIC NOT NULL DEFAULT 0,
    purchase_price NUMERIC NOT NULL DEFAULT 0,
    production_date TEXT NULL,
    quantity NUMERIC NOT NULL DEFAULT 0,
    remaining_quantity NUMERIC NOT NULL DEFAULT 0,
    unit_cost NUMERIC NOT NULL DEFAULT 0,
    expiry_date TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS purchase_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    purchase_no TEXT NOT NULL UNIQUE,
    purchase_date TEXT NOT NULL,
    purchased_at TEXT NOT NULL,
    total_amount NUMERIC NOT NULL DEFAULT 0,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS purchase_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    purchase_record_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    product_name_snapshot TEXT NOT NULL,
    quantity NUMERIC NOT NULL,
    purchase_price NUMERIC NOT NULL DEFAULT 0,
    line_total NUMERIC NOT NULL DEFAULT 0,
    production_date TEXT NULL,
    unit_cost NUMERIC NOT NULL,
    expiry_date TEXT NULL,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (purchase_record_id) REFERENCES purchase_records(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS sales_orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_no TEXT NOT NULL UNIQUE,
    sale_time TEXT NULL,
    sold_at TEXT NOT NULL,
    total_amount NUMERIC NOT NULL DEFAULT 0,
    total_cost NUMERIC NOT NULL DEFAULT 0,
    receivable_amount NUMERIC NOT NULL DEFAULT 0,
    cost_amount NUMERIC NOT NULL DEFAULT 0,
    gross_profit NUMERIC NOT NULL DEFAULT 0,
    paid_amount NUMERIC NOT NULL DEFAULT 0,
    credit_amount NUMERIC NOT NULL DEFAULT 0,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS sales_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sales_order_id INTEGER NOT NULL,
    product_id INTEGER NULL,
    product_name_snapshot TEXT NOT NULL,
    quantity NUMERIC NOT NULL,
    sale_price_snapshot NUMERIC NOT NULL,
    cost_price_snapshot NUMERIC NOT NULL,
    line_amount NUMERIC NOT NULL DEFAULT 0,
    line_cost NUMERIC NOT NULL DEFAULT 0,
    line_profit NUMERIC NOT NULL DEFAULT 0,
    profit_snapshot NUMERIC NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (sales_order_id) REFERENCES sales_orders(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS credit_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    credit_no TEXT NULL UNIQUE,
    sales_order_id INTEGER NOT NULL,
    debtor_name TEXT NULL,
    original_amount NUMERIC NOT NULL DEFAULT 0,
    paid_amount NUMERIC NOT NULL DEFAULT 0,
    remaining_amount NUMERIC NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'Unpaid',
    credit_date TEXT NULL,
    settled_at TEXT NULL,
    remark TEXT NULL,
    contact_remark TEXT NULL,
    credit_amount NUMERIC NOT NULL DEFAULT 0,
    repaid_amount NUMERIC NOT NULL DEFAULT 0,
    balance_amount NUMERIC NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (sales_order_id) REFERENCES sales_orders(id)
);

CREATE TABLE IF NOT EXISTS credit_payments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    credit_record_id INTEGER NOT NULL,
    payment_date TEXT NOT NULL,
    amount NUMERIC NOT NULL DEFAULT 0,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (credit_record_id) REFERENCES credit_records(id)
);

CREATE TABLE IF NOT EXISTS repayment_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    credit_record_id INTEGER NOT NULL,
    repayment_amount NUMERIC NOT NULL,
    repaid_at TEXT NOT NULL,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (credit_record_id) REFERENCES credit_records(id)
);

CREATE TABLE IF NOT EXISTS stock_adjustments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    adjustment_type TEXT NOT NULL,
    before_quantity NUMERIC NOT NULL,
    after_quantity NUMERIC NOT NULL,
    difference_quantity NUMERIC NOT NULL,
    reason TEXT NOT NULL,
    remark TEXT NULL,
    adjusted_at TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS waste_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL,
    stock_batch_id INTEGER NULL,
    quantity NUMERIC NOT NULL,
    unit_cost NUMERIC NOT NULL,
    waste_cost NUMERIC NOT NULL,
    reason TEXT NOT NULL,
    remark TEXT NULL,
    wasted_at TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (product_id) REFERENCES products(id),
    FOREIGN KEY (stock_batch_id) REFERENCES stock_batches(id)
);

CREATE TABLE IF NOT EXISTS inventory_checks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    check_no TEXT NOT NULL UNIQUE,
    check_date TEXT NOT NULL,
    total_profit_quantity NUMERIC NOT NULL DEFAULT 0,
    total_loss_quantity NUMERIC NOT NULL DEFAULT 0,
    total_profit_amount NUMERIC NOT NULL DEFAULT 0,
    total_loss_amount NUMERIC NOT NULL DEFAULT 0,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS inventory_check_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    inventory_check_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    product_name_snapshot TEXT NOT NULL,
    system_stock NUMERIC NOT NULL DEFAULT 0,
    actual_stock NUMERIC NOT NULL DEFAULT 0,
    difference_quantity NUMERIC NOT NULL DEFAULT 0,
    cost_price_snapshot NUMERIC NOT NULL DEFAULT 0,
    difference_amount NUMERIC NOT NULL DEFAULT 0,
    reason TEXT NULL,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (inventory_check_id) REFERENCES inventory_checks(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS scrap_records (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    scrap_no TEXT NOT NULL UNIQUE,
    scrap_date TEXT NOT NULL,
    product_id INTEGER NOT NULL,
    product_name_snapshot TEXT NOT NULL,
    quantity NUMERIC NOT NULL,
    cost_price_snapshot NUMERIC NOT NULL DEFAULT 0,
    loss_amount NUMERIC NOT NULL DEFAULT 0,
    reason TEXT NOT NULL,
    remark TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NULL,
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS backup_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    backup_type TEXT NOT NULL,
    source_path TEXT NOT NULL,
    backup_path TEXT NOT NULL,
    status TEXT NOT NULL,
    message TEXT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE INDEX IF NOT EXISTS idx_products_category_id ON products(category_id);
CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);
CREATE INDEX IF NOT EXISTS idx_stock_batches_product_id ON stock_batches(product_id);
CREATE INDEX IF NOT EXISTS idx_stock_batches_expiry_date ON stock_batches(expiry_date);
CREATE INDEX IF NOT EXISTS idx_purchase_items_product_id ON purchase_items(product_id);
CREATE INDEX IF NOT EXISTS idx_sales_orders_sold_at ON sales_orders(sold_at);
CREATE INDEX IF NOT EXISTS idx_sales_items_product_id ON sales_items(product_id);
CREATE INDEX IF NOT EXISTS idx_inventory_checks_check_date ON inventory_checks(check_date);
CREATE INDEX IF NOT EXISTS idx_inventory_check_items_product_id ON inventory_check_items(product_id);
CREATE INDEX IF NOT EXISTS idx_scrap_records_scrap_date ON scrap_records(scrap_date);
CREATE INDEX IF NOT EXISTS idx_scrap_records_product_id ON scrap_records(product_id);
CREATE INDEX IF NOT EXISTS idx_credit_records_status ON credit_records(status);
CREATE INDEX IF NOT EXISTS idx_backup_logs_created_at ON backup_logs(created_at);

INSERT OR IGNORE INTO settings (id, store_name)
VALUES (1, '小铺掌柜');
