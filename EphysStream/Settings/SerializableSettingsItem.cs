using TINS.IO;

namespace TINS.Ephys.Settings
{
	/// <summary>
	/// The direction of serialization.
	/// </summary>
	public enum SerializationDirection
	{
		In,
		Out
	}

	/// <summary>
	/// Describes a class which can be serialized.
	/// </summary>
	public abstract class SerializableSettingsItem
	{
		/// <summary>
		/// Serialize from an INI file.
		/// </summary>
		/// <param name="ini">The INI file.</param>
		/// <param name="sectionName">The section to serialize from.</param>
		/// <param name="direction">The direction of serialization.</param>
		public virtual void Serialize(INI ini, string sectionName, SerializationDirection direction)
		{
			if (string.IsNullOrWhiteSpace(sectionName)) return;

			if (direction == SerializationDirection.Out)
				INISerialization.Serialize(this, ini[sectionName]);
			else
				INISerialization.Serialize(ini[sectionName], this);
		}
	}
}
