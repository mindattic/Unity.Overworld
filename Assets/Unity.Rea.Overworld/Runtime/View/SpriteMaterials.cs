using UnityEngine;

namespace MindAttic.Overworld.Runtime
{
    /// <summary>
    /// The pipeline-agnostic layer (U.OVW-T45): the only code that knows which shader the world's quads draw with.
    /// Every presenter asks here for a material instead of naming a shader, so changing the render pipeline (URP now,
    /// see <c>Editor/RenderPipelineBuilder</c>) touches this class and the shader, nothing else. The shader lives under a
    /// Resources folder, so every player build carries it.
    /// </summary>
    public static class SpriteMaterials
    {
        /// <summary>The shader's name (Rendering/Resources/Overworld/Sprite.shader).</summary>
        public const string ShaderName = "MindAttic/Overworld/Sprite";

        /// <summary>Its Resources path.</summary>
        public const string ResourcePath = "Overworld/Sprite";

        static Shader shader;

        /// <summary>The sprite shader: unlit texel x vertex colour x material colour, source-alpha blended, no culling, no depth writes.</summary>
        public static Shader Shader
        {
            get
            {
                if (shader == null) shader = Resources.Load<Shader>(ResourcePath);
                if (shader == null) shader = Shader.Find(ShaderName);
                return shader;
            }
        }

        /// <summary>A new material on the sprite shader, named <paramref name="name"/>, in white light.</summary>
        public static Material Create(string name) => new Material(Shader) { name = name, color = Color.white };
    }
}
