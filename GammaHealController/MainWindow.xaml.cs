using System.Windows;

namespace GammaHealController
{
	/// <summary>
	/// 
	/// </summary>
	public enum ProgramType
	{
		None,
		DeviceTest,
		Stimulation,
		ClosedLoop
	}


	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 
		/// </summary>
		public ProgramType SelectedOption { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnExit_Click(object sender, RoutedEventArgs e)
		{
			SelectedOption	= ProgramType.None;
			DialogResult	= false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnTest_Click(object sender, RoutedEventArgs e)
		{
			SelectedOption	= ProgramType.DeviceTest;
			DialogResult	= true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnStim_Click(object sender, RoutedEventArgs e)
		{
			SelectedOption	= ProgramType.Stimulation;
			DialogResult	= true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnCLStim_Click(object sender, RoutedEventArgs e)
		{
			SelectedOption	= ProgramType.ClosedLoop;
			DialogResult	= true;
		}
	}
}
