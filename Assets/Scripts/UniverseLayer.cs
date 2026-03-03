/// <summary>
/// Universe scale layer. Same player speed in all layers; distance scale (ly per game minute) differs.
/// Extensible: add new enum values and corresponding scale entries in UniverseTravelTracker.
/// </summary>
public enum UniverseLayer
{
    Macro = 0,
    Micro = 1
}
