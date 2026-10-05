namespace FixFlow.Mobile.Core.Storage;

public static class DatabaseMigrations
{
    public static IReadOnlyList<DatabaseMigration> All { get; } =
    [
        new(1, "create_work_orders",
        [
            """
            CREATE TABLE work_orders (
                id TEXT NOT NULL PRIMARY KEY,
                number TEXT NOT NULL,
                device_id TEXT NOT NULL,
                device_serial_number TEXT NOT NULL,
                device_model TEXT NOT NULL,
                client_id TEXT NOT NULL,
                client_name TEXT NOT NULL,
                description TEXT NOT NULL,
                priority TEXT NOT NULL,
                status TEXT NOT NULL,
                due_date INTEGER NOT NULL,
                is_overdue INTEGER NOT NULL,
                created_at INTEGER NOT NULL,
                started_at INTEGER,
                completed_at INTEGER,
                invoiced_at INTEGER)
            """,
            "CREATE INDEX ix_work_orders_due_date ON work_orders (due_date)",
        ]),
        new(2, "create_parts",
        [
            """
            CREATE TABLE parts (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                catalog_number TEXT NOT NULL,
                stock_quantity INTEGER NOT NULL,
                unit_price TEXT NOT NULL,
                archived_at INTEGER,
                search_text TEXT NOT NULL)
            """,
        ]),
        new(3, "create_sync_states",
        [
            "CREATE TABLE sync_states (key TEXT NOT NULL PRIMARY KEY, value TEXT NOT NULL, updated_at INTEGER NOT NULL)",
        ]),
    ];
}
