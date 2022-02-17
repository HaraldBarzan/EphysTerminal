using SkiaSharp;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TINS.Ephys.UI;

namespace TINS.Ephys.Display
{
	/// <summary>
	/// The mode of the multiunit display.
	/// </summary>
	public enum MultiunitDisplayMode
	{
		Multiunit,
		Waveforms
	}

	/// <summary>
	/// Event args for the ThresholdChanged event.
	/// </summary>
	public struct ThresholdChangedEventArgs
	{
		/// <summary>
		/// The channel whose threshold was changed.
		/// </summary>
		public DataDisplay.Mapping Mapping { get; init; }

		/// <summary>
		/// The new value of the threshold.
		/// </summary>
		public float NewValue { get; init; }
	}

	/// <summary>
	/// Event args for the ThresholdChanged event.
	/// </summary>
	public struct LiveChannelChangedEventArgs
	{
		/// <summary>
		/// The new live channel label.
		/// </summary>
		public string NewChannelLabel { get; init; }
	}

	/// <summary>
	/// A multiunit display with switchable MUA and waveforms views.
	/// </summary>
	public class MultiunitDisplay : DataDisplay
	{
		/// <summary>
		/// A spike as drawn on screen.
		/// </summary>
		public struct Spike 
		{
			/// <summary>
			/// Spike position in buffer.
			/// </summary>
			public float Position { get; init; }
			
			/// <summary>
			/// Spike minimum value.
			/// </summary>
			public float Min { get; init; }
			
			/// <summary>
			/// Spike maximum value.
			/// </summary>
			public float Max { get; init; }
		}

		/// <summary>
		/// A draggable threshold slider.
		/// </summary>
		public class ThresholdSlider
		{
			/// <summary>
			/// The area in which the slider can be interacted with.
			/// </summary>
			public Rect InteractionArea { get; set; }
			
			/// <summary>
			/// Get or set the viewport of the slider.
			/// </summary>
			public SKRect Viewport { get; set; }

			/// <summary>
			/// The actual value of the slider.
			/// </summary>
			public float Value { get; set; }

			/// <summary>
			/// Get a representation of this object.
			/// </summary>
			/// <returns>A string representation of this object.</returns>
			public override string ToString() => $"{Viewport}, {InteractionArea}, {Value}";
		}

		/// <summary>
		/// Raised when a threshold value is changed.
		/// </summary>
		public event EventHandler<ThresholdChangedEventArgs> ThresholdChanged;

		/// <summary>
		/// 
		/// </summary>
		public event EventHandler<LiveChannelChangedEventArgs> LiveChannelChanged;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public MultiunitDisplay()
		{
			_multiunitPaint.Color	= new(195, 255, 0);
			_multiunitPaint.Style	= SKPaintStyle.Stroke;
			_waveformPaint.Color	= new(255, 0, 0);
			_waveformPaint.Style	= SKPaintStyle.Stroke;

			CreateContextMenu();
		}

