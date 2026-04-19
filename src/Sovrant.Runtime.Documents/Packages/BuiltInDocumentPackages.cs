namespace Sovrant.Runtime.Documents.Packages;

/// <summary>
/// The set of <see cref="DocumentPackage"/>s shipped with Sovrant. Each
/// package's items are rendered against the SAME data object — picked
/// so the templates have largely overlapping field requirements (the
/// expectation is that callers supply the union of required fields).
/// </summary>
public static class BuiltInDocumentPackages
{
    public static IReadOnlyList<DocumentPackage> All { get; } = new List<DocumentPackage>
    {
        new(
            Id: "real-estate/closing-package",
            Name: "Real-estate closing package",
            Description: "Purchase agreement + closing disclosure + property inspection report for a single transaction.",
            Items: new List<DocumentPackageItem>
            {
                new("real-estate/purchase-agreement"),
                new("real-estate/closing-disclosure"),
                new("real-estate/property-inspection"),
            }),
        new(
            Id: "business/onboarding-package",
            Name: "New-hire onboarding package",
            Description: "Offer letter + statement of work + first-week meeting notes for a new hire.",
            Items: new List<DocumentPackageItem>
            {
                new("business/employee-offer"),
                new("business/sow"),
                new("business/meeting-notes"),
            }),
        new(
            Id: "healthcare/intake-package",
            Name: "Patient intake package",
            Description: "Patient intake form + HIPAA authorization for new patient onboarding.",
            Items: new List<DocumentPackageItem>
            {
                new("healthcare/patient-intake"),
                new("healthcare/hipaa-authorization"),
            }),
    };
}

public sealed class DocumentPackageRegistry : IDocumentPackageRegistry
{
    private readonly Dictionary<string, DocumentPackage> _byId;

    public DocumentPackageRegistry(IEnumerable<DocumentPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);
        _byId = packages.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryResolve(string id, out DocumentPackage? package)
    {
        if (!string.IsNullOrWhiteSpace(id) && _byId.TryGetValue(id, out var p))
        {
            package = p;
            return true;
        }
        package = null;
        return false;
    }

    public IReadOnlyCollection<DocumentPackage> All => _byId.Values;
}
