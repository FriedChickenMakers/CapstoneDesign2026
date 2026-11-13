using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed class IslandMotion : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 0.25f;
        [SerializeField] private float yawDegreesPerSecond = 3.5f;

        private Vector3 startPosition;

        private void Awake()
        {
            startPosition = transform.localPosition;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * Mathf.PI * 2f * bobFrequency) * bobAmplitude;
            transform.localPosition = startPosition + Vector3.up * wave;
            transform.Rotate(Vector3.up, yawDegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }

    public sealed class RewardPlantMotion : MonoBehaviour
    {
        [SerializeField] private float swayDegrees = 2.5f;
        [SerializeField] private float swayFrequency = 0.35f;

        private Quaternion startRotation;

        private void Awake()
        {
            startRotation = transform.localRotation;
        }

        private void Update()
        {
            float sway = Mathf.Sin(Time.time * Mathf.PI * 2f * swayFrequency) * swayDegrees;
            transform.localRotation = startRotation * Quaternion.Euler(0f, sway, 0f);
        }
    }
}
