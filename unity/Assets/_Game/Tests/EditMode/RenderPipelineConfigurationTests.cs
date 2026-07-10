using Abbey.Rendering;
using NUnit.Framework;
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
    }
}
