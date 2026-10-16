using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//功能：相机拖拽、缩放，无边界限制（进阶可加地图边界限制）。
public class CameraController : MonoBehaviour
{
    public float dragSpeed = 0.08f;
    public float zoomSpeed = 2f;
    public float minSize = 3f;
    public float maxSize = 12f;

    private Camera cam;
    private Vector3 lastMousePos;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        // 滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minSize, maxSize);
        }
        // 左键拖拽
        if (Input.GetMouseButtonDown(0))
            lastMousePos = Input.mousePosition;
        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;
            float scaleFactor = cam.orthographicSize / 5f;
            transform.Translate(-delta * dragSpeed * scaleFactor, Space.World);
        }
    }
}