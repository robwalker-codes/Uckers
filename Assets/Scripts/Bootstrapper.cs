using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 10f, -12f);
    [SerializeField] private Vector3 lightEuler = new Vector3(45f, -30f, 0f);

    private bool built;

    private void Start()
    {
        StartCoroutine(BuildScene());
    }

    private IEnumerator BuildScene()
    {
        if (built)
        {
            yield break;
        }

        built = true;

        var boardGo = new GameObject("BoardRoot");
        boardGo.transform.position = Vector3.zero;
        var board = boardGo.AddComponent<BoardBuilder>();
        board.Build();

        var manager = gameObject.AddComponent<GameManager>();

        var uiGo = new GameObject("UIRoot");
        var ui = uiGo.AddComponent<UIController>();
        ui.Build(manager.RequestRoll);
        ui.SetRollEnabled(false);

        yield return WaitForPlayerSelection(ui);

        int playerCount = Mathf.Clamp(ui.SelectedPlayerCount, GameConfig.MinPlayers, GameConfig.MaxPlayers);
        var selectedPlayers = GameConfig.PlayerOrder.Take(playerCount).ToList();

        var tokenMap = SpawnTokens(board, selectedPlayers);
        var turnManager = new TurnManager(selectedPlayers);
        var adapter = new DomainAdapter(board, tokenMap, selectedPlayers);

        manager.Initialise(board, tokenMap, ui, turnManager, adapter);

        CreateCamera(board);
        CreateLight();
    }

    private IEnumerator WaitForPlayerSelection(UIController ui)
    {
        while (!ui.HasSelectedPlayerCount)
        {
            yield return null;
        }
    }

    private Dictionary<PlayerId, List<TokenView>> SpawnTokens(BoardBuilder board, IReadOnlyList<PlayerId> players)
    {
        var map = new Dictionary<PlayerId, List<TokenView>>();
        foreach (var player in players)
        {
            var parent = new GameObject($"{player}Tokens").transform;
            parent.SetParent(board.transform, false);
            var baseSpots = board.BaseSpots[player];
            var material = CreatePlayerMaterial(player);
            var list = new List<TokenView>();

            for (int i = 0; i < GameConfig.TokensPerPlayer; i++)
            {
                var tokenGo = new GameObject($"{player}Token_{i + 1}");
                tokenGo.transform.SetParent(parent, false);
                var token = tokenGo.AddComponent<TokenView>();
                var basePos = baseSpots[Mathf.Clamp(i, 0, baseSpots.Count - 1)];
                token.Initialise(board, player, basePos, material);
                token.SetProgress(-1, TokenPlacementState.Base);
                list.Add(token);
            }

            map[player] = list;
        }

        return map;
    }

    private Material CreatePlayerMaterial(PlayerId player)
    {
        var colour = GameConfig.PlayerColours[player];
        return MaterialsUtil.GetOrCreate($"Team{player}", new Color(colour.r, colour.g, colour.b, colour.a));
    }

    private void CreateCamera(BoardBuilder board)
    {
        var rig = new GameObject("CameraRig");
        var camGo = new GameObject("MainCamera");
        camGo.transform.SetParent(rig.transform, false);
        camGo.tag = "MainCamera";
        var camera = camGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.25f, 0.28f, 0.32f);
        camGo.AddComponent<AudioListener>();

        rig.transform.position = cameraOffset;
        camGo.transform.localPosition = Vector3.zero;
        camGo.transform.LookAt(Vector3.up * board.PlatformHeight * 0.5f);

        var orbit = rig.AddComponent<CameraOrbit>();
        orbit.Target = board.transform;
        orbit.Distance = new Vector2(cameraOffset.x, cameraOffset.z).magnitude;
        orbit.Height = cameraOffset.y;
        float yaw = Mathf.Atan2(cameraOffset.x, cameraOffset.z) * Mathf.Rad2Deg;
        orbit.SetYaw(yaw);
        orbit.SnapToTarget();
    }

    private void CreateLight()
    {
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(lightEuler);
    }
}

public class CameraOrbit : MonoBehaviour
{
    public Transform Target;
    public float Distance = 14f;
    public float Height = 8f;
    public float OrbitSpeed = 90f;
    public float ZoomSpeed = 5f;
    public float MinDistance = 8f;
    public float MaxDistance = 20f;

    private float yaw = 45f;

    public void SetYaw(float value)
    {
        yaw = value;
    }

    public void SnapToTarget()
    {
        UpdateTransform();
    }

    private void LateUpdate()
    {
        if (Target == null)
        {
            return;
        }

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * OrbitSpeed * Time.deltaTime;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            Distance = Mathf.Clamp(Distance - scroll * ZoomSpeed, MinDistance, MaxDistance);
        }

        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (Target == null)
        {
            return;
        }

        Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * Vector3.back * Distance;
        offset.y = Height;
        transform.position = Target.position + offset;
        transform.LookAt(Target.position + Vector3.up * 0.5f);
    }
}
