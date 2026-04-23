using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BaseLib.Hooks;

/// <summary>
///     Runtime context passed to health bar forecast sources.
/// </summary>
/// <param name="Creature">Creature whose health bar is being rendered.</param>
public readonly record struct HealthBarForecastContext(Creature Creature)
{
    /// <summary>
    ///     Current combat state, when the creature is in combat.
    /// </summary>
    public CombatStateWrapper? CombatState => BetaMainCompatibility.Creature_.WrappedCombatState(Creature);

    /// <summary>
    ///     Side whose turn is currently active, when available.
    /// </summary>
    public CombatSide? CurrentSide => CombatState?.CurrentSide;
}

///     Produces one or more health bar forecast segments for a creature.
/// </summary>
/// <remarks>
///     Power models can implement this directly and are discovered from <see cref="Creature.Powers" /> without extra
///     registration.
///     Non-power sources can be registered with
///     <see cref="HealthBarForecastRegistry.Register(string, string, IHealthBarForecastSource)" />
///     or <see cref="HealthBarForecastRegistry.RegisterForeign" /> for cross-assembly duck-typed segments.
/// </remarks>
public interface IHealthBarForecastSource
{
    /// <summary>
    ///     Returns forecast segments to render for <paramref name="context" />.
    /// </summary>
    /// <param name="context">Creature and combat context for the bar being drawn.</param>
    IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context);
}
