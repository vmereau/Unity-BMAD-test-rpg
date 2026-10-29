using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bow accuracy ring around the crosshair (<c>UICanvas/Game/AimSpreadRing</c>, an <see cref="AimSpreadRingGraphic"/>).
    /// Shown only while aiming a bow; its diameter is the current spread cone seen through the live camera FOV, so it
    /// tracks the aim zoom.
    /// Polled in LateUpdate from <see cref="PlayerCombat.TryGetAimSpread"/> (a continuous value, no event), sized with
    /// <see cref="PlayerCombat.AimCamera"/> — the camera the shot is cast from. Own nested Canvas: the per-frame resize
    /// while shrinking only rebatches the ring.
    /// <c>_playerCombat</c> is a <c>Player.prefab</c> override (UICanvas is nested in the Player).
    /// </summary>
    public class AimSpreadUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        [SerializeField] private PlayerCombat _playerCombat;
        [Tooltip("Ring graphic (AimSpreadRingGraphic — constant pixel stroke) sized to the spread.")]
        [SerializeField] private Graphic _ring;
        [Tooltip("Smallest ring diameter (px) — a fully focused shot still shows a ring just around the crosshair.")]
        [SerializeField, Min(0f)] private float _minDiameter = 18f;

        // Smallest diameter change (canvas units) worth a mesh rebuild.
        private const float RESIZE_EPSILON = 0.25f;

        private void Awake()
        {
            if (_ring == null)
            {
                GameLog.Error(TAG, "AimSpreadUI: _ring not assigned");
                enabled = false;
                return;
            }
            if (_playerCombat == null)
                GameLog.Warn(TAG, "AimSpreadUI: _playerCombat not assigned — the aim ring stays hidden");
            _ring.raycastTarget = false;
            _ring.enabled = false;
        }

        private void OnDisable()
        {
            if (_ring != null) _ring.enabled = false;
        }

        private void LateUpdate()
        {
            Camera cam = _playerCombat != null ? _playerCombat.AimCamera : null;
            if (cam == null || !_playerCombat.TryGetAimSpread(out float angle))
            {
                if (_ring.enabled) _ring.enabled = false;
                return;
            }

            // Same camera as the shot. Pixels → canvas units (scale factor is 1 today: no CanvasScaler).
            float scale = _ring.canvas != null && _ring.canvas.scaleFactor > 0f ? _ring.canvas.scaleFactor : 1f;
            float radius = BowSpreadMath.ScreenRadius(angle, cam.fieldOfView, cam.pixelHeight) / scale;
            float diameter = Mathf.Max(_minDiameter, radius * 2f);
            RectTransform rt = _ring.rectTransform;
            if (Mathf.Abs(rt.sizeDelta.x - diameter) > RESIZE_EPSILON)
                rt.sizeDelta = new Vector2(diameter, diameter);
            if (!_ring.enabled) _ring.enabled = true;
        }
    }
}
