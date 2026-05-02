using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Common.Rewards;
using BaseLib.Extensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace BaseLib.Patches.Content;

/// <summary>
/// Extensions to <see cref="RewardSynchronizer"/> to provide public getters to internal properties and common reward functions
/// </summary>
[HarmonyPatch(typeof(RewardSynchronizer))]
public static class RewardSynchronizerExtensions
{
    /// <summary>
    /// Struct to save a custom reward message until combat ends
    /// Prefer creating with <see cref="BufferCustomRewardMessage"/>
    /// </summary>
    public struct BufferedCustomRewardMessage
    {
        /// <summary>
        /// the id of the player who sent the message
        /// </summary>
        public ulong SenderId;
        /// <summary>
        /// The message being sent
        /// </summary>
        public CustomTargetedMessageWrapper Message;
    }

    // Not used in BaseLib as publicizer is enabled, but useful for mods without.
    /// <summary>
    /// Exposes the private INetGameService property.
    /// </summary>
    public static INetGameService? GameService(this RewardSynchronizer rewardSynchronizer) => rewardSynchronizer._gameService;
    
    /// <summary>
    /// Reference list of buffered messages<br/>
    /// </summary>
    internal static readonly SpireField<RewardSynchronizer, List<BufferedCustomRewardMessage>>
        BufferedCustomRewardMessages = new(() => []);

    /// <summary>
    /// Add a <see cref="CustomRewardMessage"/> to the combat buffer
    /// </summary>
    public static void BufferCustomRewardMessage(this RewardSynchronizer rewardSynchronizer, CustomTargetedMessageWrapper message, ulong senderId)
    {
        var bufferedMessage = new BufferedCustomRewardMessage
        {
            SenderId = senderId,
            Message = message
        };
        BufferedCustomRewardMessages[rewardSynchronizer]!.Add(bufferedMessage);
    }

    /// <summary>
    /// Method to handle transforming a card as a combat reward
    /// </summary>
    public static async Task<bool> DoLocalCardTransform(this RewardSynchronizer rewardSynchronizer, int amount = 1, bool upgrade = false)
    {
        CardTransformRewardMessage message = new CardTransformRewardMessage
        {
            Location = rewardSynchronizer._messageBuffer.CurrentLocation,
            wasSkipped = false,
            Upgrade = upgrade,
            Amount = amount
        };
        BaseLibMain.Logger.Debug($"Transforming card for local player {rewardSynchronizer.LocalPlayer}");

        rewardSynchronizer.GameService().SendMessage(message);
        return await rewardSynchronizer.DoCardTransform(rewardSynchronizer.LocalPlayer, amount, upgrade);
    }

    /// <summary>
    /// Transform a card for a specific player as a combat reward
    /// </summary>
    public static async Task<bool> DoCardTransform(this RewardSynchronizer rewardSynchronizer, Player player, int amount = 1, bool upgrade = false)
    {
        CardSelectorPrefs prefs = new CardSelectorPrefs(
                upgrade
                    ? CardSelectorPrefsExtensions.TransformAndUpgradeSelectionPrompt
                    : CardSelectorPrefs.TransformSelectionPrompt,
                1,
                amount)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        List<CardModel> cards = (await CardSelectCmd.FromDeckForTransformation(player, prefs)).ToList();

        BaseLibMain.Logger.Debug($"Current combat state for transform rewards is: IsEnding={CombatManager.Instance.IsEnding}");
        foreach (CardModel card in cards)
        {
            CardModel newCard = CardFactory.CreateRandomCardForTransform(
                    card,
                    isInCombat: false,
                    player.RunState.Rng.Niche);

            if (upgrade || card.IsUpgraded) // need a more robust handler for multi-upgrade at some point
            {
                CardCmd.Upgrade(newCard);
            }

            await CardCmd.Transform(card, newCard, CardPreviewStyle.GridLayout);
            BaseLibMain.Logger.Debug($"Player {player.NetId} transformed {card.Id} in their deck into {newCard.Id}" + (upgrade ? " and upgraded it." : "."));
        }

        return cards.Count > 0;
    }

    [HarmonyPatch(nameof(RewardSynchronizer.OnCombatEnded))]
    [HarmonyPrefix]
    private static void OnCombatEndHandleCustomBufferedMessages(RewardSynchronizer __instance)
    {
        foreach (var bufferedMessage in BufferedCustomRewardMessages[__instance]!)
        {
            __instance._messageBuffer?.CallHandlersOfType(bufferedMessage.Message.GetType(), bufferedMessage.Message, bufferedMessage.SenderId);
        }
        BufferedCustomRewardMessages[__instance]!.Clear();
    }
}
