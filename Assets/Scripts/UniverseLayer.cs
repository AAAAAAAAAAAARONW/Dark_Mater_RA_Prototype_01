/// <summary>
/// Universe scale layer. Same player speed in all layers; distance scale (ly per game minute) differs.
/// Extensible: add new enum values and corresponding scale entries in UniverseTravelTracker.
/// </summary>
public enum UniverseLayer
{
    Quasar = 0,
    Macro = 1,
    Micro = 2,
    MilkyWay = 3,
    SolarSystem = 4,
    Earth = 5,
    CosmicWeb = 6
}