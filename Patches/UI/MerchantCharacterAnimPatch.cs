using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace BaseLib.Patches.UI;

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
class MerchantCharacterAnimPatch
{
    [HarmonyPrefix]
    public static bool SkipAnimIfNotSpine(NMerchantCharacter __instance, string anim, bool loop)
    {
        return !CustomAnimation.PlayCustomAnimation(__instance, GetAnimNames(anim));
    }

    private static string[] GetAnimNames(string animName)
    {
        return animName switch
        {
            "relaxed_loop" => ["idle", "Idle", animName],
            "die" => ["Die", animName],
            _ => [animName]
        };
    }
}