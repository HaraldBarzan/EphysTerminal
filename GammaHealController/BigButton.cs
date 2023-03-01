using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TINS.Terminal.Display
{
	/// <summary>
	/// 
	/// </summary>
	public class BigButton : Button
	{
		static BigButton()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(BigButton), 
				new FrameworkPropertyMetadata(typeof(BigButton)));
		}

		/// <summary>
		/// Source dependency property.
		/// </summary>
		public static readonly DependencyProperty SourceProperty =
			DependencyProperty.Register(nameof(Source),		typeof(ImageSource), 
										typeof(BigButton),	new PropertyMetadata(null));

		/// <summary>
		/// Text dependency property.
		/// </summary>
		public static readonly DependencyProperty TextProperty = 
			DependencyProperty.Register(nameof(Text),		typeof(string), 
										typeof(BigButton),	new PropertyMetadata(null));

		public ImageSource Source
		{
			get { return (ImageSource)GetValue(SourceProperty); }
			set { SetValue(SourceProperty, value); }
		}

		public string Text
		{
			get { return (string)GetValue(TextProperty); }
			set { SetValue(TextProperty, value); }
		}
	}
}
