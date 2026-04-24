using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BaseLib.Hooks;

/// <summary>
///     Runtime context for resolving visual graft metrics on a creature health bar.
/// </summary>
public readonly record struct HealthBarVisualGraftContext(Creature Creature);

/// <summary>
///     Extra HP-length grafted onto the right end of the current HP fill for bar geometry and right-side forecasts.
/// </summary>
/// <param name="GraftHp">Additional HP units drawn past the current HP edge along the bar.</param>
/// <param name="GraftSelfModulate">Optional tint for the graft strip.</param>
/// <param name="GraftMaterial">Optional material for the graft strip.</param>
public readonly record struct HealthBarVisualGraftMetrics(
    int GraftHp,
    Color? GraftSelfModulate,
    Material? GraftMaterial)
{
    /// <summary>
    ///     Initializes metrics with default appearance.
    /// </summary>
    public HealthBarVisualGraftMetrics(int graftHp)
        : this(graftHp, null, null)
    {
    }
}

/// <summary>
///     Supplies visual graft metrics for a creature (temporary HP bar extension, etc.).
/// </summary>
public interface IHealthBarVisualGraftSource
{
    /// <summary>
    ///     Returns graft metrics for <paramref name="context" />.
    /// </summary>
    HealthBarVisualGraftMetrics GetHealthBarVisualGraft(HealthBarVisualGraftContext context);
}

/// <summary>
///     Aggregates graft metrics from creature powers, registered sources, and optional foreign providers.
/// </summary>
public static class HealthBarVisualGraftRegistry
{
    private static readonly Lock SyncRoot = new();
    private static readonly Dictionary<(string ModId, string SourceId), ProviderEntry> Providers = [];
    private static long _nextRegistrationOrder;

    /// <summary>
    ///     Registers or replaces a graft source implemented by <typeparamref name="TSource" />.
    /// </summary>
    public static void Register<TSource>(string modId, string? sourceId = null)
        where TSource : IHealthBarVisualGraftSource, new()
    {
        Register(modId, sourceId ?? typeof(TSource).FullName ?? typeof(TSource).Name, new TSource());
    }

    /// <summary>
    ///     Registers or replaces a graft source instance.
    /// </summary>
    public static void Register(string modId, string sourceId, IHealthBarVisualGraftSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(source);
        RegisterProvider(modId, sourceId, source, null);
    }

    /// <summary>
    ///     Registers a foreign provider returning a boxed metrics object (same field names as
    ///     <see cref="HealthBarVisualGraftMetrics" /> or that struct from another assembly).
    /// </summary>
    public static void RegisterForeign(string modId, string sourceId, Func<Creature, object> provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(provider);
        RegisterProvider(modId, sourceId, null, provider);
    }

    /// <summary>
    ///     Removes a previously registered typed or foreign provider.
    /// </summary>
    public static bool Unregister(string modId, string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        lock (SyncRoot)
        {
            return Providers.Remove((modId, sourceId));
        }
    }

