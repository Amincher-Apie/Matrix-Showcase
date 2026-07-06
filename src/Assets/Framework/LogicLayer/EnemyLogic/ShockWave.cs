using UnityEngine;

public class ShockWave : MonoBehaviour
{
    private ParticleSystem _mainPartic;
    private SphereCollider _sphereCollider;
    public float speed = 10.8f;

    private void Start()
    {
        _mainPartic = GetComponent<ParticleSystem>();
        _sphereCollider = GetComponent<SphereCollider>();
    }

    private void Update()
    {
        if (_mainPartic != null && !_mainPartic.IsAlive())
            Destroy(_mainPartic.gameObject);

        if (_sphereCollider != null && _sphereCollider.radius <= 21.5f)
            _sphereCollider.radius += speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        /*if (other.CompareTag("Player") && other.GetComponent<PlayerController>().godClock <= 0)
        {
            if (other.transform.position.y <= 1.0f)
            {
                //other.GetComponent<PlayerController>().GetHit();
            }
        }*/
    }
}
