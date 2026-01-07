using System;
using System.Collections;
using System.Collections.Generic;
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
			
			FixPrefabReferences(localAssetPaths, sourceFolder, targetFolder);
			
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

		        if (targetAssetPath.EndsWith(".prefab"))
		        {
		            FixPrefabReferences(targetAssetPath, sourceFolder, targetFolder);
		        }

		        FixNonPrefabReferences(targetAssetPath, sourceFolder, targetFolder);
		    }
		}

		static void FixPrefabReferences(string prefabPath, string sourceFolder, string targetFolder)
		{
		    GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
		    
		    try
		    {
		        bool changed = false;
		        
		        Component[] allComponents = prefabInstance.GetComponentsInChildren<Component>(true);
		        foreach (Component component in allComponents)
		        {
		            if (component == null) continue;
		            
		            SerializedObject serializedObject = new SerializedObject(component);
		            changed |= ProcessSerializedObject(serializedObject, sourceFolder, targetFolder);
		            
		            if (component.gameObject != prefabInstance)
		            {
		                SerializedObject gameObjectSerialized = new SerializedObject(component.gameObject);
		                changed |= ProcessSerializedObject(gameObjectSerialized, sourceFolder, targetFolder);
		            }
		        }
		        
		        if (changed)
		        {
		            PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
		        }
		    }
		    finally
		    {
		        PrefabUtility.UnloadPrefabContents(prefabInstance);
		    }
		}

		static void FixNonPrefabReferences(string assetPath, string sourceFolder, string targetFolder)
		{
		    UnityEngine.Object[] targetAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
		    
		    foreach (var targetAsset in targetAssets)
		    {
		        if (targetAsset == null) continue;
		        
		        if (targetAsset.ToString().Contains(" (UnityEngine.PrefabInstance)"))
		        {
		            continue;
		        }
		        
		        SerializedObject serializedObject = new SerializedObject(targetAsset);
		        ProcessSerializedObject(serializedObject, sourceFolder, targetFolder);
		    }
		}

		static bool ProcessSerializedObject(SerializedObject serializedObject, string sourceFolder, string targetFolder)
		{
		    bool changed = false;
		    
		    SerializedProperty property = serializedObject.GetIterator();
		    bool enterChildren = true;
		    
		    while (property.Next(enterChildren))
		    {
		        enterChildren = true;
		        
		        // Skip certain properties
		        if (property.propertyPath.Contains("m_Modification") || 
		            property.propertyPath.Contains("m_ParentPrefab") ||
		            property.propertyPath.Contains("m_CorrespondingSourceObject") ||
		            property.propertyPath.Contains("m_PrefabInstance") ||
		            property.propertyPath.Contains("m_PrefabAsset"))
		        {
		            enterChildren = false;
		            continue;
		        }
		        
		        if (property.propertyType == SerializedPropertyType.ObjectReference)
		        {
		            if (property.objectReferenceValue != null)
		            {
		                UnityEngine.Object newObject;
		                bool newChanged;
		                (newObject, newChanged) = GetTargetVersion(sourceFolder, targetFolder, property.objectReferenceValue);
		                if (newChanged && newObject != null)
		                {
		                    changed = true;
		                    property.objectReferenceValue = newObject;
		                }
		            }
		        }
		        else if (property.propertyType == SerializedPropertyType.ExposedReference)
		        {
		            if (property.exposedReferenceValue != null)
		            {
		                UnityEngine.Object newObject;
		                bool newChanged;
		                (newObject, newChanged) = GetTargetVersion(sourceFolder, targetFolder, property.exposedReferenceValue);
		                if (newChanged && newObject != null)
		                {
		                    changed = true;
		                    property.exposedReferenceValue = newObject;
		                }
		            }
		        }
		    }
		    
		    if (changed) serializedObject.ApplyModifiedProperties();
		    
		    return changed;
		}
		
		
		static void FixPrefabReferences(string[] localAssetPaths, string sourceFolder, string targetFolder)
		{
		    Dictionary<string, string> guidMap = new Dictionary<string, string>();
		    localAssetPaths = localAssetPaths.Where(x => x.EndsWith("prefab") || x.EndsWith(".fbx")).ToArray();
		    // Build GUID mapping
		    foreach (string localPath in localAssetPaths)
		    {
		        string sourcePath = sourceFolder + localPath;
		        string targetPath = targetFolder + localPath;
		        
		        if (!File.Exists(sourcePath) || !File.Exists(targetPath)) continue;
		        
	            string sourceGuid = GetGuid(sourcePath + ".meta");
	            string targetGuid = GetGuid(targetPath + ".meta");
	            if (!string.IsNullOrEmpty(sourceGuid) && !string.IsNullOrEmpty(targetGuid))
	                guidMap[sourceGuid] = targetGuid;
		   
		    }
		    
		    // Update all target prefabs
		    foreach (string localPath in localAssetPaths)
		    {
		        string targetPath = targetFolder + localPath;
		        if (!File.Exists(targetPath)) continue;
		        
	            string[] lines = File.ReadAllLines(targetPath);
	            bool modified = false;
	            
	            for (int i = 0; i < lines.Length; i++)
	            {
	                if (lines[i].Contains("guid:"))
	                {
	                    Match match = Regex.Match(lines[i], @"guid:\s*([0-9a-fA-F]{32})");
	                    if (match.Success && guidMap.TryGetValue(match.Groups[1].Value.ToLower(), out string newGuid))
	                    {
	                        lines[i] = Regex.Replace(lines[i], @"guid:\s*[0-9a-fA-F]{32}", $"guid: {newGuid}");
	                        modified = true;
	                    }
	                }
	            }
	            
	            if (modified)
	            {
	                File.WriteAllLines(targetPath, lines);
	            }
		        
		    }
		    
		    AssetDatabase.Refresh();
		}

		static string GetGuid(string metaPath)
		{
		    if (!File.Exists(metaPath)) return null;
		    foreach (string line in File.ReadLines(metaPath))
		        if (line.StartsWith("guid:"))
		            return line.Substring(5).Trim().ToLower();
		    return null;
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
