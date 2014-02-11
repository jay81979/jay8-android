using System;
using Internals;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

namespace Jay8.Android
{
	public class FingerprintHelper: OutputHandler
	{
		public FingerprintHelper(string keytool, string apk)
			:base()
		{
			_keytool = keytool;
			_apk = apk;
		}

		private string _apk;
		private string _keytool;

		public string FingerprintFromApk()
		{
			string fingerprint = "";

			if(File.Exists(_apk))
			{
				using (var unzip = new Unzip (_apk)) 
				{
					foreach (var entry in unzip.Entries) 
					{
						if (entry.Name.ToLower().EndsWith (".rsa")) 
						{
							unzip.Extract (entry.Name, "temp.rsa");
						}
					}
				}
				String output;
				using (Process p = new Process ()) 
				{
					p.StartInfo.FileName = _keytool;
					p.StartInfo.Arguments = " -v -printcert -file ./temp.rsa";
					p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					p.StartInfo.CreateNoWindow = true;
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.RedirectStandardOutput = true;
					p.StartInfo.RedirectStandardError = true;

					using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
					using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) {
						output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
						fingerprint = ExtractMd5Fingerprint (output);
						File.Delete ("./temp.rsa");
					
					}
				}
			}
			return fingerprint;
		}
	}
}

