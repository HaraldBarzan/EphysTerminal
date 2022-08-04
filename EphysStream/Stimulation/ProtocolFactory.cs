using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TINS.Ephys.Stimulation
{
	/// <summary>
	/// Use to instantiate stimulation protocols.
	/// </summary>
	public static class ProtocolFactory
	{
		/// <summary>
		/// Registered protocols.
		/// </summary>
		static Dictionary<string, Type> Protocols { get; } = new()
		{
			{ "dummy",	typeof(DummyProtocol) } // builtin type
		};

		static Type ProtocolBase { get; } = typeof(IStimulationProtocol);
		static Type ProtocolGenericBase { get; } = typeof(StimulationProtocol<,>);
		static Type ProtocolConfigBase { get; } = typeof(ProtocolConfig);
		static Type StimulusControllerBase { get; } = typeof(StimulusController);



		/// <summary>
		/// Register a protocol.
		/// </summary>
		/// <param name="protocolType">The type of the protocol.</param>
		/// <param name="protocolName">The name to register the protocol as (case sensitive).</param>
		/// <returns></returns>
		public static bool RegisterProtocol(Type protocolType, string protocolName)
		{
			if (protocolName is null) throw new ArgumentNullException("Protocol name is null.");
			if (protocolType is null) throw new ArgumentNullException("Protocol type is null.");

			if (GetProtocolUnderlyingTypes(protocolType, out _, out _))
			{
				Protocols.Add(protocolName, protocolType);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Remove a protocol from the registry.
		/// </summary>
		/// <param name="protocolName">The name of the protocol to remove.</param>
		/// <returns>True if the protocol was successfully removed.</returns>
		public static bool UnregisterProtocol(string protocolName)
		{
			if (Protocols.ContainsKey(protocolName))
			{
				Protocols.Remove(protocolName);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Get the type for a given protocol name.
		/// </summary>
		/// <param name="protocolName">The name of the protocol, case sensitive.</param>
		/// <returns>The name of the protocol.</returns>
		public static Type GetProtocolType(string protocolName)
		{
			if (Protocols.TryGetValue(protocolName, out var type))
				return type;
			throw new Exception($"Type not found for \'{protocolName}\'.");
		}

		/// <summary>
		/// Get the type for a given protocol name.
		/// </summary>
		/// <param name="protocolName">The name of the protocol, case sensitive.</param>
		/// <param name="protocolType">The type of the protocol.</param>
		/// <returns>True if the operation is successful.</returns>
		public static bool TryGetProtocolType(string protocolName, out Type protocolType)
			=> Protocols.TryGetValue(protocolName, out protocolType);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocolFile"></param>
		/// <returns></returns>
		public static IStimulationProtocol LoadProtocol(EphysTerminal stream, string protocolFile)
		{
			if (!File.Exists(protocolFile))
				throw new FileNotFoundException($"Protocol file \'{protocolFile}\' does not exist.");


			// will throw an error if something is wrong
			var file	= File.ReadAllText(protocolFile);
			var cfg		= JsonSerializer.Deserialize<ProtocolConfig>(file);
			var type	= GetProtocolType(cfg.ProtocolType);

			// typecast and load the configuration
			if (GetProtocolUnderlyingTypes(type, out var configType, out var stimCtlType))
			{
				// load the config (under the provided type)
				var config = JsonSerializer.Deserialize(file, configType);

				// create the stimulus controller
				var stimCtl = stimCtlType != StimulusControllerBase
					? Activator.CreateInstance(stimCtlType)
					: null;

				// create the protocol
				return Activator.CreateInstance(type, stream, config, stimCtl) as IStimulationProtocol;
			}

			return null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="protocolType"></param>
		/// <param name="config"></param>
		/// <param name="stimCtl"></param>
		/// <returns></returns>
		static bool GetProtocolUnderlyingTypes(Type protocolType, out Type config, out Type stimCtl)
		{
			config	= default;
			stimCtl = default;

			bool ok = false;
			while (protocolType != typeof(object))
			{
				if (protocolType.IsGenericType && protocolType.GetGenericTypeDefinition() == typeof(StimulationProtocol<,>))
				{
					ok = true;
					break;
				}
				else
					protocolType = protocolType.BaseType;
			}

			if (ok)
			{
				var genericArgs = protocolType.GetGenericArguments();
				config	= genericArgs[0];
				stimCtl	= genericArgs[1];
				return true;
			}

			return false;
		}
	}
}
