using ColossalFramework.UI;
using ICities;

namespace CsToBeamNG.Exporter.Game;

/// <summary>Helpers for the mod's page in Options.</summary>
internal static class SettingsUi
{
    /// <summary>
    /// Adds a line of text to a group. <see cref="UIHelperBase"/> has no method for that, so this uses
    /// the game's implementation, <c>UIHelper</c>, whose <c>self</c> is the group's panel.
    /// </summary>
    /// <param name="group">A group from <see cref="UIHelperBase.AddGroup"/>.</param>
    /// <param name="text">The text.</param>
    /// <returns>Whether the text was added.</returns>
    public static bool AddLabel(UIHelperBase group, string text)
    {
        if (group is not UIHelper helper || helper.self is not UIComponent panel)
        {
            return false;
        }

        UILabel label = panel.AddUIComponent<UILabel>();
        label.text = text;
        return true;
    }
}
