using UnityEngine;
using Mirror;

public enum WeaponFireMode
{
    SemiAuto = 0,
    FullAuto = 1
}

public class CombatSystem : NetworkBehaviour
{
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float bulletSpeed = 80f;
    public float fireInterval = 0.15f;
    public float hitscanDistance = 100f;
    public int damage = 25;
    public bool useLagCompensation = true;
    public LayerMask aimBlockerMask = ~0;

    [Header("Weapon Feel")]
    public WeaponFireMode defaultFireMode = WeaponFireMode.SemiAuto;
    public float hipSpread = 1.2f;
    public float aimSpread = 0.25f;
    public float recoilPitch = 1.1f;
    public float recoilYaw = 0.25f;

    [Header("Ammo")]
    public int magazineSize = 30;
    public int reserveAmmo = 90;
    public float reloadDuration = 1.6f;
    public bool autoReload = true;

    [SyncVar] private int _currentAmmo;
    [SyncVar] private int _currentReserveAmmo;
    [SyncVar] private bool _isReloading;
    [SyncVar] private WeaponFireMode _fireMode;

    private double _nextAllowedFireTime;
    private PlayerAnimCtrl _animCtrl;
    private PlayerMotionCore _motionCore;
    private double _reloadEndTime;
    private float _nextLocalFireTime;

    public int CurrentAmmo => _currentAmmo;
    public int CurrentReserveAmmo => _currentReserveAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => _isReloading;
    public WeaponFireMode FireMode => _fireMode;
    public string FireModeLabel => _fireMode == WeaponFireMode.FullAuto ? "AUTO" : "SEMI";

    private void Awake()
    {
        _animCtrl = GetComponent<PlayerAnimCtrl>();
        _motionCore = GetComponent<PlayerMotionCore>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentAmmo = magazineSize;
        _currentReserveAmmo = reserveAmmo;
        _isReloading = false;
        _fireMode = defaultFireMode;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(KeyCode.R))
            CmdReload();

        if (Input.GetKeyDown(KeyCode.B))
            CmdToggleFireMode();

        bool wantsFire = _fireMode == WeaponFireMode.FullAuto
            ? Input.GetMouseButton(0)
            : Input.GetMouseButtonDown(0);

