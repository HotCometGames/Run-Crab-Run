using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Imports the artist's full-canvas PNGs consistently, builds the lightweight
// animation profiles, and connects them to the playable scene and spawn prefabs.
// The command is deliberately idempotent so future art revisions can be dropped
// into the same paths and reapplied without hand-editing every prefab.
public static class ArtistAnimationSetup
{
    private const string CharacterSetFolder = "Assets/Animations/Character Sets";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string[] CharacterFramePaths =
    {
        "Assets/Art/Crab/crab normal.png",
        "Assets/Art/Crab/crab walk 1.png",
        "Assets/Art/Crab/crab walk 2.png",
        "Assets/Art/Crab/crab dash.png",
        "Assets/Art/Deer Assets/deer normal.PNG",
        "Assets/Art/Deer Assets/deer walk 1.PNG",
        "Assets/Art/Deer Assets/deer walk 2.png",
        "Assets/Art/Sheep Assets/sheep normal.png",
        "Assets/Art/Sheep Assets/sheep walk 1.png",
        "Assets/Art/Sheep Assets/sheep walk 2.png",
        "Assets/Art/Fox Assets/fox normal.png",
        "Assets/Art/Fox Assets/fox walk 1.png",
        "Assets/Art/Fox Assets/fox walk 2.png",
        "Assets/Art/Fox Assets/fox chase 1.png",
        "Assets/Art/Fox Assets/fox chase 2.png",
        "Assets/Art/Wolf Assets/wolf normal.png",
        "Assets/Art/Wolf Assets/wolf walk 1.png",
        "Assets/Art/Wolf Assets/wolf walk 2.png",
        "Assets/Art/Wolf Assets/wolf chase 1.png",
        "Assets/Art/Wolf Assets/wolf chase 2.png"
    };

    [MenuItem("Tools/Run Crab Run/Apply Artist Animations")]
    public static void ApplyArtistAnimations()
    {
        // The command may need to switch from a work-in-progress scene to the
        // playable scene. Let the artist/designer save or explicitly discard their
        // open scene changes before any imports or prefab edits begin.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[Artist Animation Setup] Cancelled before changing any assets.");
            return;
        }

