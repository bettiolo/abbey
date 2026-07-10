using Abbey.Rendering;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Abbey.Tests.EditMode
{
    public class RenderPipelineConfigurationTests
    {
        [Test]
        public void ProjectUsesUniversalRenderPipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;

            Assert.IsNotNull(pipeline);
            Assert.AreEqual(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                pipeline.GetType().FullName);
        }

        [Test]
        public void MaterialFactoryUsesUrpLitShader()
        {
            var material = AbbeyMaterialFactory.CreateLit("test", Color.magenta);
            try
            {
                Assert.AreEqual(AbbeyMaterialFactory.UrpLitShaderName, material.shader.name);
                Assert.IsTrue(material.HasProperty("_BaseColor"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MaterialFactoryUsesUrpUnlitForSelfLuminousMarkers()
        {
            var material = AbbeyMaterialFactory.CreateUnlit("test", Color.cyan);
            try
            {
                Assert.AreEqual(AbbeyMaterialFactory.UrpUnlitShaderName, material.shader.name);
                Assert.IsTrue(material.HasProperty("_BaseColor"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MaterialFactoryWiresImportedNormalMaps()
        {
            var normal = new Texture2D(2, 2);
            var material = AbbeyMaterialFactory.CreateLit(
                "normal-test", Color.white, normalMap: normal);
            try
            {
                Assert.AreSame(normal, material.GetTexture("_BumpMap"));
                Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(normal);
            }
        }

        [Test]
        public void PlaceholderNormalMapsUseNormalTextureImportMode()
        {
            string[] paths =
            {
                "Assets/_Game/Art/Placeholders/Materials/abbey_placeholder_ground_grass_normal_gl.jpg",
                "Assets/_Game/Art/Placeholders/Materials/abbey_placeholder_beach_sand_normal_gl.jpg",
                "Assets/_Game/Art/Placeholders/Materials/abbey_placeholder_abbey_stone_normal_gl.jpg",
                "Assets/_Game/Art/Placeholders/Materials/abbey_placeholder_weathered_wood_normal_gl.jpg"
            };

            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType, path);
                Assert.IsFalse(importer.sRGBTexture, path);
            }
        }
    }
}
