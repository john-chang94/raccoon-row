using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DumpsterDynasty.Editor
{
    public static class CourtyardSetup
    {
        public const string ScenePath = "Assets/DumpsterDynasty/Scenes/Courtyard.unity";

        // Reproducible bootstrap for the clean slate; protects the saved authored scene.
        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log($"Scene already exists: {ScenePath}");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Courtyard Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(55, 57, 46, 255);
            camera.transform.position = new Vector3(0, 0, -10);
            var view = new GameObject("Courtyard", typeof(CourtyardView)).GetComponent<CourtyardView>();
            var so = new SerializedObject(view);
            so.FindProperty("greenPortrait").objectReferenceValue = GetPortrait("scout");
            so.FindProperty("orangePortrait").objectReferenceValue = GetPortrait("hauler");
            so.FindProperty("pipPortrait").objectReferenceValue = GetPortrait("sneak");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Cannot save first courtyard scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Dumpster Dynasty Fresh";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.defaultScreenWidth = 720;
            PlayerSettings.defaultScreenHeight = 1280;
            AssetDatabase.SaveAssets();
            Debug.Log($"DD_SCENE_READY {ScenePath}");
        }

        private static Texture2D GetPortrait(string name)
        {
            const string Art = "Assets/DumpsterDynasty/Art";
            string path = $"{Art}/raccoon_thumbnail_{name}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.maxTextureSize != 256 || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.mipmapEnabled || !importer.alphaIsTransparency))
            {
                importer.maxTextureSize = 256;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new InvalidOperationException($"Approved portrait was not imported at {path}.");
        }
    }
}
