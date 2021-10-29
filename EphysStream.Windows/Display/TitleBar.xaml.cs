using CircuitGENUS.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TINS.Ephys.Display
{
	/// <summary>
	/// Interaction logic for TitleBar.xaml
	/// </summary>
	public partial class TitleBar : UserControl
    {
		/// <summary>
		/// 
		/// </summary>
        public TitleBar()
        {
            InitializeComponent();
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="window"></param>
		public void HookWindow(Window window) 
			=> _parentWindow = window;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnExit_Click(object sender, RoutedEventArgs e) 
			=> _parentWindow?.Close();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnMaximize_Click(object sender, RoutedEventArgs e)
		{
			if (_parentWindow is null) return;

			if (_parentWindow.WindowState is WindowState.Maximized)
			{
				_parentWindow.WindowState = WindowState.Normal;
				btnMaximize.Content = App.GetResource<Image>("MaximizeIcon");
			}
			else
				_parentWindow.WindowState = WindowState.Maximized;
				btnMaximize.Content = App.GetResource<Image>("RestoreIcon");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnMinimize_Click(object sender, RoutedEventArgs e)
		{
			if (_parentWindow is null) return;

			_parentWindow.WindowState = WindowState.Minimized;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ccDragBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_parentWindow is null) return;

			if (e.ChangedButton is MouseButton.Left)
				_windowDragStartPos = e.GetPosition(this);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ccDragBar_MouseMove(object sender, MouseEventArgs e)
		{
			if (_parentWindow is null) return;

			if (e.LeftButton is MouseButtonState.Pressed && _windowDragStartPos.HasValue)
			{
				var currentPos		= e.GetPosition(this);
				var offset			= currentPos - _windowDragStartPos.Value;
				_parentWindow.Left	+= offset.X;
				_parentWindow.Top	+= offset.Y;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ccDragBar_MouseUp(object sender, MouseButtonEventArgs e)
		{
			if (_parentWindow is null) return;

			if (e.ChangedButton is MouseButton.Left)
				_windowDragStartPos = null;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ccDragBar_MouseDoubleClick(object sender, MouseButtonEventArgs e) 
			=> btnMaximize_Click(sender, null);

		private Point?		_windowDragStartPos = null;
		protected Window	_parentWindow		= null;
    }
}
