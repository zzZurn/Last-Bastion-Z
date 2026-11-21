using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 28f;
    public float panBorderThickness = 10f;
    public Vector2 panLimitX = new Vector2(-70f, 50f);
    public Vector2 panLimitZ = new Vector2(-148.7f, -52.7f);
    public Vector2 groundPanLimitZ = new Vector2(-48f, 48f);

    [Header("Zoom Settings")]
    public float zoomSpeed = 6f;
    public float minZoom = 6f;
    public float maxZoom = 80f;

    [Header("PvZ Camera Angle Settings")]
    public float pitchAngle = 50f; // Plants vs. Zombies tilted pitch angle
    public float cameraHeight = 120f; // High above tallest city skyscrapers so near clip plane never slices rooftops
    public float topDownHeight = 120f;
    public bool isPvZAngle = true;

    private Camera cam;
    private Vector3 lastMousePos;
    private bool isDragging;

    // Touch
    private Vector2 touchStart;
    private float initialPinchDistance;
    private float initialOrthoSize;

    void Start()
    {
        cam = GetComponent<Camera>();
        panLimitX = new Vector2(-70f, 50f);
        groundPanLimitZ = new Vector2(-48f, 48f);
        maxZoom = 80f;
        cameraHeight = 120f;
        topDownHeight = 120f;
        if (cam != null)
        {
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1000f);
            if (cam.orthographic && cam.orthographicSize < 26f)
            {
                cam.orthographicSize = 28f;
            }
        }
        SetCameraView(isPvZAngle, preserveGroundFocus: false);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.MainMenu)
        {
            return;
        }

        // Hotkey: V to toggle between PvZ tilted angle (50°) and Top-Down view (90°)
        if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleCameraView();
        }

        // Hotkeys: PageUp / PageDown to fine-tune camera pitch angle
        if (Input.GetKey(KeyCode.PageUp))
        {
            AdjustPitch(30f * Time.unscaledDeltaTime);
        }
        else if (Input.GetKey(KeyCode.PageDown))
        {
            AdjustPitch(-30f * Time.unscaledDeltaTime);
        }

        HandleKeyboardPan();
        HandleMousePan();
        HandleMouseZoom();
        HandleTouchInput();
        ClampPosition();
    }

    private float GetActiveHeight(float pitch)
    {
        return (pitch >= 89.9f) ? topDownHeight : cameraHeight;
    }

    private static float GetCameraZOffset(float pitch, float height)
    {
        if (pitch >= 89.9f) return 0f;
        return -height / Mathf.Tan(pitch * Mathf.Deg2Rad);
    }

    public void ToggleCameraView()
    {
        isPvZAngle = !isPvZAngle;
        SetCameraView(isPvZAngle);
    }

    public void SetCameraView(bool pvzMode, bool preserveGroundFocus = true)
    {
        float oldHeight = GetActiveHeight(pitchAngle);
        float oldZOffset = GetCameraZOffset(pitchAngle, oldHeight);
        float currentGroundZ = preserveGroundFocus ? (transform.position.z - oldZOffset) : 0f;

        isPvZAngle = pvzMode;
        pitchAngle = isPvZAngle ? 50f : 90f;

        float activeHeight = GetActiveHeight(pitchAngle);
        float zOffset = GetCameraZOffset(pitchAngle, activeHeight);
        panLimitZ = new Vector2(groundPanLimitZ.x + zOffset, groundPanLimitZ.y + zOffset);

        transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        float clampedGroundZ = Mathf.Clamp(currentGroundZ, groundPanLimitZ.x, groundPanLimitZ.y);
        transform.position = new Vector3(transform.position.x, activeHeight, clampedGroundZ + zOffset);

        if (isPvZAngle)
        {
            Debug.Log("[Camera] Applied Plants vs. Zombies 2.5D tilted perspective (50°)");
        }
        else
        {
            Debug.Log("[Camera] Switched to Classic Top-Down view (90°)");
        }
    }

    public void AdjustPitch(float delta)
    {
        float oldHeight = GetActiveHeight(pitchAngle);
        float oldZOffset = GetCameraZOffset(pitchAngle, oldHeight);
        float currentGroundZ = transform.position.z - oldZOffset;

        pitchAngle = Mathf.Clamp(pitchAngle + delta, 30f, 90f);
        float activeHeight = GetActiveHeight(pitchAngle);
        float zOffset = GetCameraZOffset(pitchAngle, activeHeight);
        panLimitZ = new Vector2(groundPanLimitZ.x + zOffset, groundPanLimitZ.y + zOffset);

        transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        float clampedGroundZ = Mathf.Clamp(currentGroundZ, groundPanLimitZ.x, groundPanLimitZ.y);
        transform.position = new Vector3(transform.position.x, activeHeight, clampedGroundZ + zOffset);
    }

    void HandleKeyboardPan()
    {
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
        transform.position += move.normalized * panSpeed * Time.unscaledDeltaTime;
    }

    void HandleMousePan()
    {
        if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
        {
            lastMousePos = Input.mousePosition;
            isDragging = true;
        }
        if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }
        if (isDragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            transform.position -= new Vector3(delta.x, 0, delta.y) * panSpeed * 0.01f;
            lastMousePos = Input.mousePosition;
        }
    }

    void HandleMouseZoom()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsPointerOverUI()) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                Vector3 delta = new Vector3(-t.deltaPosition.x, 0, -t.deltaPosition.y);
                transform.position += delta * panSpeed * 0.005f;
            }
        }
        else if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);
            if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            {
                initialPinchDistance = Vector2.Distance(t0.position, t1.position);
                initialOrthoSize = cam.orthographicSize;
            }
            else if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
            {
                float currentDist = Vector2.Distance(t0.position, t1.position);
                if (Mathf.Abs(currentDist) > 0.01f)
                {
                    float factor = initialPinchDistance / currentDist;
                    cam.orthographicSize = Mathf.Clamp(initialOrthoSize * factor, minZoom, maxZoom);
                }
            }
        }
    }

    void ClampPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);
        transform.position = pos;
    }
}