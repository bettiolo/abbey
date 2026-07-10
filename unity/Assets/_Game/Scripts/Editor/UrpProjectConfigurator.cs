using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abbey.EditorTools
{
    /// <summary>
    /// Reproducibly creates and activates Abbey's URP settings. The committed
    /// assets are authoritative; this menu command repairs a missing local setup.
    /// </summary>
    public static class UrpProjectConfigurator
    {
        public const string SettingsFolder = "Assets/_Game/Settings/Rendering";
        public const string PipelineAssetPath = SettingsFolder + "/AbbeyUniversalRenderPipeline.asset";
        public const string RendererAssetPath = SettingsFolder + "/AbbeyUniversalRenderer.asset";

        [MenuItem("Tools/Abbey/Configure Universal Render Pipeline")]
        public static void Configure()
        {
            EnsureFolder(SettingsFolder);
            var pipeline = LoadOrCreatePipeline();
            ConfigurePipeline(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                throw new InvalidOperationException("Unity did not activate the Abbey URP asset.");
            }
            Debug.Log($"Abbey URP configured: {PipelineAssetPath}");
        }

        static UniversalRenderPipelineAsset LoadOrCreatePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create();
                pipeline.name = "Abbey Universal Render Pipeline";
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }
            EnsureRendererData(pipeline);
            return pipeline;
        }

        static void EnsureRendererData(UniversalRenderPipelineAsset pipeline)
        {
            var rendererData = pipeline.rendererDataList[0] as UniversalRendererData;
            if (rendererData != null
                && rendererData.postProcessData != null
                && AssetDatabase.GetAssetPath(rendererData) == RendererAssetPath)
            {
                return;
            }

            const string generatedRendererPath = "Assets/UniversalRenderer.asset";
            AssetDatabase.DeleteAsset(RendererAssetPath);
            AssetDatabase.DeleteAsset(generatedRendererPath);
            rendererData = pipeline.LoadBuiltinRendererData() as UniversalRendererData;
            if (rendererData == null)
            {
                throw new InvalidOperationException("Unity could not create the Abbey URP renderer data.");
            }
            string error = AssetDatabase.MoveAsset(generatedRendererPath, RendererAssetPath);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Could not move the Abbey URP renderer: {error}");
            }
            rendererData.name = "Abbey Universal Renderer";
            EditorUtility.SetDirty(rendererData);
        }

        static void ConfigurePipeline(UniversalRenderPipelineAsset pipeline)
        {
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = 60f;
            pipeline.shadowCascadeCount = 2;
            pipeline.useSRPBatcher = true;
        }

        static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring("Assets/".Length).Split('/'))
            {
                string next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }
                current = next;
            }
        }
    }
}
