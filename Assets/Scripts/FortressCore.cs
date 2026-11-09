using UnityEngine;
using System;

public class FortressCore : MonoBehaviour
{
    public static FortressCore Instance { get; private set; }

    [Header("Fortress Health")]
    public float maxHealth = 150f;
    public float currentHealth = 150f;

    public event Action<float, float> OnHealthChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        AudioHelper.PlaySound(SoundEffect.Hit, 0.9f);
        Debug.LogWarning($"[FortressCore] Breach! Took {amount} damage. Current HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public bool Repair(float amount, int materialCost)
    {
        if (currentHealth >= maxHealth) return false;
        if (ResourceManager.Instance == null || !ResourceManager.Instance.SpendMaterials(materialCost))
        {
            return false;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        AudioHelper.PlaySound(SoundEffect.BuildingPlace);
        Debug.Log($"[FortressCore] Repaired {amount} HP! Current HP: {currentHealth}/{maxHealth}");
        return true;
    }

    private void Die()
    {
        Debug.LogWarning("[FortressCore] The Last Fortress has fallen! Game Over!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFortressFell();
        }
    }
}
