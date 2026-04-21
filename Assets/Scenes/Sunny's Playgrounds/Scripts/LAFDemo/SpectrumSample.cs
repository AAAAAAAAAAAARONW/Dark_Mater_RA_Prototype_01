using System;

/// <summary>
/// struct that holds a single row of light spectrum information:
/// 1. index 2. wavelength 3. flux
/// </summary>
[Serializable]
public struct SpectrumSample
{
    public int Index;
    public float Wavelength;
    public float Flux;

    public SpectrumSample(int index, float wavelength, float flux)
    {
        Index = index;
        Wavelength = wavelength;
        Flux = flux;
    }
}