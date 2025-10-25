using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uckers.Domain.Model;

public class BoardBuilder : MonoBehaviour
{
    [Header("Board Settings")]
    [SerializeField] private float boardSize = 8f;
    [SerializeField] private int nodesPerSide = 10;
    [SerializeField] private float tileThickness = 0.05f;
    [SerializeField] private float stepHeight = 0.1f;
    [SerializeField] private int stepCount = 4;
    [SerializeField] private float homeLaneWidth = 0.7f;

    public BoardTopology Topology { get; private set; }
    public List<Vector3> Lap { get; private set; } = new List<Vector3>();
    public Dictionary<PlayerId, List<Vector3>> HomeLanes { get; } = new Dictionary<PlayerId, List<Vector3>>();
    public Dictionary<PlayerId, List<Vector3>> BaseSpots { get; } = new Dictionary<PlayerId, List<Vector3>>();
    public Dictionary<PlayerId, int> EntryIndices { get; } = new Dictionary<PlayerId, int>();

    public float PlatformHeight => tileThickness * 0.5f + stepHeight * stepCount;
    private float TileY => tileThickness * 0.5f;

    public void Build()
    {
        Topology = new BoardTopology();
        var topology = Topology;
        ClearChildren();

        Lap = topology.Lap.Select(ToVector3).ToList();
        EntryIndices.Clear();
        HomeLanes.Clear();
        BaseSpots.Clear();

        foreach (var player in GameConfig.PlayerOrder)
        {
            EntryIndices[player] = topology.GetEntryIndex(player);
            HomeLanes[player] = topology.GetHomeLane(player).Select(ToVector3).ToList();
            BaseSpots[player] = topology.GetBaseSpots(player).Select(ToVector3).ToList();
        }

        CreateBaseSurface();
        CreateLapTiles();
        foreach (var player in GameConfig.PlayerOrder)
        {
            CreateHomeTiles(player, HomeLanes[player]);
            CreateBasePads(player, BaseSpots[player]);
        }

        CreateCenterPlatform();
    }

    public int GetEntryIndex(PlayerId playerId) => EntryIndices[playerId];

    public Vector3 GetProgressPosition(PlayerId playerId, int progress)
    {
        if (progress < 0)
        {
            return BaseSpots[playerId][0];
        }

        if (progress < Lap.Count)
        {
            int index = Mathf.Clamp(progress, 0, Lap.Count - 1);
            return Lap[index];
        }

        int homeIndex = progress - Lap.Count;
        var homeList = HomeLanes[playerId];
        homeIndex = Mathf.Clamp(homeIndex, 0, homeList.Count - 1);
        return homeList[homeIndex];
    }

    public int GetHomeCount(PlayerId playerId) => HomeLanes[playerId].Count;
    public int GetFinishProgress(PlayerId playerId) => Lap.Count + GetHomeCount(playerId) - 1;

    public Vector3 GetBasePosition(PlayerId playerId, int slot)
    {
        var list = BaseSpots[playerId];
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

    private void CreateHomeTiles(PlayerId player, IReadOnlyList<Vector3> nodes)
    {
        var parent = new GameObject($"{player}Home").transform;
        parent.SetParent(transform, false);

        float spacing = boardSize / (nodesPerSide - 1);
        float tileSize = Mathf.Min(homeLaneWidth, spacing * 0.9f);
        var mat = GetPlayerMaterial(player);

        for (int i = 0; i < nodes.Count; i++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"{player}Home_{i}";
            tile.transform.SetParent(parent, false);
            tile.transform.localScale = new Vector3(tileSize, tileThickness, tileSize);
            tile.transform.localPosition = nodes[i];
            var renderer = tile.GetComponent<MeshRenderer>();
            renderer.material = mat;
            Destroy(tile.GetComponent<Collider>());
        }
    }

    private void CreateBasePads(PlayerId player, IReadOnlyList<Vector3> pads)
    {
        var parent = new GameObject($"{player}Base").transform;
        parent.SetParent(transform, false);
        var mat = GetPlayerMaterial(player);

        for (int i = 0; i < pads.Count; i++)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tile.name = $"{player}Base_{i}";
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

        CreateFinalPads(parent);
    }

    private void CreateFinalPads(Transform parent)
    {
        float radius = 0.6f;
        float height = PlatformHeight + 0.05f;

        var angles = new Dictionary<PlayerId, float>
        {
            [PlayerId.Red] = Mathf.PI * 1.5f,
            [PlayerId.Blue] = Mathf.PI * 0.5f,
            [PlayerId.Green] = Mathf.PI,
            [PlayerId.Yellow] = 0f
        };

        foreach (var player in GameConfig.PlayerOrder)
        {
            var mat = GetPlayerMaterial(player);
            float baseAngle = angles[player];
            for (int i = 0; i < GameConfig.TokensPerPlayer; i++)
            {
                float angle = baseAngle + (i - (GameConfig.TokensPerPlayer - 1) / 2f) * Mathf.PI / 12f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.transform.SetParent(parent, false);
                pad.transform.localScale = new Vector3(0.35f, 0.05f, 0.35f);
                pad.transform.localPosition = pos;
                pad.GetComponent<MeshRenderer>().material = mat;
                Destroy(pad.GetComponent<Collider>());
            }
        }
    }

    private Material GetPlayerMaterial(PlayerId player)
    {
        var colour = GameConfig.PlayerColours[player];
        return MaterialsUtil.GetOrCreate($"Team{player}", new Color(colour.r, colour.g, colour.b, colour.a));
    }

    private Vector3 ToVector3(BoardPoint point)
    {
        return new Vector3(point.X, point.Y, point.Z);
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
}
