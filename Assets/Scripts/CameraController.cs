using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Camera cam;
    public float zoomSpeed = 5f;
    public float minZoom = 1f;
    public float maxZoom = 20f;
    public float panSpeed = 0.01f;

    private Vector3 dragOrigin;
    private bool dragging = false;

    void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float size = cam.orthographicSize;
            size -= scroll * zoomSpeed * size * 0.2f;
            cam.orthographicSize = Mathf.Clamp(size, minZoom, maxZoom);
        }
    }

    void HandlePan()
    {
        // Middle mouse button (or right alt + left drag) for pan
        if (Input.GetMouseButtonDown(2))
        {
            dragging = true;
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }
        if (Input.GetMouseButtonUp(2))
        {
            dragging = false;
        }
        if (dragging)
        {
            Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
            cam.transform.position += difference;
        }
    }
}
