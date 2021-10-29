using TINS.IO;

namespace TINS.Ephys.Settings
{
	public class StimulationSettings
		: SerializableSettingsItem
	{
		/// <summary>
		/// 
		/// </summary>
		[INILine(Key = "LOCK_ACTIVE_CHANNEL", Default = true)]
		public bool LockActiveChannel { get; set; }

		[INILine(Key = "TOPKEK", Default = "apexshrek")]
		public string Topkek { get; set; }
	}
}
