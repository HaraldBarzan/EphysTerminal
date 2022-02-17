using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TINS.Ephys.Display
{
	/// <summary>
	/// 
	/// </summary>
	public enum ValidationResult
	{
		Unvalidated,
		True,
		False
	}

	/// <summary>
	/// 
	/// </summary>
	public class TextBoxValidation : TextBox
	{

		public static readonly DependencyProperty ValidationStateProperty =
			DependencyProperty.Register(nameof(ValidationState), typeof(ValidationResult),
					typeof(TextBox), new PropertyMetadata(ValidationResult.Unvalidated));
		public static readonly DependencyProperty ValidationFunctionProperty =
			DependencyProperty.Register(nameof(ValidationFunction), typeof(Func<ValidationResult>),
					typeof(TextBox), new PropertyMetadata(null));

		/// <summary>
		/// 
		/// </summary>
		public ValidationResult ValidationState
		{
			get => (ValidationResult)GetValue(ValidationStateProperty);
			set => SetValue(ValidationStateProperty, value);
		}

		/// <summary>
		/// 
		/// </summary>
		public Func<ValidationResult> ValidationFunction
		{
			get => (Func<ValidationResult>)GetValue(ValidationFunctionProperty);
			set => SetValue(ValidationFunctionProperty, value);
		}

		/// <summary>
		/// 
		/// </summary>
		public virtual ValidationResult ValidateInput()
		{
			ValidationState = ValidationFunction is object
				? ValidationFunction()
				: ValidationResult.Unvalidated;
			return ValidationState;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		protected override void OnLostFocus(RoutedEventArgs e)
		{
			ValidateInput();
			base.OnLostFocus(e);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
				ValidateInput();
			base.OnKeyUp(e);
		}
	}
}
