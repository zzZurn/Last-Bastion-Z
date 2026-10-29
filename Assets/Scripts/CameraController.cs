using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 20f;
    public float panBorderThickness = 10f;
    public Vector2 panLimitX = new Vector2(-30f, 30f);
    public Vector2 panLimitZ = new Vector2(-20f, 20f);

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoom = 5f;
    public float maxZoom = 30f;

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
    }

    void Update()
    {
        HandleKeyboardPan();
        HandleMousePan();
        HandleMouseZoom();
        HandleTouchInput();
        ClampPosition();
    }

    void HandleKeyboardPan()
    {
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1;
        transform.position += move.normalized * panSpeed * Time.deltaTime;
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