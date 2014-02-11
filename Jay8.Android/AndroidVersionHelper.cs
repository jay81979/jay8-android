using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Jay8.Android
{
	public class AndroidVersionHelper
	{
		public AndroidVersionHelper (string sdkLocation)
		{
			_platformsLocation = string.Format ("{0}/{1}", sdkLocation, "platforms");
			_buildToolsLocation = string.Format("{0}/{1}", sdkLocation, "build-tools");
			if (Directory.Exists (sdkLocation) && Directory.Exists (_platformsLocation) && Directory.Exists(_buildToolsLocation)) {
				FindPlatformVersions ();
				FindBuildToolsVersions ();
			} 
			else 
			{
				throw new DirectoryNotFoundException ( "The Android SDK was not found or the file structure is corrupted!" );

			}
		}

		private string _platformsLocation;
		private string _buildToolsLocation;
		private List<int> _platformVersions;
		private List<BuildToolsVersion> _buildToolsVersions;

		private void FindPlatformVersions()
		{
			_platformVersions = new List<int> ();
			string[] versions = Directory.GetDirectories (_platformsLocation);
			foreach (var version in versions) 
			{
				var newVersion = version.Replace (_platformsLocation, "");
				newVersion = newVersion.Replace ("/android-", "");
				_platformVersions.Add (int.Parse(newVersion));
			}
		} 

		private void FindBuildToolsVersions()
		{
			_buildToolsVersions = new List<BuildToolsVersion> ();
			string[] versions = Directory.GetDirectories (_buildToolsLocation);
			foreach (var version in versions) 
			{
				var newVersion = version.Replace (_buildToolsLocation, "");
				newVersion = newVersion.Replace ("/", "");

				string pattern = "^\\d*\\.\\d*.\\d*$";
				Regex regEx = new Regex (pattern);
				Match match = regEx.Match (newVersion);
				if (match.Length > 0) 
				{
					string[] split = Regex.Split (newVersion, "\\.");
					if (split.Length == 3) 
					{
						_buildToolsVersions.Add(new BuildToolsVersion(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), version));
					}
				} 
			}
		} 

		public string GetHighestPlatformLocation()
		{
			_platformVersions.Sort ();
			return string.Format ("{0}{1}{2}", _platformsLocation, "/android-", _platformVersions[_platformVersions.Count - 1]);
		}

		public BuildToolsVersion GetHighestBuildToolsVersion()
		{
			BuildToolsVersion highestBuildToolVersion = _buildToolsVersions[0];
			foreach (var version in _buildToolsVersions) 
			{
				if (version.MajorVersion == highestBuildToolVersion.MajorVersion) 
				{
					if (version.MinorVersion == highestBuildToolVersion.MinorVersion) 
					{
						if (version.PatchVersion > highestBuildToolVersion.PatchVersion) 
						{
							highestBuildToolVersion = version;
						}
					} 
					else if (version.MinorVersion > highestBuildToolVersion.MinorVersion) 
					{
						highestBuildToolVersion = version;
					}
				}
				else if (version.MajorVersion > highestBuildToolVersion.MajorVersion) 
				{
					highestBuildToolVersion = version;
				}
			}
			return highestBuildToolVersion;
		}
	}

	public class BuildToolsVersion
	{
		public BuildToolsVersion(int majorVersion, int minorVersion, int patchVersion, string location)
		{
			_majorVersion = majorVersion;
			_minorVersion = minorVersion;
			_patchVersion = patchVersion;
			_location = location;
		}


		private int _majorVersion;
		private int _minorVersion;
		private int _patchVersion;
		private string _location;

		public int MajorVersion { get { return _majorVersion; } } 
		public int MinorVersion { get { return _minorVersion; } } 
		public int PatchVersion { get { return _patchVersion; } } 
		public string Location { get { return _location; } } 
	}
}

