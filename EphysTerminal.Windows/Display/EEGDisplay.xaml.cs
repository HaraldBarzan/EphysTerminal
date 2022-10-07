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

namespace TINS.Terminal.Display
{
	/// <summary>
	/// Interaction logic for EEGDisplay.xaml
	/// </summary>
	public partial class EEGDisplay 
		: UserControl
		, IChannelDisplay
	{
		/// <summary>
		/// Display type.
		/// </summary>
		public DisplayType DisplayType => DisplayType.EEG;

		/// <summary>
		/// Default constructor.
		/// </summary>
		public EEGDisplay()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Initialize the display.
		/// </summary>
		/// <param name="terminal">The terminal.</param>
		public void InitializeChannelDisplay(EphysTerminal terminal)
		{

		}

		/// <summary>
		/// Clear any data from the display.
		/// </summary>
		public void ClearDisplay()
		{

		}
	}
}
