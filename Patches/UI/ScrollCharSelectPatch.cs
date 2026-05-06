using BaseLib.Abstracts;
using BaseLib.BaseLibScenes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace BaseLib.Patches.UI;

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen._Ready))]
class ScrollCharSelectPatch
{
    private const int VisibleButtons = 8;
    private static bool ScrollEnabled = ModelDb.AllCharacters.Count(character => character is not CustomCharacterModel
        { HideFromVanillaCharacterSelect: true }) >= VisibleButtons - 1; //-1 to count random button
    
    [HarmonyPrefix]
    static void AdjustCharSelectButtons(NCharacterSelectScreen __instance)
    {
        if (!ScrollEnabled)
            return;
        
        BaseLibMain.Logger.Info("More than 8 selection options, enabling character select scroll");
        
        var selectButtons = __instance.GetNode<Control>("CharSelectButtons");
        var buttonContainer = selectButtons.GetNode<Control>("ButtonContainer");

        var horizontalScroll = NHorizontalScrollContainer.Create("CharSelectButtons", buttonContainer, control =>
        {
            control.Size = control.CustomMinimumSize = new(116f * VisibleButtons, 200);
        });
        
        selectButtons.ReplaceBy(horizontalScroll);
        horizontalScroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.KeepSize);
        buttonContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft, Control.LayoutPresetMode.KeepSize);
        
        selectButtons.QueueFree();
    }

    [HarmonyPostfix]
    static void AdjustMouseBehavior(NCharacterSelectScreen __instance)
    {
        if (!ScrollEnabled)
            return;
        
        var horizontalScroll = __instance.GetNode("CharSelectButtons") as NHorizontalScrollContainer;
        if (horizontalScroll == null)
            return;
        
        var buttonContainer = horizontalScroll.GetNode<Control>("ButtonContainer");
        
        foreach (var selectButton in buttonContainer.GetChildren().OfType<NCharacterSelectButton>())
        {
            selectButton.MouseFilter = Control.MouseFilterEnum.Pass;
        }
    }
}