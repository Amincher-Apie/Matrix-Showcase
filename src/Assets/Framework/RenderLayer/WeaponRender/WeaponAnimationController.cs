using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a weapon Animator from combat presentation events.
/// Attach this to the weapon prefab root that also owns the weapon Animator.
/// </summary>
[DisallowMultipleComponent]
public class WeaponAnimationController : MonoBehaviour
{
    private static readonly int IsFiringHash = Animator.StringToHash("IsFiring");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int FireSpinSpeedHash = Animator.StringToHash("FireSpinSpeed");

    [SerializeField] private Animator _animator;
    [SerializeField] private float _singleShotSpinHoldSeconds = 0.12f;
    [SerializeField] private bool _useFireSpinSpeed;

    private ulong _ownerActorId;
    private string _weaponId;
    private bool _isBound;
    private Coroutine _firePulseRoutine;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        }
    }

    private void OnEnable()
    {
        EventCenter.Instance.AddListener<WeaponFiredEvt>(EventName.LocalWeaponFired, OnLocalWeaponFired);
        EventCenter.Instance.AddListener<WeaponFiredEvt>(EventName.RemoteWeaponFired, OnRemoteWeaponFired);
        EventCenter.Instance.AddListener<ReloadEvt>(EventName.ReloadStarted, OnReloadStarted);
        EventCenter.Instance.AddListener<ReloadEvt>(EventName.ReloadFinished, OnReloadFinished);
    }

    private void OnDisable()
    {
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.RemoveListener<WeaponFiredEvt>(EventName.LocalWeaponFired, OnLocalWeaponFired);
            EventCenter.Instance.RemoveListener<WeaponFiredEvt>(EventName.RemoteWeaponFired, OnRemoteWeaponFired);
            EventCenter.Instance.RemoveListener<ReloadEvt>(EventName.ReloadStarted, OnReloadStarted);
            EventCenter.Instance.RemoveListener<ReloadEvt>(EventName.ReloadFinished, OnReloadFinished);
        }

        StopFire();
    }

    public void BindOwner(ulong ownerActorId, string weaponId)
    {
        _ownerActorId = ownerActorId;
        _weaponId = weaponId;
        _isBound = ownerActorId != 0;
    }

    public void PlayFirePulse(float holdSeconds = -1f)
    {
        if (_animator == null)
        {
            return;
        }

        if (_firePulseRoutine != null)
        {
            StopCoroutine(_firePulseRoutine);
        }

        float duration = holdSeconds > 0f ? holdSeconds : _singleShotSpinHoldSeconds;
        _firePulseRoutine = StartCoroutine(FirePulseRoutine(duration));
    }

    public void StopFire()
    {
        if (_firePulseRoutine != null)
        {
            StopCoroutine(_firePulseRoutine);
            _firePulseRoutine = null;
        }

        if (_animator != null)
        {
            _animator.SetBool(IsFiringHash, false);
        }
    }

    public void PlayReload(float duration)
    {
        if (_animator == null)
        {
            return;
        }

        StopFire();
        _animator.ResetTrigger(ReloadHash);
        _animator.SetTrigger(ReloadHash);
    }

    public void SetFireSpinSpeed(float speed)
    {
        if (_animator == null || !_useFireSpinSpeed)
        {
            return;
        }

        _animator.SetFloat(FireSpinSpeedHash, speed);
    }

    private IEnumerator FirePulseRoutine(float duration)
    {
        _animator.SetBool(IsFiringHash, true);
        yield return new WaitForSeconds(duration);
        _animator.SetBool(IsFiringHash, false);
        _firePulseRoutine = null;
    }

    private void OnLocalWeaponFired(WeaponFiredEvt evt)
    {
        if (!Matches(evt.actorId, evt.weaponId))
        {
            return;
        }

        PlayFirePulse();
    }

    private void OnRemoteWeaponFired(WeaponFiredEvt evt)
    {
        if (evt.isLocalPlayer)
        {
            return;
        }

        if (!Matches(evt.actorId, evt.weaponId))
        {
            return;
        }

        PlayFirePulse();
    }

    private void OnReloadStarted(ReloadEvt evt)
    {
        if (!Matches(evt.actorId, evt.weaponId))
        {
            return;
        }

        PlayReload(evt.duration);
    }

    private void OnReloadFinished(ReloadEvt evt)
    {
        if (!Matches(evt.actorId, evt.weaponId))
        {
            return;
        }

        StopFire();
    }

    private bool Matches(ulong actorId, string weaponId)
    {
        if (!_isBound || actorId != _ownerActorId)
        {
            return false;
        }

        return string.IsNullOrEmpty(_weaponId) || string.IsNullOrEmpty(weaponId) || weaponId == _weaponId;
    }
}
