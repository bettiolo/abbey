using System;
using UnityEngine;

namespace Abbey.Rendering
{
    /// <summary>
    /// Creates the small set of transient materials used by generated scenes and
    /// runtime proof props. URP Lit is authoritative; legacy shaders remain only
    /// as a defensive fallback for opening an old checkout before packages resolve.
    /// </summary>
    public static class AbbeyMaterialFactory
    {
        public const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        public const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

        public static Shader FindLitShader()
        {
            return Shader.Find(UrpLitShaderName)
                ?? Shader.Find("Standard")
                ?? Shader.Find("Legacy Shaders/Diffuse");
        }

        public static Material CreateLit(
            string name,
            Color color,
            Texture texture = null,
            Vector2? textureScale = null,
            float smoothness = 0.12f)
        {
            var shader = FindLitShader();
            if (shader == null)
            {
                throw new InvalidOperationException("No supported Abbey lit shader is available.");
            }

            var material = new Material(shader) { name = name };
            SetBaseColor(material, texture != null ? Color.white : color);
            SetBaseTexture(material, texture, textureScale ?? Vector2.one);

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            return material;
        }

        public static Material CreateUnlit(string name, Color color)
        {
            var shader = Shader.Find(UrpUnlitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("The Abbey URP Unlit shader is unavailable.");
            }
            var material = new Material(shader) { name = name };
            SetBaseColor(material, color);
            return material;
        }

        static void SetBaseColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        static void SetBaseTexture(Material material, Texture texture, Vector2 scale)
        {
            if (texture == null)
            {
                return;
            }
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", scale);
            }
        }
    }
}
