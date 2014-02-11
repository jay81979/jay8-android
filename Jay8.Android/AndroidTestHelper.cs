using System;
using Managed.Adb;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Net;
using System.IO;
using System.Runtime;

namespace Jay8.Android
{
	public class AndroidTestHelper:OutputHandler
	{
		public AndroidTestHelper (string adbLocation)
			:base()
		{
			_adbLocation = adbLocation;
		}

		private const string SHELL_ACTIVITY_MANAGER = "shell am";
		private const string SHELL_INSTRUMENT = SHELL_ACTIVITY_MANAGER + " instrument";
		private const string SHELL_START_ACTIVITY = SHELL_ACTIVITY_MANAGER + " start -a android.intent.action.MAIN -n";

		private const string TEST_SERVER_PACKAGE = "com.example.test.test";
		private const string TEST_SERVER_ACTIVITY_PACKAGE = "sh.calaba.instrumentationbackend";
		private const string TEST_SERVER_START_ACTIVITY = TEST_SERVER_PACKAGE + "/" + TEST_SERVER_ACTIVITY_PACKAGE + ".CalabashInstrumentationTestRunner";
		private const string TEST_SERVER_WAKEUP = TEST_SERVER_PACKAGE + "/" + TEST_SERVER_ACTIVITY_PACKAGE + ".WakeUp";

		private string _adbLocation;

		public bool StartServer(IDevice device)
		{
			bool started = false;
			string args = "-e target_package com.example.test ";
			args += "-e main_activity com.example.test.MainActivity ";
			args += "-e test_server_port 7102 ";
			args += "-e debug false ";
			args += "-e class sh.calaba.instrumentationbackend.InstrumentationBackend";
			string output;
			using (Process p = new Process ()) {
				p.StartInfo.FileName = _adbLocation;
				p.StartInfo.Arguments = SHELL_INSTRUMENT + " " + args + " " + TEST_SERVER_START_ACTIVITY;
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;


				using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) {
					output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
					Console.WriteLine (output);

					started = SetupPorts ();
				}
			}
			return started;
		}

		private bool SetupPorts()
		{
			bool started = false;
			string output;
			using (Process p = new Process ()) {
				p.StartInfo.FileName = _adbLocation;
				p.StartInfo.Arguments = string.Format ("forward tcp:{0} tcp:{1}", "34777", "7102");
				p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;


				using (AutoResetEvent outputWaitHandle = new AutoResetEvent (false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent (false)) {
					output = HandleOutput (p, outputWaitHandle, errorWaitHandle, -1, false);
					Console.WriteLine (output);
					started = CheckStarted ();
				}
			}
			return started;
		}

		private bool CheckStarted()
		{
			for (int i = 0; i < 10; i++) 
			{
				if(PerformAction ("ping").Equals("pong")) 
				{
					Console.WriteLine ("Server started");
					return true;
				}
				Thread.Sleep (3000);
			}
			return false;
		}

		public string PerformAction(string method)
		{
			return PerformAction (method, "{}");
		}

		public string PerformAction(string method, string data)
		{
			var url = "http://127.0.0.1:34777";
			var bytes = Encoding.ASCII.GetBytes(data);
			var response = "";
			var client = HttpWebRequest.Create (string.Format ("{0}/{1}", url, method));
			client.ContentType = "application/json;charset=utf-8";
			client.Method = "POST";
			Stream stream= null;
			try
			{
				stream = client.GetRequestStream();
				stream.Write(bytes, 0, bytes.Length);
				stream.Close();

				// get the response
				try
				{
					HttpWebResponse resp = (HttpWebResponse) client.GetResponse();
					if (resp != null) 
					{
						// expected response is a 200 
						if ((int)(resp.StatusCode) != 200)
							throw new Exception(String.Format("unexpected status code ({0})", resp.StatusCode));
						for(int i=0; i < resp.Headers.Count; ++i)  
							;  //whatever

						var MyStreamReader = new System.IO.StreamReader(resp.GetResponseStream());
						string fullResponse = MyStreamReader.ReadToEnd().Trim();
						response = fullResponse;
						Console.WriteLine(fullResponse);
					}
				}
				catch (Exception ex1)
				{
					Console.WriteLine(ex1.Message);
				}
			}    
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
			return response;
		}
	}
}

