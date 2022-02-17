using System;
using System.IO;
using TINS.Data;
using TINS.Data.EPD;
using TINS.IO;
using TINS.Utilities;

namespace TINS.Ephys.Data
{
	/// <summary>
	/// Event args for data output.
	/// </summary>
	public class DataOutputEventArgs : EventArgs
	{
		/// <summary>
		/// The directory the dataset should be saved in. 
		/// </summary>
		public string DatasetDirectory { get; init; }

		/// <summary>
		/// The name of the dataset.
		/// </summary>
		public string DatasetName { get; init; }
	}

	/// <summary>
	/// A continuous data output stream that saves to EPD format.
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
		/// Create a data output stream (to EPD format).
		/// </summary>
		/// <param name="outputFolder">The path to the output dataset. Will overwrite an existing one.</param>
		/// <param name="datasetName">The path to the output dataset. Will overwrite an existing one.</param>
		/// <param name="samplingRate">The sampling rate for the new dataset.</param>
		/// <param name="channelLabels">The labels of the channels in the dataset.</param>
		public DataOutputStream(string outputFolder, string datasetName, float samplingRate, Vector<string> channelLabels)
			: base(false)
		{
			// create the dataset
			_datasetName	= datasetName;
			_dataset		= new Dataset()
			{
				Directory			= outputFolder,
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
		/// Finalize the writing of the dataset, providing the event code/timestamp files and the .epd file itself.
		/// </summary>
		protected void FinalizeDatasetWrite()
		{
			if (_dataset is not null)
			{
				// close all the streams
				foreach (var stream in _channelStreams)
					stream?.Dispose();
				_channelStreams.Clear();
				_eventMarkers.Sort();

				// save the timestamps
				_dataset.SaveEvents(_eventMarkers);
				_eventMarkers.Clear();

				// save the dataset
				_dataset.Save(Path.Combine(_dataset.Directory, _datasetName) + ".epd");
			}
		}

		protected Vector<IOStream>		_channelStreams		= new();
		protected Vector<EventMarker>	_eventMarkers		= new();
		protected Dataset				_dataset			= null;
		protected string				_datasetName		= string.Empty;
		protected Vector<int>			_supportedTriggers	= null;
		private bool					_disposed			= false;
	}
}
