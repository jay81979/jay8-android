using System;
using Jay8.Android;

namespace TestApp
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			string jdk = "/Library/Java/Home";
			string sdk = "/Applications/adt-bundle-mac-x86_64-20130911/sdk";
			string apk = "/Users/jay/Development/android/Test/bin/Test.apk";
			AndroidTestRunner testRunner = new AndroidTestRunner (jdk, sdk, apk);
		}
	
	}
}
