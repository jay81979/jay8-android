using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Jay8.Android
{
	public class KeystoreHelper
	{
		public KeystoreHelper(string keytool)
		{
			_keytool = keytool;
		}

		private string _keytool;
		private List<Keystore> _keystores;

		public List<Keystore> GetKeystores()
		{
			_keystores = new List<Keystore> ();
			ReadKeystore(string.Format("{0}{1}", Environment.GetHome(),"/.android/debug.keystore"));
			//read_keystore_with_default_password_and_alias(File.join(ENV["HOME"], "/.android/debug.keystore")),
			//read_keystore_with_default_password_and_alias("debug.keystore"),
			//read_keystore_with_default_password_and_alias(File.join(ENV["HOME"], ".local/share/Xamarin/Mono\ for\ Android/debug.keystore")),
			//read_keystore_with_default_password_and_alias(File.join(ENV["HOME"], "AppData/Local/Xamarin/Mono for Android/debug.keystore")),
			return _keystores;
		}

		private void ReadKeystore(string fileName)
		{
			if (File.Exists (fileName)) 
			{
				_keystores.Add(new Keystore (_keytool, fileName, "androiddebugkey", "android"));
			}
		}
	}

	public class Keystore:OutputHandler
	{
		public Keystore(string keytool, string location, string keystoreAlias, string password)
			:base()
		{
			_keytool = keytool;
			_location = location;
			_keystoreAlias = keystoreAlias;
			_password = password;
			GetData();
			//string data = system_with_stdout_on_success(Env.keytool_path, '-list', '-v', '-alias', keystore_alias, '-keystore', location, '-storepass', password)
		}

		private string _keytool;
		private string _location;
		private string _keystoreAlias;
		private string _password;
		private string _fingerprint;

		public string Location
		{
			get 
			{
				return _location;
			}
		}

		public string KeystoreAlias
		{
			get 
			{
				return _keystoreAlias;
			}
		}

		public string Password
		{
			get 
			{
				return _password;
			}
		}

		public string Fingerprint
		{
			get 
			{
				return _fingerprint;
			}
		}

		private void GetData()
		{
			string data;
			using (Process p = new Process())
			{
				p.StartInfo.FileName = _keytool;
				p.StartInfo.Arguments = string.Format(" -list -v -alias {0} -keystore {1} -storepass {2}", _keystoreAlias, _location, _password);
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;

				using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) 
				{
					data = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
					_fingerprint = ExtractMd5Fingerprint (data);
				}
			}
		}
	}
}

