using System;
using System.Diagnostics;
using System.Threading;

namespace Jay8.Android
{
	public class ApkInstallHelper: OutputHandler
	{
		public ApkInstallHelper (string adbLocation)
			:base()
		{
			_adbLocation = adbLocation;
		}

		private string _adbLocation;

		public void InstallApk(string apkLocation, string deviceName)
		{
			string output;
			using (Process p = new Process ()) 
			{
				p.StartInfo.FileName = _adbLocation;
				p.StartInfo.Arguments = string.Format("-s {0} install {1}", deviceName, apkLocation);
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;


				using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) {
					output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
					Console.WriteLine (output);
				}
			}
		}

	}
}

