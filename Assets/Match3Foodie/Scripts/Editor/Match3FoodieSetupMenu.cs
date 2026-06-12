using System.IO;
using Match3Foodie;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Match3FoodieEditor
{
    public static class Match3FoodieSetupMenu
    {
        private const string RootPath = "Assets/Match3Foodie";
        private const string GeneratedPath = RootPath + "/Generated";
        private const string SpritesPath = GeneratedPath + "/Sprites";
        private const string ElementsPath = GeneratedPath + "/Elements";
        private const string PrefabsPath = GeneratedPath + "/Prefabs";
        private const string SettingsPath = GeneratedPath + "/Settings";

        [MenuItem("Tools/Match3 Foodie/Create Starter Setup")]
        public static void CreateStarterSetup()
        {
            EnsureFolders();

            var prefab = CreatePiecePrefab();
            var elements = CreateElementDefinitions(prefab);
            var boardSettings = CreateBoardSettings(prefab, elements);
            var levelSettings = CreateLevelSettings(elements);

            CreateBoardInScene(boardSettings, levelSettings);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("Match3 Foodie starter setup created. Press Play and swap adjacent pieces.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootPath, "Generated");
            CreateFolderIfMissing(GeneratedPath, "Sprites");
            CreateFolderIfMissing(GeneratedPath, "Elements");
            CreateFolderIfMissing(GeneratedPath, "Prefabs");
            CreateFolderIfMissing(GeneratedPath, "Settings");
        }

        private static void CreateFolderIfMissing(string parent, string folderName)
        {
            var path = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static Match3PieceView CreatePiecePrefab()
        {
            const string prefabPath = PrefabsPath + "/Default Match3 Piece.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Match3PieceView>(prefabPath);
            if (existing != null)
            {
                return existing;
            }

            var pieceObject = new GameObject("Default Match3 Piece");
            var spriteRenderer = pieceObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;

            var collider = pieceObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            var view = pieceObject.AddComponent<Match3PieceView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("spriteRenderer").objectReferenceValue = spriteRenderer;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(pieceObject, prefabPath).GetComponent<Match3PieceView>();
            Object.DestroyImmediate(pieceObject);
            return prefab;
        }

        private static Match3ElementDefinition[] CreateElementDefinitions(Match3PieceView prefab)
        {
            var specs = new[]
            {
                new ElementSpec("Tomato", new Color(0.92f, 0.12f, 0.08f)),
                new ElementSpec("Cheese", new Color(1f, 0.78f, 0.16f)),
                new ElementSpec("Milk", new Color(0.48f, 0.74f, 1f)),
                new ElementSpec("Bread", new Color(0.88f, 0.54f, 0.24f)),
                new ElementSpec("Banana", new Color(1f, 0.92f, 0.18f)),
                new ElementSpec("Fish", new Color(0.34f, 0.64f, 0.9f)),
            };

            var elements = new Match3ElementDefinition[specs.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var sprite = CreateSprite(specs[i]);
                var assetPath = $"{ElementsPath}/{specs[i].Name}.asset";
                var definition = AssetDatabase.LoadAssetAtPath<Match3ElementDefinition>(assetPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<Match3ElementDefinition>();
                    AssetDatabase.CreateAsset(definition, assetPath);
                }

                var serializedDefinition = new SerializedObject(definition);
                serializedDefinition.FindProperty("elementId").stringValue = specs[i].Name.ToLowerInvariant();
                serializedDefinition.FindProperty("sprite").objectReferenceValue = sprite;
                serializedDefinition.FindProperty("tint").colorValue = Color.white;
                serializedDefinition.FindProperty("spawnWeight").intValue = 1;
                serializedDefinition.FindProperty("piecePrefab").objectReferenceValue = prefab;
                serializedDefinition.FindProperty("specialEffectType").enumValueIndex = GetSpecialEffectType(specs[i].Name);
                serializedDefinition.FindProperty("mathBonusSeconds").floatValue = 10f;
                serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(definition);
                elements[i] = definition;
            }

            return elements;
        }

        private static Sprite CreateSprite(ElementSpec spec)
        {
            var texturePath = $"{SpritesPath}/{spec.Name}.png";
            if (!File.Exists(texturePath))
            {
                var texture = GenerateTokenTexture(spec.Color);
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 128f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        }

        private static Texture2D GenerateTokenTexture(Color color)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.43f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance > radius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    var light = Mathf.InverseLerp(radius, 0f, distance) * 0.34f;
                    var pixel = Color.Lerp(color, Color.white, light);
                    pixel.a = 1f;
                    texture.SetPixel(x, y, pixel);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Match3BoardSettings CreateBoardSettings(Match3PieceView prefab, Match3ElementDefinition[] elements)
        {
            const string assetPath = SettingsPath + "/Starter Board Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<Match3BoardSettings>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<Match3BoardSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("width").intValue = 8;
            serializedSettings.FindProperty("height").intValue = 8;
            serializedSettings.FindProperty("cellSize").floatValue = 1f;
            serializedSettings.FindProperty("gapSize").vector2Value = new Vector2(0.08f, 0.08f);
            serializedSettings.FindProperty("elementSize").floatValue = 0.86f;
            serializedSettings.FindProperty("swapDuration").floatValue = 0.16f;
            serializedSettings.FindProperty("shuffleDuration").floatValue = 0.28f;
            serializedSettings.FindProperty("fallDurationPerCell").floatValue = 0.055f;
            serializedSettings.FindProperty("refillFallDurationPerCell").floatValue = 0.055f;
            serializedSettings.FindProperty("clearDelay").floatValue = 0.06f;
            serializedSettings.FindProperty("refillDelay").floatValue = 0.04f;
            serializedSettings.FindProperty("refillRandomDelay").vector2Value = new Vector2(0f, 0.12f);
            serializedSettings.FindProperty("refillSpawnScale").floatValue = 0.72f;
            serializedSettings.FindProperty("refillPopScale").floatValue = 1.08f;
            serializedSettings.FindProperty("collectFlightSpeed").floatValue = 12f;
            serializedSettings.FindProperty("collectExitDistance").floatValue = 0.35f;
            serializedSettings.FindProperty("collectExitDuration").floatValue = 0.12f;
            serializedSettings.FindProperty("collectArrivePopScale").floatValue = 1.25f;
            serializedSettings.FindProperty("collectArrivePopDuration").floatValue = 0.14f;
            serializedSettings.FindProperty("avoidInitialMatches").boolValue = true;
            serializedSettings.FindProperty("allowInputWhileResolving").boolValue = false;
            serializedSettings.FindProperty("matchLines").boolValue = true;
            serializedSettings.FindProperty("matchTShapes").boolValue = true;
            serializedSettings.FindProperty("matchSquares").boolValue = true;
            serializedSettings.FindProperty("matchCrosses").boolValue = true;
            serializedSettings.FindProperty("matchCorners").boolValue = true;
            serializedSettings.FindProperty("fishFlightSpeed").floatValue = 9f;
            serializedSettings.FindProperty("fishRandomDelay").vector2Value = new Vector2(0f, 0.12f);
            serializedSettings.FindProperty("fishWaveAmplitude").floatValue = 0.18f;
            serializedSettings.FindProperty("fishWaveFrequency").floatValue = 7f;
            serializedSettings.FindProperty("fishKeepBoardOrientation").boolValue = false;
            serializedSettings.FindProperty("fishFaceFlightDirection").boolValue = true;
            serializedSettings.FindProperty("fishSpriteForwardAngle").floatValue = 180f;
            serializedSettings.FindProperty("fishMaxTiltAngle").floatValue = 35f;
            serializedSettings.FindProperty("fishFlightSortingOrderBoost").intValue = 100;
            serializedSettings.FindProperty("defaultPiecePrefab").objectReferenceValue = prefab;

            var elementsProperty = serializedSettings.FindProperty("elements");
            elementsProperty.arraySize = elements.Length;
            for (var i = 0; i < elements.Length; i++)
            {
                elementsProperty.GetArrayElementAtIndex(i).objectReferenceValue = elements[i];
            }

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Match3LevelSettings CreateLevelSettings(Match3ElementDefinition[] elements)
        {
            const string assetPath = SettingsPath + "/Starter Level Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<Match3LevelSettings>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<Match3LevelSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("timeLimitSeconds").floatValue = 120f;
            serializedSettings.FindProperty("failWhenTimerEnds").boolValue = true;

            var goalsProperty = serializedSettings.FindProperty("goals");
            goalsProperty.arraySize = 4;
            SetGoal(goalsProperty.GetArrayElementAtIndex(0), elements[3], 18);
            SetGoal(goalsProperty.GetArrayElementAtIndex(1), elements[2], 16);
            SetGoal(goalsProperty.GetArrayElementAtIndex(2), elements[1], 12);
            SetGoal(goalsProperty.GetArrayElementAtIndex(3), elements[0], 20);

            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void SetGoal(SerializedProperty goalProperty, Match3ElementDefinition element, int requiredAmount)
        {
            goalProperty.FindPropertyRelative("element").objectReferenceValue = element;
            goalProperty.FindPropertyRelative("requiredAmount").intValue = requiredAmount;
        }

        private static void CreateBoardInScene(Match3BoardSettings boardSettings, Match3LevelSettings levelSettings)
        {
            var existingBoard = Object.FindFirstObjectByType<Match3Board>();
            GameObject boardObject;
            Match3Board board;

            if (existingBoard != null)
            {
                board = existingBoard;
                boardObject = existingBoard.gameObject;
            }
            else
            {
                boardObject = new GameObject("Match3 Board");
                board = boardObject.AddComponent<Match3Board>();
            }

            var root = boardObject.transform.Find("Pieces");
            if (root == null)
            {
                root = new GameObject("Pieces").transform;
                root.SetParent(boardObject.transform, false);
            }

            var serializedBoard = new SerializedObject(board);
            serializedBoard.FindProperty("settings").objectReferenceValue = boardSettings;
            serializedBoard.FindProperty("piecesRoot").objectReferenceValue = root;
            serializedBoard.ApplyModifiedPropertiesWithoutUndo();

            var levelController = Object.FindFirstObjectByType<Match3LevelController>();
            if (levelController == null)
            {
                levelController = boardObject.AddComponent<Match3LevelController>();
            }

            var serializedLevelController = new SerializedObject(levelController);
            serializedLevelController.FindProperty("levelSettings").objectReferenceValue = levelSettings;
            serializedLevelController.FindProperty("board").objectReferenceValue = board;
            serializedLevelController.FindProperty("startTimerOnEnable").boolValue = true;
            serializedLevelController.FindProperty("disableBoardWhenLevelEnds").boolValue = true;
            serializedLevelController.ApplyModifiedPropertiesWithoutUndo();

            serializedBoard = new SerializedObject(board);
            serializedBoard.FindProperty("levelController").objectReferenceValue = levelController;
            serializedBoard.ApplyModifiedPropertiesWithoutUndo();

            EnsureCamera();
            Selection.activeObject = boardObject;
        }

        private static int GetSpecialEffectType(string elementName)
        {
            return elementName switch
            {
                "Fish" => (int)Match3SpecialEffectType.Fish,
                "Cheese" => (int)Match3SpecialEffectType.MathBonus,
                _ => (int)Match3SpecialEffectType.None,
            };
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            }

            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
        }

        private readonly struct ElementSpec
        {
            public ElementSpec(string name, Color color)
            {
                Name = name;
                Color = color;
            }

            public string Name { get; }
            public Color Color { get; }
        }
    }
}
