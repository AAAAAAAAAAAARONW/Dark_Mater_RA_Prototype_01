using UnityEngine;

// Enum definition defining phases
public enum LayerTransitionPhase 
{
    Idle, // no transition
    Entering, // transition entering new layer
    Inside, // inside a layer
    Exiting // transition moving out layer
}

// Struct representing current layer state
public struct LayerStateSnapshot 
{
    // currentDefinition is the most important you can read this for doing stuff
    public readonly LayerDefinition currentDefinition; // ref to current profile, allows parameter read
    public readonly LayerDefinition previousDefinition; // ref to previous profile, possibly useful for transition
    public readonly string currentLayerId; // identity handle for active layer
    public readonly string previousLayerId; // identity handle for previous layer

    public readonly UniverseLayer currentUniverseLayer; // current layer using previous broad mode enum
    public readonly UniverseLayer previousUniverseLayer; // previous layer using previous broad mode enum

    public readonly LayerTransitionPhase phase; // stores enum of current transition phase

    public readonly float timeInPhase; // time in current phase
    public readonly float normalizedProgress01; // normalized progression tracker

    public LayerStateSnapshot( // constructor
        LayerDefinition currentDefinition,
        LayerDefinition previousDefinition,
        string currentLayerId,
        string previousLayerId,
        UniverseLayer currentUniverseLayer,
        UniverseLayer previousUniverseLayer,
        LayerTransitionPhase phase,
        float timeInPhase,
        float normalizedProgress01)
    {
        this.currentDefinition = currentDefinition;
        this.previousDefinition = previousDefinition;
        this.currentLayerId = currentLayerId;
        this.previousLayerId = previousLayerId;
        this.currentUniverseLayer = currentUniverseLayer;
        this.previousUniverseLayer = previousUniverseLayer;
        this.phase = phase;
        this.timeInPhase = timeInPhase;
        this.normalizedProgress01 = normalizedProgress01;
    }
}