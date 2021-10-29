using System;
using System.IO;
using TINS.Data;
using TINS.Data.EPD;
using TINS.IO;
using TINS.Utilities;

namespace TINS.Ephys.Data
{
	/// <summary>
	/// 
	/// </summary>
	public class DataOutputStream
		: AsynchronousObject
	{
		/// <summary>
		/// Asynchronous caller used to write channel data asynchronously.
		/// </summary>
		[AsyncInvoke(nameof(OnWriteData))]
		public readonly EventHandler<Matrix<float>> WriteData = default;

		/// <summary>
		/// Asynchronous caller used to write events asynchronously.
		/// </summary>
		[AsyncInvoke(nameof(OnWriteEvent))]
		public readonly EventHandler<Vector<EventMarker>> WriteEvents = default;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="outputPath"></param>
		/// <param name="samplingRate"></param>
		/// <param name="channelLabels"></param>
		public DataOutputStream(string outputPath, float samplingRate, Vector<string> channelLabels)
			: base(false)
		{
			if (!outputPath.EndsWith(".epd", StringComparison.InvariantCultureIgnoreCase))
				outputPath += ".epd";

			// create the dataset
			_datasetName	= Path.GetFileNameWithoutExtension(outputPath);
			_dataset		= new Dataset()
			{
				Directory			= Directory.GetParent(outputPath).ToString(),
				EventCodesFile		= $"{_datasetName}-EventCodes.bin",
				EventTimestampsFile = $"{_datasetName}-EventTimestamps.bin",
				SamplingRate		= samplingRate
			};

			// create channels and open data streams
			foreach (var label in channelLabels)
			{
				var channel			= new SignalChannel(label, label + ".bin");
				_dataset.Channels	.PushBack(channel);
				_channelStreams		.PushBack(new IOStream(_dataset.GetChannelPath(channel), FileAccess.Write));
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;

			// close the stream
			Close();

			// clear vectors
			_channelStreams	?.Dispose();
			_eventMarkers	?.Dispose();

			_disposed = true;
			base.Dispose(disposing);
		}

		/// <summary>
		/// Close the stream. Closed streams may not be reopened (this operation is analogous to a dispose).
		/// </summary>
		public void Close()
		{
			FinalizeDatasetWrite();
		}

		/// <summary>
		/// Get the directory and name of the dataset.
		/// </summary>
		/// <param name="directory">The directory of the written dataset.</param>
		/// <param name="datasetName">The name of the written dataset.</param>
		public void GetPath(out string directory, out string datasetName)
		{
			directory	= _dataset.Directory;
			datasetName = _datasetName;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="data"></param>
		protected void OnWriteData(object sender, Matrix<float> data)
		{
			_ = sender;

			if (_dataset is null)
				return;

			if (_channelStreams.Size != data.BufferCount)
				throw new Exception("Streams not fit for source buffer.");

			// write each buffer to disk
			for (int iCh = 0; iCh < _channelStreams.Size; ++iCh)
			{
				if (_channelStreams[iCh] is null || !_channelStreams[iCh].IsOpen)
					throw new Exception($"Output stream for channel {_dataset.Channels[iCh].Label} ({iCh}) is closed or invalid.");
				
				_channelStreams[iCh].Write<float>(data.GetBuffer(iCh));
			}

			_dataset.RecordingLength += data.Cols;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="events"></param>
		protected void OnWriteEvent(object sender, Vector<EventMarker> events)
		{
			_ = sender;

			if (_dataset is null)
				return;

			foreach (var e in events)
				_eventMarkers.PushBack(new EventMarker(e.EventCode, e.Timestamp + _dataset.RecordingLength));
		}

		/// <summary>
		/// 
		/// </summary>
		protected void FinalizeDatasetWrite()
		{
			if (_dataset is not null)
			{
				// close all the streams
				foreach (var stream in _channelStreams)
					stream?.Dispose();
				_channelStreams.Clear();

				// save the timestamps
				_eventMarkers.Sort();
				_dataset.SaveEvents(_eventMarkers);
				_eventMarkers.Clear();

				// save the dataset
				_dataset.Save(Path.Combine(_dataset.Directory, _datasetName) + ".epd");
			}
		}

		protected Vector<IOStream>		_channelStreams	= new();
		protected Vector<EventMarker>	_eventMarkers	= new();
		protected Dataset				_dataset		= null;
		protected string				_datasetName	= string.Empty;
		private bool					_disposed		= false;
	}
}
