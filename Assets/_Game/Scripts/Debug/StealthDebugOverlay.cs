using System.Collections.Generic;
using System.Text;
using Game.AI;
using Game.Core;
using Game.Stealth;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.DevTools
{
    /// <summary>
    /// In-game stealth detection overlay (editor + development builds only), toggled with F3
    /// (<c>Player.ToggleStealthDebug</c>). Draws every active <see cref="EntityPerception"/>'s cones, proximity
    /// circles and line of sight with GL lines in URP's <c>endCameraRendering</c> (gizmos don't render in builds),
    /// a pooled TMP label above each entity, and a summary panel. Lives in Core.unity. Starts hidden.
    /// No per-frame allocations: labels are built in a cached StringBuilder with manual number formatting.
    /// </summary>
    public class StealthDebugOverlay : MonoBehaviour
    {
        private const string TAG = "[DevTools]";
        private const int ARC_SEGMENTS = 24;
        private const float LABEL_HEIGHT_OFFSET = 0.5f;
        private const float LAST_SEEN_RADIUS = 0.25f;
        private const int LAST_SEEN_SEGMENTS = 8;
        private const float DIM_ALPHA = 0.35f;

        [Tooltip("Vertex-colour line material (shader Hidden/Internal-Colored) — referenced here so builds include it.")]
        [SerializeField] private Material _lineMaterial;
        [Tooltip("Screen Space Overlay canvas holding the summary and the label pool. Hidden with the overlay.")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private TMP_Text _labelPrefab;
        [SerializeField] private TMP_Text _summaryText;
        [SerializeField] private float _maxDrawDistance = 40f;
        [SerializeField] private int _labelPoolSize = 16;

        private readonly Vector3[] _buffer = new Vector3[(ARC_SEGMENTS + 2) * 2];
        private readonly StringBuilder _sb = new(256);
        private readonly List<TMP_Text> _labels = new();
        private InputSystem_Actions _input;
        private Camera _camera;
        private bool _visible;

        private void Awake()
        {
            if (!UnityEngine.Debug.isDebugBuild)
            {
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                enabled = false;
                return;
            }
            if (_lineMaterial == null || _canvas == null || _labelPrefab == null || _summaryText == null)
            {
                GameLog.Error(TAG, "StealthDebugOverlay: missing line material, canvas, label prefab or summary text — disabled");
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            for (int i = 0; i < _labelPoolSize; i++)
            {
                TMP_Text label = Instantiate(_labelPrefab, _canvas.transform);
                label.gameObject.SetActive(false);
                _labels.Add(label);
            }
            _camera = Camera.main;
            SetVisible(false);
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.ToggleStealthDebug.started += HandleToggleStarted;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.ToggleStealthDebug.started -= HandleToggleStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void HandleToggleStarted(InputAction.CallbackContext ctx)
        {
            SetVisible(!_visible);
            GameLog.Info(TAG, $"Stealth debug overlay: {(_visible ? "shown" : "hidden")}");
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _canvas.gameObject.SetActive(visible);
        }

        private Camera MainCamera
        {
            get
            {
                if (_camera == null) _camera = Camera.main;
                return _camera;
            }
        }

        // --- Labels and summary ---

        private void LateUpdate()
        {
            if (!_visible) return;
            Camera cam = MainCamera;
            int used = 0;

            EntityPerception top = null;
            IStealthTarget player = null;
            IReadOnlyList<EntityPerception> active = EntityPerception.Active;
            for (int i = 0; i < active.Count; i++)
            {
                EntityPerception p = active[i];
                if (p == null || !p.IsActive) continue;
                if (player == null && p.Target != null) player = p.Target.StealthTarget;
                if (top == null || p.Awareness > top.Awareness) top = p;

                if (cam == null || used >= _labels.Count || !IsInDrawRange(cam, p)) continue;
                Vector3 screen = cam.WorldToScreenPoint(p.EyePosition + Vector3.up * LABEL_HEIGHT_OFFSET);
                if (screen.z < 0f) continue;

                TMP_Text label = _labels[used++];
                BuildLabel(p);
                label.SetText(_sb);
                label.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
                if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            }
            for (int i = used; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);

            BuildSummary(top, player);
            _summaryText.SetText(_sb);
        }

        private bool IsInDrawRange(Camera cam, EntityPerception p) =>
            (p.transform.position - cam.transform.position).sqrMagnitude <= _maxDrawDistance * _maxDrawDistance;

        private void BuildLabel(EntityPerception p)
        {
            _sb.Clear();
            AppendState(p);
            _sb.Append('\n');
            AppendPercent(p.Awareness);
            _sb.Append("  LOS ").Append(p.HasLineOfSight ? "yes" : "no");
            _sb.Append("  ");
            AppendFixed1(p.DistanceToTarget);
            _sb.Append(" m");
        }

        private void BuildSummary(EntityPerception top, IStealthTarget player)
        {
            _sb.Clear();
            _sb.Append("STEALTH DEBUG (F3)\n");
            if (player == null || top == null)
            {
                _sb.Append("no hostile perceiving");
                return;
            }
            _sb.Append("Player sneaking: ").Append(player.IsSneaking ? "yes" : "no");
            _sb.Append("\nVisibility height: ");
            Vector3 point = player.VisibilityPoint;
            AppendFixed1(top.Target != null ? point.y - top.Target.Transform.position.y : point.y);
            _sb.Append(" m\nTop awareness ");
            AppendPercent(top.Awareness);
            _sb.Append(" — ").Append(top.DebugName).Append(" (");
            AppendState(top);
            _sb.Append(')');
        }

        private void AppendState(EntityPerception p)
        {
            _sb.Append(p.Brain != null ? p.Brain.DebugStateName : "—");
        }

        // StringBuilder.Append(int/float) allocates on Mono — format digits by hand.
        private void AppendPercent(float awareness)
        {
            AppendInt(Mathf.RoundToInt(Mathf.Clamp01(awareness) * 100f));
            _sb.Append('%');
        }

        private void AppendFixed1(float value)
        {
            int tenths = Mathf.RoundToInt(value * 10f);
            if (tenths < 0) { _sb.Append('-'); tenths = -tenths; }
            AppendInt(tenths / 10);
            _sb.Append('.').Append((char)('0' + tenths % 10));
        }

        private void AppendInt(int value)
        {
            if (value < 0) { _sb.Append('-'); value = -value; }
            if (value >= 10) AppendInt(value / 10);
            _sb.Append((char)('0' + value % 10));
        }

        // --- GL lines ---

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!_visible || cam != MainCamera) return;

            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            _lineMaterial.SetPass(0);
            GL.Begin(GL.LINES);

            IReadOnlyList<EntityPerception> active = EntityPerception.Active;
            for (int i = 0; i < active.Count; i++)
            {
                EntityPerception p = active[i];
                if (p == null || !p.IsActive || !IsInDrawRange(cam, p)) continue;
                DrawPerception(p);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawPerception(EntityPerception p)
        {
            StealthConfigSO config = p.Config;
            Color color = StealthDebugGeometry.AwarenessColor(p.Awareness, config.suspicionThreshold);
            Color dim = new Color(color.r, color.g, color.b, DIM_ALPHA);
            Transform t = p.transform;
            Vector3 eye = p.EyePosition;
            Vector3 ground = t.position + Vector3.up * 0.05f;
            float sight = p.SightRange;
            float proximity = p.ProximityRadius;

            DrawSegments(StealthDebugGeometry.BuildConeOutline(eye, t.forward, sight, p.ViewAngle, ARC_SEGMENTS, _buffer), color);
            DrawSegments(StealthDebugGeometry.BuildConeOutline(
                eye, t.forward, sight * config.sneakSightRangeMultiplier, p.ViewAngle, ARC_SEGMENTS, _buffer), dim);
            DrawSegments(StealthDebugGeometry.BuildCircle(ground, proximity, ARC_SEGMENTS, _buffer), color);
            DrawSegments(StealthDebugGeometry.BuildCircle(
                ground, proximity * config.sneakProximityMultiplier, ARC_SEGMENTS, _buffer), dim);

            if (p.Target != null && p.Target.StealthTarget != null)
            {
                GL.Color(p.HasLineOfSight ? Color.green : Color.red);
                GL.Vertex(eye);
                GL.Vertex(p.Target.StealthTarget.VisibilityPoint);
            }
            if (p.HasLastSeenPosition && p.TimeSinceSeen > 0f)
                DrawSegments(StealthDebugGeometry.BuildCircle(p.LastSeenPosition, LAST_SEEN_RADIUS, LAST_SEEN_SEGMENTS, _buffer), Color.magenta);
        }

        private void DrawSegments(int count, Color color)
        {
            GL.Color(color);
            for (int i = 0; i + 1 < count; i += 2)
            {
                GL.Vertex(_buffer[i]);
                GL.Vertex(_buffer[i + 1]);
            }
        }
    }
}
