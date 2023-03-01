using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GammaHealController
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
			=> Current.TryFindResource(resourceName) as T;
	}
}
