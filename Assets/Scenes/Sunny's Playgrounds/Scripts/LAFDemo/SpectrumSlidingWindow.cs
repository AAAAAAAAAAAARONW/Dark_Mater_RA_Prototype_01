using System;
using System.Collections.Generic;
/// <summary>
/// Sliding window over a persistent master spectrum data (the CSV) that acts as window/current data buffer
/// Scrolls one step in csv source space toward lower indices:
/// - Remove the rightmost sample
/// - Prepend the next earlier master sample on the left
/// Screen X positions are fixed 0..N-1, so curve appears to move right on HUD, imitating red-shift
/// </summary>
public sealed class SpectrumSlidingWindow
{
    private readonly IReadOnlyList<SpectrumSample> _master;
    private readonly List<SpectrumSample> _visible;
    private readonly int _windowSize;
    public int WindowSize => _windowSize;
    public int WindowStartIndex { get; private set; }
    /// <summary>Indexed access to the visible deque/buffer for future stamping or edits.</summary>
    public IReadOnlyList<SpectrumSample> Visible => _visible;
    public SpectrumSlidingWindow(IReadOnlyList<SpectrumSample> master, int windowSize, bool startAtEnd = true)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _windowSize = windowSize;
        _visible = new List<SpectrumSample>(windowSize);
        if (_master.Count < _windowSize)
            throw new ArgumentException($"Master has {_master.Count} samples; window size {_windowSize} is too large.");
        if (startAtEnd)
            WindowStartIndex = _master.Count - _windowSize; // e.g. 1000 - 100 = 900
        else
            WindowStartIndex = 0;
        RebuildFromMaster();
    }
    private void RebuildFromMaster()
    {
        _visible.Clear();
        for (int i = 0; i < _windowSize; i++)
            _visible.Add(_master[WindowStartIndex + i]);
    }
    /// <summary>Try to step the window one sample left in the master array (toward index 0).</summary>
    /// <returns>True if a step was applied; false if blocked at the beginning (unless loop).</returns>
    public bool TryStepLeft(bool loop)
    {
        if (WindowStartIndex <= 0)
        {
            if (!loop) return false;
            WindowStartIndex = _master.Count - _windowSize;
            RebuildFromMaster();
            return true;
        }
        WindowStartIndex--;
        // Deque update: new sample enters on the LEFT, oldest (rightmost) leaves.
        _visible.RemoveAt(_visible.Count - 1);
        _visible.Insert(0, _master[WindowStartIndex]);
        return true;
    }
    /// <summary>Optional: overwrite a visible slot after load (e.g. temporary dip). Index 0 = left of graph.</summary>
    public void StampVisible(int visibleIndex, SpectrumSample sample)
    {
        if ((uint)visibleIndex >= (uint)_visible.Count)
            throw new ArgumentOutOfRangeException(nameof(visibleIndex));
        _visible[visibleIndex] = sample;
    }
}