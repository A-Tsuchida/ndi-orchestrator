namespace ndiWrapper;

/// <summary>
/// Represents an NDI source discovered on the network.
/// </summary>
public sealed class NdiSource
{
    /// <summary>Human-readable source name in the form "MACHINE_NAME (SOURCE_NAME)".</summary>
    public string Name { get; }

    /// <summary>Network URL/address of the source. May be null if not yet resolved.</summary>
    public string? UrlAddress { get; }

    internal NdiSource(string name, string? urlAddress)
    {
        Name = name;
        UrlAddress = urlAddress;
    }

    public override string ToString() => Name;
}
