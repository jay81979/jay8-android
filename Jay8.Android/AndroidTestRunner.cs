using System;
using System.IO;
using Managed.Adb;
using System.Collections.Generic;
using Internals;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.Timers;

namespace Jay8.Android
{
	public class AndroidTestRunner:OutputHandler
	{
		public AndroidTestRunner (string jdkLocation, string sdkLocation, string apkLocation)
			:base()
		{

			_apkLocation = apkLocation;
			_jdkLocation = jdkLocation;
			_adbLocation = string.Format ("{0}{1}", sdkLocation, "//platform-tools//adb");

			_keyTool = string.Format ("{0}{1}", _jdkLocation, "//bin//keytool");
			_jarSigner = string.Format ("{0}{1}", _jdkLocation, "//bin//jarsigner");
			_adb = AndroidDebugBridge.CreateBridge (_adbLocation, true);
			_devices = new List<IDevice> ();
			_androidVersionHelper = new AndroidVersionHelper (sdkLocation);
			_fingerprintHelper = new FingerprintHelper (_keyTool, _apkLocation);
			_keystoreHelper = new KeystoreHelper (_keyTool);
			_apkInstallHelper = new ApkInstallHelper (_adbLocation);
			_testHelper = new AndroidTestHelper (_adbLocation);
			BuildToolsVersion buildToolsVersion = _androidVersionHelper.GetHighestBuildToolsVersion ();
			_aaptLocation = string.Format ("{0}/{1}", buildToolsVersion.Location, "aapt");
			_keystores = _keystoreHelper.GetKeystores ();
			_apkFingerprint = _fingerprintHelper.FingerprintFromApk ();
			_adb.DeviceConnected += OnDeviceConnected;
			_adb.DeviceChanged += OnDeviceChanged;
			_adb.DeviceDisconnected += OnDeviceDisconnected;
			_adb.Start ();
			Console.WriteLine ("Checking apk signing...");
			if (!CheckFingerprint ()) 
			{
				Console.WriteLine ("No sign match, re-sign needed");
				ResignApk ();
			} 
			else 
			{
				Console.WriteLine ("Sign match, no need to re-sign");
			}
			GetPackageName ();
			Console.WriteLine ("Preparing Test Server");
			PrepareTestServer ();
			CheckDevicesForInstall ();
		}

		private const string APK_TOOL_JAR = "Resources/apktool-cli-1.5.3-SNAPSHOT.jar"; 

		private AndroidDebugBridge _adb;
		private string _jdkLocation;
		private string _adbLocation;
		private string _apkLocation;
		private string _testServerApkLocation;
		private string _aaptLocation;
		private string _keyTool;
		private string _jarSigner;
		private string _apkFingerprint;
		private FingerprintHelper _fingerprintHelper;
		private KeystoreHelper _keystoreHelper;
		private AndroidVersionHelper _androidVersionHelper;
		private ApkInstallHelper _apkInstallHelper;
		private AndroidTestHelper _testHelper;
		private List<Keystore> _keystores;
		private Keystore _appKeystore;
		private string _packageName;
		private List<IDevice> _devices;
		private System.Timers.Timer _deviceConnectTimer;
		private int _deviceConnectionCounter;


		private void OnBridgeChanged(object sender, AndroidDebugBridgeEventArgs e)
		{

		}

		private void OnDeviceConnected(object sender, DeviceEventArgs e)
		{
			_devices.Add (e.Device);
		}

		private void OnDeviceChanged(object sender, DeviceEventArgs e)
		{

		}

		private void OnDeviceDisconnected(object sender, DeviceEventArgs e)
		{
			_devices.Remove (e.Device);
		}

		private bool CheckFingerprint()
		{
			foreach (var keystore in _keystores) 
			{
				if (keystore.Fingerprint.Equals (_apkFingerprint)) 
				{
					_appKeystore = keystore;
					return true;
				}
			}
			return false;
		}

		private void GetPackageName ()
		{
			Console.WriteLine ("\nExtracting Test APK Package Name...");
			string apkToolDirectory = "apkTool";
			Directory.CreateDirectory (apkToolDirectory);
			apkToolDirectory = string.Format ("{0}/{1}", Directory.GetCurrentDirectory(), apkToolDirectory);
			string jarLocation = string.Format ("{0}/{1}", Directory.GetCurrentDirectory(), APK_TOOL_JAR);
			string manifestLocation = string.Format ("{0}/{1}", apkToolDirectory, "AndroidManifest.xml");
			string output;
			using (Process p = new Process ()) 
			{
				p.StartInfo.FileName = Environment.GetJavaExe(_jdkLocation);
				p.StartInfo.Arguments = string.Format(" -jar {0} d -s -f {1} {2}", jarLocation, _apkLocation, apkToolDirectory);
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;


				using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) {
					output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
					Console.WriteLine (output);
					ExtractPackageName (manifestLocation);
				}
			}
		}