        if (wantsFire)
            TryFire();
    }

    private void TryFire()
    {
        if (Time.time < _nextLocalFireTime) return;
        if (_isReloading) return;
        if (_currentAmmo <= 0)
        {
            if (autoReload)
                CmdReload();
            return;
        }

        _nextLocalFireTime = Time.time + fireInterval;
        Vector3 fireOrigin = firePoint != null ? firePoint.position : transform.position;
        Vector3 cameraOrigin;
        Vector3 cameraDirection;

        if (Camera.main != null)
        {
            cameraOrigin = Camera.main.transform.position;
            cameraDirection = Camera.main.transform.forward;
        }
        else
        {
            cameraOrigin = fireOrigin;
            cameraDirection = transform.forward;
        }

        bool isAiming = _motionCore != null && _motionCore.MotionData.AimInput;
        ApplyLocalRecoil();
        CmdFire(NetworkTime.time, cameraOrigin, cameraDirection, fireOrigin, isAiming);
    }

    [Command]
    public void CmdFire(double clientFireTime, Vector3 cameraOrigin, Vector3 cameraDirection, Vector3 fireOrigin, bool isAiming)
    {
        ServerFire(clientFireTime, cameraOrigin, cameraDirection, fireOrigin, isAiming, connectionToClient);
    }

    [Server]
    public void ServerBotFire(Vector3 targetPoint, float spreadDegrees)
    {
        if (firePoint == null)
            return;

        Vector3 fireOrigin = firePoint.position;
        Vector3 direction = targetPoint - fireOrigin;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        ServerFire(NetworkTime.time, fireOrigin, direction.normalized, fireOrigin, true, null, spreadDegrees);
    }

    [Server]
    private void ServerFire(double clientFireTime, Vector3 cameraOrigin, Vector3 cameraDirection, Vector3 fireOrigin, bool isAiming, NetworkConnectionToClient attackerConnection)
    {
        ServerFire(clientFireTime, cameraOrigin, cameraDirection, fireOrigin, isAiming, attackerConnection, -1f);
    }

    [Server]
    private void ServerFire(double clientFireTime, Vector3 cameraOrigin, Vector3 cameraDirection, Vector3 fireOrigin, bool isAiming, NetworkConnectionToClient attackerConnection, float overrideSpread)
    {
        if (bulletPrefab == null || firePoint == null) return;
        if (NetworkTime.time < _nextAllowedFireTime) return;
        if (_isReloading) return;

        if (_currentAmmo <= 0)
        {
            if (autoReload)
                ServerStartReload();
            return;
        }

        _nextAllowedFireTime = NetworkTime.time + fireInterval;
        _currentAmmo--;
        cameraDirection = cameraDirection.sqrMagnitude > 0.0001f ? cameraDirection.normalized : transform.forward;
        cameraDirection = ApplySpread(cameraDirection, isAiming, overrideSpread);

        Vector3 aimPoint = ResolveAimPoint(cameraOrigin, cameraDirection);
        Vector3 fireDirection = GetDirectionFromFirePoint(fireOrigin, aimPoint, cameraDirection);
        Vector3 visualTargetPoint = ResolveProjectileBlockPoint(fireOrigin, fireDirection, hitscanDistance, out float blockDistance);
        if (useLagCompensation &&
            LagCompensationSystem.RaycastHitboxes(attackerConnection, clientFireTime, fireOrigin, fireDirection, hitscanDistance, out LagCompensationTarget.HitResult hit))
        {
            if (blockDistance <= hit.distance)
            {
                SpawnBulletVisual(fireOrigin, visualTargetPoint, fireDirection, attackerConnection);
                RpcPlayFireAnimation();
                if (_currentAmmo <= 0 && autoReload)
                    ServerStartReload();
                return;
            }

            visualTargetPoint = hit.point;
            Health health = hit.target.GetComponent<Health>();
            if (health != null)
                health.TakeDamage(Mathf.RoundToInt(damage * hit.damageMultiplier), attackerConnection);

            NetworkIdentity hitIdentity = hit.target.GetComponent<NetworkIdentity>();
            if (hitIdentity != null)
                RpcPlayHitAnimation(hitIdentity.netId);
            if (attackerConnection != null)
                TargetShowHitMarker(attackerConnection, hit.damageMultiplier >= 1.5f);
            Debug.Log($"Hit {hit.target.name} {hit.hitbox?.hitGroup.ToString() ?? "Body"} at {hit.point}, x{hit.damageMultiplier}");
        }

        SpawnBulletVisual(fireOrigin, visualTargetPoint, fireDirection, attackerConnection);
        RpcPlayFireAnimation();

        if (_currentAmmo <= 0 && autoReload)
            ServerStartReload();
    }

    [Command]
    public void CmdReload()
    {
        ServerStartReload();
    }

    [Command]
    public void CmdToggleFireMode()
    {
        _fireMode = _fireMode == WeaponFireMode.SemiAuto
            ? WeaponFireMode.FullAuto
            : WeaponFireMode.SemiAuto;
    }

    [Server]
    private void ServerStartReload()
    {
        if (_isReloading) return;
        if (_currentAmmo >= magazineSize) return;
        if (_currentReserveAmmo <= 0) return;

        _isReloading = true;
        _reloadEndTime = NetworkTime.time + reloadDuration;
        Invoke(nameof(ServerFinishReload), reloadDuration);
        RpcPlayReloadAnimation();
    }

    [Server]
    private void ServerFinishReload()
    {
        if (!_isReloading) return;
        if (NetworkTime.time + 0.05f < _reloadEndTime) return;

        int neededAmmo = magazineSize - _currentAmmo;
        int ammoToLoad = Mathf.Min(neededAmmo, _currentReserveAmmo);
        _currentAmmo += ammoToLoad;
        _currentReserveAmmo -= ammoToLoad;
        _isReloading = false;
    }

    private Vector3 ResolveAimPoint(Vector3 cameraOrigin, Vector3 cameraDirection)
    {
        Ray ray = new Ray(cameraOrigin, cameraDirection);
        if (Physics.Raycast(ray, out RaycastHit hit, hitscanDistance, aimBlockerMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return cameraOrigin + cameraDirection * hitscanDistance;
    }

    private Vector3 ApplySpread(Vector3 direction, bool isAiming, float overrideSpread = -1f)
    {
        float spread = overrideSpread >= 0f ? overrideSpread : (isAiming ? aimSpread : hipSpread);
        if (spread <= 0f) return direction;

        Vector3 forward = direction.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;

        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;
        Vector2 offset = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
        return (forward + right * offset.x + up * offset.y).normalized;
    }

    private Vector3 ResolveProjectileBlockPoint(Vector3 origin, Vector3 direction, float maxDistance, out float blockDistance)
    {
        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, aimBlockerMask, QueryTriggerInteraction.Ignore))
        {
            blockDistance = hit.distance;
            return hit.point;
        }

        blockDistance = maxDistance;
        return origin + direction * maxDistance;
    }

    [Server]
    private void SpawnBulletVisual(Vector3 fireOrigin, Vector3 visualTargetPoint, Vector3 fallbackDirection, NetworkConnectionToClient owner)
    {
        Vector3 bulletDirection = GetDirectionFromFirePoint(fireOrigin, visualTargetPoint, fallbackDirection);
        Quaternion bulletRotation = Quaternion.LookRotation(bulletDirection);
        GameObject bullet = Instantiate(bulletPrefab, fireOrigin, bulletRotation);
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
            bulletScript.Fire(bulletDirection * bulletSpeed, owner);

        NetworkServer.Spawn(bullet);
    }

    private void ApplyLocalRecoil()
    {
        if (Camera.main == null) return;

        TPSFollowCam followCam = Camera.main.GetComponent<TPSFollowCam>();
        if (followCam == null) return;

        float yawKick = Random.Range(-recoilYaw, recoilYaw);
        followCam.AddRecoil(recoilPitch, yawKick);
    }

    private Vector3 GetDirectionFromFirePoint(Vector3 fireOrigin, Vector3 targetPoint, Vector3 fallbackDirection)
    {
        Vector3 direction = targetPoint - fireOrigin;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallbackDirection;
    }

    [ClientRpc]
    private void RpcPlayFireAnimation()
    {
        _animCtrl ??= GetComponent<PlayerAnimCtrl>();
        if (_animCtrl != null)
            _animCtrl.PlayFire();
    }

    [ClientRpc]
    private void RpcPlayReloadAnimation()
    {
        _animCtrl ??= GetComponent<PlayerAnimCtrl>();
        if (_animCtrl != null)
            _animCtrl.PlayReloading();
    }

    [ClientRpc]
    private void RpcPlayHitAnimation(uint targetNetId)
    {
        if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity identity)) return;

        PlayerAnimCtrl targetAnim = identity.GetComponent<PlayerAnimCtrl>();
        if (targetAnim != null)
            targetAnim.PlayHit();
    }

    [TargetRpc]
    private void TargetShowHitMarker(NetworkConnectionToClient target, bool isCritical)
    {
        GameplayHUD.NotifyHitMarker(isCritical);
    }
}
