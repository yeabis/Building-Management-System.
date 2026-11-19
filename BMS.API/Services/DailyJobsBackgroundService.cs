using BMS.Application.Interfaces;

namespace BMS.API.Services;

public class DailyJobsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<DailyJobsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("DailyJobs:Enabled", true))
        {
            logger.LogInformation("Daily jobs are disabled (DailyJobs:Enabled = false).");
            return;
        }

        try
        {

            await Task.Delay(TimeSpan.FromSeconds(config.GetValue("DailyJobs:StartupDelaySeconds", 20)), stoppingToken);

            await RunOnceAsync(stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {

        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        logger.LogInformation("Daily jobs started.");
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        await Step("generate invoices", async () => (await sp.GetRequiredService<IInvoiceService>().GenerateInvoicesAsync(null, ct)).Generated);
        await Step("mark overdue invoices", () => sp.GetRequiredService<IInvoiceService>().MarkOverdueAsync(ct));
        await Step("expire ended leases", () => sp.GetRequiredService<ILeaseService>().ExpireEndedLeasesAsync(ct));

        var notifications = sp.GetRequiredService<INotificationService>();
        await Step("payment-due alerts", () => notifications.SendPaymentDueAlertsAsync(ct));
        await Step("overdue alerts", () => notifications.SendOverdueAlertsAsync(ct));
        await Step("lease-expiry alerts", () => notifications.SendLeaseExpiryAlertsAsync(ct));
        await Step("document-expiry alerts", () => notifications.SendDocumentExpiryAlertsAsync(ct));
        logger.LogInformation("Daily jobs finished.");
    }

    private async Task Step(string name, Func<Task<int>> action)
    {
        try
        {
            var count = await action();
            logger.LogInformation("Daily job '{Step}': {Count} item(s) processed.", name, count);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Daily job '{Step}' failed.", name);
        }
    }
}

