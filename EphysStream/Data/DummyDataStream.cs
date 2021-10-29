using System;
using TINS.Ephys.Settings;

namespace TINS.Ephys.Data
{
	public class DummyDataStream
		: DataInputStream
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		/// <param name="ringBufferSize"></param>
		public DummyDataStream(EphysSettings settings, int ringBufferSize = 3)
			: base(settings, ringBufferSize)
		{
			InitTemplate(settings);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			_template?.Dispose();
			base.Dispose(disposing);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected override DataStreamError ConnectStream()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void DisconnectStream()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="settings"></param>
		protected void InitTemplate(EphysSettings settings)
		{

		}





		protected Matrix<float> _template = new();
	}
}
