using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace BaseLib.Hooks;

/// <summary>
///     Which edge of the health bar a forecast segment grows from.
/// </summary>
public enum HealthBarForecastDirection
{
    /// <summary>
    ///     Grows inward from the current HP edge (e.g. poison-style).
    /// </summary>
    FromRight = 0,

    /// <summary>
    ///     Grows outward from the empty side (e.g. doom-style).
    /// </summary>
    FromLeft = 1,
}

/// <summary>
///     How <see cref="HealthBarForecastDirection.FromLeft" /> segments share the empty-edge origin.
/// </summary>
public enum HealthBarForecastLeftOriginLayout
{
    /// <summary>
    ///     Segments connect end-to-end from the empty edge (legacy layout).
    /// </summary>
    Chained = 0,

    /// <summary>
    ///     Each segment spans from the empty edge by its own <c>Amount</c> (capped). Larger <c>Amount</c> is drawn behind;
    ///     smaller in front. Equal widths rotate front/back on a timer.
    /// </summary>
    OverlapFromOrigin = 1,
}

/// <summary>
///     One forecast overlay segment for a creature health bar.
/// </summary>
/// <param name="Amount">HP amount represented by this segment.</param>
/// <param name="Color">
///     Lethal HP label theming; also used as the forecast nine-patch <see cref="CanvasItem.SelfModulate" /> when
///     <see cref="OverlaySelfModulate" /> is null.
/// </param>
/// <param name="Direction">Which edge the segment grows from.</param>
/// <param name="Order">
///     Lower values are rendered earlier in the chain.
///     For <see cref="HealthBarForecastDirection.FromRight" />, earlier segments stay closer to the current HP edge;
///     for <see cref="HealthBarForecastDirection.FromLeft" />, earlier segments stay closer to the empty edge.
/// </param>
/// <param name="OverlayMaterial">
///     Optional Godot material (e.g. shader like vanilla doom). When null, only <see cref="Color" /> tint applies.
/// </param>
/// <param name="OverlaySelfModulate">
///     Optional <see cref="CanvasItem.SelfModulate" /> for the forecast nine-patch. When null, <see cref="Color" /> is
///     used
///     for both overlay tint and lethal HP label; when set, <see cref="Color" /> is still used for lethal label theming.
/// </param>
/// <param name="LeftOriginLayout">
///     For <see cref="HealthBarForecastDirection.FromLeft" /> only: chained vs overlap-from-origin stacking.
/// </param>
/// <param name="LeftExclusiveZGroup">
///     For <see cref="HealthBarForecastLeftOriginLayout.OverlapFromOrigin" />: larger draws above smaller. Within a
///     group, longer strips sit behind shorter strips; equal widths rotate.
/// </param>
public readonly record struct HealthBarForecastSegment(
    int Amount,
    Color Color,
    HealthBarForecastDirection Direction,
    int Order,
    Material? OverlayMaterial,
    Color? OverlaySelfModulate = null,
    HealthBarForecastLeftOriginLayout LeftOriginLayout = HealthBarForecastLeftOriginLayout.Chained,
    int LeftExclusiveZGroup = 0)
{
    /// <summary>
    ///     Initializes a segment without overlay material or separate overlay modulate.
    /// </summary>
    public HealthBarForecastSegment(int amount, Color color, HealthBarForecastDirection direction, int order = 0)
        : this(amount, color, direction, order, null, null, HealthBarForecastLeftOriginLayout.Chained, 0)
    {
    }

    /// <summary>
    ///     Initializes a segment with an optional <see cref="OverlayMaterial" /> and default overlay modulate.
    /// </summary>
    // ReSharper disable once RedundantOverload.Global
    public HealthBarForecastSegment(
        int amount,
        Color color,
        HealthBarForecastDirection direction,
        int order,
        Material? overlayMaterial)
        : this(amount, color, direction, order, overlayMaterial, null, HealthBarForecastLeftOriginLayout.Chained, 0)
    {
    }
}

/// <summary>
///     Helpers for common turn-relative ordering of forecast segments.
/// </summary>
public static class HealthBarForecastOrder
{
    /// <summary>
    ///     Returns an order key for effects that trigger at the start of <paramref name="triggerSide" />'s turn.
    /// </summary>
    /// <param name="creature">Creature used to read the active combat side.</param>
    /// <param name="triggerSide">Side whose turn start is being modeled.</param>
    /// <returns>Higher value when it is currently that side's turn (segments stack after others).</returns>
    public static int ForSideTurnStart(Creature creature, CombatSide triggerSide)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.CombatState?.CurrentSide == triggerSide ? 1 : 0;
    }

    /// <summary>
    ///     Returns an order key for effects that trigger at the end of <paramref name="triggerSide" />'s turn.
    /// </summary>
    /// <param name="creature">Creature used to read the active combat side.</param>
    /// <param name="triggerSide">Side whose turn end is being modeled.</param>
    /// <returns>Higher value when it is not currently that side's turn.</returns>
    public static int ForSideTurnEnd(Creature creature, CombatSide triggerSide)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.CombatState?.CurrentSide == triggerSide ? 0 : 1;
    }
}
