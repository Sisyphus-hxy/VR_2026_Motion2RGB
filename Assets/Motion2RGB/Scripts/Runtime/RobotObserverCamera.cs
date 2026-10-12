using UnityEngine;

namespace Motion2RGB
{
    [DefaultExecutionOrder(250)]
    public class RobotObserverCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 localOffset = new Vector3(2.6f, 1.2f, -3.7f);

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 lookAt = target.position + Vector3.up * -0.1f;
            transform.position = target.TransformPoint(localOffset);
            transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
        }
    }
}
