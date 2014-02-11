using System;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;

namespace Jay8.Android
{
	public abstract class OutputHandler
	{
		protected string HandleOutput(Process p, AutoResetEvent outputWaitHandle, AutoResetEvent errorWaitHandle, int timeout, bool forceRegular)
		{
			StringBuilder output = new StringBuilder();
			StringBuilder error = new StringBuilder();

			p.OutputDataReceived += (sender, e) =>
			{
				if (e.Data == null)
					outputWaitHandle.Set();
				else
					output.AppendLine(e.Data);
			};
			p.ErrorDataReceived += (sender, e) =>
			{
				if (e.Data == null)
					errorWaitHandle.Set();
				else
					error.AppendLine(e.Data);
			};

			p.Start();

			p.BeginOutputReadLine();
			p.BeginErrorReadLine();

			if (p.WaitForExit(timeout) && outputWaitHandle.WaitOne(timeout) && errorWaitHandle.WaitOne(timeout))
			{
				string strReturn = "";

				if (error.ToString().Trim().Length.Equals(0) || forceRegular)
					strReturn = output.ToString().Trim();
				else
					strReturn = error.ToString().Trim();

				return strReturn;
			}
			else
			{
				// Timed out.
				return "PROCESS TIMEOUT";
			}
		}

		protected string ExtractMd5Fingerprint(String output)
		{
			string pattern = "MD5:\\s*([a-fA-F\\d]{2}:){15}[a-fA-F\\d]{2}";
			Regex regEx = new Regex (pattern);
			Match match = regEx.Match (output);
			return match.Value;
		}
	}
}

