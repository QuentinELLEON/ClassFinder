using UnityEngine;

// Displays an AR arrow that always points towards a target bearing (default: true north).
// Attach this script to an empty GameObject holding your arrow model as a child (or directly on the arrow).
public class NavigationArrowAR : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform arCamera; // Assign your AR Camera (e.g. XROrigin's Camera)

    [Header("Placement")]
    [SerializeField] private float distanceFromCamera = 1.5f; // meters in front of the camera
    [SerializeField] private float verticalOffset = -0.3f;    // meters below eye level

    [Header("Target direction")]
    [Tooltip("Bearing in degrees, 0 = North, 90 = East, 180 = South, 270 = West")]
    [SerializeField] private float targetBearing = 0f;
    [SerializeField] private bool useTrueHeading = true; // requires location services on iOS/Android

    [Header("Smoothing")]
    [SerializeField] private float rotationSmoothSpeed = 8f;

    private Quaternion smoothedRotation;

    private void Start()
    {
        // Compass requires location services to be enabled to give accurate heading
        Input.location.Start();
        Input.compass.enabled = true;

        if (arCamera == null && Camera.main != null)
            arCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (arCamera == null) return;

        PositionArrow();
        RotateArrow();
    }

    private void PositionArrow()
    {
        // Flatten the camera's forward vector so the arrow stays on the horizontal plane
        Vector3 flatForward = arCamera.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = arCamera.up; // edge case: phone pointing straight up/down

        flatForward.Normalize();

        transform.position = arCamera.position
                              + flatForward * distanceFromCamera
                              + Vector3.up * verticalOffset;
    }

    private void RotateArrow()
    {
        // Current device heading relative to true/magnetic north
        float deviceHeading = useTrueHeading ? Input.compass.trueHeading : Input.compass.magneticHeading;

        // Camera's yaw in the AR world (ignoring pitch/roll)
        Vector3 flatForward = arCamera.forward;
        flatForward.y = 0f;
        flatForward.Normalize();
        float cameraWorldYaw = Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg;

        // Angle between where the phone points and the target bearing
        float relativeAngle = targetBearing - deviceHeading;

        float targetYaw = cameraWorldYaw + relativeAngle;
        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);

        smoothedRotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
        transform.rotation = smoothedRotation;
    }

    // Sets a new target bearing directly (e.g. 0 = North).
    public void SetTargetBearing(float bearingDegrees)
    {
        targetBearing = bearingDegrees;
    }

    // Computes and sets the bearing towards a GPS coordinate, for future use
    // once you want to point to a real destination instead of just North.
    public void SetTargetFromGPS(double targetLat, double targetLon)
    {
        if (!Input.location.isEnabledByUser) return;

        double currentLat = Input.location.lastData.latitude;
        double currentLon = Input.location.lastData.longitude;

        targetBearing = (float)CalculateBearing(currentLat, currentLon, targetLat, targetLon);
    }

    private double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = (lon2 - lon1) * Mathf.Deg2Rad;
        lat1 *= Mathf.Deg2Rad;
        lat2 *= Mathf.Deg2Rad;

        double y = Mathf.Sin((float)dLon) * Mathf.Cos((float)lat2);
        double x = Mathf.Cos((float)lat1) * Mathf.Sin((float)lat2)
                    - Mathf.Sin((float)lat1) * Mathf.Cos((float)lat2) * Mathf.Cos((float)dLon);

        double bearing = Mathf.Atan2((float)y, (float)x) * Mathf.Rad2Deg;
        return (bearing + 360) % 360;
    }
}