		private void ExtractPackageName(string manifestLocation)
		{
			using (XmlTextReader reader = new XmlTextReader (manifestLocation)) 	
			{
				while (reader.Read()) 
				{
					if(reader.NodeType == XmlNodeType.Element)
					{
						if (reader.Name.Equals ("manifest")) 
						{
							while (reader.MoveToNextAttribute ()) 
							{
								if (reader.Name.Equals ("package")) 
								{
									_packageName = reader.Value;
								}
							}
						}
					}
				}
			}
		}

		private void ResignApk()
		{
			string unpackDirectory = "temp";
			Directory.CreateDirectory (unpackDirectory);

			using (var unzip = new Unzip (_apkLocation)) {
				foreach (var entry in unzip.Entries) {
					if (!entry.Name.StartsWith ("META-INF/")) {
						string[] nameSplit = Regex.Split (entry.Name, "/");
						StringBuilder sb = new StringBuilder ();
						sb.Append (string.Format("{0}/", unpackDirectory));
						for (var i = 0; i < nameSplit.Length - 1; i++) {
							sb.Append (string.Format ("{0}/", nameSplit [i]));
							if (!Directory.Exists (sb.ToString ())) {
								Directory.CreateDirectory (sb.ToString ());
							}

						}
						unzip.Extract (entry.Name, string.Format ("{0}/{1}", unpackDirectory, entry.Name));
					}

				}
			}
			_apkLocation = PackageAndSignApk ("TestApk.apk", unpackDirectory);
		}

