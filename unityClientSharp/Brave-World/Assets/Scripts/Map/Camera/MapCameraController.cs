using UnityEngine;

namespace UnityClientSharp.Map.Rendering
{
    /// <summary>
    /// 地图正交相机控制器：左键拖拽平移 + 滚轮缩放（以鼠标为锚点），并对外暴露 Zoom（用于网格线宽自适应）。
    /// 移植自 clinetcsharp/Scripts/CameraController.cs 的缩放/平移部分（简化为纯地图浏览）。
    /// Zoom 语义对齐 Godot Camera2D.Zoom：>1 放大，<1 缩小。
    /// </summary>
    public class MapCameraController : MonoBehaviour
    {
        public static MapCameraController Instance;

        public Camera Cam;
        public float MinZoom = 0.2f;
        public float MaxZoom = 10.0f; // 放大上限对齐 GridRenderMaxZoom（网格渲染安全区）；Godot 原值为 3.0
        public float ZoomStep = 1.1f;
        public float PanSpeed = 1.0f;

        private float _baseOrtho;
        private float _zoom = 1f;

        public float Zoom => _zoom;

        private Vector3 _dragStart;
        private Vector3 _camStart;
        private bool _dragging;

        /// <summary>跟随目标（玩家）。未拖拽时相机直接跟随（简化版；Godot 的 ReturnDelay/平滑回归随移动阶段对齐）。</summary>
        public Transform FollowTarget;

        private void Awake()
        {
            Instance = this;
            if (Cam == null) Cam = GetComponent<Camera>();
            if (Cam == null) Cam = Camera.main;
            if (Cam != null)
            {
                Cam.orthographic = true;
                _baseOrtho = Cam.orthographicSize;
            }
        }

        private void Update()
        {
            HandleZoom();
            HandlePan();
            HandleFollow();
        }

        private void HandleFollow()
        {
            if (_dragging || FollowTarget == null || Cam == null) return;
            var p = Cam.transform.position;
            var t = FollowTarget.position;
            Cam.transform.position = new Vector3(t.x, t.y, p.z);
        }

        private void HandleZoom()
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) <= 1e-4f) return;

            float factor = wheel > 0 ? ZoomStep : 1f / ZoomStep;
            float newZoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            if (Mathf.Approximately(newZoom, _zoom)) return;

            // 以鼠标为锚点：保持鼠标指针下的世界点不动（对齐 Godot CameraController 的缩放行为）
            Vector3 mouseWorldBefore = Cam.ScreenToWorldPoint(Input.mousePosition);
            _zoom = newZoom;
            ApplyZoom();
            Cam.transform.position += mouseWorldBefore - Cam.ScreenToWorldPoint(Input.mousePosition);
        }

        private void ApplyZoom()
        {
            if (Cam != null)
                Cam.orthographicSize = _baseOrtho / _zoom;
        }

        private void HandlePan()
        {
            // 正常模式仅左键拖拽（对齐 Godot CameraController 的 DragButtons 默认值 {Left}），右键/中键留给后续交互
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                _dragging = true;
                _dragStart = Input.mousePosition;
                _camStart = Cam.transform.position;
            }

            if (_dragging && Input.GetMouseButton(0))
            {
                Vector3 delta = Input.mousePosition - _dragStart;
                float worldPerPixel = (Cam.orthographicSize * 2f) / Screen.height;
                Vector3 move = new Vector3(-delta.x * worldPerPixel * PanSpeed, -delta.y * worldPerPixel * PanSpeed, 0);
                Cam.transform.position = _camStart + move;
            }
            else
            {
                _dragging = false;
            }
        }

        /// <summary>指针是否悬停在任一 UI 元素上（无 EventSystem 时视为否）。</summary>
        private static bool IsPointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        /// <summary>将相机对准并以留边方式完整显示给定世界范围的地图。</summary>
        public void FocusOn(Vector3 center, float worldW, float worldH)
        {
            if (Cam == null) return;
            Cam.transform.position = new Vector3(center.x, center.y, Cam.transform.position.z);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float sizeY = (worldH / 2f) * 1.1f;
            float sizeX = (worldW / 2f / aspect) * 1.1f;
            Cam.orthographicSize = Mathf.Max(sizeY, sizeX);
            _baseOrtho = Cam.orthographicSize;
            _zoom = 1f;
        }
    }
}
