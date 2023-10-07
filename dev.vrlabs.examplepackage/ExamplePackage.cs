using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VRLabs
{
	public class MyFancyPackage : ScriptableObject
	{
		public const string packageName = "ExamplePackage";
		
		public static BindingFlags ALL = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
		                          BindingFlags.NonPublic;

		public static string[] excludeRegexs =
		{
			".*\\.cs",
			".*\\.asmdef",
			"package.json"
		};

		[MenuItem("VRLabs/FunkyThing")]
		public static void FancyPackage()
		{
			Type instancerType = AppDomain.CurrentDomain.GetAssemblies()
				.Where(x => x.GetType("VRLabs.Instancer") != null)
				.Select(x => x.GetType("VRLabs.Instancer")).FirstOrDefault();

			if (instancerType == null)
			{
				Debug.LogError("Instancer not found. This Shouldn't Compile. Please tell the VRLabs devs");	
				return;
			}

			MethodInfo instanceMethod = instancerType.GetMethod("Instance", ALL);

			if (instanceMethod == null)
			{
				Debug.LogError("Instance method not found");
				return;
			}

			// Gives disk path, Unity uses Packages/dev.vrlabs.fancy, can't use this
			// string assetPath = new System.Diagnostics.StackTrace(true).GetFrame(0).GetFileName();
			
			var editor = ScriptableObject.CreateInstance<MyFancyPackage>();
			var script = MonoScript.FromScriptableObject(editor);
			var assetPath =  AssetDatabase.GetAssetPath(script);
			
			instanceMethod.Invoke(null, new object[] { packageName, assetPath, excludeRegexs });
		}
	}	
}