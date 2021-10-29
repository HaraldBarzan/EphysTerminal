using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TINS.Ephys.Display
{
	/// <summary>
	/// Interaction logic for StimulusDisplay.xaml
	/// </summary>
	public partial class StimulusDisplay : UserControl
	{
		/// <summary>
		/// 
		/// </summary>
		public StimulusDisplay()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="stimulusOn"></param>
		/// <param name="stimulusFrequency"></param>
		/// <param name="stimulusIntensity"></param>
		public void Update(bool stimulusOn, double stimulusFrequency, double stimulusIntensity)
		{
			if (!Dispatcher.CheckAccess())
				Dispatcher.BeginInvoke(new Action<bool, double, double>(Update), stimulusOn, stimulusFrequency, stimulusIntensity);

			Foreground				= stimulusOn ? Brushes.White : Brushes.Transparent;
			lblFrequency.Foreground = stimulusOn ? Brushes.Black : Brushes.White;
			lblFrequency.Foreground	= stimulusOn ? Brushes.Black : Brushes.White;
			lblFrequency.Content	= $"{stimulusFrequency} Hz";
			lblIntensity.Content	= $"{stimulusIntensity} %";
		}
	}
}
