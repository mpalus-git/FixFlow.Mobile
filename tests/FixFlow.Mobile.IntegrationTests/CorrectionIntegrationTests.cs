using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class CorrectionIntegrationTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Should_Return_Parts_To_Stock_When_Correction_Is_Sent()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, WorkEntry(scenario.PartId, quantity: 4));
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, Correction(scenario.PartId, quantity: 3));

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.Sent.ShouldBe(3);
        (await admin.GetAsync($"api/v1/parts/{scenario.PartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(TechnicianScenario.InitialStock - 1);
        var stored = await device.Get<ServiceEntryRepository>().GetForWorkOrderAsync(scenario.WorkOrderId);
        NetPartUsage.Calculate(stored, []).ShouldHaveSingleItem().NetQuantity.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Move_Correction_To_Conflicts_When_It_Returns_More_Than_Was_Used()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, WorkEntry(scenario.PartId, quantity: 1));
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, Correction(scenario.PartId, quantity: 2));

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.Rejected.ShouldBe(1);
        var conflict = (await device.Get<OutboxRepository>().GetConflictsAsync()).ShouldHaveSingleItem();
        conflict.ErrorCode.ShouldBe("ServiceEntry.ReturnExceedsConsumption");
        (await admin.GetAsync($"api/v1/parts/{scenario.PartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(TechnicianScenario.InitialStock - 1);
    }

    private static PendingServiceEntry WorkEntry(Guid partId, int quantity)
    {
        var startedAt = DateTimeOffset.UtcNow;
        return new PendingServiceEntry(
            Guid.CreateVersion7(), "Wymiana filtra", false, startedAt, startedAt.AddMinutes(1), null, null,
            [new PendingServiceEntryPart(partId, "Filtr testowy", "FT", quantity, 12.34m)]);
    }

    private static PendingServiceEntry Correction(Guid partId, int quantity) =>
        new(Guid.CreateVersion7(), "Pomyłka w ilości", true, null, null, null, null, [new PendingServiceEntryPart(partId, "Filtr testowy", "FT", quantity, 12.34m)]);
}
