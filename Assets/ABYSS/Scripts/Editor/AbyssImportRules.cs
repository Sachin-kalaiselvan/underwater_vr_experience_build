using UnityEditor;
using UnityEngine;

namespace Abyss.EditorTools
{
    // import settings for anything dropped into Assets/ABYSS, so nobody has to remember them
    public class AbyssImportRules : AssetPostprocessor
    {
        bool Ours => assetPath.StartsWith("Assets/ABYSS/");

        void OnPreprocessModel()
        {
            if (!Ours) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.None; // we assign our own vertex colour material
            m.animationType = ModelImporterAnimationType.None;
            m.importAnimation = false;
            m.importBlendShapes = false;
            m.importCameras = false;
            m.importLights = false;
            m.isReadable = assetPath.Contains("seafloor"); // terrain needs it for the mesh collider
            m.meshCompression = ModelImporterMeshCompression.Low;
            m.optimizeMeshPolygons = true;
            m.optimizeMeshVertices = true;
            m.importNormals = ModelImporterNormals.Import;
            // scans use normal maps, everything else is vertex colour only
            m.importTangents = assetPath.Contains("/Scanned/") ? ModelImporterTangents.CalculateMikk : ModelImporterTangents.None;
            m.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!Ours) return;
            var t = (TextureImporter)assetImporter;
            if (assetPath.Contains("_normal") || assetPath.Contains("_nor_gl"))
            {
                t.textureType = TextureImporterType.NormalMap;
                t.maxTextureSize = 1024;
            }
            else if (assetPath.Contains("/Scanned/"))
            {
                t.sRGBTexture = true;
                t.maxTextureSize = 1024;
                t.wrapMode = TextureWrapMode.Repeat;
            }
            if (assetPath.Contains("caustics"))
            {
                t.textureType = TextureImporterType.Default;
                t.sRGBTexture = false;
                t.wrapMode = TextureWrapMode.Repeat;
                t.mipmapEnabled = true;
                t.maxTextureSize = 512;
            }
        }

        void OnPreprocessAudio()
        {
            if (!Ours) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = true;
            var s = a.defaultSampleSettings;
            bool loop = assetPath.Contains("_loop");
            s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = loop ? 0.6f : 0.8f;
            a.defaultSampleSettings = s;
        }
    }
}
