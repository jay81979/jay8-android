using System;

namespace Jay8.Android
{
	public class Environment
	{
		public static string GetHome()
		{

			return (System.Environment.OSVersion.Platform == PlatformID.Unix || 
				System.Environment.OSVersion.Platform == PlatformID.MacOSX)
			    ? System.Environment.GetEnvironmentVariable("HOME")
			    : System.Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
		}

		public static string GetJavaExe(string jdkLocation)
		{
			string javaExe = string.Format ("{0}/{1}/{2}", jdkLocation, "bin", "java");
			return (System.Environment.OSVersion.Platform == PlatformID.Unix || 
				System.Environment.OSVersion.Platform == PlatformID.MacOSX)
				? javaExe : string.Format("{0}{1}", javaExe, ".exe");
		}

	}
}

