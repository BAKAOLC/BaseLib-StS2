using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace BaseLib.Hooks;

/// <summary>
/// Interface for models that should know when a card is downgraded
/// </summary>
public interface IAfterCardDowngraded
{
    /// <summary>
    /// Called after a card is downgraded.
    /// </summary>
    void AfterCardDowngraded(CardModel card);

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.DowngradeInternal))]
    private static class DowngradeHook
    {
        [HarmonyPostfix]
        private static void Patch(CardModel __instance)
        {
            var combatState = BetaMainCompatibility.CardModel_.WrappedCombatState(__instance);
            var runState = __instance.Owner?.RunState ?? (combatState == null ? NullRunState.Instance : combatState.RunState);
            foreach (var item in BetaMainCompatibility.RunState.IterateHookListeners.Invoke<IEnumerable<AbstractModel>>(runState, combatState?.WrappedState) ?? [])
            {
                (item as IAfterCardDowngraded)?.AfterCardDowngraded(__instance);
            }
        }
    }
}