using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Prometheus;

namespace Services;

public class MetricsService : BackgroundService
{
    private readonly string[] metricLabels = new[] { "name", "level" };
    private readonly Gauge gauge = null!;
    private readonly MetricServer metricServer = null!;

    private readonly IOptions<Options.Metrics> options = null!;
    private readonly CheckupdatesService checkupdatesService = null!;
    private readonly ILogger<MetricsService> logger = null!;

    public MetricsService(IOptions<Options.Metrics> options, CheckupdatesService checkupdatesService, ILogger<MetricsService> logger)
    {
        this.options = options;
        this.checkupdatesService = checkupdatesService;
        this.logger = logger;

        gauge = Metrics.CreateGauge(
            "checkupdates_available",
            "Available updates to applied",
            labelNames: metricLabels
        );

        checkupdatesService.UpdatesChanged += OnUpdatesChanged;

        var port = options.Value.Port!.Value;
        metricServer = new MetricServer(port: port);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Setting gauges for initial state...");
        SetGauges(checkupdatesService.GetCurrentUpdates());

        logger.LogDebug("Starting metrics server...");
        metricServer.Start();
        logger.LogInformation("Started metrics server");

        return Task.CompletedTask;
    }

    private void OnUpdatesChanged(object? sender, CheckupdatesService.UpdatesChangedEventArgs e)
    {
        SetGauges(e.Updates);
    }

    private void SetGauges(HashSet<Update> updates)
    {
        IEnumerable<string[]> obsoleteGauges = gauge.GetAllLabelValues().Where(g => !updates.Contains(Update.FromArray(g)));
        foreach (var g in obsoleteGauges)
            this.gauge.WithLabels(g).Remove();
        logger.LogInformation("Removed {ObsoleteCount} obsolete gauge values", obsoleteGauges.Count());

        foreach (var u in updates)
            this.gauge.WithLabels(u.ToArray()).Set(1);
        logger.LogInformation("Set {UpdateCount} gauges", updates.Count);
    }
}
