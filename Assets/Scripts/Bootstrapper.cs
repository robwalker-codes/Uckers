using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

public class Bootstrapper : MonoBehaviour
{
    private const string CameraRigName = "CameraRig";
    private const string MainCameraName = "Main Camera";
    private const string DirectionalLightName = "Directional Light";

    [SerializeField] private Vector3 defaultCameraOffset = new Vector3(0f, 10f, -12f);
    [SerializeField] private Vector3 defaultLightEuler = new Vector3(45f, -30f, 0f);
    [SerializeField] private Color cameraBackgroundColor = new Color(0.25f, 0.28f, 0.32f);
    [SerializeField] private Color directionalLightColor = new Color(1f, 0.96f, 0.9f);
    [SerializeField] private float directionalLightIntensity = 1.1f;
    [SerializeField] private float playerSelectionTimeout = 5f;
    [SerializeField] private float cameraDistancePadding = 2f;
    [SerializeField] private float cameraHeightPadding = 3f;

    private bool built;
    private bool playerSelectionTimedOut;
    private Camera mainCamera;
    private CameraOrbit cameraOrbit;
    private Light directionalLight;

    private void Awake()
    {
        EnsureCameraAndLightFallback();
    }

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

        EnsureCameraAndLightFallback();

        var boardGo = new GameObject("BoardRoot");
        boardGo.transform.SetParent(transform, false);
        boardGo.transform.position = Vector3.zero;
        var board = boardGo.AddComponent<BoardBuilder>();
        board.Build();

        ConfigureCameraForBoard(board);

        var manager = gameObject.AddComponent<GameManager>();

        var uiGo = new GameObject("UIRoot");
        uiGo.transform.SetParent(transform, false);
        var ui = uiGo.AddComponent<UIController>();
        ui.Build(manager.RequestRoll);
        ui.SetRollEnabled(false);

        yield return WaitForPlayerSelection(ui);

        int playerCount = playerSelectionTimedOut
            ? GameConfig.DefaultPlayerCount
            : Mathf.Clamp(ui.SelectedPlayerCount, GameConfig.MinPlayers, GameConfig.MaxPlayers);
        var selectedPlayers = GameConfig.PlayerOrder.Take(playerCount).ToList();

        var tokenMap = SpawnTokens(board, selectedPlayers);
        var turnManager = new TurnManager(selectedPlayers);
        var adapter = new DomainAdapter(board, tokenMap, selectedPlayers);

        manager.Initialise(board, tokenMap, ui, turnManager, adapter);

