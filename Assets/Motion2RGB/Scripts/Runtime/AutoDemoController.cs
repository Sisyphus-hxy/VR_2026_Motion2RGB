using UnityEngine;

namespace Motion2RGB
{
    public class AutoDemoController : MonoBehaviour
    {
        public FirstPersonController firstPersonController;
        public bool autoDemoEnabled;
        public float loopDuration = 22f;
        public float pathRadius = 5.5f;
        public float cruiseHeight = 1.05f;
        public Transform[] patrolWaypoints;
        private float timeInLoop;
        private Vector3 startPosition;
        private Quaternion startRotation;

        private void Awake()
        {
            if (firstPersonController == null) firstPersonController = GetComponent<FirstPersonController>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F)) SetEnabled(!autoDemoEnabled);
            if (!autoDemoEnabled) return;

            timeInLoop = Mathf.Repeat(timeInLoop + Time.deltaTime, loopDuration);
            if (patrolWaypoints != null && patrolWaypoints.Length > 1)
            {
                ApplyWaypointCruise();
                return;
            }

            float t = timeInLoop;
            Vector3 targetPosition = startPosition;
            Quaternion targetRotation = startRotation;
            if (t < 2f)
            {
                targetPosition = startPosition;
                targetRotation = startRotation;
            }
            else if (t < 9f)
            {
                float u = Mathf.InverseLerp(2f, 9f, t);
                targetPosition = startPosition + Vector3.forward * (u * 10f);
                targetRotation = Quaternion.Euler(0f, 0f, 0f);
            }
            else if (t < 14f)
            {
                float u = Mathf.InverseLerp(9f, 14f, t);
                targetPosition = startPosition + Vector3.forward * 10f + Vector3.right * (u * 5f);
                targetRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 90f, u), 0f);
            }
            else if (t < 19f)
            {
                float u = Mathf.InverseLerp(14f, 19f, t);
                targetPosition = startPosition + Vector3.forward * (10f - u * 5f) + Vector3.right * 5f;
                targetRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                float u = Mathf.InverseLerp(19f, loopDuration, t);
                targetPosition = startPosition + Vector3.forward * 5f + Vector3.right * (5f - u * 5f);
                targetRotation = Quaternion.Euler(0f, Mathf.Lerp(90f, 180f, u), 0f);
            }

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (cc != null) cc.enabled = true;
        }

        private void ApplyWaypointCruise()
        {
            int count = patrolWaypoints.Length;
            float routePosition = timeInLoop / Mathf.Max(0.1f, loopDuration) * count;
            int index = Mathf.FloorToInt(routePosition) % count;
            float blend = routePosition - Mathf.Floor(routePosition);
            blend = blend * blend * (3f - 2f * blend);
            Transform current = patrolWaypoints[index];
            Transform next = patrolWaypoints[(index + 1) % count];
            if (current == null || next == null) return;
            Vector3 targetPosition = Vector3.Lerp(current.position, next.position, blend);
            Vector3 forward = next.position - current.position;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            if (cc != null) cc.enabled = true;
        }

        public void ConfigureRoute(Transform[] waypoints)
        {
            patrolWaypoints = waypoints;
            if (waypoints != null && waypoints.Length > 1)
            {
                startPosition = waypoints[0].position;
                Vector3 initialForward = waypoints[1].position - waypoints[0].position;
                if (initialForward.sqrMagnitude > 0.001f) startRotation = Quaternion.LookRotation(initialForward.normalized, Vector3.up);
                transform.position = startPosition;
                transform.rotation = startRotation;
            }
        }

        public void SetEnabled(bool enabled)
        {
            autoDemoEnabled = enabled;
            timeInLoop = 0f;
            if (firstPersonController != null) firstPersonController.SetManualInput(!enabled);
            if (!enabled)
            {
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.SetPositionAndRotation(startPosition, startRotation);
                if (cc != null) cc.enabled = true;
            }
        }
    }
}
