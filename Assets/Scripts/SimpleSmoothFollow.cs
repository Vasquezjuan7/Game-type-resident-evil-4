using UnityEngine;

namespace Gameplay
{
    public class SimpleSmoothFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0.5f, 1.8f, -3.2f);
        public Vector3 lookOffset = new Vector3(0f, 1.4f, 0f);
        public float smoothSpeed = 10f;
        public float rotationSmooth = 12f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = target.position + target.TransformDirection(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            Vector3 lookTarget = target.position + lookOffset;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmooth * Time.deltaTime);
        }

        /// <summary>Coloca la cámara inmediatamente en la posición objetivo sin interpolación.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + target.TransformDirection(offset);
            Vector3 lookTarget = target.position + lookOffset;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }
    }
}

