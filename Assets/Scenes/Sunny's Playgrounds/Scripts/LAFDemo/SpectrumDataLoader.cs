using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads the full master dataset from CSV. 
/// </summary>

public static class SpectrumDataLoader
{
    public static List<SpectrumSample> LoadFromText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("CSV is empty");
        }

        var lines = input.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
    
        var result = new List<SpectrumSample>(lines.Length - 1);

        for (int i = 1; i < lines.Length; i++) // starts from 1 because of csv header
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            
            var parts = line.Split(',');
            if (parts.Length < 3)
            {
                throw new InvalidDataException($"Line {i + 1}: expected 3 columns, got {parts.Length}.");
            }
           
            int index = int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            float wavelength = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            float flux = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
            
            result.Add(new SpectrumSample(index, wavelength, flux));
        }
        return result;
    }

    public static List<SpectrumSample> LoadFromTextAsset(TextAsset asset)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        return LoadFromText(asset.text);
    }
}