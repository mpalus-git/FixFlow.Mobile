using FixFlow.Mobile.Core.Storage;

namespace FixFlow.Mobile.UnitTests.Storage;

public sealed class DatabaseMigrationsTests : IDisposable
{
    private readonly TemporaryDatabaseFile _file = new();

    [Fact]
    public async Task Should_Keep_Work_Order_With_Empty_Client_Contact_When_Schema_Is_Upgraded()
    {
        await using (var database = await LocalDatabase.OpenAsync(_file.Path, [.. DatabaseMigrations.All.Where(migration => migration.Version < 10)], TimeProvider.System))
        {
            await database.Connection.ExecuteAsync(
                """
                INSERT INTO work_orders (id, number, device_id, device_serial_number, device_model, client_id, client_name, description, priority, status, due_date, is_overdue, created_at)
                VALUES (?, 'ZL-2026-0001', ?, 'SN-1', 'Altherma 3', ?, 'Piekarnia', 'Przegląd', 'Normal', 'Assigned', 0, 0, 0)
                """,
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString());
        }

        await using var upgraded = await LocalDatabase.OpenAsync(_file.Path, DatabaseMigrations.All, TimeProvider.System);

        (await upgraded.Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM work_orders WHERE client_city = '' AND client_phone = ''")).ShouldBe(1);
    }

    public void Dispose() => _file.Dispose();
}
