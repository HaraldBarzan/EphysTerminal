using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TINS.Ephys.Display
{
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

		/// <summary>
		/// Orientation dependency property.
		/// </summary>
		//public static readonly DependencyProperty OrientationProperty = 
		//	DependencyProperty.Register(nameof(Orientation),	typeof(Orientation), 
		//								typeof(BigButton),		new PropertyMetadata(null));

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

		//public Orientation Orientation
		//{
		//	get { return (Orientation)GetValue(OrientationProperty); }
		//	set { SetValue(OrientationProperty, value); }
		//}
	}
}
