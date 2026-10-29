using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Player-only post-animation bow aim fix: while aiming, turns Spine → Chest → UpperChest (yaw about world up,
    /// then pitch) so the chest-locked arrow line points at the crosshair aim point. The Mixamo archer clips are
    /// authored side-on and the hips come from locomotion, so no fixed body yaw lines the bow up on its own.
    /// Writes bone transforms after the Animator — never animator parameters. Blended in / out with the aim.
    /// </summary>
    [DefaultExecutionOrder(200)] // after CameraController (0) and CinemachineBrain (100): this frame's camera pose
    public class BowAimCorrection : MonoBehaviour
    {
        private const string TAG = "[BowAim]";
        private const float MIN_LINE_SQR = 1e-6f;
        // Remaining yaw beyond which the correction fades out (target behind the arrow line — sign unstable at ±180°).
        private const float YAW_REACH_FULL = 120f;
        private const float YAW_REACH_ZERO = 160f;

        private static readonly HumanBodyBones[] SpineBones =
            { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest };

        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerStateManager _stateManager;
        [SerializeField] private PlayerCombat _playerCombat;
        [SerializeField] private CombatConfigSO _config;

        [Tooltip("Arrow line direction (right hand → left hand in the Aim Overdraw pose), in the top spine bone's " +
                 "(UpperChest) local space. Calibrated by sampling the clip on the rig — see Scripts/Combat/CLAUDE.md.")]
        [SerializeField] private Vector3 _arrowLineLocal;
        [Tooltip("Arrow line origin (right hand in the Aim Overdraw pose), in the top spine bone's local space.")]
        [SerializeField] private Vector3 _arrowOriginLocal;

        private Transform[] _bones;
        private Transform _referenceBone; // top spine bone — the arrow line is locked to it
        private Quaternion[] _animatedLocal; // pose the Animator wrote this frame
        private Quaternion[] _writtenLocal;  // pose this component wrote last frame
        private bool _hasWritten;
        private Camera _camera;
        private bool _cameraWarned;
        private Vector3 _lastAimPoint;
        private bool _hasAimPoint;
        private float _weight; // 0 = animated pose, 1 = full correction

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator == null || !_animator.isHuman || _stateManager == null || _playerCombat == null
                || _config == null)
            {
                GameLog.Error(TAG, "BowAimCorrection: needs a humanoid Animator, PlayerStateManager, PlayerCombat " +
                                   "and CombatConfigSO — disabled");
                enabled = false;
                return;
            }
            if (_arrowLineLocal.sqrMagnitude < MIN_LINE_SQR)
            {
                GameLog.Error(TAG, "BowAimCorrection: _arrowLineLocal not calibrated — disabled");
                enabled = false;
                return;
            }
            if (_animator.GetBoneTransform(HumanBodyBones.UpperChest) == null)
            {
                GameLog.Error(TAG, "BowAimCorrection: rig has no UpperChest (the arrow line is calibrated in it) — disabled");
                enabled = false;
                return;
            }

            int count = 0;
            var found = new Transform[SpineBones.Length];
            foreach (HumanBodyBones bone in SpineBones)
            {
                Transform t = _animator.GetBoneTransform(bone);
                if (t != null) found[count++] = t;
            }
            if (count < SpineBones.Length)
                GameLog.Warn(TAG, $"BowAimCorrection: only {count} of {SpineBones.Length} spine bones mapped — " +
                                  "less reach per aim");
            _bones = new Transform[count];
            System.Array.Copy(found, _bones, count);
            _referenceBone = _bones[count - 1];
            _animatedLocal = new Quaternion[count];
            _writtenLocal = new Quaternion[count];
        }

        private void OnDisable()
        {
            RestoreAnimatedPose();
            _weight = 0f;
            _hasAimPoint = false;
        }

        private void LateUpdate()
        {
            if (_bones == null) // Awake bailed out; re-enabled from outside
            {
                enabled = false;
                return;
            }

            CaptureAnimatedPose();

            bool active = _stateManager.IsAiming && _config.aimCorrectionEnabled;
            // Unscaled, same blend as the aim camera (CameraController): the menu / death screen set timeScale = 0.
            _weight = Mathf.MoveTowards(_weight, active ? 1f : 0f, _config.aimBlendSpeed * Time.unscaledDeltaTime);
            if (_weight <= 0f)
            {
                _hasAimPoint = false;
                return;
            }

            // Hold the last point while blending out after the bow was unequipped.
            if (_playerCombat.TryGetAimPoint(out Vector3 aimPoint))
            {
                _lastAimPoint = aimPoint;
                _hasAimPoint = true;
            }
            if (!_hasAimPoint) return;

            Vector3 target = PushOutClosePoint(_lastAimPoint);
            Vector2 total = MeasureDelta(target);
            float weight = _weight * AimCorrectionMath.YawReachFactor(total.x, YAW_REACH_FULL, YAW_REACH_ZERO);
            if (weight <= 0f) return;

            // Linear blend: hold back (1 − weight) of the animated-pose delta, solve the rest exactly.
            Vector2 heldBack = total * (1f - weight);
            float max = _config.aimCorrectionMaxAnglePerBone;
            for (int i = 0; i < _bones.Length; i++)
                RotateBone(_bones[i], AimCorrectionMath.BoneStep(MeasureDelta(target) - heldBack, _bones.Length - i, max));
            // Residual pass: the last rotation moved the hand origin too — re-measure once more.
            RotateBone(_referenceBone, AimCorrectionMath.BoneStep(MeasureDelta(target) - heldBack, 1, max));

            for (int i = 0; i < _bones.Length; i++) _writtenLocal[i] = _bones[i].localRotation;
            _hasWritten = true;
        }

        // The Animator normally rewrites the spine every frame. If it didn't (paused / not evaluated), the bones still
        // hold last frame's correction — put the animated pose back so corrections never stack.
        private void CaptureAnimatedPose()
        {
            RestoreAnimatedPose();
            for (int i = 0; i < _bones.Length; i++) _animatedLocal[i] = _bones[i].localRotation;
        }

        private void RestoreAnimatedPose()
        {
            if (!_hasWritten || _bones == null) return;
            for (int i = 0; i < _bones.Length; i++)
                if (_bones[i].localRotation == _writtenLocal[i]) _bones[i].localRotation = _animatedLocal[i];
            _hasWritten = false;
        }

        private Vector2 MeasureDelta(Vector3 target)
        {
            Vector3 origin = _referenceBone.TransformPoint(_arrowOriginLocal);
            Vector3 line = _referenceBone.TransformDirection(_arrowLineLocal);
            return AimCorrectionMath.YawPitchDelta(line, target - origin);
        }

        private void RotateBone(Transform bone, Vector2 step)
        {
            Quaternion yaw = Quaternion.AngleAxis(step.x, Vector3.up);
            Vector3 flat = yaw * _referenceBone.TransformDirection(_arrowLineLocal);
            flat.y = 0f;
            Quaternion pitch = flat.sqrMagnitude < MIN_LINE_SQR
                ? Quaternion.identity
                // Pitch about the line's horizontal right axis; a positive X rotation tilts down, hence −step.y.
                : Quaternion.AngleAxis(-step.y, Vector3.Cross(Vector3.up, flat).normalized);
            bone.rotation = pitch * yaw * bone.rotation;
        }

        // An aim point right in front of the player (wall, close NPC) would whip the spine: push it out along the
        // crosshair ray to at least aimCorrectionMinDistance past the bow.
        private Vector3 PushOutClosePoint(Vector3 aimPoint)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    if (!_cameraWarned) GameLog.Warn(TAG, "Camera.main not found — close aim points are not pushed out");
                    _cameraWarned = true;
                    return aimPoint;
                }
            }
            Transform cam = _camera.transform;
            Vector3 forward = cam.forward;
            float along = Vector3.Dot(aimPoint - cam.position, forward);
            float minAlong = Vector3.Dot(_referenceBone.TransformPoint(_arrowOriginLocal) - cam.position, forward)
                             + _config.aimCorrectionMinDistance;
            return along >= minAlong ? aimPoint : cam.position + forward * minAlong;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_animator == null || !_animator.isHuman) return;
            Transform reference = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (reference == null || _arrowLineLocal.sqrMagnitude < MIN_LINE_SQR) return;
            Vector3 origin = reference.TransformPoint(_arrowOriginLocal);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, reference.TransformDirection(_arrowLineLocal).normalized * 20f);
        }
#endif
    }
}