		private string PackageAndSignApk(string apkName, string folder) 
		{
			FileStream fileStreamOut = File.Create(apkName);
			string apkLocation = string.Format("{0}/{1}", Directory.GetCurrentDirectory (), apkName);
			ZipOutputStream zipStream = new ZipOutputStream(fileStreamOut);
			int folderOffset = folder.Length + (folder.EndsWith("\\") ? 0 : 1);

			CompressFolder(folder, zipStream, folderOffset);
			zipStream.IsStreamOwner = true;
			zipStream.Close();
			string output;
			using (Process p = new Process())
			{
				p.StartInfo.FileName = _jarSigner;
				p.StartInfo.Arguments = string.Format("-sigalg MD5withRSA -digestalg SHA1 -storepass {0} -keystore {1} {2} {3}", _appKeystore.Password, _appKeystore.Location, apkName, _appKeystore.KeystoreAlias);
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;

				using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) 
				{
					output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
					Console.WriteLine (output);
				}
			}
			return apkLocation;
		}

		private void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset) {

			string[] files = Directory.GetFiles(path);

			foreach (string filename in files) {

				FileInfo fi = new FileInfo(filename);

				string entryName = filename.Substring(folderOffset); // Makes the name in zip based on the folder
				entryName = ZipEntry.CleanName(entryName); // Removes drive from name and fixes slash direction
				ZipEntry newEntry = new ZipEntry(entryName);
				newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity

				newEntry.Size = fi.Length;

				zipStream.PutNextEntry(newEntry);

				byte[ ] buffer = new byte[4096];
				using (FileStream streamReader = File.OpenRead(filename)) {
					StreamUtils.Copy(streamReader, zipStream, buffer);
				}
				zipStream.CloseEntry();
			}
			string[ ] folders = Directory.GetDirectories(path);
			foreach (string folder in folders) {
				CompressFolder(folder, zipStream, folderOffset);
			}
		}

		private void PrepareTestServer()
		{
			string manifestLocation = string.Format("{0}{1}",Directory.GetCurrentDirectory (), "/Resources/AndroidManifest.xml");
			string testServerLocation = string.Format ("{0}{1}", Directory.GetCurrentDirectory(), "/testServer");
			string tempManifestLocation = string.Format ("{0}{1}", testServerLocation, "/AndroidManifest.xml");
			string dummyApk = string.Format("{0}/{1}",Directory.GetCurrentDirectory (),"dummy.apk");
			Directory.CreateDirectory (testServerLocation);

			if (File.Exists (manifestLocation)) 
			{
				string output;
				string fileContent = File.ReadAllText(manifestLocation);
				fileContent = fileContent.Replace ("#targetPackage#", _packageName);
				fileContent = fileContent.Replace ("#testPackage#", string.Format("{0}{1}",_packageName, ".test"));
				using (StreamWriter file = new StreamWriter(tempManifestLocation, true))
				{
					file.Write(fileContent);
				}
				using (Process p = new Process ()) 
				{
					p.StartInfo.FileName = _aaptLocation;
					p.StartInfo.Arguments = string.Format("package -M {0} -I {1}/{2} -F {3}", tempManifestLocation, _androidVersionHelper.GetHighestPlatformLocation(), "android.jar", dummyApk);
					p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					p.StartInfo.CreateNoWindow = true;
					p.StartInfo.UseShellExecute = false;
					p.StartInfo.RedirectStandardOutput = true;
					p.StartInfo.RedirectStandardError = true;

					using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
					using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) 
					{
						output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
						Console.WriteLine (output);
						using (var unzip = new Unzip (dummyApk)) 
						{
							unzip.Extract ("AndroidManifest.xml", tempManifestLocation);
						}
						RepackageTestServer ();
					}
				}
			}
		}

		private void RepackageTestServer() 
		{
			string testServer = string.Format("{0}{1}",Directory.GetCurrentDirectory (), "/Resources/TestServer.apk");
			string testServerLocation = string.Format ("{0}{1}", Directory.GetCurrentDirectory(), "/testServer");
			string tempManifestLocation = string.Format ("{0}{1}", testServerLocation, "/AndroidManifest.xml");

			if (File.Exists (testServer) && File.Exists(tempManifestLocation)) 
			{
				using (var unzip = new Unzip (testServer)) 
				{
					foreach (var entry in unzip.Entries) 
					{
						if (!entry.Name.Equals("AndroidManifest.xml")) 
						{
							string[] nameSplit = Regex.Split (entry.Name, "/");
							StringBuilder sb = new StringBuilder ();
							sb.Append ("testServer/");
							for (var i = 0; i < nameSplit.Length - 1; i++) 
							{
								sb.Append (string.Format("{0}/", nameSplit [i]));
								if(!Directory.Exists(sb.ToString()))
								{
									Directory.CreateDirectory (sb.ToString ());
								}

							}
							unzip.Extract (entry.Name, string.Format("{0}/{1}", testServerLocation, entry.Name));
						}
					}
				}
			}
			_testServerApkLocation = PackageAndSignApk ("TestServer.apk", testServerLocation);
		}

		private void CheckDevicesForInstall() 
		{
			if (_devices.Count == 0) 
			{
				Console.WriteLine ("No Devices connected. Waiting...");
				_deviceConnectTimer = new System.Timers.Timer (60000);
				_deviceConnectionCounter = 60;
				Console.WriteLine (string.Format("Timeout in {0} seconds", _deviceConnectionCounter));
				_deviceConnectTimer.Elapsed += new ElapsedEventHandler (OnTimedEvent);
				_deviceConnectTimer.Interval = 1000;
				_deviceConnectTimer.Enabled = true;

			} 
			else 
			{
				InstallApks ();
			}
		}

		public static void ClearLine()
		{
			Console.SetCursorPosition(0, Console.CursorTop - 1);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, Console.CursorTop - 1);
		}

		private void OnTimedEvent(object source, ElapsedEventArgs e)
		{
			_deviceConnectionCounter--;
			ClearLine ();
			Console.WriteLine (string.Format("Timeout in {0} seconds", _deviceConnectionCounter));
			if (_devices.Count > 0) 
			{
				Console.WriteLine ("Device detected, starting install...");
				InstallApks ();
			}
			else if (_deviceConnectionCounter == 0) 
			{
				_deviceConnectTimer.Stop ();
				ClearLine ();
				Console.WriteLine ("Device connection timeout. Check devices and start again.");
			}

		}

		private void InstallApks ()
		{
			foreach (var device in _devices) 
			{
				InstallTestApk (device);
				InstallTestServerApk (device);
				StartTests (device);
			}
		}

		private void InstallTestApk(IDevice device)
		{
			Console.WriteLine(string.Format("Installing Test APK on device {0}", device.SerialNumber));
			_apkInstallHelper.InstallApk (_apkLocation, device.SerialNumber);
		}

		private void InstallTestServerApk(IDevice device)
		{
			Console.WriteLine(string.Format("Installing Test Server APK on device {0}", device.SerialNumber));
			_apkInstallHelper.InstallApk (_testServerApkLocation, device.SerialNumber);
		}


		private void StartTests(IDevice device)
		{
			Console.WriteLine ("Waking up " + device.SerialNumber);
			_testHelper.StartServer (device);
			//PerformAction ("ping");
		}

		public void PerformAction(string method)
		{
			_testHelper.PerformAction (method);
		}
		/*
		log "Action: #{action} - Params: #{arguments.join(', ')}"

		params = {"command" => action, "arguments" => arguments}

		Timeout.timeout(300) do
			begin
			result = http("/", params, {:read_timeout => 350})
			       rescue Exception => e
			       log "Error communicating with test server: #{e}"
			       raise e
			       end
			       log "Result:'" + result.strip + "'"
			       raise "Empty result from TestServer" if result.chomp.empty?
				       result = JSON.parse(result)
			       if not result["success"] then
				       raise "Action '#{action}' unsuccessful: #{result["message"]}"
				       end
				       result
				       end
*/
	
	}
}