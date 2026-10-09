using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class WorkOrderSyncIntegrationTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Should_Log_In_And_Pull_Work_Order_When_Dispatcher_Assigned_It()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var email = $"tech-{Guid.NewGuid():N}@fixflow.test";
        var password = ApiEnvironment.GeneratePassword();
        var technicianId = await admin.CreateTechnicianAsync(email, password);
        var workOrderId = await admin.CreateAssignedWorkOrderAsync(technicianId);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, email, password);

        var result = await device.Get<WorkOrderSyncService>().PullAsync(CancellationToken.None);

        result.ShouldBe(PullResult.Succeeded);
        (await device.Get<WorkOrderRepository>().GetAllAsync()).ShouldHaveSingleItem().Id.ShouldBe(workOrderId);
    }
}