        try
        {
            ConfigureTextureImports();
            EnsureFolder(CharacterSetFolder);

            CharacterAnimationSet crab = CreateOrUpdateSet(
                "Crab",
                "Assets/Art/Crab/crab normal.png",
                new[]
                {
                    "Assets/Art/Crab/crab walk 1.png",
                    "Assets/Art/Crab/crab walk 2.png"
                },
                Array.Empty<string>(),
                new[] { "Assets/Art/Crab/crab dash.png" },
                4.5f,
                5.5f,
                5.5f);

            CharacterAnimationSet deer = CreateOrUpdateSet(
                "Deer",
                "Assets/Art/Deer Assets/deer normal.PNG",
                new[]
                {
                    "Assets/Art/Deer Assets/deer walk 1.PNG",
                    "Assets/Art/Deer Assets/deer walk 2.png"
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                3.5f,
                5.5f,
                5.5f);

            CharacterAnimationSet sheep = CreateOrUpdateSet(
                "Sheep",
                "Assets/Art/Sheep Assets/sheep normal.png",
                new[]
                {
                    "Assets/Art/Sheep Assets/sheep walk 1.png",
                    "Assets/Art/Sheep Assets/sheep walk 2.png"
                },
                Array.Empty<string>(),
                Array.Empty<string>(),
                2.75f,
                5.5f,
                5.5f);

            CharacterAnimationSet fox = CreateOrUpdateSet(
                "Fox",
                "Assets/Art/Fox Assets/fox normal.png",
                new[]
                {
                    "Assets/Art/Fox Assets/fox walk 1.png",
                    "Assets/Art/Fox Assets/fox walk 2.png"
                },
                new[]
                {
                    "Assets/Art/Fox Assets/fox chase 1.png",
                    "Assets/Art/Fox Assets/fox chase 2.png"
                },
                Array.Empty<string>(),
                4f,
                5.5f,
                5.5f);

            CharacterAnimationSet wolf = CreateOrUpdateSet(
                "Wolf",
                "Assets/Art/Wolf Assets/wolf normal.png",
                new[]
                {
                    "Assets/Art/Wolf Assets/wolf walk 1.png",
                    "Assets/Art/Wolf Assets/wolf walk 2.png"
                },
                new[]
                {
                    "Assets/Art/Wolf Assets/wolf chase 1.png",
                    "Assets/Art/Wolf Assets/wolf chase 2.png"
                },
                Array.Empty<string>(),
                3.75f,
                5.5f,
                5.5f);

            ConfigureCreaturePrefab("Assets/Prefabs/Creatures/Deer.prefab", deer);
            ConfigureCreaturePrefab("Assets/Prefabs/Creatures/Sheep.prefab", sheep);
            ConfigureCreaturePrefab("Assets/Prefabs/Creatures/Fox.prefab", fox);
            ConfigureCreaturePrefab("Assets/Prefabs/Creatures/Wolf.prefab", wolf);
            ConfigureImposterPrefab(
                "Assets/Prefabs/Creatures/Sheep_Imposter.prefab",
                sheep,
                wolf);
            ConfigurePlayer(crab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Artist Animation Setup] Applied final crab, deer, sheep, fox, and wolf art successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static void ConfigureTextureImports()
    {
        foreach (string path in CharacterFramePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Missing character frame at {path}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // The crab was drawn with much more transparent canvas around it than
            // the animals. A lower PPU keeps its visible body aligned with the
            // player's one-unit collider instead of making it a hard-to-read dot.
            importer.spritePixelsPerUnit = path.StartsWith("Assets/Art/Crab/", StringComparison.Ordinal)
                ? 500f
                : 1000f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 100;
            importer.SaveAndReimport();
        }
    }

    private static CharacterAnimationSet CreateOrUpdateSet(
        string characterName,
        string idlePath,
        string[] walkPaths,
        string[] chasePaths,
        string[] sprintPaths,
        float walkFramesPerSecond,
        float chaseFramesPerSecond,
        float sprintFramesPerSecond)
    {
        string assetPath = $"{CharacterSetFolder}/{characterName}AnimationSet.asset";
        CharacterAnimationSet set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(assetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
            set.name = $"{characterName} Animation Set";
            AssetDatabase.CreateAsset(set, assetPath);
        }

        set.idleFrame = LoadSprite(idlePath);
        set.walkFrames = LoadSprites(walkPaths);
        set.chaseFrames = LoadSprites(chasePaths);
        set.sprintFrames = LoadSprites(sprintPaths);
        set.walkFramesPerSecond = walkFramesPerSecond;
        set.chaseFramesPerSecond = chaseFramesPerSecond;
        set.sprintFramesPerSecond = sprintFramesPerSecond;
        EditorUtility.SetDirty(set);
        return set;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException($"Could not load imported sprite at {path}");
        return sprite;
    }

    private static Sprite[] LoadSprites(string[] paths)
    {
        Sprite[] sprites = new Sprite[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            sprites[i] = LoadSprite(paths[i]);
        return sprites;
    }

    private static void ConfigureCreaturePrefab(string prefabPath, CharacterAnimationSet set)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureCharacter(root, set);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureImposterPrefab(
        string prefabPath,
        CharacterAnimationSet disguiseSet,
        CharacterAnimationSet predatorSet)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureCharacter(root, disguiseSet);

            ImposterComponent imposter = root.GetComponent<ImposterComponent>();
            if (imposter == null)
                throw new InvalidOperationException($"{prefabPath} has no ImposterComponent.");

            imposter.predatorSprite = predatorSet.idleFrame;
            imposter.predatorAnimationSet = predatorSet;
            EditorUtility.SetDirty(imposter);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePlayer(CharacterAnimationSet set)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != SampleScenePath)
            scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);

        PlayerController player = null;
        PlayerController[] loadedPlayers = Resources.FindObjectsOfTypeAll<PlayerController>();
        foreach (PlayerController candidate in loadedPlayers)
        {
            if (candidate != null && candidate.gameObject.scene == scene)
            {
                player = candidate;
                break;
            }
        }

        if (player == null)
            throw new InvalidOperationException($"No PlayerController found in {SampleScenePath}.");

        ConfigureCharacter(player.gameObject, set);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureCharacter(GameObject root, CharacterAnimationSet set)
    {
        SpriteRenderer renderer = GetOrCreateVisualRenderer(root);
        renderer.sprite = set.idleFrame;
        renderer.flipX = false;
        renderer.flipY = false;
        EditorUtility.SetDirty(renderer);

        CharacterAnimation2D animation = root.GetComponent<CharacterAnimation2D>();
        if (animation == null)
            animation = root.AddComponent<CharacterAnimation2D>();
        animation.Configure(set, renderer);
        EditorUtility.SetDirty(animation);
    }

    private static SpriteRenderer GetOrCreateVisualRenderer(GameObject root)
    {
        Transform existingVisual = root.transform.Find("Visual");
        SpriteRenderer rootRenderer = root.GetComponent<SpriteRenderer>();

        if (existingVisual != null && rootRenderer == null)
        {
            SpriteRenderer existingRenderer = existingVisual.GetComponent<SpriteRenderer>();
            if (existingRenderer != null)
                return existingRenderer;
        }

        if (rootRenderer == null)
        {
            SpriteRenderer nestedRenderer = root.GetComponentInChildren<SpriteRenderer>(true);
            if (nestedRenderer != null)
                return nestedRenderer;
            throw new InvalidOperationException($"{root.name} has no SpriteRenderer to migrate.");
        }

        GameObject visualObject;
        if (existingVisual != null)
        {
            visualObject = existingVisual.gameObject;
        }
        else
        {
            visualObject = new GameObject("Visual");
            visualObject.layer = root.layer;
            visualObject.transform.SetParent(root.transform, false);
        }

        SpriteRenderer visualRenderer = visualObject.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
            visualRenderer = visualObject.AddComponent<SpriteRenderer>();

        EditorUtility.CopySerialized(rootRenderer, visualRenderer);
        UnityEngine.Object.DestroyImmediate(rootRenderer);
        return visualRenderer;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = folderPath.Substring(0, folderPath.LastIndexOf('/'));
        string folderName = folderPath.Substring(folderPath.LastIndexOf('/') + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
