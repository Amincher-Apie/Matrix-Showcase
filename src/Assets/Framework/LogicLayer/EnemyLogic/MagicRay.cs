using System.Collections;
using UnityEngine;

public class MagicRay : MonoBehaviour
{
    private BoxCollider _hitBox;
    private float _clock;

    private void Awake()
    {
        _hitBox = GetComponent<BoxCollider>();
        _clock = 2.0f;
        if (_hitBox != null)
            _hitBox.enabled = false;
    }

    private void Start()
    {
        StartCoroutine(OnStartMagic());
    }

    private void Update()
    {
        if (_clock >= 0)
            _clock -= Time.deltaTime;
        else
            Destroy(gameObject);
    }

    private IEnumerator OnStartMagic()
    {
        yield return new WaitForSeconds(0.25f);
        if (_hitBox != null)
            _hitBox.enabled = true;
    }
}
