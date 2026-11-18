using System.Text.Json.Nodes;
using CsToBeamNG.Converter.BeamNG.Scene;

namespace CsToBeamNG.Converter.BeamNG.Level;

/// <summary>
/// Looks for the level's water that use only textures every install has: the shared water textures in
/// <c>/assets/materials/tileable/water/</c> and the global <c>cubemap_ocean_reflection</c>. The values follow official
/// levels of BeamNG.drive 0.40.5: the sea those of Italy's ocean (<c>WaterPlane</c> <c>Ocean1</c>), lakes those of its
/// quarry ponds (<c>WaterBlock</c>), with a clearer depth colour (docs/research/beamng-level-format.md, Water).
/// </summary>
public static class WaterStyles
{
    /// <summary>The reflection cubemap; the only water reflection the game defines globally.</summary>
    public const string ReflectionCubemap = "cubemap_ocean_reflection";

    private const string Textures = "/assets/materials/tileable/water/";

    /// <summary>The open sea: deep blue-green, gentle waves.</summary>
    /// <returns>A new style.</returns>
    public static WaterStyle Sea()
    {
        var style = new WaterStyle();
        SceneFields fields = style.Fields;
        fields.Set("Foam", new JsonArray(Foam(0.5f, 1, 1, 0.02f, 2), Foam(-1, 0.5f, 1, 0.03f, 2)));
        fields.Set("Ripples (texture animation)", new JsonArray(
            Ripple(-1, -1, 0.2f, 0.0025001f, 200), Ripple(1, -1, 0.5f, 0.015f, 10), Ripple(-1, 1, 0.45f, 0.05f, 2)));
        fields.Set("Waves (vertex undulation)", new JsonArray(
            Wave(0, -1, 0.8f, 2), Wave(0.3f, -1.2f, 0.5f, -1.2f), Wave(-0.3f, -0.8f, 0.7f, 2)));
        fields.Set("baseColor", new JsonArray(18, 93, 131, 255));
        fields.Set("clarity", 0f);
        fields.Set("cubemap", ReflectionCubemap);
        fields.Set("depthGradientMax", 100f);
        fields.Set("depthGradientTex", Textures + "depthcolor_ramp/depthcolor_ramp_italy_ocean2_b.png");
        fields.Set("distortEndDist", 10f);
        fields.Set("distortFullDepth", 5.5f);
        fields.Set("foamAmbientLerp", 1.3f);
        fields.Set("foamMaxDepth", 0.5f);
        fields.Set("foamRippleInfluence", 0.015f);
        fields.Set("foamTex", Textures + "water_effects/foam2_b.color.dds");
        fields.Set("fresnelBias", 0.1f);
        fields.Set("fresnelPower", 2.5f);
        fields.Set("fullReflect", false);
        fields.Set("gridElementSize", 0.8f);
        fields.Set("gridSize", 200);
        fields.Set("overallFoamOpacity", 0.65f);
        fields.Set("overallRippleMagnitude", 0.75f);
        fields.Set("overallWaveMagnitude", 0.04f);
        fields.Set("reflectDetailAdjust", -1f);
        fields.Set("reflectivity", 0.3f);
        fields.Set("rippleTex", Textures + "water_effects/ripple4_nm.normal.dds");
        fields.Set("specularPower", 200f);
        fields.Set("underwaterColor", new JsonArray(42, 78, 116, 253));
        fields.Set("viscosity", 0.001f);
        fields.Set("waterFogDensity", 0.4f);
        fields.Set("waterFogDensityOffset", 3f);
        fields.Set("wetDarkening", 0.3f);
        fields.Set("wetDepth", 0.5f);
        return style;
    }

    /// <summary>A lake: clear, calm water without waves.</summary>
    /// <returns>A new style.</returns>
    public static WaterStyle Lake()
    {
        var style = new WaterStyle();
        SceneFields fields = style.Fields;
        fields.Set("Ripples (texture animation)", new JsonArray(
            Ripple(0, 1, 0.8f, 0.001f, 12), Ripple(0, 1, null, 0.02f, 6), Ripple(0.7f, -0.7f, 1, 0.02f, 3)));
        fields.Set("Waves (vertex undulation)", new JsonArray(
            Wave(0, 1, 0.2f, 1), Wave(0.707f, 0.707f, 0.2f, 1), Wave(0.5f, 0.86f, 0.2f, 1)));
        fields.Set("baseColor", new JsonArray(189, 253, 255, 255));
        fields.Set("cubemap", ReflectionCubemap);
        fields.Set("depthGradientMax", 30f);
        fields.Set("depthGradientTex", Textures + "depthcolor_ramp/depthcolor_ramp_italy_ocean_b.png");
        fields.Set("foamAmbientLerp", 1.3f);
        fields.Set("foamMaxDepth", 0.15f);
        fields.Set("foamRippleInfluence", 0.015f);
        fields.Set("foamTex", Textures + "water_effects/foam2_b.color.dds");
        fields.Set("fresnelBias", 0.2f);
        fields.Set("fresnelPower", 20f);
        fields.Set("fullReflect", false);
        fields.Set("overallRippleMagnitude", 0.2f);
        fields.Set("overallWaveMagnitude", 0f);
        fields.Set("reflectivity", 0.8f);
        fields.Set("rippleTex", Textures + "water_effects/ripple_nm.normal.dds");
        fields.Set("specularPower", 200f);
        fields.Set("waterFogDensity", 1f);
        fields.Set("waterFogDensityOffset", 0.1f);
        fields.Set("wetDarkening", 0.5f);
        fields.Set("wetDepth", 0.2f);
        return style;
    }

    private static JsonObject Foam(float directionX, float directionY, float opacity, float speed, float textureScale) => new()
    {
        ["foamDir"] = BeamNGJson.Floats(directionX, directionY),
        ["foamOpacity"] = BeamNGJson.ToJson(opacity),
        ["foamSpeed"] = BeamNGJson.ToJson(speed),
        ["foamTexScale"] = BeamNGJson.Floats(textureScale, textureScale),
    };

    private static JsonObject Ripple(float directionX, float directionY, float? magnitude, float speed, float textureScale)
    {
        var ripple = new JsonObject { ["rippleDir"] = BeamNGJson.Floats(directionX, directionY) };
        if (magnitude is float value)
        {
            ripple["rippleMagnitude"] = BeamNGJson.ToJson(value);
        }

        ripple["rippleSpeed"] = BeamNGJson.ToJson(speed);
        ripple["rippleTexScale"] = BeamNGJson.Floats(textureScale, textureScale);
        return ripple;
    }

    private static JsonObject Wave(float directionX, float directionY, float magnitude, float speed) => new()
    {
        ["waveDir"] = BeamNGJson.Floats(directionX, directionY),
        ["waveMagnitude"] = BeamNGJson.ToJson(magnitude),
        ["waveSpeed"] = BeamNGJson.ToJson(speed),
    };
}
