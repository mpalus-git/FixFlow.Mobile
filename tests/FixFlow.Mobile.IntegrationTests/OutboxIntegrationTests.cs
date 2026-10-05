using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class OutboxIntegrationTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Should_Start_Add_Entry_With_Part_And_Complete_When_Queue_Is_Sent()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, CreateEntry(scenario.PartId, quantity: 2));
        await writer.CompleteWorkOrderAsync(scenario.WorkOrderId);

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.ShouldBe(new DispatchSummary(Sent: 3, Rejected: 0, Remaining: 0, SessionRejected: false));
        (await admin.GetAsync($"api/v1/work-orders/{scenario.WorkOrderId}")).GetProperty("status").GetString().ShouldBe("Completed");
        (await admin.GetAsync($"api/v1/parts/{scenario.PartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(TechnicianScenario.InitialStock - 2);
    }

    [Fact]
    public async Task Should_Not_Duplicate_Entry_When_Same_Entry_Is_Sent_Again_After_Lost_Response()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        var dispatcher = device.Get<OutboxDispatcher>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        var entry = CreateEntry(scenario.PartId, quantity: 3);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, entry);
        await dispatcher.DispatchAsync(CancellationToken.None);

        await writer.AddServiceEntryAsync(scenario.WorkOrderId, entry);
        var retry = await dispatcher.DispatchAsync(CancellationToken.None);

        retry.Sent.ShouldBe(1);
        using var entries = await device.Get<IFixFlowApi>().ListServiceEntriesAsync(scenario.WorkOrderId, CancellationToken.None);
        entries.Content.ShouldNotBeNull().ShouldHaveSingleItem().Id.ShouldBe(entry.Id);
        (await admin.GetAsync($"api/v1/parts/{scenario.PartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(TechnicianScenario.InitialStock - 3);
    }

    [Fact]
    public async Task Should_Move_Start_To_Conflicts_When_Dispatcher_Reassigned_Work_Order_Meanwhile()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        var otherTechnicianId = await admin.CreateTechnicianAsync($"tech-{Guid.NewGuid():N}@fixflow.test", ApiEnvironment.GeneratePassword());
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        await device.Get<OutboxWriter>().StartWorkAsync(scenario.WorkOrderId);
        await admin.ReassignAsync(scenario.WorkOrderId, otherTechnicianId);

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.Rejected.ShouldBe(1);
        var conflict = (await device.Get<OutboxRepository>().GetConflictsAsync()).ShouldHaveSingleItem();
        conflict.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        conflict.ErrorCode.ShouldBe("WorkOrder.NotFound");
        conflict.Kind.ShouldBe(OutboxOperationKind.StartWork);
    }

    private static PendingServiceEntry CreateEntry(Guid partId, int quantity)
    {
        var workStartedAt = DateTimeOffset.UtcNow;
        return new PendingServiceEntry(
            Guid.CreateVersion7(),
            "Wymiana filtra w teście integracyjnym",
            false,
            workStartedAt,
            workStartedAt.AddMinutes(1),
            50.0614,
            19.9366,
            [new PendingServiceEntryPart(partId, "Filtr testowy", "FT", quantity, 12.34m)]);
    }
}
