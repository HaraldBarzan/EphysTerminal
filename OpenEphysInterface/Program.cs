using OpalKelly.FrontPanel;

class Program
{
	static void Main(string[] args)
	{
		var dev = new okCFrontPanel();
		int nDevices = dev.GetDeviceCount();

		Console.WriteLine("Found " + nDevices + " Opal Kelly device" + ((nDevices == 1) ? "" : "s") + " connected:");
		for (int i = 0; i < nDevices; i++)
			Console.WriteLine("  Device #" + (i + 1) + ": Opal Kelly " + dev.GetDeviceListModel(i) + " with serial number " + dev.GetDeviceListSerial(i));
	}
}