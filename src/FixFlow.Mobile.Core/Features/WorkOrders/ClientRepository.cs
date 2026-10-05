using FixFlow.Mobile.Core.Storage;
using SQLite;

namespace FixFlow.Mobile.Core.Features.WorkOrders;

public sealed class ClientRepository(UserDatabase userDatabase)
{
    private SQLiteAsyncConnection Connection => userDatabase.Current.Connection;

    public async Task UpsertAsync(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        await Connection.InsertOrReplaceAsync(ClientRow.From(client));
    }

    public async Task<Client?> GetAsync(Guid id)
    {
        var rows = await Connection.QueryAsync<ClientRow>("SELECT * FROM clients WHERE id = ?", id);
        return rows.FirstOrDefault()?.ToClient();
    }
}
