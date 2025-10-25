using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 10f, -12f);
    [SerializeField] private Vector3 lightEuler = new Vector3(45f, -30f, 0f);

    private bool built;

    private void Start()
    {
        BuildScene();
        StartCoroutine(FallbackRebuild());
    }

    private void BuildScene()
    {
        if (built)
        {
            return;
        }

        built = true;

        var boardGo = new GameObject("BoardRoot");
        boardGo.transform.position = Vector3.zero;
        var board = boardGo.AddComponent<Board>();
        board.Build();

        var manager = gameObject.AddComponent<GameManager>();

        var uiGo = new GameObject("UIRoot");
        var ui = uiGo.AddComponent<UIController>();

        var redTokens = SpawnTokens(board, TokenColor.Red);
        var blueTokens = SpawnTokens(board, TokenColor.Blue);

        manager.Initialise(board, redTokens, blueTokens, ui);

        CreateCamera(board);
        CreateLight();
    }

    private IEnumerator FallbackRebuild()
    {
        yield return new WaitForSeconds(10f);
        if (FindObjectOfType<GameManager>() == null)
        {
            built = false;
            BuildScene();
        }
    }

    private List<Token> SpawnTokens(Board board, TokenColor color)
    {
        var list = new List<Token>();
        var parent = new GameObject($"{color}Tokens").transform;
        parent.SetParent(board.transform, false);
        var baseSpots = color == TokenColor.Red ? board.RedBaseSpots : board.BlueBaseSpots;
        var mat = color == TokenColor.Red
            ? MaterialsUtil.GetOrCreate("TeamRed", new Color(0.8f, 0.1f, 0.1f))
            : MaterialsUtil.GetOrCreate("TeamBlue", new Color(0.1f, 0.3f, 0.9f));

        for (int i = 0; i < 4; i++)
        {
            var tokenGo = new GameObject($"{color}Token_{i + 1}");
            tokenGo.transform.SetParent(parent, false);
            var token = tokenGo.AddComponent<Token>();
            var basePos = baseSpots[Mathf.Clamp(i, 0, baseSpots.Count - 1)];
            token.Initialise(board, color, basePos, mat);
            token.SetProgress(-1, TokenPlacementState.Base);
            list.Add(token);
        }

        return list;
    }

    private void CreateCamera(Board board)
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