		/// <summary>
		/// Override setup specifically for graphs.
		/// </summary>
		/// <param name="channelMapping"></param>
		public override void SetChannelConfiguration(Matrix<Mapping> channelMapping)
		{
			base.SetChannelConfiguration(channelMapping);

			// resize multiunit graphs
			foreach (var m in _multiunits) 
				m?.Dispose(); 
			_multiunits.Clear();
			_multiunits.Resize(channelMapping.Dimensions);
			
			// resize spike vectors
			foreach (var s in _spikes) s?.Dispose();
			_spikes.Clear();
			_spikes.Resize(channelMapping.Dimensions);
			
			// resize waveform paths
			foreach (var w in _waveforms)
			{
				w?.ForEach(x => x?.Dispose());
				w?.Dispose();
			}
			_waveforms.Clear();
			_waveforms.Resize(channelMapping.Dimensions);

			// create threshold slider matrix
			_thresholds.Resize(channelMapping.Dimensions);
			_thresholds.Fill(null);
			for (int i = 0; i < channelMapping.Size; ++i)
				if (!channelMapping[i].IsInvalid) _thresholds[i] = new() { Value = -0.5f };
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="muaData"></param>
		public void Update(ContinuousDisplayAccumulator muaAcc, SpikeDisplayAccumulator spikeAcc = null)
		{
			if (muaAcc is null || muaAcc.BufferSize < 2)
				return;

			Monitor.Enter(muaAcc);
			if (spikeAcc is not null) 
				Monitor.Enter(spikeAcc);

			// update thresholds if needed
			if (_thresholdSDAuto.HasValue)
			{
				AutoUpdateThresholds(muaAcc, _thresholdSDAuto.Value);
				_thresholdSDAuto = null;
				UpdateDisplay();
			}


			for (int iRow = 0; iRow < _multiunits.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _multiunits.Cols; ++iCol)
				{
					if (_channelMapping[iRow, iCol].IsInvalid)
						continue;

					int sourceChIndex = _channelMapping[iRow, iCol].SourceIndex;

					// MULTIUNIT
					_multiunits[iRow, iCol] ??= new SKPath();
					var lineGraph		= _multiunits[iRow, iCol];
					var lineData		= muaAcc.GetBuffer(_channelMapping[iRow, iCol].SourceIndex);
					
					lineGraph.Rewind();
					lineGraph.MoveTo(0, -lineData[0]);
					for (int i = 1; i < lineData.Length; ++i)
						lineGraph.LineTo(i, -lineData[i]);

					// SPIKE TIMES
					if (spikeAcc is object)
					{
						var srcTimes = spikeAcc.GetTimestamps(sourceChIndex);

						// prepare spike data vector
						_spikes[iRow, iCol] ??= new();
						var dst = _spikes[iRow, iCol];
						dst.Resize(srcTimes.Length);

						// get spikes
						for (int i = 0; i < srcTimes.Length; ++i)
						{
							var waveform = spikeAcc.GetWaveform(sourceChIndex, i);
							dst[i] = new() 
							{ 
								Position	= srcTimes[i], 
								Min			= -Min(waveform), 
								Max			= -Max(waveform) 
							};
						}

						_waveforms[iRow, iCol] ??= new(MaxWaveforms);

						// get waveforms
						int nWaveforms = Math.Min(srcTimes.Length, MaxWaveforms);
						for (int i = 0; i < nWaveforms; ++i)
						{
							_waveforms[iRow, iCol][i] ??= new SKPath();
							var waveGraph	= _waveforms[iRow, iCol][i];
							var waveform	= spikeAcc.GetWaveform(sourceChIndex, i);

							waveGraph.Rewind();
							waveGraph.MoveTo(0, -waveform[0]);
							for (int j = 1; j < waveform.Length; ++j)
								waveGraph.LineTo(j, -waveform[j]);
						}
					}
				}
			}

			Monitor.Exit(muaAcc);
			if (spikeAcc is not null)
				Monitor.Exit(spikeAcc);

			data.InvalidateVisual();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="thresholds"></param>
		public void SetThresholds(Matrix<float> thresholds)
		{
			if (thresholds is object && thresholds.Dimensions == _thresholds.Dimensions)
			{
				for (int i = 0; i < thresholds.Size; ++i)
				{
					_thresholds[i] ??= new();
					_thresholds[i].Value = thresholds[i];
				}
				UpdateDisplay();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		/// <param name="threshold"></param>
		public void SetThreshold(int row, int column, float threshold)
		{
			_thresholds[row, column].Value = threshold;
			UpdateDisplay();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalSDs"></param>
		public virtual void EnqueueAutoThreshold(double signalSDs)
			=> _thresholdSDAuto = (float)signalSDs;

		/// <summary>
		/// Maximum number of waveforms to draw.
		/// </summary>
		public int MaxWaveforms { get; set; } = 10;

		/// <summary>
		/// Set what the display should show (spike timings or waveforms).
		/// </summary>
		public MultiunitDisplayMode DisplayMode
		{
			get => _mode;
			set
			{
				var old = _mode;
				_mode = value;

				if (_mode != old)
					UpdateDisplay();
			}
		}

		/// <summary>
		/// Set the X-range for the waveforms.
		/// </summary>
		public (float Lower, float Upper) WaveformXRange
		{
			get => _waveformXRange;
			set
			{
				var old = _waveformXRange;
				_waveformXRange = value;

				if (_waveformXRange != old)
					UpdateDisplay();
			}
		}

		/// <summary>
		/// Get or set the live channel.
		/// </summary>
		public string LiveChannel
		{
			get => _liveChannel.HasValue
				? _channelMapping[_liveChannel.Value.Row, _liveChannel.Value.Col].Label
				: null;
			set
			{
				_liveChannel = null;
				for (int i = 0; i < _channelMapping.Rows; ++i)
				for (int j = 0; j < _channelMapping.Cols; ++j)
				{
					if (_channelMapping[i, j].Label == value)
						_liveChannel = (i, j);
				}

				LiveChannelChanged?.Invoke(this, new() { NewChannelLabel = value });
			}
		}
			

		/// <summary>
		/// 
		/// </summary>
		protected override void ResetData()
		{
			// rewind multiunit activity
			foreach (var g in _multiunits)
				g?.Rewind();

			// clear spikes
			foreach (var l in _spikes)
				l?.Clear();

			// rewind waveforms
			foreach (var w in _waveforms)
				if (w is object)
					foreach (var g in w)
						g?.Rewind();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected override void RenderAxes(SKCanvas c)
		{
			_canDrawData = false;

			// check config
			if (_channelMapping.Size == 0)
			{
				RenderText(c, "Invalid config matrix.");
				return;
			}

			// check xy ranges
			if (!_xRange.IsValid() || !_yRange.IsValid())
			{
				RenderText(c, "Invalid X or Y range.");
				return;
			}


			// fonts and paints
			using var tickFont	= new SKFont()			{ Size = 10, Typeface = SKTypeface.FromFamilyName("Arial") };
			using var tickPaint = new SKPaint(tickFont) { Color = new(214, 214, 214), Style = SKPaintStyle.Fill };
			using var linePaint = new SKPaint()			{ Color = new(214, 214, 214), Style = SKPaintStyle.Stroke };
			
			// text and text size
			var strXRange = _mode switch 
			{
				MultiunitDisplayMode.Multiunit	=> (Lower: _xRange.Lower.ToString(),			Upper: _xRange.Upper.ToString()),
				MultiunitDisplayMode.Waveforms	=> (Lower: _waveformXRange.Lower.ToString(),	Upper: _waveformXRange.Upper.ToString()),
				_								=> default
			};
			var strYRange		= (Lower: _yRange.Lower.ToString(), Upper: _yRange.Upper.ToString());
			var xTickWidths		= (Lower: tickPaint.MeasureText(strXRange.Lower), Upper: tickPaint.MeasureText(strXRange.Upper));
			var yTickWidths		= (Lower: tickPaint.MeasureText(strYRange.Lower), Upper: tickPaint.MeasureText(strYRange.Upper));
			float maxYTickWidth = Math.Max(yTickWidths.Lower, yTickWidths.Upper);
			float yScale		= _axisRect.Height / _yRange.Size();
			
			// precompute sizes
			var availableArea	= c.DeviceClipBounds;
			float tickOffset	= 5;
			_panelRect			= SKRect.Create(0, 0, availableArea.Width / _channelMapping.Cols, availableArea.Height / _channelMapping.Rows);
			_axisRect			= SKRect.Create(tickOffset + maxYTickWidth + _margin, _margin, 
												_panelRect.Width - tickOffset - 2 * _margin - maxYTickWidth,
												_panelRect.Height - tickOffset - 2 * _margin - tickFont.Size);

			// begin draw
			c.Clear();
			for (int iRow = 0; iRow < _channelMapping.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _channelMapping.Cols; ++iCol)
				{
					if (_channelMapping[iRow, iCol].IsInvalid)
						continue;

					// adjust the threshold viewports
					if (_thresholds[iRow, iCol] is object)
					{
						_thresholds[iRow, iCol].Viewport = SKRect.Create(
							iCol * _panelRect.Width + _axisRect.Left,
							iRow * _panelRect.Height + _axisRect.Top,
							_axisRect.Width, _axisRect.Height);
					}

					// go to the current panel
					c.ResetMatrix();
					c.Translate(iCol * _panelRect.Width, iRow * _panelRect.Height);

					// draw labels
					c.DrawText(strYRange.Upper, _margin, _axisRect.Top + tickFont.Size, tickPaint);
					c.DrawText(strYRange.Lower, _margin, _axisRect.Bottom, tickPaint);
					c.DrawText(strXRange.Lower, _axisRect.Left, _axisRect.Bottom + tickOffset + tickFont.Size, tickPaint);
					c.DrawText(strXRange.Upper, _axisRect.Right - xTickWidths.Upper, _axisRect.Bottom + tickOffset + tickFont.Size, tickPaint);
					c.DrawText(_channelMapping[iRow, iCol].Label, _axisRect.Right - tickPaint.MeasureText(_channelMapping[iRow, iCol].Label), _axisRect.Top + tickFont.Size, tickPaint);

					// draw axes
					c.DrawRect(_axisRect, linePaint);
				}
			}

			_canDrawData = true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected override void RenderOverlay(SKCanvas c)
		{
			float yScale		= _axisRect.Height / _yRange.Size();
			using var thrPaint	= new SKPaint() { Color = new(255, 000, 000), Style = SKPaintStyle.Stroke };

			// begin draw
			c.Clear();
			for (int iRow = 0; iRow < _channelMapping.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _channelMapping.Cols; ++iCol)
				{
					if (_channelMapping[iRow, iCol].IsInvalid || _thresholds[iRow, iCol] is null)
						continue;

					float thresholdValue = _thresholds[iRow, iCol].Value;

					// go to the current panel
					c.ResetMatrix();
					c.Translate(iCol * _panelRect.Width + _axisRect.Left, iRow * _panelRect.Height + _axisRect.Top);

					// render live channel
					if (_liveChannel.HasValue  && 
						_liveChannel.Value == (iRow, iCol))
					{
						using var liveChannelPaint = new SKPaint() { Color = new(000, 255, 255), Style = SKPaintStyle.Stroke };
						c.DrawRect(0, 0, _axisRect.Width, _axisRect.Height, liveChannelPaint);
					}

					// draw threshold
					c.Scale(1, yScale);
					c.Translate(0, _yRange.Upper);
					c.Scale(1, -1);
					c.DrawLine(0, thresholdValue, _axisRect.Width, thresholdValue, thrPaint);

					// set threshold interaction area
					_thresholds[iRow,iCol].InteractionArea = new(
						iCol * _panelRect.Width + _axisRect.Left,
						iRow * _panelRect.Height + _axisRect.Top + (_yRange.Upper - thresholdValue) * yScale - 10,
						_axisRect.Width, 20);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected override void RenderData(SKCanvas c)
		{
			if (!_canDrawData) return;

			switch (_mode)
			{
				case MultiunitDisplayMode.Multiunit: 
					RenderMultiunitData(c); 
					break;

				case MultiunitDisplayMode.Waveforms:
					RenderWaveformData(c);
					break;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected void RenderMultiunitData(SKCanvas c)
		{
			float yScale = _axisRect.Height / _yRange.Size();

			c.Clear();
			for (int iRow = 0; iRow < _multiunits.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _multiunits.Cols; ++iCol)
				{
					if (_multiunits[iRow, iCol] is null || _multiunits[iRow, iCol].PointCount < 2)
						continue;

					float xScale = _axisRect.Width / _multiunits[iRow, iCol].PointCount;

					// reset the affine matrix and save its state
					int state = c.Save();

					// focus on the axis rect only
					var currentAxisRect = _axisRect;
					currentAxisRect.Offset(_panelRect.Width * iCol, _panelRect.Height * iRow);
					c.ClipRect(currentAxisRect);
					c.Translate(currentAxisRect.Left, currentAxisRect.Top);

					// map data coordinates to pixel coordinates
					c.Scale(xScale, yScale);
					c.Translate(0, _yRange.Upper);
					
					// draw the graph
					c.DrawPath(_multiunits[iRow, iCol], _multiunitPaint);

					if (_spikes[iRow, iCol] is object)
					{
						// draw the spikes
						var chSpikes = _spikes[iRow, iCol];
						for (int i = 0; i < chSpikes.Size; ++i)
						{
							var spk = chSpikes[i];
							c.DrawLine(	spk.Position, spk.Max, 
										spk.Position, spk.Min, 
										_waveformPaint);
						}
					}

					c.RestoreToCount(state);
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="c"></param>
		protected void RenderWaveformData(SKCanvas c)
		{
			float yScale = _axisRect.Height / _yRange.Size();

			c.Clear();
			for (int iRow = 0; iRow < _multiunits.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _multiunits.Cols; ++iCol)
				{
					if (_waveforms[iRow, iCol] is null || _waveforms[iRow, iCol].Front is null)
						continue;

					float xScale = _axisRect.Width / _waveforms[iRow, iCol].Front.PointCount;

					// reset the affine matrix and save its state
					int state = c.Save();

					// focus on the axis rect only
					var currentAxisRect = _axisRect;
					currentAxisRect.Offset(_panelRect.Width * iCol, _panelRect.Height * iRow);
					c.ClipRect(currentAxisRect);
					c.Translate(currentAxisRect.Left, currentAxisRect.Top);

					// map data coordinates to pixel coordinates
					c.Scale(xScale, yScale);
					c.Translate(0, _yRange.Upper);

					foreach (var g in _waveforms[iRow, iCol])
						if (g is object)
							c.DrawPath(g, _waveformPaint);
					
					c.RestoreToCount(state);
				}
			}
		}

		/// <summary>
		/// Attempt to get 
		/// </summary>
		/// <param name="location"></param>
		/// <param name="slider"></param>
		/// <returns></returns>
		protected bool TryGetSliderAt(Point location, out ThresholdSlider slider, out int row, out int col)
		{
			row = col = -1;
			slider = null;
			if (_thresholds.Size == 0)
				return false;

			for (int iRow = 0; iRow < _thresholds.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _thresholds.Cols; ++iCol)
				{
					if (_thresholds[iRow, iCol] is object && _thresholds[iRow, iCol].InteractionArea.Contains(location))
					{
						row = iRow;
						col = iCol;
						slider = _thresholds[iRow, iCol];
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="signalSDs"></param>
		protected void AutoUpdateThresholds(ContinuousDisplayAccumulator channelData, float signalSDs)
		{
			for (int iRow = 0; iRow < _thresholds.Rows; ++iRow)
			{
				for (int iCol = 0; iCol < _thresholds.Cols; ++iCol)
				{
					if (_channelMapping[iRow, iCol].IsInvalid)
						continue;

					int sourceChIndex	= _channelMapping[iRow, iCol].SourceIndex;
					var channel			= channelData.GetBuffer(sourceChIndex);
					RunningMSD msd		= default;

					// compute SD
					var data = channelData.GetBuffer(sourceChIndex);
					for (int i = 0; i < data.Length; ++i)
						msd.Push(data[i]);
					float mean	= msd.Mean;
					float sd	= msd.StandardDeviation;

					// set the threshold
					float threshold = Numerics.Clamp(mean + signalSDs * sd, _yRange);
					_thresholds[iRow, iCol].Value = threshold;
				}
			}
				

			
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void over_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var pos = e.GetPosition(this);
			if (e.ChangedButton is MouseButton.Left && TryGetSliderAt(pos, out var slider, out _, out _))
			{
				_draggingSlider = slider;
				_draggingYPos	= pos.Y;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void over_MouseMove(object sender, MouseEventArgs e)
		{
			// display the right cursor when going over a slider
			var mousePos = e.GetPosition(this);
			if (TryGetSliderAt(mousePos, out var slider, out _, out _))
			{
				// set the cursor sprite
				this.Cursor = Cursors.SizeNS;

				// verify we are dragging a slider with the left button
				if (e.LeftButton is MouseButtonState.Pressed &&
					_draggingSlider is object &&
					_draggingSlider == slider)
				{
					var yDiff				= (_draggingYPos - mousePos.Y) / (_axisRect.Height / _yRange.Size());
					_draggingSlider.Value	= Numerics.Clamp(_draggingSlider.Value + (float)yDiff, _yRange);
					_draggingYPos			= mousePos.Y;

					// invalidate the overlay (this will update the interaction area)
					over.InvalidateVisual();
				}
				else
				{
					// reject the slider
					_draggingSlider = null;
				}
			}
			else
			{
				// return the cursor to its original sprite
				this.Cursor = Cursors.Arrow;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void over_MouseUp(object sender, MouseButtonEventArgs e)
		{
			// ensure we are raising event for the correct slider
			if (e.ChangedButton is MouseButton.Left && 
				TryGetSliderAt(e.GetPosition(this), out var slider, out var iRow, out var iCol) &&
				slider == _draggingSlider)
			{
				// raise event
				ThresholdChanged?.Invoke(this, new()
				{
					Mapping		= _channelMapping[iRow, iCol],
					NewValue	= _draggingSlider.Value
				});

				// reset dragging params
				_draggingSlider = null;
				_draggingYPos	= 0;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		protected void CreateContextMenu()
		{
			mitSelectMultiunit			= new MenuItem() { Header = "Multiunit", IsCheckable = false, IsChecked = true };
			mitSelectWaveforms			= new MenuItem() { Header = "Waveforms", IsCheckable = false };
			mitSetLiveChannel			= new MenuItem() { Header = "Set live channel" };
			mitDisableLiveChannel		= new MenuItem() { Header = "Disable live channel" };
			mitSelectMultiunit.Click	+= MenuMultiunit_Click;
			mitSelectWaveforms.Click	+= MenuMultiunit_Click;
			mitSetLiveChannel.Click		+= MitSetLiveChannel_Click;
			mitDisableLiveChannel.Click += MitSetLiveChannel_Click;

			ContextMenu.Items.Insert(0, mitSelectMultiunit);
			ContextMenu.Items.Insert(1, mitSelectWaveforms);
			ContextMenu.Items.Insert(2, new Separator());
			ContextMenu.Items.Insert(3, mitSetLiveChannel);
			ContextMenu.Items.Insert(4, mitDisableLiveChannel);
			ContextMenu.Items.Insert(5, new Separator());

			ContextMenu.Opened += (_, _) => _contextMenuLocation = Mouse.GetPosition(this);
			ContextMenu.Closed += (_, _) => _contextMenuLocation = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MitSetLiveChannel_Click(object sender, RoutedEventArgs e)
		{
			if (ReferenceEquals(mitSetLiveChannel, sender)	&&  
				_contextMenuLocation.HasValue				&&
				TryGetChannelAt(_contextMenuLocation.Value, out var mapping, out var row, out var col))
			{
				// set the live channel
				_liveChannel = (row, col);
				over.InvalidateVisual();

				// raise event
				LiveChannelChanged?.Invoke(this, new() { NewChannelLabel = mapping.Label });
			}
			else if (ReferenceEquals(mitDisableLiveChannel, sender))
			{
				LiveChannelChanged?.Invoke(this, new() { NewChannelLabel = null });
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MenuMultiunit_Click(object sender, RoutedEventArgs e)
		{
			if (sender is MenuItem item)
			{
				if (ReferenceEquals(item, mitSelectMultiunit))
				{
					DisplayMode = MultiunitDisplayMode.Multiunit;
					mitSelectMultiunit.IsChecked = true;
					mitSelectWaveforms.IsChecked = false;
				}
				else if (ReferenceEquals(item, mitSelectWaveforms))
				{
					DisplayMode = MultiunitDisplayMode.Waveforms;
					mitSelectMultiunit.IsChecked = false;
					mitSelectWaveforms.IsChecked = true;
				}
			}
		}

		// data
		protected (float Lower, float Upper)	_waveformXRange			= (-0.6f, 1.2f);
		protected Matrix<Vector<SKPath>>		_waveforms				= new();
		protected Matrix<Vector<Spike>>			_spikes					= new();
		protected SKPaint						_multiunitPaint			= new();
		protected SKPaint						_waveformPaint			= new();
		protected Matrix<SKPath>				_multiunits				= new();
		protected MultiunitDisplayMode			_mode					= new();
		protected float?						_thresholdSDAuto		= null;

		// thresholds
		protected Matrix<ThresholdSlider>		_thresholds				= new();
		protected ThresholdSlider				_draggingSlider			= null;
		protected double						_draggingYPos			= 0;

		// live channel
		protected (int Row, int Col)?			_liveChannel			= null;
		protected Point?						_contextMenuLocation	= null;

		// specific menu items
		private MenuItem mitSelectMultiunit;
		private MenuItem mitSelectWaveforms;
		private MenuItem mitSetLiveChannel;
		private MenuItem mitDisableLiveChannel;

		/// <summary>
		/// Find the maximum of a list of values.
		/// </summary>
		/// <param name="data">The list of values.</param>
		/// <returns>The maximum value.</returns>
		protected static float Max(Span<float> data)
		{
			if (data.Length == 0) return 0;

			float max = data[0];
			for (int i = 1; i < data.Length; ++i)
				if (data[i] > max)
					max = data[i];

			return max;
		}

		/// <summary>
		/// Find the minimum of a list of values.
		/// </summary>
		/// <param name="data">The list of values.</param>
		/// <returns>The maximum value.</returns>
		protected static float Min(Span<float> data)
		{
			if (data.Length == 0) return 0;

			float min = data[0];
			for (int i = 1; i < data.Length; ++i)
				if (data[i] < min)
					min = data[i];

			return min;
		}
	}
}
