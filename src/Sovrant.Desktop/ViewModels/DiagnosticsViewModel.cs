using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Api.Routing;

namespace Sovrant.Desktop.ViewModels;

public partial class DiagnosticsViewModel : ViewModelBase
{
    private readonly ISmartRouter _router;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<ProviderHealthItem> Providers { get; } = [];

    public DiagnosticsViewModel(ISmartRouter router)
    {
        _router = router;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Providers.Clear();
        var statuses = _router.GetStatus();
        foreach (var s in statuses)
        {
            Providers.Add(new ProviderHealthItem
            {
                Name = s.Name,
                IsHealthy = s.Healthy,
                Status = s.Healthy ? "PASS" : "FAIL",
                LatencyMs = s.RequestCount > 0 ? $"{s.LatencyMs:F0}ms" : "—",
                RequestCount = s.RequestCount,
                ErrorCount = s.ErrorCount,
                ErrorRate = s.ErrorRate,
                Score = s.Score,
            });
        }
    }
}

public partial class ProviderHealthItem : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isHealthy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _latencyMs = "—";

    [ObservableProperty]
    private int _requestCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private string _errorRate = "0%";

    [ObservableProperty]
    private string _score = "N/A";
}
