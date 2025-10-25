using System.Collections.Generic;
using UnityEngine;

public static class MaterialsUtil
{
    private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

    public static Material GetOrCreate(string name, Color color)
    {
        if (Cache.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Diffuse");
        }

        var mat = new Material(shader)
        {
            name = name,
            color = color
        };

        Cache[name] = mat;
        return mat;
    }
}
