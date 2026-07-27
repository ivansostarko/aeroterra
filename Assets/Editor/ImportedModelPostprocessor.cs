using UnityEditor;
using UnityEngine;

namespace AeroTerra.EditorTools
{
    /// <summary>
    /// Configures import settings for the one imported (non-procedural) drone model
    /// in the project — Assets/Resources/Models/AT-H12/drone.fbx — automatically on
    /// first import, so nobody has to hand-tune it in the Inspector. Scoped to that
    /// one path: everything else in the project stays hand-authored/procedural, per
    /// the project's usual convention (see CLAUDE.md "Repo shape").
    /// </summary>
    public class ImportedModelPostprocessor : AssetPostprocessor
    {
        private const string ImportedModelsFolder = "Assets/Resources/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ImportedModelsFolder)) return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false;
            importer.importLights = false;
            importer.generateAnimations = ModelImporterGenerateAnimations.GenerateAnimations;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.addCollider = false; // DroneFactory adds its own BoxCollider sized from spec
        }
    }
}
