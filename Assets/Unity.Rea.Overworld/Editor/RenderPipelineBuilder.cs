using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MindAttic.Overworld.Editor
{
    /// <summary>
    /// Builds the game's URP pipeline in code (U.OVW-T45): <see cref="PipelinePath"/> with a Universal (forward) renderer
    /// (<see cref="RendererPath"/>), made the default pipeline and every quality level's. The original drew unlit,
    /// colour-keyed quads with fixed-function blending and no post-processing, so the pipeline is set to match: no HDR,
    /// no MSAA, no shadows, no depth or opaque textures, render scale 1. It only creates what is missing and only assigns
    /// what differs, so rerunning it leaves the committed assets and settings untouched. Runs from the menu, in batch mode
    /// (<c>-executeMethod MindAttic.Overworld.Editor.RenderPipelineBuilder.BuildPipeline</c>) and before every player build
    /// (<see cref="OverworldBuild.BuildPlayer"/>). The shaders the game draws with are chosen only by
    /// <c>Runtime.SpriteMaterials</c>, so nothing else in the project names a pipeline.
    /// </summary>
    public static class RenderPipelineBuilder
    {
        public const string Folder = "Assets/Unity.Rea.Overworld/Rendering";
        public const string PipelinePath = Folder + "/OverworldURP.asset";
        public const string RendererPath = Folder + "/OverworldRenderer.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Overworld/Build Render Pipeline")]
        public static void BuildPipeline()
        {
            Ensure();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>Creates the pipeline and renderer if missing and makes them the active pipeline everywhere.</summary>
        public static UniversalRenderPipelineAsset Ensure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null) pipeline = CreatePipeline();

            if (GraphicsSettings.defaultRenderPipeline != pipeline) GraphicsSettings.defaultRenderPipeline = pipeline;

            // URP writes its MSAA count into the active quality level whenever it renders; matching every level here
            // keeps QualitySettings stable (one sample is antiAliasing 0).
            var current = QualitySettings.GetQualityLevel();
            for (var level = 0; level < QualitySettings.count; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                if (QualitySettings.renderPipeline != pipeline) QualitySettings.renderPipeline = pipeline;
                if (QualitySettings.antiAliasing != 0) QualitySettings.antiAliasing = 0;
            }

            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        static UniversalRenderPipelineAsset CreatePipeline()
        {
            Directory.CreateDirectory(Folder);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                // As URP's own "Universal Renderer" create menu does: the default post-process data, then the package's
                // shaders and materials filled into every empty resource slot.
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                var serialized = new SerializedObject(renderer);
                var post = serialized.FindProperty("postProcessData") ?? serialized.FindProperty("m_PostProcessData");
                if (post != null) post.objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);
            }

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.renderScale = 1f;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 0f;
            var settings = new SerializedObject(pipeline);
            SetBool(settings, "m_MainLightShadowsSupported", false);
            SetBool(settings, "m_AdditionalLightShadowsSupported", false);
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        static void SetBool(SerializedObject o, string name, bool value)
        {
            var p = o.FindProperty(name);
            if (p != null) p.boolValue = value;
        }
    }
}
