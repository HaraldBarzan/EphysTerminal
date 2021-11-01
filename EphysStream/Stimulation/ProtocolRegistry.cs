using System;


namespace TINS.Ephys.Stimulation
{
	public static class ProtocolRegistry
	{

		/// <summary>
		/// An attribute to mark protocols with.
		/// </summary>
		public class AddAttribute : Attribute
		{
			/// <summary>
			/// The protocol's attribute.
			/// </summary>
			/// <param name="protocolIdentifier"></param>
			public AddAttribute(string protocolIdentifier)
			{
			}

			/// <summary>
			/// The identifier for this protocol.
			/// </summary>
			public string Identifier { get; init; }
		}
	}
}