        ConfigureCameraForBoard(board);
    }

    private IEnumerator WaitForPlayerSelection(UIController ui)
    {
        playerSelectionTimedOut = false;
        float elapsed = 0f;
        while (!ui.HasSelectedPlayerCount && elapsed < playerSelectionTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        playerSelectionTimedOut = !ui.HasSelectedPlayerCount;
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

    private void ConfigureCameraForBoard(BoardBuilder board)
    {
        if (board == null)
        {
            return;
        }

        EnsureCameraAndLightFallback();

        if (cameraOrbit == null)
        {
            return;
        }

        var bounds = CalculateBoardBounds(board);
        cameraOrbit.Target = board.transform;

        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        float paddedRadius = radius + cameraDistancePadding;
        float desiredDistance = Mathf.Max(paddedRadius, cameraOrbit.MinDistance);
        cameraOrbit.MaxDistance = Mathf.Max(cameraOrbit.MaxDistance, desiredDistance);
        cameraOrbit.Distance = desiredDistance;

        float desiredHeight = Mathf.Max(board.PlatformHeight + cameraHeightPadding, cameraOrbit.Height);
        cameraOrbit.Height = desiredHeight;
        cameraOrbit.SetYaw(CalculateDefaultYaw());
        cameraOrbit.SnapToTarget();
    }

    private Bounds CalculateBoardBounds(BoardBuilder board)
    {
        var renderers = board.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(board.transform.position, Vector3.one);
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void EnsureCameraAndLightFallback()
    {
        mainCamera = EnsureMainCamera();
        cameraOrbit = EnsureCameraOrbit(mainCamera);
        directionalLight = EnsureDirectionalLight();
    }

    private Camera EnsureMainCamera()
    {
        Camera camera = Camera.main;
#if UNITY_2023_1_OR_NEWER
        camera ??= Object.FindFirstObjectByType<Camera>();
#else
        camera ??= Object.FindObjectOfType<Camera>();
#endif
        if (camera != null)
        {
            ConfigureCameraComponent(camera);
            AttachCameraToRig(camera);
            return camera;
        }

        var rig = GameObject.Find(CameraRigName) ?? new GameObject(CameraRigName);
        var cameraGo = GameObject.Find(MainCameraName) ?? new GameObject(MainCameraName);
        camera = cameraGo.GetComponent<Camera>() ?? cameraGo.AddComponent<Camera>();
        ConfigureCameraComponent(camera);
        camera.transform.SetParent(rig.transform, false);
        camera.transform.localPosition = Vector3.zero;
        rig.transform.position = defaultCameraOffset;
        rig.transform.rotation = CalculateDefaultRotation();
        return camera;
    }

    private void ConfigureCameraComponent(Camera camera)
    {
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = cameraBackgroundColor;
        camera.tag = "MainCamera";
        if (!camera.TryGetComponent<AudioListener>(out _))
        {
            camera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void AttachCameraToRig(Camera camera)
    {
        if (camera == null)
        {
            return;
        }

        var rig = camera.transform.parent;
        if (rig != null && rig.name == CameraRigName)
        {
            return;
        }

        var rigGo = GameObject.Find(CameraRigName) ?? new GameObject(CameraRigName);
        camera.transform.SetParent(rigGo.transform, false);
        camera.transform.localPosition = Vector3.zero;
        rigGo.transform.position = defaultCameraOffset;
        rigGo.transform.rotation = CalculateDefaultRotation();
    }

    private CameraOrbit EnsureCameraOrbit(Camera camera)
    {
        if (camera == null)
        {
            return null;
        }

        var rig = camera.transform.parent;
        if (rig == null)
        {
            AttachCameraToRig(camera);
            rig = camera.transform.parent;
        }

        if (rig == null)
        {
            return null;
        }

        var orbit = rig.GetComponent<CameraOrbit>();
        if (orbit == null)
        {
            orbit = rig.gameObject.AddComponent<CameraOrbit>();
        }

        float defaultDistance = new Vector2(defaultCameraOffset.x, defaultCameraOffset.z).magnitude;
        if (orbit.Distance <= 0f)
        {
            orbit.Distance = Mathf.Max(defaultDistance, orbit.MinDistance);
        }

        if (orbit.Height <= 0f)
        {
            orbit.Height = Mathf.Max(defaultCameraOffset.y, 1f);
        }

        if (orbit.Target == null)
        {
            orbit.SetYaw(CalculateDefaultYaw());
            orbit.SnapToTarget();
        }

        return orbit;
    }

    private Light EnsureDirectionalLight()
    {
        if (directionalLight != null)
        {
            return directionalLight;
        }

        Light[] lights;
#if UNITY_2023_1_OR_NEWER
        lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        lights = Object.FindObjectsOfType<Light>();
#endif
        directionalLight = lights.FirstOrDefault(l => l.type == LightType.Directional);
        if (directionalLight != null)
        {
            ConfigureDirectionalLight(directionalLight);
            return directionalLight;
        }

        var lightGo = GameObject.Find(DirectionalLightName) ?? new GameObject(DirectionalLightName);
        directionalLight = lightGo.GetComponent<Light>() ?? lightGo.AddComponent<Light>();
        ConfigureDirectionalLight(directionalLight);
        lightGo.transform.rotation = Quaternion.Euler(defaultLightEuler);
        return directionalLight;
    }

    private void ConfigureDirectionalLight(Light light)
    {
        light.type = LightType.Directional;
        light.color = directionalLightColor;
        light.intensity = directionalLightIntensity;
        light.shadows = LightShadows.Soft;
        light.gameObject.name = DirectionalLightName;
    }

    private float CalculateDefaultYaw()
    {
        return Mathf.Atan2(defaultCameraOffset.x, defaultCameraOffset.z) * Mathf.Rad2Deg;
    }

    private Quaternion CalculateDefaultRotation()
    {
        Vector3 forward = -defaultCameraOffset;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.back;
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    internal static void AutoBootstrap()
    {
#if UNITY_2023_1_OR_NEWER
        var existing = Object.FindFirstObjectByType<Bootstrapper>();
#else
        var existing = Object.FindObjectOfType<Bootstrapper>();
#endif
        if (existing != null)
        {
            return;
        }

        var go = new GameObject(nameof(Bootstrapper));
        go.AddComponent<Bootstrapper>();
    }

#if UNITY_EDITOR
    public void EditorEnsureCameraAndLightFallback()
    {
        EnsureCameraAndLightFallback();
    }
#endif
}
