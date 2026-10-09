using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class ConflictIntegrationTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Should_Send_Nothing_When_All_Changes_Of_Reassigned_Work_Order_Are_Discarded()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        var otherTechnicianId = await admin.CreateTechnicianAsync($"tech-{Guid.NewGuid():N}@fixflow.test", ApiEnvironment.GeneratePassword());
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        var dispatcher = device.Get<OutboxDispatcher>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, CreateEntry(scenario.PartId, quantity: 1));
        await admin.ReassignAsync(scenario.WorkOrderId, otherTechnicianId);
        (await dispatcher.DispatchAsync(CancellationToken.None)).Rejected.ShouldBe(1);

        await device.Get<ConflictResolver>().DiscardWorkOrderChangesAsync(scenario.WorkOrderId);
        var summary = await dispatcher.DispatchAsync(CancellationToken.None);

        summary.ShouldBe(new DispatchSummary(Sent: 0, Rejected: 0, Remaining: 0, SessionRejected: false));
        (await device.Get<OutboxRepository>().GetConflictsAsync()).ShouldBeEmpty();
        (await admin.GetAsync($"api/v1/work-orders/{scenario.WorkOrderId}")).GetProperty("status").GetString().ShouldBe("Assigned");
        (await admin.GetAsync($"api/v1/parts/{scenario.PartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(TechnicianScenario.InitialStock);
    }

    [Fact]
    public async Task Should_Send_Entry_Once_When_Rejected_Entry_Is_Retried_After_Restock()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        var scarcePartId = await admin.CreatePartAsync(stockQuantity: 1);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        var dispatcher = device.Get<OutboxDispatcher>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        var entry = await writer.AddServiceEntryAsync(scenario.WorkOrderId, CreateEntry(scarcePartId, quantity: 2));
        await dispatcher.DispatchAsync(CancellationToken.None);
        (await device.Get<OutboxRepository>().GetConflictsAsync()).ShouldHaveSingleItem().ErrorCode.ShouldBe("Part.InsufficientStock");
        await admin.RestockAsync(scarcePartId, quantity: 5);

        await device.Get<ConflictResolver>().RetryAsync(entry.Id);
        var summary = await dispatcher.DispatchAsync(CancellationToken.None);

        summary.ShouldBe(new DispatchSummary(Sent: 1, Rejected: 0, Remaining: 0, SessionRejected: false) { ChangedPartStock = true });
        using var entries = await device.Get<IFixFlowApi>().ListServiceEntriesAsync(scenario.WorkOrderId, CancellationToken.None);
        entries.Content.ShouldNotBeNull().ShouldHaveSingleItem().Id.ShouldBe(OutboxPayload.ToServiceEntry(entry).Id);
        (await admin.GetAsync($"api/v1/parts/{scarcePartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(4);
    }

    private static PendingServiceEntry CreateEntry(Guid partId, int quantity)
    {
        var workStartedAt = DateTimeOffset.UtcNow;
        return new PendingServiceEntry(
            Guid.CreateVersion7(),
            "Wymiana uszczelki w teście konfliktu",
            false,
            workStartedAt,
            workStartedAt.AddMinutes(1),
            null,
            null,
            [new PendingServiceEntryPart(partId, "Filtr testowy", "FT", quantity, 12.34m)]);
    }
}
