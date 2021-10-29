using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CircuitGENUS.Windows
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// Attempt to get a resource by type and name.
		/// </summary>
		/// <typeparam name="T">The type of the resource.</typeparam>
		/// <param name="resourceName">The name of the resource.</param>
		/// <returns>A resource object, if found, null otherwise.</returns>
		public static T GetResource<T>(string resourceName) where T : class
			=> Application.Current.TryFindResource(resourceName) as T;

		/// <summary>
		/// Show a non-blocking message box.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="title"></param>
		/// <param name="owner"></param>
		/// <param name="buttons"></param>
		/// <param name="image"></param>
		public static Task<MessageBoxResult> MessageBoxAsync(
			string				text,
			string				title,
			MessageBoxButton	buttons = MessageBoxButton.OK,
			MessageBoxImage		image = MessageBoxImage.Information)
		{
			// define the result
			var result = new Task<MessageBoxResult>(() => MessageBox.Show(text, title, buttons, image));
			
			// run the thread
			var t = new Thread(() => result.RunSynchronously());
			t.SetApartmentState(ApartmentState.STA);
			t.Start();

			// return the await handle
			return result;
		}
	}
}
