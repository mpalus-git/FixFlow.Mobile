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
        new(4, "create_clients_and_devices",
        [
            """
            CREATE TABLE clients (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                street TEXT NOT NULL,
                building_number TEXT NOT NULL,
                postal_code TEXT NOT NULL,
                city TEXT NOT NULL,
                contact_person TEXT NOT NULL,
                phone TEXT NOT NULL,
                email TEXT,
                archived_at INTEGER)
            """,
            """
            CREATE TABLE devices (
                id TEXT NOT NULL PRIMARY KEY,
                client_id TEXT NOT NULL,
                serial_number TEXT NOT NULL,
                model TEXT NOT NULL,
                manufacturer TEXT NOT NULL,
                installation_date TEXT NOT NULL,
                archived_at INTEGER)
            """,
        ]),
        new(5, "create_service_entries",
        [
            """
            CREATE TABLE service_entries (
                id TEXT NOT NULL PRIMARY KEY,
                work_order_id TEXT NOT NULL,
                technician_id TEXT NOT NULL,
                technician_name TEXT NOT NULL,
                note TEXT NOT NULL,
                is_correction INTEGER NOT NULL,
                work_started_at INTEGER,
                work_finished_at INTEGER,
                latitude REAL,
                longitude REAL,
                created_at INTEGER NOT NULL)
            """,
            "CREATE INDEX ix_service_entries_work_order_id ON service_entries (work_order_id)",
            """
            CREATE TABLE service_entry_parts (
                service_entry_id TEXT NOT NULL,
                position INTEGER NOT NULL,
                part_id TEXT NOT NULL,
                part_name TEXT NOT NULL,
                catalog_number TEXT NOT NULL,
                quantity INTEGER NOT NULL,
                unit_price TEXT NOT NULL,
                PRIMARY KEY (service_entry_id, position))
            """,
        ]),
        new(6, "create_device_work_orders",
        [
            """
            CREATE TABLE device_work_orders (
                device_id TEXT NOT NULL,
                work_order_id TEXT NOT NULL,
                number TEXT NOT NULL,
                status TEXT NOT NULL,
                priority TEXT NOT NULL,
                description TEXT NOT NULL,
                technician_name TEXT,
                created_at INTEGER NOT NULL,
                started_at INTEGER,
                completed_at INTEGER,
                service_entry_count INTEGER NOT NULL,
                PRIMARY KEY (device_id, work_order_id))
            """,
        ]),
        new(7, "create_outbox",
        [
            """
            CREATE TABLE outbox_operations (
                id TEXT NOT NULL PRIMARY KEY,
                work_order_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                payload TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                attempt_count INTEGER NOT NULL,
                next_attempt_at INTEGER,
                last_error TEXT)
            """,
            "CREATE INDEX ix_outbox_operations_work_order_id ON outbox_operations (work_order_id, created_at)",
            """
            CREATE TABLE sync_conflicts (
                operation_id TEXT NOT NULL PRIMARY KEY,
                work_order_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                payload TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                status_code INTEGER,
                error_code TEXT,
                detail TEXT,
                rejected_at INTEGER NOT NULL)
            """,
        ]),
        new(8, "create_work_timers",
        [
            """
            CREATE TABLE work_timers (
                work_order_id TEXT NOT NULL PRIMARY KEY,
                started_at INTEGER NOT NULL,
                location_status TEXT NOT NULL,
                latitude REAL,
                longitude REAL)
            """,
        ]),
    ];
}
