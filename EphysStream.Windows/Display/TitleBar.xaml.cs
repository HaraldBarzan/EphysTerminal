using EphysStream.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TINS.Ephys.Display
{
	/// <summary>
	/// Interaction logic for TitleBar.xaml
	/// </summary>
	public partial class TitleBar : UserControl
    {
		public static readonly DependencyProperty TitleProperty =
			DependencyProperty.Register(nameof(Title), typeof(string), typeof(TitleBar), new PropertyMetadata(null));

		public static readonly DependencyProperty SourceProperty =
			DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(TitleBar), new PropertyMetadata(null));

		public static readonly DependencyProperty WindowProperty =
			DependencyProperty.Register(nameof(Window), typeof(Window), typeof(TitleBar), new PropertyMetadata(null));

		/// <summary>
		/// Default constructor.
		/// </summary>
		public TitleBar()
        {
            InitializeComponent();
        }

		/// <summary>
		/// Get or set the title.
		/// </summary>
		public string Title
		{
			get => (string)GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		/// <summary>
		/// Get or set the image source.
		/// </summary>
		public ImageSource Source
		{
			get => (ImageSource)GetValue(SourceProperty);
			set => SetValue(SourceProperty, value);
		}

		/// <summary>
		/// 
		/// </summary>
		public Window Window
		{
			get => (Window)GetValue(WindowProperty);
			set => SetValue(WindowProperty, value);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnExit_Click(object sender, RoutedEventArgs e) 
			=> Window?.Close();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnMaximize_Click(object sender, RoutedEventArgs e)
		{
			var window = Window;

			if (window is null) return;

			if (window.WindowState is WindowState.Maximized)
			{
				window.WindowState = WindowState.Normal;
				imgMaximize.Source = App.GetResource<ImageSource>("MaximizeIcon");
			}
			else
			{
				window.WindowState = WindowState.Maximized;
				imgMaximize.Source = App.GetResource<ImageSource>("RestoreIcon");
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void btnMinimize_Click(object sender, RoutedEventArgs e)
		{
			if (Window is null) return;

			Window.WindowState = WindowState.Minimized;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ccDragBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (Window is null) return;

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
			if (Window is null) return;

			if (e.LeftButton is MouseButtonState.Pressed && _windowDragStartPos.HasValue)
			{
				var currentPos		= e.GetPosition(this);
				var offset			= currentPos - _windowDragStartPos.Value;
				Window.Left	+= offset.X;
				Window.Top	+= offset.Y;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void ccDragBar_MouseUp(object sender, MouseButtonEventArgs e)
		{
			if (Window is null) return;

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

		private Point? _windowDragStartPos = null;
    }
}
