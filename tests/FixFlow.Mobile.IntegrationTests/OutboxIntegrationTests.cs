using System.Net;
using FixFlow.Mobile.Core.Api;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;
using FixFlow.Mobile.Core.Features.WorkOrders;

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
        var start = await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, CreateEntry(scenario.PartId, quantity: 2, start));
        await writer.CompleteWorkOrderAsync(scenario.WorkOrderId);

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        summary.ShouldBe(new DispatchSummary(Sent: 3, Rejected: 0, Remaining: 0, SessionRejected: false) { ChangedPartStock = true });
        (await admin.GetAsync($"api/v1/work-orders/{scenario.WorkOrderId}")).GetProperty("status").GetString().ShouldBe("Completed");
        (await admin.GetAsync($"api/v1/parts/{scenario.PartId}")).GetProperty("stockQuantity").GetInt32().ShouldBe(TechnicianScenario.InitialStock - 2);
        await device.Get<WorkOrderDetailsSyncService>().PullAsync(scenario.WorkOrderId, CancellationToken.None);
        var events = await device.Get<WorkOrderEventRepository>().GetForWorkOrderAsync(scenario.WorkOrderId);
        events.Select(item => item.Type).ShouldBe([WorkOrderEventType.Created, WorkOrderEventType.Assigned, WorkOrderEventType.Started, WorkOrderEventType.Completed]);
        events[2].OccurredAt.ShouldBe(OutboxPayload.StartedAt(start), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Should_Treat_Start_And_Completion_As_Sent_When_They_Are_Sent_Again_After_Lost_Response()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        var dispatcher = device.Get<OutboxDispatcher>();
        var start = await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, CreateEntry(scenario.PartId, quantity: 1, start));
        await writer.CompleteWorkOrderAsync(scenario.WorkOrderId);
        await dispatcher.DispatchAsync(CancellationToken.None);

        await writer.StartWorkAsync(scenario.WorkOrderId);
        await writer.CompleteWorkOrderAsync(scenario.WorkOrderId);
        var retry = await dispatcher.DispatchAsync(CancellationToken.None);

        retry.ShouldBe(new DispatchSummary(Sent: 2, Rejected: 0, Remaining: 0, SessionRejected: false));
        (await admin.GetAsync($"api/v1/work-orders/{scenario.WorkOrderId}")).GetProperty("status").GetString().ShouldBe("Completed");
    }

    [Fact]
    public async Task Should_Not_Duplicate_Entry_When_Same_Entry_Is_Sent_Again_After_Lost_Response()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var writer = device.Get<OutboxWriter>();
        var dispatcher = device.Get<OutboxDispatcher>();
        var start = await writer.StartWorkAsync(scenario.WorkOrderId);
        var entry = CreateEntry(scenario.PartId, quantity: 3, start);
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

    [Fact]
    public async Task Should_Keep_Operation_In_Queue_When_Api_Container_Is_Stopped()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var api = environment.CreateApiContainer();
        await api.StartAsync(TestContext.Current.CancellationToken);
        await using var device = await TechnicianDevice.LogInAsync(ApiEnvironment.AddressOf(api), scenario.Email, scenario.Password);
        await device.Get<OutboxWriter>().StartWorkAsync(scenario.WorkOrderId);
        await api.StopAsync(TestContext.Current.CancellationToken);

        var summary = await device.Get<OutboxDispatcher>().DispatchAsync(CancellationToken.None);

        var waiting = (await device.Get<OutboxRepository>().GetAllAsync()).ShouldHaveSingleItem();
        waiting.AttemptCount.ShouldBe(1);
        waiting.NextAttemptAt.ShouldNotBeNull();
        summary.ShouldBe(new DispatchSummary(Sent: 0, Rejected: 0, Remaining: 1, SessionRejected: false) { NextAttemptAt = waiting.NextAttemptAt });
        (await device.Get<OutboxRepository>().GetConflictsAsync()).ShouldBeEmpty();
        (await admin.GetAsync($"api/v1/work-orders/{scenario.WorkOrderId}")).GetProperty("status").GetString().ShouldBe("Assigned");
    }

    private static PendingServiceEntry CreateEntry(Guid partId, int quantity, OutboxOperation start) =>
        new(
            Guid.CreateVersion7(),
            "Wymiana filtra w teście integracyjnym",
            false,
            OutboxPayload.StartedAt(start),
            DateTimeOffset.UtcNow,
            50.0614,
            19.9366,
            [new PendingServiceEntryPart(partId, "Filtr testowy", "FT", quantity, 12.34m)]);
}