    /// <summary>
    ///     Sums graft HP from powers, registered providers, and foreign delegates; first non-null appearance wins for
    ///     tint/material.
    /// </summary>
    public static HealthBarVisualGraftMetrics Aggregate(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        var sumHp = 0;
        Color? color = null;
        Material? material = null;
        var context = new HealthBarVisualGraftContext(creature);

        foreach (var source in creature.Powers.OfType<IHealthBarVisualGraftSource>())
        {
            try
            {
                var m = source.GetHealthBarVisualGraft(context);
                sumHp += Math.Max(0, m.GraftHp);
                color ??= m.GraftSelfModulate;
                material ??= m.GraftMaterial;
            }
            catch (Exception ex)
            {
                BaseLibMain.Logger.Warn(
                    $"[HealthBarGraft] Power '{source.GetType().FullName}' failed for '{creature}': {ex}");
            }
        }

        ProviderEntry[] snapshot;
        lock (SyncRoot)
        {
            snapshot = Providers.Values.OrderBy(e => e.RegistrationOrder).ToArray();
        }

        foreach (var entry in snapshot)
        {
            try
            {
                if (entry.Source != null)
                {
                    var m = entry.Source.GetHealthBarVisualGraft(context);
                    sumHp += Math.Max(0, m.GraftHp);
                    color ??= m.GraftSelfModulate;
                    material ??= m.GraftMaterial;
                    continue;
                }

                if (entry.ForeignProvider != null)
                {
                    var boxed = entry.ForeignProvider(creature);
                    if (TryReadForeignMetrics(boxed, out var fm))
                    {
                        sumHp += Math.Max(0, fm.GraftHp);
                        color ??= fm.GraftSelfModulate;
                        material ??= fm.GraftMaterial;
                    }
                }
            }
            catch (Exception ex)
            {
                BaseLibMain.Logger.Warn(
                    $"[HealthBarGraft] Source '{entry.SourceId}' from mod '{entry.ModId}' failed for '{creature}': {ex}");
            }
        }

        return new HealthBarVisualGraftMetrics(sumHp, color, material);
    }

    private static void RegisterProvider(
        string modId,
        string sourceId,
        IHealthBarVisualGraftSource? source,
        Func<Creature, object>? foreignProvider)
    {
        lock (SyncRoot)
        {
            var key = (modId, sourceId);
            var registrationOrder = Providers.TryGetValue(key, out var existing)
                ? existing.RegistrationOrder
                : _nextRegistrationOrder++;

            Providers[key] = new ProviderEntry(modId, sourceId, source, foreignProvider, registrationOrder);
        }
    }

    private static bool TryReadForeignMetrics(object? boxed, out HealthBarVisualGraftMetrics metrics)
    {
        metrics = default;
        if (boxed == null)
            return false;

        if (boxed is HealthBarVisualGraftMetrics direct)
        {
            metrics = direct;
            return true;
        }

        var t = boxed.GetType();
        var hp = ReadIntMember(boxed, t, "GraftHp");
        if (hp == null)
            return false;

        metrics = new HealthBarVisualGraftMetrics(
            hp.Value,
            ReadColorMember(boxed, t, "GraftSelfModulate"),
            ReadMaterialMember(boxed, t, "GraftMaterial"));
        return true;
    }

    private static int? ReadIntMember(object target, Type t, string name)
    {
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (p.Name != name || p.PropertyType != typeof(int))
                continue;
            return (int)p.GetValue(target)!;
        }

        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (f.Name != name || f.FieldType != typeof(int))
                continue;
            return (int)f.GetValue(target)!;
        }

        return null;
    }

    private static Color? ReadColorMember(object target, Type t, string name)
    {
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (p.Name != name)
                continue;
            if (p.PropertyType == typeof(Color?))
                return (Color?)p.GetValue(target);
            if (p.PropertyType == typeof(Color))
                return (Color)p.GetValue(target)!;
        }

        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (f.Name != name)
                continue;
            if (f.FieldType == typeof(Color?))
                return (Color?)f.GetValue(target);
            if (f.FieldType == typeof(Color))
                return (Color)f.GetValue(target)!;
        }

        return null;
    }

    private static Material? ReadMaterialMember(object target, Type t, string name)
    {
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (p.Name != name || !typeof(Material).IsAssignableFrom(p.PropertyType))
                continue;
            return (Material?)p.GetValue(target);
        }

        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (f.Name != name || !typeof(Material).IsAssignableFrom(f.FieldType))
                continue;
            return (Material?)f.GetValue(target);
        }

        return null;
    }

    private readonly record struct ProviderEntry(
        string ModId,
        string SourceId,
        IHealthBarVisualGraftSource? Source,
        Func<Creature, object>? ForeignProvider,
        long RegistrationOrder);
}
