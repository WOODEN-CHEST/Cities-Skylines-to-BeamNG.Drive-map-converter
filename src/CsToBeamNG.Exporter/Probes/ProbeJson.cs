using System.Collections.Generic;
using CsToBeamNG.Dump.Json;
using UnityEngine;

namespace CsToBeamNG.Exporter.Probes;

/// <summary>Writes game values into a probe's JSON: vectors and matrices as number arrays, meshes as objects of arrays.</summary>
internal static class ProbeJson
{
    public static void WriteVector(JsonWriter json, string name, Vector2 value)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(value.x);
        json.WriteNumberValue(value.y);
        json.WriteEndArray();
    }

    public static void WriteVector(JsonWriter json, string name, Vector3 value)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(value.x);
        json.WriteNumberValue(value.y);
        json.WriteNumberValue(value.z);
        json.WriteEndArray();
    }

    public static void WriteVector(JsonWriter json, string name, Vector4 value)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue(value.x);
        json.WriteNumberValue(value.y);
        json.WriteNumberValue(value.z);
        json.WriteNumberValue(value.w);
        json.WriteEndArray();
    }

    /// <summary>Writes a matrix row by row: m00, m01, m02, m03, m10, ... m33.</summary>
    public static void WriteMatrix(JsonWriter json, string name, Matrix4x4 value)
    {
        json.WriteStartArray(name);
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                json.WriteNumberValue(value[row, column]);
            }
        }

        json.WriteEndArray();
    }

    public static void WriteVectors(JsonWriter json, string name, Vector2[]? values, int count)
    {
        if (values is null)
        {
            return;
        }

        json.WriteStartArray(name);
        for (int i = 0; i < count && i < values.Length; i++)
        {
            json.WriteNumberValue(values[i].x);
            json.WriteNumberValue(values[i].y);
        }

        json.WriteEndArray();
    }

    public static void WriteVectors(JsonWriter json, string name, Vector3[]? values, int count)
    {
        if (values is null)
        {
            return;
        }

        json.WriteStartArray(name);
        for (int i = 0; i < count && i < values.Length; i++)
        {
            json.WriteNumberValue(values[i].x);
            json.WriteNumberValue(values[i].y);
            json.WriteNumberValue(values[i].z);
        }

        json.WriteEndArray();
    }

    public static void WriteVectors(JsonWriter json, string name, Vector4[]? values, int count)
    {
        if (values is null)
        {
            return;
        }

        json.WriteStartArray(name);
        for (int i = 0; i < count && i < values.Length; i++)
        {
            json.WriteNumberValue(values[i].x);
            json.WriteNumberValue(values[i].y);
            json.WriteNumberValue(values[i].z);
            json.WriteNumberValue(values[i].w);
        }

        json.WriteEndArray();
    }

    public static void WriteColors(JsonWriter json, string name, Color32[]? values, int count)
    {
        if (values is null)
        {
            return;
        }

        json.WriteStartArray(name);
        for (int i = 0; i < count && i < values.Length; i++)
        {
            json.WriteNumberValue(values[i].r);
            json.WriteNumberValue(values[i].g);
            json.WriteNumberValue(values[i].b);
            json.WriteNumberValue(values[i].a);
        }

        json.WriteEndArray();
    }

    public static void WriteIndices(JsonWriter json, string name, int[]? values, int count)
    {
        if (values is null)
        {
            return;
        }

        json.WriteStartArray(name);
        for (int i = 0; i < count && i < values.Length; i++)
        {
            json.WriteNumberValue(values[i]);
        }

        json.WriteEndArray();
    }

    /// <summary>Writes the first vertices and triangle indices of render-group mesh data.</summary>
    public static void WriteMeshData(JsonWriter json, RenderGroup.MeshData data, int vertexCount, int triangleCount)
    {
        json.WriteNumber("vertexCount", vertexCount);
        json.WriteNumber("triangleIndexCount", triangleCount);
        WriteVectors(json, "vertices", data.m_vertices, vertexCount);
        WriteVectors(json, "normals", data.m_normals, vertexCount);
        WriteVectors(json, "tangents", data.m_tangents, vertexCount);
        WriteVectors(json, "uvs", data.m_uvs, vertexCount);
        WriteVectors(json, "uvs2", data.m_uvs2, vertexCount);
        WriteVectors(json, "uvs3", data.m_uvs3, vertexCount);
        WriteVectors(json, "uvs4", data.m_uvs4, vertexCount);
        WriteColors(json, "colors", data.m_colors, vertexCount);
        WriteIndices(json, "triangles", data.m_triangles, triangleCount);
    }

    /// <summary>
    /// Writes a Unity mesh: its readability and, if the CPU can read it, its vertex data and the triangles of
    /// each submesh. Reading a mesh that isn't readable would only log errors.
    /// </summary>
    public static void WriteUnityMesh(JsonWriter json, Mesh mesh)
    {
        json.WriteString("name", mesh.name);
        json.WriteBoolean("readable", mesh.isReadable);
        json.WriteNumber("vertexCount", mesh.vertexCount);
        json.WriteNumber("subMeshCount", mesh.subMeshCount);
        WriteVector(json, "boundsCenter", mesh.bounds.center);
        WriteVector(json, "boundsSize", mesh.bounds.size);
        if (!mesh.isReadable)
        {
            return;
        }

        int count = mesh.vertexCount;
        WriteVectors(json, "vertices", mesh.vertices, count);
        WriteVectors(json, "normals", mesh.normals, count);
        WriteVectors(json, "tangents", mesh.tangents, count);
        WriteVectors(json, "uvs", mesh.uv, count);
        WriteColors(json, "colors", mesh.colors32, count);
        json.WriteStartArray("submeshes");
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int[] triangles = mesh.GetTriangles(i);
            json.WriteStartObject();
            WriteIndices(json, "triangles", triangles, triangles.Length);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }
}

/// <summary>Gives each mesh a probe a number once, so that shared meshes are written once.</summary>
internal sealed class ProbeMeshRegistry
{
    private readonly Dictionary<object, int> _ids = new();
    private readonly List<object> _meshes = new();
    private readonly List<string> _roles = new();

    public IList<object> Meshes => _meshes;

    public IList<string> Roles => _roles;

    /// <summary>The mesh's number, or -1 for none.</summary>
    /// <param name="mesh">A <see cref="Mesh"/> or <see cref="RenderGroup.MeshData"/>.</param>
    /// <param name="role">What the mesh is for, written with it.</param>
    public int Register(object? mesh, string role)
    {
        if (mesh is null || (mesh is Mesh unityMesh && unityMesh == null))
        {
            return -1;
        }

        if (_ids.TryGetValue(mesh, out int id))
        {
            return id;
        }

        id = _meshes.Count;
        _ids.Add(mesh, id);
        _meshes.Add(mesh);
        _roles.Add(role);
        return id;
    }
}
