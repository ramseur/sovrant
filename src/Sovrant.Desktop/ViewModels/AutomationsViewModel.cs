using CommunityToolkit.Mvvm.ComponentModel;
using Sovrant.Agents.Swarm;

namespace Sovrant.Desktop.ViewModels;

public partial class AutomationsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _swarmEnabled;
    [ObservableProperty] private int _maxConcurrent;
    [ObservableProperty] private int _maxTokenBudget;
    [ObservableProperty] private int _maxRetries;
    [ObservableProperty] private bool _qualityGateEnabled;

    public AutomationsViewModel()
    {
        var config = SwarmConfigLoader.Load();
        SwarmEnabled = config.Enabled;
        MaxConcurrent = config.MaxConcurrent;
        MaxTokenBudget = config.MaxTokenBudget;
        MaxRetries = config.MaxRetries;
        QualityGateEnabled = config.QualityGateEnabled;
    }
}
