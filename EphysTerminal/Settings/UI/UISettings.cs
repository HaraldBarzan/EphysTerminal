using TINS.IO;
using TINS.Ephys.Settings;

namespace TINS.Terminal.Settings.UI
{
    /// <summary>
    /// Settings to display a graphical user interface (GUI).
    /// </summary>
    public abstract class UISettings
        : SerializableSettingsItem
    {
        /// <summary>
        /// The type name of this UI settings item.
        /// </summary>
        public abstract string TypeName { get; }
    }
}
