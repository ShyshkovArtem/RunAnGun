using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

namespace RunGun.Levels
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Movement Placement Guide")]
    public sealed class MovementPlacementGuide : MonoBehaviour
    {
        public enum MovementPreset
        {
            WalkJump,
            RunJump,
            SlideJump,
            FastSlideJump,
            BunnyHop,
            Custom
        }

        [Header("Path")]
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform targetPoint;
        [SerializeField] private bool aimAtTarget;
        [SerializeField] private MovementPreset preset = MovementPreset.RunJump;

        [Header("Movement Values")]
        [SerializeField] private float walkSpeed = 6f;
        [SerializeField] private float runSpeed = 9f;
        [SerializeField] private float slideJumpSpeed = 12.5f;
        [SerializeField] private float fastSlideJumpSpeed = 18f;
        [SerializeField] private float bunnyHopSpeed = 16f;
        [SerializeField] private float customSpeed = 10f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;

        [Header("Landing")]
        [SerializeField] private float landingHeightOffset;
        [SerializeField] private float landingTolerance = 0.45f;
        [SerializeField] private float targetRadius = 0.75f;

        [Header("Drawing")]
        [SerializeField] private bool drawGuide = true;
        [SerializeField, Range(8, 80)] private int sampleCount = 36;
        [SerializeField] private Color arcColor = new Color(0f, 0.85f, 1f, 1f);
        [SerializeField] private Color landingColor = new Color(0.2f, 1f, 0.35f, 1f);
        [SerializeField] private Color missedTargetColor = new Color(1f, 0.25f, 0.2f, 1f);

        private readonly List<Vector3> _samples = new();

        public MovementPreset Preset => preset;
        public float HorizontalSpeed => GetPresetSpeed();
        public float InitialVerticalSpeed => Mathf.Sqrt(Mathf.Max(0f, jumpHeight * -2f * gravity));
        public float FlightTime => CalculateFlightTime();
        public Vector3 LandingPoint => CalculatePositionAtTime(FlightTime);
        public bool HasTarget => targetPoint != null;

        public bool TargetIsReachable
        {
            get
            {
                if (targetPoint == null)
                    return false;

                Vector3 landing = LandingPoint;
                Vector2 landingXZ = new(landing.x, landing.z);
                Vector2 targetXZ = new(targetPoint.position.x, targetPoint.position.z);
                float horizontalDistance = Vector2.Distance(landingXZ, targetXZ);
                float verticalDistance = Mathf.Abs(landing.y - targetPoint.position.y);
                return horizontalDistance <= targetRadius && verticalDistance <= landingTolerance;
            }
        }

        public void LoadFromPlayer(PlayerController player)
        {
            if (player == null)
                return;

            walkSpeed = player.speed;
            runSpeed = player.runSpeed;
            slideJumpSpeed = player.slideStartSpeed;
            fastSlideJumpSpeed = Mathf.Min(player.maxSlideSpeed, Mathf.Max(player.slideStartSpeed, player.runSpeed + player.slideStartBoost));
            bunnyHopSpeed = Mathf.Min(player.maxAirSpeed, Mathf.Max(player.runSpeed, player.jumpRedirectMinSpeed));
            jumpHeight = player.jumpHeight;
            gravity = player.gravity;
        }

        public IReadOnlyList<Vector3> GetSamples()
        {
            _samples.Clear();

            float time = FlightTime;
            int count = Mathf.Max(2, sampleCount);
            for (int i = 0; i < count; i++)
            {
                float t = time * i / (count - 1);
                _samples.Add(CalculatePositionAtTime(t));
            }

            return _samples;
        }

        public Vector3 GetOrigin()
        {
            return startPoint != null ? startPoint.position : transform.position;
        }

        public Vector3 GetDirection()
        {
            if (aimAtTarget && targetPoint != null)
            {
                Vector3 toTarget = targetPoint.position - GetOrigin();
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                    return toTarget.normalized;
            }

            Vector3 direction = transform.forward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        public float GetHorizontalDistance()
        {
            return HorizontalSpeed * FlightTime;
        }

        private float GetPresetSpeed()
        {
            return preset switch
            {
                MovementPreset.WalkJump => walkSpeed,
                MovementPreset.RunJump => runSpeed,
                MovementPreset.SlideJump => slideJumpSpeed,
                MovementPreset.FastSlideJump => fastSlideJumpSpeed,
                MovementPreset.BunnyHop => bunnyHopSpeed,
                MovementPreset.Custom => customSpeed,
                _ => runSpeed
            };
        }

        private Vector3 CalculatePositionAtTime(float time)
        {
            Vector3 origin = GetOrigin();
            Vector3 horizontal = GetDirection() * (HorizontalSpeed * time);
            float vertical = (InitialVerticalSpeed * time) + (0.5f * gravity * time * time);
            return origin + horizontal + Vector3.up * vertical;
        }

        private float CalculateFlightTime()
        {
            float gravityMagnitude = Mathf.Max(0.01f, -gravity);
            float landingDelta = landingHeightOffset;
            if (targetPoint != null)
                landingDelta = targetPoint.position.y - GetOrigin().y;

            float verticalSpeed = InitialVerticalSpeed;
            float discriminant = (verticalSpeed * verticalSpeed) - (2f * gravityMagnitude * landingDelta);
            if (discriminant < 0f)
                return verticalSpeed / gravityMagnitude;

            return Mathf.Max(0.01f, (verticalSpeed + Mathf.Sqrt(discriminant)) / gravityMagnitude);
        }

        private void OnDrawGizmos()
        {
            if (!drawGuide)
                return;

            IReadOnlyList<Vector3> samples = GetSamples();
            if (samples.Count < 2)
                return;

            Gizmos.color = targetPoint == null || TargetIsReachable ? arcColor : missedTargetColor;
            for (int i = 1; i < samples.Count; i++)
                Gizmos.DrawLine(samples[i - 1], samples[i]);

            Gizmos.color = landingColor;
            Gizmos.DrawWireSphere(LandingPoint, 0.25f);

            if (targetPoint == null)
                return;

            Gizmos.color = TargetIsReachable ? landingColor : missedTargetColor;
            Gizmos.DrawWireSphere(targetPoint.position, targetRadius);
            Gizmos.DrawLine(LandingPoint, targetPoint.position);
        }
    }
}
