using FixFlow.Mobile.Core.Features.Parts;
using FixFlow.Mobile.Core.Features.ServiceEntries;
using FixFlow.Mobile.Core.Features.Sync;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class ServiceEntryIntegrationTests(ApiEnvironment environment)
{
    [Fact]
    public async Task Should_Send_Entry_With_Part_From_Local_Catalog_And_Refresh_Stock_When_Synchronized()
    {
        using var admin = await AdminApi.LogInAsync(environment);
        var scenario = await TechnicianScenario.CreateAsync(admin);
        await using var device = await TechnicianDevice.LogInAsync(environment.BaseAddress, scenario.Email, scenario.Password);
        var coordinator = device.Get<SyncCoordinator>();
        var parts = device.Get<PartRepository>();
        await coordinator.SynchronizeAsync();
        var part = (await parts.GetAsync(scenario.PartId)).ShouldNotBeNull();
        var writer = device.Get<OutboxWriter>();
        await writer.StartWorkAsync(scenario.WorkOrderId);
        var workStartedAt = DateTimeOffset.UtcNow;
        var entry = new PendingServiceEntry(
            Guid.CreateVersion7(),
            "Wymiana filtra z katalogu offline",
            false,
            workStartedAt,
            workStartedAt.AddMinutes(1),
            null,
            null,
            [new PendingServiceEntryPart(part.Id, part.Name, part.CatalogNumber, 4, part.UnitPrice)]);
        await writer.AddServiceEntryAsync(scenario.WorkOrderId, entry);
        PartAvailability.Available(part, [entry]).ShouldBe(TechnicianScenario.InitialStock - 4);

        var result = await coordinator.SynchronizeAsync();

        result.Dispatch.ShouldNotBeNull().Sent.ShouldBe(2);
        (await parts.GetAsync(scenario.PartId)).ShouldNotBeNull().StockQuantity.ShouldBe(TechnicianScenario.InitialStock - 4);
        var stored = (await device.Get<ServiceEntryRepository>().GetForWorkOrderAsync(scenario.WorkOrderId)).ShouldHaveSingleItem();
        stored.Id.ShouldBe(entry.Id);
        stored.Parts.ShouldHaveSingleItem().UnitPrice.ShouldBe(part.UnitPrice);
    }
}
