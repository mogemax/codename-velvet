#if UNITY_EDITOR
using CodenameVelvet.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu music and the volume setting. Safe to run again.
/// <list type="number">
/// <item>The menu track is imported to stream from disk (it is long) as Vorbis.</item>
/// <item>Every menu scene gets a "Menu Music" (<see cref="MenuMusic"/>) with it, so it plays from whichever opens first.</item>
/// <item>The settings scene's "VolumeSlider" drives the game's volume (<see cref="VolumeSlider"/>).</item>
/// </list>
/// Opens and saves the scenes it changes; save your own changes first.
/// </summary>
public static class VelvetMenuAudioSetup
{
    private const string Tag = "[VelvetMenuAudioSetup]";
    private const string MusicPath = "Assets/Audio/Music/Glimmer of Dawn.mp3";
    private const string SettingsScene = "Assets/Scenes/SettingsMenu.unity";
    private const string SliderName = "VolumeSlider";
    private const float MusicVolume = 0.6f;

    private static readonly string[] MenuScenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/SettingsMenu.unity",
        "Assets/Scenes/Credits.unity",
        "Assets/Scenes/CharacterMenu.unity",
    };

    [MenuItem("Tools/Velvet/Menus/Setup Menu Music and Volume")]
    public static void Setup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AudioClip music = ImportMusic();
        if (music == null)
        {
            Debug.LogError($"{Tag} Missing {MusicPath}");
            return;
        }

        string current = EditorSceneManager.GetActiveScene().path;
        foreach (string path in MenuScenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            PlaceMusic(music);
            if (path == SettingsScene) WireSlider();
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
        Debug.Log($"{Tag} Done");
    }

    private static AudioClip ImportMusic()
    {
        if (AssetImporter.GetAtPath(MusicPath) is not AudioImporter importer)
            return null;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        if (settings.loadType != AudioClipLoadType.Streaming || settings.compressionFormat != AudioCompressionFormat.Vorbis)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
    }

    private static void PlaceMusic(AudioClip music)
    {
        MenuMusic existing = Object.FindFirstObjectByType<MenuMusic>(FindObjectsInactive.Include);
        GameObject go = existing != null ? existing.gameObject : new GameObject("Menu Music", typeof(AudioSource), typeof(MenuMusic));
        var source = go.GetComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.volume = MusicVolume;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    private static void WireSlider()
    {
        foreach (Slider slider in Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (slider.name != SliderName) continue;
            if (!slider.TryGetComponent(out VolumeSlider _)) slider.gameObject.AddComponent<VolumeSlider>();
            slider.value = VolumeRules.DefaultPercent;
            EditorSceneManager.MarkSceneDirty(slider.gameObject.scene);
            return;
        }

        Debug.LogWarning($"{Tag} No \"{SliderName}\" in {SettingsScene}");
    }
}
#endif
