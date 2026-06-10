using UnityEngine;

public class Health : MonoBehaviour
{
    private PlayerController _playerController;

    public int CurrentHealth => _playerController != null ? _playerController.CurrentHealth : 0;
    public bool IsDead => _playerController != null && _playerController.IsDead;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    public void TakeDamage(int amount, Mirror.NetworkConnectionToClient attacker)
    {
        if (_playerController != null)
            _playerController.ServerTakeDamage(amount, attacker);
    }
}
