using UnityEditor;
using UnityEngine;

public static class CreateAruMaruPrefab
{
    public static void Create()
    {
        const string sourcePath = "Assets/Prefabs/Test Players/TopDown Test Player/TD Test Player A.prefab";
        const string modelPath = "Assets/Character/Arumajiro.fbx";
        const string outputPath = "Assets/Prefabs/Arumajiro Player.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null || model == null) throw new System.Exception("Source prefab or character model was not found.");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        root.name = "Arumajiro Player";
        var sphereRenderer = root.GetComponent<MeshRenderer>();
        if (sphereRenderer != null) sphereRenderer.enabled = false;
        var character = (GameObject)PrefabUtility.InstantiatePrefab(model);
        character.name = "Arumajiro";
        character.transform.SetParent(root.transform, false);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = Vector3.one;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Character/Texture/Materials/aka.mat");
        if (material == null) throw new System.Exception("aka.mat was not found.");
        foreach (var renderer in character.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = material;
        var renderers = character.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float rootScale = Mathf.Max(root.transform.lossyScale.x, root.transform.lossyScale.y, root.transform.lossyScale.z);
            if (maxDimension > 0f) character.transform.localScale = Vector3.one * (4.5f / (maxDimension * rootScale));
        }
        PrefabUtility.SaveAsPrefabAsset(root, outputPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("Created " + outputPath);
    }
}



