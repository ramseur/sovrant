namespace Sovrant.Api.Routing;

/// <summary>Controls how the SmartRouter selects providers.</summary>
public enum RouterMode
{
    /// <summary>Routes to the optimal provider based on scoring.</summary>
    Smart,
    /// <summary>Always uses the first configured healthy provider.</summary>
    Fixed
}
