using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField] private float boardSize = 8f;
    [SerializeField] private int nodesPerSide = 10;
    [SerializeField] private float tileThickness = 0.05f;
    [SerializeField] private float stepHeight = 0.1f;
    [SerializeField] private int stepCount = 4;
    [SerializeField] private float homeLaneWidth = 0.7f;
    [SerializeField] private float baseOffset = 1.2f;

    public List<Vector3> Lap { get; private set; } = new List<Vector3>();
    public List<Vector3> RedHome { get; private set; } = new List<Vector3>();
    public List<Vector3> BlueHome { get; private set; } = new List<Vector3>();
    public List<Vector3> RedBaseSpots { get; private set; } = new List<Vector3>();
    public List<Vector3> BlueBaseSpots { get; private set; } = new List<Vector3>();

    public Dictionary<TokenColor, int> EntryIndices { get; private set; } = new Dictionary<TokenColor, int>();

    public float PlatformHeight => tileThickness * 0.5f + stepHeight * stepCount;

    private float TileY => tileThickness * 0.5f;

    public void Build()
    {
        ClearChildren();

        Lap = GenerateLap();
        EntryIndices[TokenColor.Red] = FindClosestIndex(Lap, new Vector3(0f, TileY, -boardSize * 0.5f));
        EntryIndices[TokenColor.Blue] = FindClosestIndex(Lap, new Vector3(0f, TileY, boardSize * 0.5f));

        RedHome = GenerateHomePath(TokenColor.Red);
        BlueHome = GenerateHomePath(TokenColor.Blue);

        RedBaseSpots = GenerateBaseSpots(TokenColor.Red);
        BlueBaseSpots = GenerateBaseSpots(TokenColor.Blue);

        CreateBaseSurface();
        CreateLapTiles();
        CreateHomeTiles(TokenColor.Red, RedHome);
        CreateHomeTiles(TokenColor.Blue, BlueHome);
        CreateCenterPlatform();
        CreateBasePads(TokenColor.Red, RedBaseSpots);
        CreateBasePads(TokenColor.Blue, BlueBaseSpots);
    }

    public int GetEntryIndex(TokenColor color) => EntryIndices[color];

    public Vector3 GetProgressPosition(TokenColor color, int progress)
    {
        if (progress < 0)
        {
            return color == TokenColor.Red ? RedBaseSpots[0] : BlueBaseSpots[0];
        }

        if (progress < Lap.Count)
        {
            int index = (GetEntryIndex(color) + progress) % Lap.Count;
            return Lap[index];
        }

        int homeIndex = progress - Lap.Count;
        var homeList = color == TokenColor.Red ? RedHome : BlueHome;
        homeIndex = Mathf.Clamp(homeIndex, 0, homeList.Count - 1);
        return homeList[homeIndex];
    }

    public int GetHomeCount(TokenColor color) => color == TokenColor.Red ? RedHome.Count : BlueHome.Count;

    public int GetFinishProgress(TokenColor color) => Lap.Count + GetHomeCount(color) - 1;

    public Vector3 GetBasePosition(TokenColor color, int slot)
    {
        var list = color == TokenColor.Red ? RedBaseSpots : BlueBaseSpots;
        slot = Mathf.Clamp(slot, 0, list.Count - 1);
        return list[slot];
    }

    private void CreateBaseSurface()
    {
        var parent = new GameObject("Base").transform;
        parent.SetParent(transform, false);

        int tilesPerAxis = 8;
        float tileSize = boardSize / tilesPerAxis;
        var dark = MaterialsUtil.GetOrCreate("WoodDark", new Color(0.25f, 0.17f, 0.1f));
        var light = MaterialsUtil.GetOrCreate("WoodLight", new Color(0.65f, 0.5f, 0.32f));

        for (int x = 0; x < tilesPerAxis; x++)
        {
            for (int z = 0; z < tilesPerAxis; z++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"BaseTile_{x}_{z}";
                tile.transform.SetParent(parent, false);
                tile.transform.localScale = new Vector3(tileSize, tileThickness, tileSize);
                float posX = (-tilesPerAxis * 0.5f + x + 0.5f) * tileSize;
                float posZ = (-tilesPerAxis * 0.5f + z + 0.5f) * tileSize;
                tile.transform.localPosition = new Vector3(posX, -TileY, posZ);
                var renderer = tile.GetComponent<MeshRenderer>();
                renderer.material = ((x + z) % 2 == 0) ? dark : light;
                Destroy(tile.GetComponent<Collider>());
            }
        }
    }

    private void CreateLapTiles()
    {
        var parent = new GameObject("LapTiles").transform;
        parent.SetParent(transform, false);

        float spacing = boardSize / (nodesPerSide - 1);
        float tileSize = spacing * 0.8f;
        var marble = MaterialsUtil.GetOrCreate("MarbleWhite", new Color(0.9f, 0.9f, 0.95f));

        for (int i = 0; i < Lap.Count; i++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Lap_{i}";
            tile.transform.SetParent(parent, false);
            tile.transform.localScale = new Vector3(tileSize, tileThickness, tileSize);
            tile.transform.localPosition = Lap[i];
            var renderer = tile.GetComponent<MeshRenderer>();
            renderer.material = marble;
            Destroy(tile.GetComponent<Collider>());
        }
    }

    private void CreateHomeTiles(TokenColor color, IReadOnlyList<Vector3> nodes)
    {
        var parent = new GameObject($"{color}Home").transform;
        parent.SetParent(transform, false);

        float spacing = boardSize / (nodesPerSide - 1);
        float tileSize = Mathf.Min(homeLaneWidth, spacing * 0.9f);
        var mat = color == TokenColor.Red
            ? MaterialsUtil.GetOrCreate("TeamRed", new Color(0.8f, 0.1f, 0.1f))
            : MaterialsUtil.GetOrCreate("TeamBlue", new Color(0.1f, 0.3f, 0.9f));

        for (int i = 0; i < nodes.Count; i++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"{color}Home_{i}";
            tile.transform.SetParent(parent, false);
            tile.transform.localScale = new Vector3(tileSize, tileThickness, tileSize);
            tile.transform.localPosition = nodes[i];
            var renderer = tile.GetComponent<MeshRenderer>();
            renderer.material = mat;
            Destroy(tile.GetComponent<Collider>());
        }
    }

    private void CreateBasePads(TokenColor color, IReadOnlyList<Vector3> pads)
    {
        var parent = new GameObject($"{color}Base").transform;
        parent.SetParent(transform, false);
        var mat = color == TokenColor.Red
            ? MaterialsUtil.GetOrCreate("TeamRed", new Color(0.8f, 0.1f, 0.1f))
            : MaterialsUtil.GetOrCreate("TeamBlue", new Color(0.1f, 0.3f, 0.9f));

        for (int i = 0; i < pads.Count; i++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tile.name = $"{color}Base_{i}";
            tile.transform.SetParent(parent, false);
            tile.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
            tile.transform.localPosition = pads[i];
            var renderer = tile.GetComponent<MeshRenderer>();
            renderer.material = mat;
            Destroy(tile.GetComponent<Collider>());
        }
    }

    private void CreateCenterPlatform()
    {
        var parent = new GameObject("CenterPlatform").transform;
        parent.SetParent(transform, false);

        var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        platform.transform.SetParent(parent, false);
        platform.transform.localScale = new Vector3(2.2f, stepHeight * stepCount * 0.5f, 2.2f);
        platform.transform.localPosition = new Vector3(0f, PlatformHeight * 0.5f, 0f);
        var platformRenderer = platform.GetComponent<MeshRenderer>();
        platformRenderer.material = MaterialsUtil.GetOrCreate("MarbleWhite", new Color(0.9f, 0.9f, 0.95f));
        Destroy(platform.GetComponent<Collider>());

        var lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lip.transform.SetParent(parent, false);
        lip.transform.localScale = new Vector3(2.3f, 0.05f, 2.3f);
        lip.transform.localPosition = new Vector3(0f, PlatformHeight + 0.05f, 0f);
        var lipRenderer = lip.GetComponent<MeshRenderer>();
        lipRenderer.material = MaterialsUtil.GetOrCreate("WoodDark", new Color(0.25f, 0.17f, 0.1f));
        Destroy(lip.GetComponent<Collider>());

        CreateFinalPads(parent, TokenColor.Red);
        CreateFinalPads(parent, TokenColor.Blue);
    }

    private void CreateFinalPads(Transform parent, TokenColor color)
    {
        var mat = color == TokenColor.Red
            ? MaterialsUtil.GetOrCreate("TeamRed", new Color(0.8f, 0.1f, 0.1f))
            : MaterialsUtil.GetOrCreate("TeamBlue", new Color(0.1f, 0.3f, 0.9f));

        float radius = 0.6f;
        float height = PlatformHeight + 0.05f;
        for (int i = 0; i < 4; i++)
        {
            float angle = color == TokenColor.Red ? Mathf.PI / 4f + i * Mathf.PI / 6f : Mathf.PI * 5f / 4f + i * Mathf.PI / 6f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.transform.SetParent(parent, false);
            pad.transform.localScale = new Vector3(0.35f, 0.05f, 0.35f);
            pad.transform.localPosition = pos;
            pad.GetComponent<MeshRenderer>().material = mat;
            Destroy(pad.GetComponent<Collider>());
        }
    }

    private List<Vector3> GenerateLap()
    {
        var list = new List<Vector3>();
        float half = boardSize * 0.5f;
        float spacing = boardSize / (nodesPerSide - 1);
        float y = TileY;

        for (int i = 0; i < nodesPerSide; i++)
        {
            float x = -half + spacing * i;
            list.Add(new Vector3(x, y, -half));
        }
        for (int i = 1; i < nodesPerSide; i++)
        {
            float z = -half + spacing * i;
            list.Add(new Vector3(half, y, z));
        }
        for (int i = 1; i < nodesPerSide; i++)
        {
            float x = half - spacing * i;
            list.Add(new Vector3(x, y, half));
        }
        for (int i = 1; i < nodesPerSide - 1; i++)
        {
            float z = half - spacing * i;
            list.Add(new Vector3(-half, y, z));
        }

        return list;
    }

    private List<Vector3> GenerateHomePath(TokenColor color)
    {
        var list = new List<Vector3>();
        float half = boardSize * 0.5f;
        float spacing = boardSize / (nodesPerSide - 1);
        float startZ = color == TokenColor.Red ? -half + spacing : half - spacing;

        for (int i = 0; i < stepCount; i++)
        {
            float t = (i + 1f) / stepCount;
            float y = TileY + stepHeight * (i + 1);
            float z = Mathf.Lerp(startZ, 0f, t);
            list.Add(new Vector3(0f, y, z));
        }

        return list;
    }

    private List<Vector3> GenerateBaseSpots(TokenColor color)
    {
        var list = new List<Vector3>();
        float half = boardSize * 0.5f;
        float spacing = boardSize / (nodesPerSide - 1);
        float y = TileY;
        float offsetZ = color == TokenColor.Red ? -half - baseOffset : half + baseOffset;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = 0; z < 2; z++)
            {
                Vector3 pos = new Vector3(x * spacing * 0.8f, y, offsetZ + z * spacing * 0.9f * (color == TokenColor.Red ? 1f : -1f));
                list.Add(pos);
            }
        }

        return list;
    }

    private int FindClosestIndex(IReadOnlyList<Vector3> list, Vector3 target)
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            float dist = Vector3.Distance(list[i], target);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void ClearChildren()
    {
        var children = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            children.Add(transform.GetChild(i));
        }

        foreach (var child in children)
        {
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if (Lap != null)
        {
            for (int i = 0; i < Lap.Count; i++)
            {
                Gizmos.DrawSphere(transform.TransformPoint(Lap[i]), 0.05f);
            }
        }

        if (RedHome != null)
        {
            Gizmos.color = Color.red;
            foreach (var p in RedHome)
            {
                Gizmos.DrawCube(transform.TransformPoint(p), Vector3.one * 0.1f);
            }
        }

        if (BlueHome != null)
        {
            Gizmos.color = Color.blue;
            foreach (var p in BlueHome)
            {
                Gizmos.DrawCube(transform.TransformPoint(p), Vector3.one * 0.1f);
            }
        }
    }
#endif
}
