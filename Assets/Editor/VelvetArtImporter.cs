#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the hand-drawn art in Assets/Art (prepared by Tools/prepare_art.py), applied the first time a
/// file is imported only, so later changes in the Inspector are kept:
/// <list type="bullet">
/// <item>UI: sprite, centered, no mipmaps, up to 512 px.</item>
/// <item>Items and Environment: sprite with the pivot at the bottom center (the drawing's base, since the script
/// trims the empty margin), mipmaps (seen small and far), up to 1024 px.</item>
/// </list>
/// </summary>
public class VelvetArtImporter : AssetPostprocessor
{
    private const string Root = "Assets/Art/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root) || !assetImporter.importSettingsMissing)
            return;

        var importer = (TextureImporter)assetImporter;
        bool isUi = assetPath.StartsWith(Root + "UI/");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = !isUi;
        importer.maxTextureSize = isUi ? 512 : 1024;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Compressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)(isUi ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
#endif
