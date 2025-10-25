using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Uckers.Domain.Model;

public enum TokenPlacementState
{
    Base,
    Track,
    Home,
    Finished
}

public class TokenView : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float arcHeight = 0.15f;

    public PlayerId Player { get; private set; }
    public int Progress { get; private set; } = -1;
    public TokenPlacementState PlacementState { get; private set; } = TokenPlacementState.Base;
    public bool IsAnimating => moveRoutine != null;

    public float CaptureRadius => 0.25f;

    private Vector3 basePosition;
    private Transform visualsRoot;
    private MeshRenderer highlightRenderer;
    private Coroutine moveRoutine;

    public void Initialise(BoardBuilder boardRef, PlayerId player, Vector3 baseSpot, Material material)
    {
        _ = boardRef;
        Player = player;
        basePosition = baseSpot + Vector3.up * 0.3f;
        name = $"{player} Token";
        transform.position = basePosition;

        BuildVisuals(material);
        SnapToBase();
    }

    private void BuildVisuals(Material material)
    {
        visualsRoot = new GameObject("Visuals").transform;
        visualsRoot.SetParent(transform, false);

        var baseDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseDisc.transform.SetParent(visualsRoot, false);
        baseDisc.transform.localScale = new Vector3(0.45f, 0.05f, 0.45f);
        baseDisc.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        var baseRenderer = baseDisc.GetComponent<MeshRenderer>();
        baseRenderer.material = material;
        Destroy(baseDisc.GetComponent<Collider>());

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.transform.SetParent(visualsRoot, false);
        body.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
        body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
        var bodyRenderer = body.GetComponent<MeshRenderer>();
        bodyRenderer.material = material;
        Destroy(body.GetComponent<Collider>());

        var highlight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        highlight.transform.SetParent(visualsRoot, false);
        highlight.transform.localScale = new Vector3(0.6f, 0.01f, 0.6f);
        highlight.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        highlightRenderer = highlight.GetComponent<MeshRenderer>();
        highlightRenderer.material = MaterialsUtil.GetOrCreate("Highlight", new Color(1f, 1f, 1f, 0.75f));
        highlightRenderer.enabled = false;
        Destroy(highlight.GetComponent<Collider>());

        var collider = gameObject.AddComponent<SphereCollider>();
        collider.radius = 0.45f;
        collider.center = new Vector3(0f, 0.45f, 0f);
    }

    public void SnapToBase()
    {
        Progress = -1;
        PlacementState = TokenPlacementState.Base;
        transform.position = basePosition;
    }

    public void SetHighlight(bool enabled)
    {
        if (highlightRenderer != null)
        {
            highlightRenderer.enabled = enabled;
        }

        if (visualsRoot != null)
        {
            visualsRoot.localScale = enabled ? Vector3.one * 1.1f : Vector3.one;
        }
    }

    public void SetProgress(int progress, TokenPlacementState placement)
    {
        Progress = progress;
        PlacementState = placement;
    }

    public void SetBasePosition(Vector3 pos)
    {
        basePosition = pos + Vector3.up * 0.3f;
        if (PlacementState == TokenPlacementState.Base)
        {
            transform.position = basePosition;
        }
    }

    public void ReturnToBase()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        SnapToBase();
    }

    public Coroutine AnimateMove(List<Vector3> positions, List<int> progressSequence, List<TokenPlacementState> states)
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveRoutine(positions, progressSequence, states));
        return moveRoutine;
    }

    private IEnumerator MoveRoutine(IReadOnlyList<Vector3> positions, IReadOnlyList<int> progressSequence, IReadOnlyList<TokenPlacementState> states)
    {
        for (int i = 0; i < positions.Count; i++)
        {
            var target = positions[i];
            Vector3 start = transform.position;
            float distance = Vector3.Distance(start, target);
            float duration = distance / Mathf.Max(0.01f, moveSpeed);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float arc = Mathf.Sin(Mathf.PI * eased) * arcHeight;
                Vector3 lerpPos = Vector3.Lerp(start, target, eased);
                lerpPos.y += arc;
                transform.position = lerpPos;
                yield return null;
            }

            transform.position = target;
            Progress = progressSequence[i];
            PlacementState = states[i];
        }

        moveRoutine = null;
    }

    public Vector3 GetWorldPosition()
    {
        return transform.position;
    }
}
