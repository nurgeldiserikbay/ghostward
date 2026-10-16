using UnityEditor;
using UnityEngine;

namespace GhostWard.EditorTools
{
    // Настройки импорта для картинок и музыки сущностей (docs/ART_AND_MUSIC.md):
    // музыка играет потоком из сжатого файла, а не распаковывается в память целиком;
    // картинки сжимаются и не больше 1024 по большей стороне.
    public class ImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = true;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.5f;
            s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            importer.defaultSampleSettings = s;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Entities/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;   // игра один раз находит рамку фигуры (Entity.Figure), чтобы ступни стояли на полу
        }
    }
}
