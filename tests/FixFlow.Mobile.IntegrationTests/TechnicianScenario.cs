namespace FixFlow.Mobile.IntegrationTests;

public sealed record TechnicianScenario(Guid TechnicianId, string Email, string Password, Guid WorkOrderId, Guid PartId)
{
    public const int InitialStock = 10;

    public static async Task<TechnicianScenario> CreateAsync(AdminApi admin)
    {
        ArgumentNullException.ThrowIfNull(admin);

        var email = $"tech-{Guid.NewGuid():N}@fixflow.test";
        var password = ApiEnvironment.GeneratePassword();
        var technicianId = await admin.CreateTechnicianAsync(email, password);
        var workOrderId = await admin.CreateAssignedWorkOrderAsync(technicianId);
        var partId = await admin.CreatePartAsync(InitialStock);
        return new TechnicianScenario(technicianId, email, password, workOrderId, partId);
    }
}
