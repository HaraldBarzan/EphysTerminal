using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TINS.Terminal.Display
{
	public enum NumericType
	{
		Integer,
		Real
	}

	/// <summary>
	/// Text box which only accepts numeric types.
	/// </summary>
	public class NumericTextBox : TextBox
	{
		public NumericTextBox()
			: base()
			=> DataObject.AddPastingHandler(this, OnPaste);

		public static readonly DependencyProperty NumericTypeProperty
			= DependencyProperty.Register(nameof(NumericType), typeof(NumericType),
				typeof(NumericTextBox), new PropertyMetadata(NumericType.Real));

		/// <summary>
		/// Get or set the supported numeric type.
		/// </summary>
		public NumericType NumericType
		{
			get => (NumericType)GetValue(NumericTypeProperty);
			set => SetValue(NumericTypeProperty, value);
		}

		/// <summary>
		/// Get the integer value.
		/// </summary>
		public int IntegerValue
		{
			get
			{
				if (int.TryParse(Text, out var number) && NumericType == NumericType.Integer)
					return number;
				throw new Exception("Real values not supported.");
			}
		}

		/// <summary>
		/// Get the real value.
		/// </summary>
		public double RealValue
		{
			get
			{
				if (double.TryParse(Text, out var number))
					return number;
				return 0;
			}
		}

		/// <summary>
		/// Use to filter for numerical values.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			int key = (int)e.Key;
			e.Handled = !(	Numerics.IsClamped(key, ((int)Key.D0,		(int)Key.D9))		||	// key is on alphanumeric keyboard
							Numerics.IsClamped(key, ((int)Key.NumPad0,	(int)Key.NumPad9))	||	// key is on num pad
							e.Key == Key.Back												||	// key is backspace
							(NumericType == NumericType.Real && (e.Key == Key.OemPeriod)));		// key is period if floating point                                   
			base.OnPreviewKeyDown(e);
		} 

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void OnPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
			{
				e.CancelCommand();
				return;
			}

			var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
			for (int i = 0; i < text.Length; ++i)
			{
				if (!char.IsDigit(text[i]) || (text[i] == '.' && NumericType == NumericType.Real))
					e.CancelCommand();
			}
		}
	}
}
