using System;

/// <summary>
/// Marks a MonoBehaviour so the Hierarchy shows a tag for it.
///
/// Put it on your own scripts and the label travels with the class — nothing central to
/// keep in sync, and a renamed or deleted script takes its tag with it. For scripts you
/// do not own (package code, the legacy stack, anything you would rather not edit) use
/// BadgeRegistry_NEW instead.
///
///     [HierarchyBadge_NEW("SPAWN", "#4A94F2")]
///     public class EnemySpawner : MonoBehaviour { }
///
/// The colour is optional. Leave it out and one is derived from the class name, so every
/// type gets a stable, distinct colour without anyone choosing it:
///
///     [HierarchyBadge_NEW("SPAWN")]
///
/// This lives in the runtime assembly because attributes must be visible to the classes
/// that carry them. It holds two strings and no logic, so it costs nothing in a build.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HierarchyBadge_NEW : Attribute
{
    /// <summary>Short text shown in the tag. Keep it under about ten characters.</summary>
    public string Label { get; }

    /// <summary>Optional "#RRGGBB". Null or empty means derive one from the class name.</summary>
    public string ColorHex { get; }

    public HierarchyBadge_NEW(string label, string colorHex = null)
    {
        Label = label;
        ColorHex = colorHex;
    }
}
