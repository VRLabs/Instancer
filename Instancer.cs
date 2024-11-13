using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRLabs.Instancer
{
	[InitializeOnLoad]
	public class Instancer : MonoBehaviour
	{
		static bool renameInstances;
		
		static Instancer()
		{
			renameInstances = PlayerPrefs.GetString("VRLabs.Instancer.RenameInstances", "False") == "True"; ;
		}
		
		[MenuItem("VRLabs/Rename new Instances", priority = 1)]
		public static void RenameInstancesToggle()
		{
			renameInstances = !renameInstances;
			PlayerPrefs.SetString("VRLabs.Instancer.RenameInstances", renameInstances.ToString());
		}

		[MenuItem("VRLabs/Rename new Instances", true)]
		public static bool RenameInstancesToggleValidate()
		{
			Menu.SetChecked("VRLabs/Rename new Instances", renameInstances);
			return true;
		}

		[MenuItem("VRLabs/Create Instance/Any Package")]
		public static void CreateInstanceAnyPackageStart()
		{
			string sourceFolder = EditorUtility.OpenFolderPanel("Select Directory To Copy Assets From", "Assets/", "");

			if (sourceFolder == "" || sourceFolder == null)
			{
				Debug.LogError("No folder selected, please select a folder to copy the assets to.");
				return;
			}
			
			string targetFolder = EditorUtility.OpenFolderPanel("Select Directory To Copy Assets To", "Assets/", "");

			if (targetFolder == "" || targetFolder == null)
			{
				Debug.LogError("No folder selected, please select a folder to copy the assets to.");
				return;
			}

			if (!targetFolder.Contains(Application.dataPath))
			{
				Debug.LogError("Selected folder is not in the Assets folder, please select a folder in the Assets directory.");
				return;
			}

			if (renameInstances)
			{
				EnterValueWindow.Open("", "Enter Old Package name", (oldName) =>
				{
					EnterValueWindow.Open(oldName, "Enter New Instance name", (newName) =>
					{
						FinishInstancing(oldName, "Assets" + sourceFolder.Replace(Application.dataPath, ""), new[]
						{
							".*\\.cs",
							".*\\.asmdef",
							".*\\.shader",
							"package.json"
						}, targetFolder, newName, null, true);
					});
				});
			}
			else
			{
				EnterValueWindow.Open("", "Enter New Instance name", (newName) =>
				{
					FinishInstancing(newName, "Assets" + sourceFolder.Replace(Application.dataPath, ""), new[]
					{
						".*\\.cs",
						".*\\.asmdef",
						".*\\.shader",
						"package.json"
					}, targetFolder, null, null, true);
				});
			}
		}

		public static void Instance(string packageName, string installFilePath, string[] excludeRegexs)
		{
			InstanceWithCallback(packageName, installFilePath, excludeRegexs, null);
		}
		
		// Done this way because existing packages call the Instance method with 3 parameters
		public static void InstanceWithCallback(string packageName, string installFilePath, string[] excludeRegexs, Action<string> callBack = null)
		{
			string targetFolder = EditorUtility.OpenFolderPanel("Select Directory To Copy Assets To", "Assets/", "");

			if (targetFolder == "" || targetFolder == null)
			{
				Debug.LogError("No folder selected, please select a folder to copy the assets to.");
				return;
			}

			if (!targetFolder.Contains(Application.dataPath))
			{
				Debug.LogError("Selected folder is not in the Assets folder, please select a folder in the Assets directory.");
				return;
			}

			if (renameInstances)
			{
				EnterValueWindow.Open(packageName, "Enter new Instance Name",(newName) => FinishInstancing(packageName, installFilePath, excludeRegexs, targetFolder, newName, callBack));
				return;
			} 
			
			FinishInstancing(packageName, installFilePath, excludeRegexs, targetFolder, callBack: callBack);
		}

		// Done this way because we need the unity editor to continue running during the rename popup window.
		public static void FinishInstancing(string packageName, string installFilePath, string[] excludeRegexs,
			string targetFolder, string newInstanceName = null, Action<string> callBack = null, bool isAnyPackage = false)
		{
			targetFolder = PrepareTargetFolderPath(targetFolder, newInstanceName != null ? newInstanceName : packageName);

			string sourceFolder = isAnyPackage ? installFilePath : GetSourceFolder(installFilePath);

			string[] localAssetPaths = GetLocalAssetPaths(sourceFolder, excludeRegexs);

			CreateDirectories(localAssetPaths, targetFolder);

			CopyFiles(localAssetPaths, sourceFolder, targetFolder);

			AssetDatabase.Refresh();

			FixReferences(localAssetPaths, sourceFolder, targetFolder);

			if (newInstanceName != null)
			{
				RenameInstance(localAssetPaths, targetFolder, packageName, newInstanceName);
			}

			AssetDatabase.Refresh();
			
			callBack?.Invoke(targetFolder);
		}

		static string PrepareTargetFolderPath(string folderPath, string packageName)
		{
			folderPath = "Assets" + folderPath.Remove(0, Application.dataPath.Length) + "/" + packageName;

			if (Directory.Exists(folderPath))
			{
				int i = 1;
				while (Directory.Exists(folderPath + i.ToString()))
				{
					i++;
				}
				
				folderPath += i;
			}

			Directory.CreateDirectory(folderPath);
			AssetDatabase.ImportAsset(folderPath);
			return folderPath;
		}
		
		static string GetSourceFolder(string installFilePath)
		{
			string sourceFolder = installFilePath;
#if UNITY_2019
			while (!File.Exists("." + sourceFolder + "/package.json"))
#else
			if (sourceFolder.StartsWith("/Assets")) sourceFolder = sourceFolder.Replace("/Assets", "./Assets");
			while (!File.Exists(sourceFolder + "/package.json"))
#endif
			{
				if (sourceFolder == null)
				{
					throw new ArgumentException("Supplied path not in correct format");
				}
				sourceFolder = Path.GetDirectoryName(sourceFolder); 
			}
			
#if UNITY_2019
			return sourceFolder.Replace("\\", "/").Substring(1);
#else
			return sourceFolder.Replace("\\", "/").Substring(2);
#endif
		}

		static string[] GetLocalAssetPaths(string sourceFolder, string[] excludeRegexs)
		{
			string[] assetPaths = AssetDatabase.FindAssets("", new [] { sourceFolder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();

			string[] filteredLocalAssetPaths = assetPaths
				.Select(path => path.Remove(0,sourceFolder.Length))
				.Where(path => excludeRegexs.All(regex => !Regex.Match(path, regex).Success))
				.ToArray();

			return filteredLocalAssetPaths;
		}
		
		static void CreateDirectories(string[] filePaths, string targetFolder)
		{
			try
			{
				AssetDatabase.StartAssetEditing();
				foreach (string path in filePaths)
				{
					string targetPath = Path.GetDirectoryName(targetFolder + path);
					if (!Directory.Exists(targetPath))
					{
						Directory.CreateDirectory(targetPath);
						AssetDatabase.ImportAsset(targetPath);
					}
				}
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
			}
		}

		static void CopyFiles(string[] filePaths, string sourceFolder, string targetFolder)
		{
			try
			{
				AssetDatabase.StartAssetEditing();
				foreach (string path in filePaths)
				{
					if (!Directory.Exists(sourceFolder + path))
					{
						AssetDatabase.CopyAsset(sourceFolder + path, targetFolder + path);
					}
				}
			}
			finally{
				AssetDatabase.StopAssetEditing();
			}
		}

		static void FixReferences(string[] localAssetPaths, string sourceFolder, string targetFolder)
		{
			foreach (string localAssetPath in localAssetPaths)
			{
				string targetAssetPath = targetFolder + localAssetPath;
				UnityEngine.Object[] targetAssets = AssetDatabase.LoadAllAssetsAtPath(targetAssetPath).Where(x => x != null).ToArray();
				foreach (var targetAsset in targetAssets)
				{
					SerializedObject serializedObject = new SerializedObject(targetAsset);
					SerializedProperty property = serializedObject.GetIterator();
					bool changed = false;
					bool newChanged = false;
					do
					{
						if (property.propertyPath.Contains("m_Modification")) continue;
						if (property.propertyType == SerializedPropertyType.ObjectReference)
						{
							if (property.objectReferenceValue != null)
							{
								Object newObject;
								(newObject, newChanged) = GetTargetVersion(sourceFolder, targetFolder, property.objectReferenceValue);
								if (newChanged)
								{
									changed = true;
									property.objectReferenceValue = newObject;
								}
							}
						}

						if (property.propertyType == SerializedPropertyType.ExposedReference)
						{
							if (property.exposedReferenceValue != null)
							{
								Object newObject;
								(newObject, newChanged) = GetTargetVersion(sourceFolder, targetFolder, property.exposedReferenceValue);
								if (newChanged)
								{
									changed = true;
									property.exposedReferenceValue = newObject;
								}
							}
						}
					} while (property.Next(true));
				
					if (changed) serializedObject.ApplyModifiedProperties();
				}
			}
		}
		
		static void RenameInstance(string[] localAssetPaths, string targetFolder, string packageName, string newInstanceName)
		{
			foreach (string localAssetPath in localAssetPaths)
			{
				string targetAssetPath = targetFolder + localAssetPath;
				UnityEngine.Object[] targetAssets = AssetDatabase.LoadAllAssetsAtPath(targetAssetPath).Where(x => x != null).ToArray();
				if (targetAssets.Length == 0) continue;
				string[] possibleNames = new []{packageName, packageName.Replace("-", ""), packageName.Replace("-", " ")};
				
				foreach (var targetAsset in targetAssets)
				{
					SerializedObject serializedObject = new SerializedObject(targetAsset);
					SerializedProperty property = serializedObject.GetIterator();
					do
					{
						if (property.propertyPath.Contains("m_Modification")) continue;
						if (property.propertyType == SerializedPropertyType.String)
						{
							string value = property.stringValue;
							if (value == null) continue;
				
							foreach (string possibleName in possibleNames)
							{
								if (value.StartsWith(possibleName) && !value.StartsWith(newInstanceName))
								{
									property.stringValue = ReplaceAtStart(value, possibleName, newInstanceName);
									break;
								}	
							}
						}
					} while (property.Next(true));
				
					serializedObject.ApplyModifiedProperties();	
				}
				
				String fileName = Path.GetFileName(targetAssetPath);
				foreach (string possibleName in possibleNames)
				{
					if (fileName.StartsWith(possibleName))
					{
						fileName = ReplaceAtStart(fileName, possibleName, newInstanceName);
						AssetDatabase.RenameAsset(targetAssetPath, fileName);
						break;
					}
				}
			}
		}

		public static string ReplaceAtStart(string str, string oldValue, string newValue)
		{
			return newValue + str.Substring(oldValue.Length);
		}
		private static (Object, bool) GetTargetVersion(string sourceFolder, string targetFolder, Object target)
		{
			string targetPath = AssetDatabase.GetAssetPath(target);
			if (targetPath.StartsWith(sourceFolder))
			{
				string newTargetPath = targetFolder + targetPath.Remove(0, sourceFolder.Length);
				Object newObject = AssetDatabase.LoadAllAssetsAtPath(newTargetPath).Where(obj => obj.GetType() == target.GetType()).FirstOrDefault(x => x.name == target.name);
				return (newObject, newObject != null);
			}

			return (target, false);
		}
	}

	public class EnterValueWindow : EditorWindow
	{
		private string value = "";
		private string windowTitle;
		private Action<string> callBack;
		public static void Open(string packageName, string windowTitle, Action<string> callBack)
		{
			var window = GetWindow<EnterValueWindow>(windowTitle);
			window.windowTitle = windowTitle;
			window.value = packageName;
			window.callBack = callBack;
			window.Show();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField(windowTitle, EditorStyles.boldLabel);
			value = EditorGUILayout.TextField(value);

			if (GUILayout.Button("Submit"))
			{				
				Close();
				callBack(value);
			}
		}
	}
}
