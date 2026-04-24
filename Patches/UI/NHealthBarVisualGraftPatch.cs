using BaseLib.Hooks;
using BaseLib.Utils;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace BaseLib.Patches.UI;

/// <summary>
///     Widens the HP bar when <see cref="HealthBarVisualGraftRegistry" /> reports positive graft HP, draws a graft strip,
///     and restores vanilla width when graft returns to zero.
/// </summary>
[HarmonyPatch]
public static class NHealthBarVisualGraftPatch
{
    private static readonly Color DefaultGraftColor = new("5C8F6E");

    private static readonly SpireField<NHealthBar, GraftUiState?> UiStates = new(() => null);

    [HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshForeground))]
    [HarmonyPostfix]
    [HarmonyPriority(10)]
    private static void RefreshForegroundGraftPostfix(NHealthBar __instance)
    {
        RefreshGraftOverlay(__instance);
    }

    private static void RefreshGraftOverlay(NHealthBar healthBar)
    {
        var creature = healthBar._creature;
        if (creature.CurrentHp <= 0 || creature.ShowsInfiniteHp)
        {
            ResetGraft(healthBar);
            return;
        }

        var metrics = HealthBarVisualGraftRegistry.Aggregate(creature);
        var graftHp = Math.Max(0, metrics.GraftHp);
        if (creature.MaxHp <= 0)
            return;

        if (graftHp <= 0)
        {
            ResetGraft(healthBar);
            return;
        }

        var d = Math.Max(creature.MaxHp, creature.CurrentHp + graftHp);
        var scale = d / (float)creature.MaxHp;
        if (scale <= 1.0001f)
        {
            ResetGraft(healthBar);
            return;
        }

        if (!EnsureGraftStrip(healthBar, out var state))
            return;

        var w0 = healthBar.HpBarContainer.Size.X / state.LastAppliedScale;
        state.LastAppliedScale = scale;
        var target = new Vector2(w0 * scale, healthBar.HpBarContainer.Size.Y);
        healthBar.SetHpBarContainerSizeWithOffsetsImmediately(target);

        ApplyMainForegroundDenom(healthBar, creature, d);
        PositionGraftStrip(healthBar, creature, graftHp, d, metrics, state);
    }

    private static void ResetGraft(NHealthBar healthBar)
    {
        if (UiStates[healthBar] is not { } state)
            return;

        if (state.Strip != null)
        {
            state.Strip.Visible = false;
            state.Strip.Material = null;
            state.Strip.SelfModulate = Colors.White;
        }

        if (state.LastAppliedScale > 1.0001f)
        {
            var w = healthBar.HpBarContainer.Size.X / state.LastAppliedScale;
            healthBar.SetHpBarContainerSizeWithOffsetsImmediately(new Vector2(w, healthBar.HpBarContainer.Size.Y));
        }

        state.LastAppliedScale = 1f;
    }

    private static void ApplyMainForegroundDenom(NHealthBar healthBar, Creature creature, int visualDenom)
    {
        if (!healthBar._hpForeground.Visible)
            return;

        var e = GetMaxFgWidth(healthBar);
        if (e <= 0f || visualDenom <= 0)
            return;

        var val = (float)creature.CurrentHp / visualDenom * e;
        var wFill = Math.Max(val, creature.CurrentHp > 0 ? 12f : 0f);
        healthBar._hpForeground.OffsetRight = wFill - e;
    }

    private static void PositionGraftStrip(
        NHealthBar healthBar,
        Creature creature,
        int graftHp,
        int visualDenom,
        HealthBarVisualGraftMetrics metrics,
        GraftUiState state)
    {
        var strip = state.Strip;
        if (strip == null)
            return;

        var e = GetMaxFgWidth(healthBar);
        if (e <= 0f || visualDenom <= 0)
        {
            strip.Visible = false;
            return;
        }

        var wMain = Math.Max(
            (float)creature.CurrentHp / visualDenom * e,
            creature.CurrentHp > 0 ? 12f : 0f);
        var wGraft = (float)graftHp / visualDenom * e;
        if (wGraft < 0.5f)
        {
            strip.Visible = false;
            return;
        }

        strip.Visible = true;
        strip.Material = metrics.GraftMaterial;
        strip.SelfModulate = metrics.GraftSelfModulate ?? DefaultGraftColor;
        strip.OffsetLeft = wMain > 0f ? Math.Max(0f, wMain - strip.PatchMarginLeft) : 0f;
        strip.OffsetRight = wMain + wGraft - e;
    }

    private static float GetMaxFgWidth(NHealthBar healthBar)
    {
        var expectedMaxFgWidth = healthBar._expectedMaxFgWidth;
        return expectedMaxFgWidth > 0f
            ? expectedMaxFgWidth
            : healthBar._hpForegroundContainer.Size.X;
    }

    private static bool EnsureGraftStrip(NHealthBar healthBar, out GraftUiState state)
    {
        var existing = UiStates[healthBar];
        if (existing?.Strip != null)
        {
            state = existing;
            return true;
        }

        if (healthBar._poisonForeground is not NinePatchRect poisonTemplate ||
            poisonTemplate.GetParent() is not Control mask ||
            healthBar._hpForeground is not Control hpForeground)
        {
            state = null!;
            return false;
        }

        var strip = (NinePatchRect)poisonTemplate.Duplicate();
        strip.Name = "BaseLibVisualGraftStrip";
        strip.Visible = false;
        strip.MouseFilter = Control.MouseFilterEnum.Ignore;
        mask.AddChild(strip);
        var insertAt = Math.Clamp(hpForeground.GetIndex() + 1, 0, mask.GetChildCount() - 1);
        mask.MoveChild(strip, insertAt);

        state = existing ?? new GraftUiState();
        state.Strip = strip;
        if (state.LastAppliedScale <= 0f)
            state.LastAppliedScale = 1f;
        UiStates[healthBar] = state;
        return true;
    }

    private sealed class GraftUiState
    {
        public float LastAppliedScale { get; set; } = 1f;
        public NinePatchRect? Strip { get; set; }
    }
}
