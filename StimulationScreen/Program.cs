using CommandLine;
using StimulationScreen;

/// <summary>
/// The main class of the program.
/// </summary>
static class Program
{
	/// <summary>
	/// Options for the stimulation screen.
	/// </summary>
	class Options
	{
		/// <summary>
		/// The zero-based index of the monitor on which stimulation will run.
		/// </summary>
		[Option('m', "monitorno", Required = false, Default = 0, HelpText = "The zero-based index of the monitor on which stimulation will run.")]
		public int MonitorIndex { get; set; }

		/// <summary>
		/// The handle for the IPC pipe which links the screen to the launcher.
		/// </summary>
		[Option('h', "pipehandle", Required = false, HelpText = "The handle for the IPC pipe which links the screen to the launcher.")]
		public string? PipeHandle { get; set; }
	}


	/// <summary>
	/// The main function.
	/// </summary>
	/// <param name="args">Command line arguments.</param>
	public static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			// DEBUG
			new SkiaScreen().ShowDialog();

		}
		else
		{
			// parse the options from the command line
			var opts = default(Options);
			Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
			{
				bool ok = true;
				if (!SkiaScreen.CheckValidMonitorIndex(o.MonitorIndex, out var dm))
				{
					Console.WriteLine($"Monitor index {o.MonitorIndex} is invalid (DXGI detects {dm} monitors).");
					ok = false;
				}

				if (ok)
					opts = o;
			});
		}
	}
}