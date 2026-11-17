using System;
using System.Numerics;
using System.Text.Json.Nodes;
using CsToBeamNG.Converter.BeamNG.Scene;

namespace CsToBeamNG.Converter.BeamNG.Level;

/// <summary>
/// The environment objects every level needs: <c>theLevelInfo</c>, the time of day and the sky. The values are
/// those of the official template level in BeamNG.drive 0.40.5 (<c>level_objects/sky_and_sun</c>), which reference
/// only the game's own shared files.
/// </summary>
/// <remarks>
/// The game looks these objects up by name: <c>theLevelInfo</c>, <c>tod</c> and <c>sunsky</c>
/// (docs/research/beamng-level-format.md, Scene objects), so the names are fixed.
/// </remarks>
public static class LevelEnvironment
{
    /// <summary>The name the game looks the level settings up by.</summary>
    public const string LevelInfoName = "theLevelInfo";

    /// <summary>The name the game looks the time of day up by.</summary>
    public const string TimeOfDayName = "tod";

    /// <summary>The name the game looks the sky up by.</summary>
    public const string SkyName = "sunsky";

    /// <summary>The level settings: fog, gravity, view distance and the environment map.</summary>
    /// <param name="key">The object's key.</param>
    /// <param name="visibleDistance">How far the camera sees, in metres.</param>
    /// <returns>The <c>LevelInfo</c> object.</returns>
    public static GenericSceneObject CreateLevelInfo(string key, float visibleDistance)
    {
        var levelInfo = new GenericSceneObject("LevelInfo") { Key = key, Name = LevelInfoName };
        levelInfo.Fields.Set("canvasClearColor", "black");
        levelInfo.Fields.Set("fogAtmosphereHeight", 1600f);
        levelInfo.Fields.Set("fogColor", BeamNGJson.Floats(0.741176009f, 0.815685987f, 0.925490022f, 1));
        levelInfo.Fields.Set("fogDensity", 4.99999987e-05f);
        levelInfo.Fields.Set("globalEnviromentMap", "BNG_Sky_02_cubemap");
        levelInfo.Fields.Set("gravity", -9.81000042f);
        levelInfo.Fields.Set("visibleDistance", visibleDistance);
        return levelInfo;
    }

    /// <summary>The time of day, not running.</summary>
    /// <param name="key">The object's key.</param>
    /// <param name="time">The time as a fraction of a day starting at noon: 0 is 12:00, 0.25 is 18:00, 0.5 is midnight.</param>
    /// <returns>The <c>TimeOfDay</c> object.</returns>
    public static GenericSceneObject CreateTimeOfDay(string key, float time)
    {
        if (!(time >= 0 && time < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "The time is a fraction of a day, from 0 to below 1.");
        }

        var timeOfDay = new GenericSceneObject("TimeOfDay") { Key = key, Name = TimeOfDayName, Position = Vector3.Zero };
        timeOfDay.Fields.Set("axisTilt", -20f);
        timeOfDay.Fields.Set("play", false);
        timeOfDay.Fields.Set("startTime", time);
        timeOfDay.Fields.Set("time", time);
        return timeOfDay;
    }

    /// <summary>The sky and sun, without the moon and stars details.</summary>
    /// <param name="key">The object's key.</param>
    /// <returns>The <c>ScatterSky</c> object.</returns>
    public static GenericSceneObject CreateSky(string key)
    {
        var sky = new GenericSceneObject("ScatterSky") { Key = key, Name = SkyName, Position = Vector3.Zero };
        SceneFields fields = sky.Fields;
        fields.Set("ambientScale", Color(1, 0.894117653f, 0.78039217f));
        fields.Set("ambientScaleGradientFile", "art/sky_gradients/default/gradient_ambient.png");
        fields.Set("colorize", Color(0.215686277f, 0.349019617f, 0.603921592f));
        fields.Set("colorizeGradientFile", "art/sky_gradients/default/gradient_colorize.png");
        fields.Set("enableFogFallBack", false);
        fields.Set("flareScale", 5f);
        fields.Set("flareType", "BNG_Sunflare_3");
        fields.Set("fogScale", Color(0.396078438f, 0.666666687f, 1));
        fields.Set("fogScaleGradientFile", "art/sky_gradients/default/gradient_fog.png");
        fields.Set("mieScattering", 0.00026087396f);
        fields.Set("moonMat", "Moon_Glow_Mat");
        fields.Set("nightColor", Color(0, 0, 0));
        fields.Set("nightCubemap", "nightCubemap");
        fields.Set("nightFogColor", Color(0, 0, 0));
        fields.Set("nightFogGradientFile", "art/sky_gradients/default/gradient_fog.png");
        fields.Set("nightGradientFile", "art/sky_gradients/default/gradient_ambient.png");
        fields.Set("occlusionScale", 0.0250000004f);
        fields.Set("shadowDarkenColor", BeamNGJson.Floats(0, 0, 0, 0));
        fields.Set("skyBrightness", 40f);
        fields.Set("sunScale", Color(0.996078432f, 0.831372559f, 0.729411781f));
        fields.Set("sunScaleGradientFile", "art/sky_gradients/default/gradient_sunscale.png");
        return sky;
    }

    private static JsonArray Color(float red, float green, float blue) => BeamNGJson.Floats(red, green, blue, 1);
}
