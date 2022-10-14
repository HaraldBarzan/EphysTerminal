using SkiaSharp;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TINS.Terminal.UI;

namespace TINS.Terminal.Display.Ephys
{
    /// <summary>
    /// Continuous data (LFP/MUA) display. 
    /// </summary>
    public class ContinuousDisplay
        : DataDisplay
    {

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ContinuousDisplay()
        {
            _graphPaint.Color   = new(200, 0, 255);
            _graphPaint.Style   = SKPaintStyle.Stroke;
            over.Visibility     = Visibility.Hidden;

            CreateContextMenu();
        }

        /// <summary>
        /// Override setup specifically for graphs.
        /// </summary>
        /// <param name="channelMapping"></param>
        public override void SetChannelConfiguration(Matrix<Mapping> channelMapping)
        {
            base.SetChannelConfiguration(channelMapping);

            foreach (var g in _graphs)
                g?.Dispose();
            _graphs.Clear();
            _graphs.Resize(channelMapping.Dimensions);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lfpAcc"></param>
        public void Update(ContinuousDisplayAccumulator lfpAcc)
        {
            if (lfpAcc is null || lfpAcc.BufferSize < 2)
                return;

            lock (lfpAcc)
            {
                for (int iRow = 0; iRow < _graphs.Rows; ++iRow)
                {
                    for (int iCol = 0; iCol < _graphs.Cols; ++iCol)
                    {
                        if (_channelMapping[iRow, iCol].IsInvalid)
                            continue;

                        int sourceChIndex = _channelMapping[iRow, iCol].SourceIndex;

                        // create graph
                        _graphs[iRow, iCol] ??= new SKPath();
                        var lineGraph = _graphs[iRow, iCol];
                        var lineData = lfpAcc.GetBuffer(sourceChIndex);

                        // rewind the graph and start from scratch
                        lineGraph.Rewind();

                        // push data to path
                        lineGraph.MoveTo(0, -lineData[0]);
                        for (int i = 1; i < lineData.Length; ++i)
                            lineGraph.LineTo(i, -lineData[i]);
                    }
                }

                foreach (var rts in _rtSpecDisplays)
                    rts.Update(lfpAcc);
            }

            data.InvalidateVisual();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void ResetData()
        {
            foreach (var g in _graphs)
                g?.Rewind();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c"></param>
        protected override void RenderData(SKCanvas c)
        {
            if (!_canDrawData) return;

            float yScale = _axisRect.Height / _yRange.Size();

            c.Clear();
            for (int iRow = 0; iRow < _graphs.Rows; ++iRow)
            {
                for (int iCol = 0; iCol < _graphs.Cols; ++iCol)
                {
                    if (_graphs[iRow, iCol] is null || _graphs[iRow, iCol].PointCount < 2)
                        continue;

                    float xScale = _axisRect.Width / _graphs[iRow, iCol].PointCount;

                    // reset the affine matrix and save its state
                    int state = c.Save();

                    // focus on the axis rect only
                    var currentAxisRect = _axisRect;
                    currentAxisRect.Offset(_panelRect.Width * iCol, _panelRect.Height * iRow);
                    c.ClipRect(currentAxisRect);

                    // set 0 to middle of yaxis
                    c.Translate(currentAxisRect.Left, currentAxisRect.Top + currentAxisRect.Height / 2);

                    // map data coordinates to pixel coordinates
                    c.Scale(xScale, yScale);

                    // draw the graph
                    c.DrawPath(_graphs[iRow, iCol], _graphPaint);

                    c.RestoreToCount(state);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void MitRTSpectrum_Click(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(mitRTSpectrum, sender) &&
                _contextMenuLocation.HasValue &&
                TryGetChannelAt(_contextMenuLocation.Value, out var mapping, out _, out _))
            {
                new Thread(() =>
                {
                    var rts = new RealTimeSpectrumDisplay();
                    rts.Text = $"{mapping.Label} - Fourier spectrum";
                    rts.TopMost = true;
                    rts.Initialize(mapping.SourceIndex, 2048, 1000); // TODO remove hardcoding
                    rts.FormClosing += (o, _) =>
                    {
                        if (o is RealTimeSpectrumDisplay d)
                            _rtSpecDisplays.Remove(d);
                    };
                    _rtSpecDisplays.PushBack(rts);
                    rts.ShowDialog();
                }).Start();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        protected void CreateContextMenu()
        {
            mitRTSpectrum = new MenuItem() { Header = "Real-time Fourier spectrum" };
            mitRTSpectrum.Click += MitRTSpectrum_Click;

            ContextMenu.Items.Insert(0, mitRTSpectrum);

            ContextMenu.Opened += (_, _) => _contextMenuLocation = Mouse.GetPosition(this);
            ContextMenu.Closed += (_, _) => _contextMenuLocation = null;
        }


        // context menus
        protected Point?                            _contextMenuLocation;
        protected MenuItem                          mitRTSpectrum;

        // data
        protected SKPaint                           _graphPaint     = new();
        protected Matrix<SKPath>                     _graphs        = new();
        protected Vector<RealTimeSpectrumDisplay>   _rtSpecDisplays = new();

    }
}
