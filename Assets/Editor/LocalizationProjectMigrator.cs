#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LocalizationProjectMigrator
{
    // Idempotent: safe to run again after adding new UI text.
    private const string SessionKey = "YJHan29.LocalizationProjectMigrator.RanV2";

    private static readonly Dictionary<string, string> StaticTextKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Stage Select", "ui.home.stage_select" },
            { "UPGRADE", "ui.home.upgrade" },
            { "START", "ui.home.start" },
            { "EXIT", "ui.home.exit" },
            { "X", "ui.common.close" },
            { "Hell Kitchin", "game.title" },
            { "Hell Kitchen", "game.title" },
            { "Replay", "ui.game.replay" },
            { "Home", "ui.game.home" },
            { "KR", "ui.language.korean" },
            { "ENG", "ui.language.english" },
            { "00:00", "ui.game.time_format" }
        };

    [InitializeOnLoadMethod]
    private static void RunOnceAfterCompilation()
    {
        if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Migrate;
    }

    [MenuItem("Tools/Localization/Migrate UI Text")]
    public static void Migrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Localization migration cannot run while entering or using Play Mode.");
            return;
        }

        foreach (string scenePath in new[]
        {
            "Assets/Scenes/HomeScene.unity",
            "Assets/Scenes/GameScene.unity"
        })
        {
            MigrateScene(scenePath);
        }

        foreach (string prefabPath in new[]
        {
            "Assets/Resources/Prefabs/BTN_StagePrefab.prefab",
            "Assets/Resources/Prefabs/UpgradePrefab.prefab"
        })
        {
            MigratePrefab(prefabPath);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Localization UI migration completed.");
    }

    private static void MigrateScene(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            changed |= MigrateTexts(root);
        }

        if (scenePath.EndsWith("HomeScene.unity", StringComparison.Ordinal))
        {
            changed |= EnsureLanguageButtons(scene);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!wasLoaded)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void MigratePrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (MigrateTexts(root))
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool MigrateTexts(GameObject root)
    {
        bool changed = false;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            string key = ResolveKey(text);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            LocalizedText localized = text.GetComponent<LocalizedText>();
            if (localized == null)
            {
                localized = text.gameObject.AddComponent<LocalizedText>();
                changed = true;
            }

            SerializedObject serialized = new SerializedObject(localized);
            SerializedProperty keyProperty = serialized.FindProperty("stringKey");
            SerializedProperty targetProperty = serialized.FindProperty("target");
            if (keyProperty.stringValue != key || targetProperty.objectReferenceValue != text)
            {
                keyProperty.stringValue = key;
                targetProperty.objectReferenceValue = text;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (!string.IsNullOrEmpty(text.text))
            {
                text.text = string.Empty;
                EditorUtility.SetDirty(text);
                changed = true;
            }
        }
        return changed;
    }

    private static string ResolveKey(TMP_Text text)
    {
        string trimmedText = text.text.Trim();
        if (StaticTextKeys.TryGetValue(trimmedText, out string key))
        {
            return key;
        }

        if (text.name == "Text (TMP)" && text.transform.parent != null
            && text.transform.parent.name == "BTN_Buy")
        {
            return "ui.common.number_format";
        }

        switch (text.name)
        {
            case "LevelText": return "ui.game.level_format";
            case "CoinText": return "ui.game.coin_format";
            case "TXT_Coin": return "ui.common.number_format";
            case "TimeText": return "ui.game.time_format";
            case "CardDesc": return "card.1.desc";
            case "TXT_Stage": return "stage.1.name";
            case "TXT_Status": return "status.attack_power";
            default: return null;
        }
    }

    private static bool EnsureLanguageButtons(Scene scene)
    {
        HomeSceneController controller = null;
        Canvas canvas = null;
        TMP_Text sample = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller ??= root.GetComponentInChildren<HomeSceneController>(true);
            canvas ??= root.GetComponentInChildren<Canvas>(true);
            sample ??= root.GetComponentInChildren<TMP_Text>(true);
        }

        if (controller == null || canvas == null)
        {
            return false;
        }

        Button korean = FindByName<Button>(scene, "BTN_Korean");
        Button english = FindByName<Button>(scene, "BTN_English");
        bool changed = false;
        if (korean == null)
        {
            korean = CreateLanguageButton(canvas.transform, "BTN_Korean",
                new Vector2(-220f, -45f), "ui.language.korean", sample);
            changed = true;
        }
        if (english == null)
        {
            english = CreateLanguageButton(canvas.transform, "BTN_English",
                new Vector2(-70f, -45f), "ui.language.english", sample);
            changed = true;
        }

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty koreanProperty = serialized.FindProperty("koreanButton");
        SerializedProperty englishProperty = serialized.FindProperty("englishButton");
        if (koreanProperty.objectReferenceValue != korean || englishProperty.objectReferenceValue != english)
        {
            koreanProperty.objectReferenceValue = korean;
            englishProperty.objectReferenceValue = english;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }
        return changed;
    }

    private static Button CreateLanguageButton(Transform parent, string name,
        Vector2 anchoredPosition, string key, TMP_Text sample)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = LayerMask.NameToLayer("UI");
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(130f, 52f);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.9f);

        GameObject textObject = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.layer = buttonObject.layer;
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 24f;
        label.color = Color.white;
        if (sample != null)
        {
            label.font = sample.font;
        }

        LocalizedText localized = textObject.AddComponent<LocalizedText>();
        SerializedObject serialized = new SerializedObject(localized);
        serialized.FindProperty("stringKey").stringValue = key;
        serialized.FindProperty("target").objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        label.text = string.Empty;
        return buttonObject.GetComponent<Button>();
    }

    private static T FindByName<T>(Scene scene, string objectName) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                if (component.name == objectName)
                {
                    return component;
                }
            }
        }
        return null;
    }
}
#endif
