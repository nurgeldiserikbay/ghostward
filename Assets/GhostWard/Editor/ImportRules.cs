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
            if (assetPath.Replace('\\', '/').Contains("/Resources/Sfx/"))
            {
                // короткие эффекты: моно и в памяти, SoundBank читает данные, обрезает и выравнивает громкость
                var sfx = (AudioImporter)assetImporter;
                sfx.forceToMono = true;
                var ss = sfx.defaultSampleSettings;
                ss.loadType = AudioClipLoadType.DecompressOnLoad;
                ss.compressionFormat = AudioCompressionFormat.Vorbis;
                ss.quality = 0.6f;
                sfx.defaultSampleSettings = ss;
                return;
            }
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
            // глубина картинок сущностей (рельеф): данные, а не цвет — без sRGB и сжатия
            if (assetPath.Replace('\\', '/').Contains("/Resources/EntityDepth/"))
            {
                var d = (TextureImporter)assetImporter;
                d.textureType = TextureImporterType.Default;
                d.sRGBTexture = false;
                d.mipmapEnabled = false;
                d.wrapMode = TextureWrapMode.Clamp;
                d.textureCompression = TextureImporterCompression.Uncompressed;
                d.alphaSource = TextureImporterAlphaSource.None;
                return;
            }
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
