using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VRLabs
{
	public class Instancer : MonoBehaviour
	{
		public static string Instance(string packageName, string installFilePath, string[] excludeRegexs)
		{
			if (!installFilePath.Replace("\\", "/").StartsWith("Packages/"))
			{
				Debug.LogError($"Package not found in Packages folder, please move the {packageName} package to the Packages folder.");
				return null;
			}
			
			string targetFolder = EditorUtility.OpenFolderPanel("Select Directory To Copy Assets To", "Assets/", "");

			if (targetFolder == "" || targetFolder == null)
			{
				Debug.LogError("No folder selected, please select a folder to copy the assets to.");
				return null;
			}

			if (!targetFolder.Contains(Application.dataPath))
			{
				Debug.LogError("Selected folder is not in the Assets folder, please select a folder in the Assets directory.");
				return null;
			}

			targetFolder = PrepareTargetFolderPath(targetFolder, packageName);
			
			string sourceFolder = GetSourceFolder(installFilePath);

			string[] localAssetPaths = GetLocalAssetPaths(sourceFolder, excludeRegexs);

			CreateDirectories(localAssetPaths, targetFolder);

			CopyFiles(localAssetPaths, sourceFolder, targetFolder);

			FixReferences(localAssetPaths, sourceFolder, targetFolder);
			
			return targetFolder;
		}
		
		public static void Install(string packageName, string installFilePath, string[] filePaths, Action<string> callBack)
		{
			string instancePath = Instance(packageName, installFilePath, filePaths);
			callBack(instancePath);
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

			while (Path.GetDirectoryName(sourceFolder) != "Packages")
			{
				sourceFolder = Path.GetDirectoryName(sourceFolder);
			}

			return sourceFolder.Replace("\\", "/");
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
					AssetDatabase.CopyAsset(sourceFolder + path, targetFolder + path);
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
				UnityEngine.Object[] targetAssets = AssetDatabase.LoadAllAssetsAtPath(targetAssetPath);
				foreach (var targetAsset in targetAssets)
				{
					SerializedObject serializedObject = new SerializedObject(targetAsset);
					SerializedProperty property = serializedObject.GetIterator();
					do
					{
						if (property.propertyType == SerializedPropertyType.ObjectReference)
						{
							// Debug.Log($"O{property.name}, {property.displayName}, {property.objectReferenceValue}");
							if (property.objectReferenceValue != null)
							{
								property.objectReferenceValue = GetTargetVersion(sourceFolder, targetFolder, property.objectReferenceValue);
							}
						}

						if (property.propertyType == SerializedPropertyType.ExposedReference)
						{
							// Debug.Log($"E{property.name}, {property.displayName}, {property.exposedReferenceValue}");
							if (property.exposedReferenceValue != null)
							{
								property.exposedReferenceValue = GetTargetVersion(sourceFolder, targetFolder, property.exposedReferenceValue);
							}
						}
					} while (property.Next(true));
				
					serializedObject.ApplyModifiedProperties();	
				}
			}
		}

		private static Object GetTargetVersion(string sourceFolder, string targetFolder, Object target)
		{
			string targetPath = AssetDatabase.GetAssetPath(target);
			if (targetPath.StartsWith(sourceFolder))
			{
				string newTargetPath = targetFolder + targetPath.Remove(0, sourceFolder.Length); 
				return AssetDatabase.LoadAssetAtPath(newTargetPath, target.GetType());
			}

			return target;
		}
	}
}
