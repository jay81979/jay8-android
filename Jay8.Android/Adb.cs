using System;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;
using Jay8.Android.Utils;

namespace Jay8.Android
{
	public class Adb
	{
		public Adb (string sdkLocation)
		{
			_sdkLocation = sdkLocation;
			_adbLocation = EnvironmentUtils.GetAdbLocation (_sdkLocation);
			_fastbootLocation = EnvironmentUtils.GetFastbootLocation (_sdkLocation);
			_connectedDevices = new List<string>();
			KillServer ();
			StartServer ();
			GetDevices ();
			Console.WriteLine ("No of devices " + _connectedDevices.Count);
			foreach (string device in _connectedDevices) 
			{
				Console.WriteLine (device);
			}
		}

		private string _sdkLocation;
		private string _adbLocation;
		private string _fastbootLocation;
		private List<string> _connectedDevices;

		private void StartServer()
		{
			new AndroidCommand (_adbLocation, "start-server").ExecuteNoReturn();
		}

		private void KillServer()
		{
			new AndroidCommand (_adbLocation, "kill-server").ExecuteNoReturn();
		}

		private string CheckDevices()
		{
			AndroidCommand devicesCommand = new AndroidCommand (_adbLocation, "devices");
			string result = devicesCommand.Execute ();
			return result;
		}

		private void GetDevices()
		{
			UpdateDeviceList ();
			foreach (var deviceSerial in _connectedDevices) 
			{
			}
		}

		private void UpdateDeviceList()
		{
			string deviceList = "";

			this._connectedDevices.Clear();

			deviceList = CheckDevices ();
			if (deviceList.Length > 29)
			{
				using (StringReader s = new StringReader(deviceList))
				{
					string line;

					while (s.Peek() != -1)
					{
						line = s.ReadLine();

						if (line.StartsWith("List") || line.StartsWith("\r\n") || line.Trim() == "")
							continue;

						if (line.IndexOf('\t') != -1)
						{
							line = line.Substring(0, line.IndexOf('\t'));
							this._connectedDevices.Add(line);
						}
					}
				}
			}

			AndroidCommand fastBootDevices = new AndroidCommand (_fastbootLocation, "devices");
			deviceList = fastBootDevices.Execute ();
			if (deviceList.Length > 0)
			{
				using (StringReader s = new StringReader(deviceList))
				{
					string line;

					while (s.Peek() != -1)
					{
						line = s.ReadLine();

						if (line.StartsWith("List") || line.StartsWith("\r\n") || line.Trim() == "")
							continue;

						if (line.IndexOf('\t') != -1)
						{
							line = line.Substring(0, line.IndexOf('\t'));
							this._connectedDevices.Add(line);
						}
					}
				}
			}
		}

	}
}

