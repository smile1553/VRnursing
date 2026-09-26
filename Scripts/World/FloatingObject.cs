using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Float")]
    [SerializeField] private float amplitude = 0.15f;
    [SerializeField] private float frequency = 1.2f;

    [Header("Rotation")]
    [SerializeField] private bool rotate = false;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 45f, 0f);

    private Vector3 startLocalPosition;
    private float randomOffset;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        randomOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float yOffset = Mathf.Sin((Time.time * frequency) + randomOffset) * amplitude;
        transform.localPosition = startLocalPosition + new Vector3(0f, yOffset, 0f);

        if (rotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
