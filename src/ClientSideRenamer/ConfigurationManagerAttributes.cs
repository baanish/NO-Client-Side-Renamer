using BepInEx.Configuration;

namespace ClientSideRenamer;

// ConfigurationManager reads these tag properties by name. Keeping the tag local
// lets the plugin run normally when ConfigurationManager is not installed.
internal sealed class ConfigurationManagerAttributes
{
    public Action<ConfigEntryBase> CustomDrawer;
    public bool? HideDefaultButton;
    public bool? HideSettingName;
    public int? Order;
}
