using System.Numerics;

namespace CsToBeamNG.Converter.BeamNG.Terrain;

/// <summary>
/// Terrain materials made only of textures that every install has, so a level needs no texture files of its own.
/// The blend settings follow the official template level's materials of the same kind (BeamNG.drive 0.40.5).
/// </summary>
/// <remarks>
/// <para>
/// The macro and detail layers use the game's shared textures in <c>/assets/materials/terrain/</c> (1024 × 1024
/// pixels). The base layer needs baked, nearly uniform maps: the game draws the material's overall colour from it,
/// and the macro and detail layers only modulate that. Every official level bakes its own; macro textures in the
/// base slot render grey or magenta (verified in 0.40.5; docs/research/beamng-level-format.md, Terrain materials).
/// These presets therefore borrow the official template level's base maps (512 × 512) by their absolute virtual
/// paths, as the template itself borrows meshes from another level.
/// </para>
/// <para>Use <see cref="TextureSet"/>, whose sizes fit these textures.</para>
/// </remarks>
public static class TerrainMaterialPresets
{
    /// <summary>The pixel size of the template level's base textures.</summary>
    public const int BaseTextureSize = 512;

    /// <summary>The pixel size of the shared macro and detail textures.</summary>
    public const int SharedTextureSize = 1024;

    private const string SharedTextures = "/assets/materials/terrain/";
    private const string TemplateBaseTextures = "/levels/template/art/terrains/t_terrain_base_";

    /// <summary>Short grass.</summary>
    /// <param name="internalName">The name the <c>.ter</c> file uses.</param>
    /// <returns>The material.</returns>
    public static TerrainMaterial Grass(string internalName) => new()
    {
        InternalName = internalName,
        GroundModelName = "GRASS",
        Base = TemplateBaseLayer("grass", textureSize: 512),
        Macro = SharedLayer("soil/macro_clumply/t_macro_clumpy", textureSize: 64) with
        {
            BaseColor = SharedMap("soil/macro_clumply/t_macro_clumpy_b", new(0.05f, 0.12f)),
            Normal = SharedMap("soil/macro_clumply/t_macro_clumpy_nm", new(0.2f, 0.4f)),
            Roughness = SharedMap("soil/macro_clumply/t_macro_clumpy_r", new(0.15f, 0.8f)),
        },
        Detail = SharedLayer("grass/ind_grass/t_ind_grass", textureSize: null) with
        {
            BaseColor = SharedMap("grass/ind_grass/t_ind_grass_b", new(0.25f, 0.25f)),
            Normal = SharedMap("grass/ind_grass/t_ind_grass_nm", new(0.7f, 0.15f)),
            Roughness = SharedMap("grass/ind_grass/t_ind_grass_r", new(0.3f, 0.3f)),
        },
        MacroDistances = new Vector4(0, 10, 100, 3000),
    };

    /// <summary>Asphalt.</summary>
    /// <param name="internalName">The name the <c>.ter</c> file uses.</param>
    /// <returns>The material.</returns>
    public static TerrainMaterial Asphalt(string internalName) => new()
    {
        InternalName = internalName,
        GroundModelName = "ASPHALT",
        Annotation = "ASPHALT",
        Base = TemplateBaseLayer("asphalt", textureSize: 150),
        Macro = SharedLayer("asphalt/macro_asphalt/t_macro_asphalt", textureSize: 80) with
        {
            AmbientOcclusion = SharedMap("asphalt/macro_asphalt/t_macro_asphalt_ao", new(1, 0.5f)),
            BaseColor = SharedMap("asphalt/macro_asphalt/t_macro_asphalt_b", new(0.2f, 0.1f)),
            Normal = SharedMap("asphalt/macro_asphalt/t_macro_asphalt_nm", new(0.6f, 0.6f)),
            Roughness = SharedMap("asphalt/macro_asphalt/t_macro_asphalt_r", new(0.7f, 1)),
        },
        Detail = SharedLayer("asphalt/t_asphalt_02/t_asphalt_02", textureSize: null) with
        {
            AmbientOcclusion = SharedMap("asphalt/t_asphalt_02/t_asphalt_02_ao", new(1, 0)),
            BaseColor = SharedMap("asphalt/t_asphalt_02/t_asphalt_02_b", new(0.3f, 0)),
            Normal = SharedMap("asphalt/t_asphalt_02/t_asphalt_02_nm", new(0.6f, 0.2f)),
            Roughness = SharedMap("asphalt/t_asphalt_02/t_asphalt_02_r", new(1, 0)),
        },
        MacroDistances = new Vector4(0, 10, 100, 3000),
        DetailDistances = new Vector4(0, 0, 30, 50),
    };

    /// <summary>A texture set that fits these presets.</summary>
    /// <param name="name">The set's name.</param>
    /// <returns>The texture set.</returns>
    public static TerrainMaterialTextureSet TextureSet(string name) => new(name, BaseTextureSize, SharedTextureSize, SharedTextureSize);

    // The template level's baked base maps of a surface kind: t_terrain_base_<kind>_b, _nm, _r, _ao and _h.
    private static TerrainMaterialLayer TemplateBaseLayer(string kind, float textureSize) => new()
    {
        TextureSize = textureSize,
        BaseColor = Map(TemplateBaseTextures + kind + "_b", strength: null),
        Normal = Map(TemplateBaseTextures + kind + "_nm", strength: null),
        Roughness = Map(TemplateBaseTextures + kind + "_r", strength: null),
        AmbientOcclusion = Map(TemplateBaseTextures + kind + "_ao", strength: null),
        Height = Map(TemplateBaseTextures + kind + "_h", strength: null),
    };

    // All five maps of a shared texture family named <prefix>_b, _nm, _r, _ao and _h, without strengths.
    private static TerrainMaterialLayer SharedLayer(string prefix, float? textureSize) => new()
    {
        TextureSize = textureSize,
        BaseColor = SharedMap(prefix + "_b", strength: null),
        Normal = SharedMap(prefix + "_nm", strength: null),
        Roughness = SharedMap(prefix + "_r", strength: null),
        AmbientOcclusion = SharedMap(prefix + "_ao", strength: null),
        Height = SharedMap(prefix + "_h", strength: null),
    };

    private static TerrainMaterialMap SharedMap(string texture, Vector2? strength) => Map(SharedTextures + texture, strength);

    private static TerrainMaterialMap Map(string texture, Vector2? strength) =>
        new(VirtualPath.Parse(texture + ".png")) { Strength = strength };
}
