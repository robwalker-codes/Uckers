using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraOrbit : MonoBehaviour
{
    public Transform Target;
    public float Distance = 14f;
    public float Height = 8f;
    public float OrbitSpeed = 90f;
    public float ZoomSpeed = 5f;
    public float MinDistance = 8f;
    public float MaxDistance = 24f;

    private float yaw = 45f;
    private Vector3 fallbackPivot = Vector3.zero;

    public void SetYaw(float value)
    {
        yaw = value;
    }

    public void SnapToTarget()
    {
        if (Target != null)
        {
            fallbackPivot = Target.position;
        }

        UpdateTransform();
    }

    private void LateUpdate()
    {
        if (Target != null)
        {
            fallbackPivot = Target.position;
        }

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * OrbitSpeed * Time.deltaTime;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
        {
            MinDistance = Mathf.Max(0.1f, MinDistance);
            MaxDistance = Mathf.Max(MinDistance, MaxDistance);
            float desired = Mathf.Clamp(Distance - scroll * ZoomSpeed, MinDistance, MaxDistance);
            Distance = desired;
        }

        UpdateTransform();
    }

    private void UpdateTransform()
    {
        Vector3 pivot = Target != null ? Target.position : fallbackPivot;
        float clampedDistance = Mathf.Max(Distance, 0.1f);
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 offset = rotation * (Vector3.back * clampedDistance);
        Vector3 position = pivot + Vector3.up * Height + offset;
        transform.position = position;

        Vector3 lookTarget = pivot + Vector3.up * Mathf.Max(Height * 0.25f, 0.5f);
        transform.rotation = Quaternion.LookRotation(lookTarget - position, Vector3.up);
    }
}
