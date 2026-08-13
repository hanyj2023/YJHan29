using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHP = 100f;

    [SerializeField, Min(0f)]
    private float invincibleTime = 1f;

    [Header("References")]
    [SerializeField]
    private Image hpImage = null;

    [SerializeField]
    private SpriteRenderer playerSprite = null;

    [Header("Hit Effect")]
    [SerializeField, Min(0.02f)]
    private float blinkInterval = 0.1f;

    private float currentHP;
    private bool isInvincible;
    private bool isDead;
    private Coroutine invincibilityRoutine;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public bool IsInvincible => isInvincible;
    public bool IsDead => isDead;

    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        if (hpImage == null)
        {
            Image[] childImages = GetComponentsInChildren<Image>(true);
            foreach (Image childImage in childImages)
            {
                if (childImage.name == "HPImage")
                {
                    hpImage = childImage;
                    break;
                }
            }
        }

        if (playerSprite == null)
        {
            playerSprite = GetComponentInChildren<SpriteRenderer>(true);
        }

        currentHP = maxHP;
        UpdateHealthUI();
    }

    /// <summary>Returns true only when damage was actually applied.</summary>
    public bool TakeDamage(float damage)
    {
        if (damage <= 0f || isDead || isInvincible
            || LevelUpPanelController.IsPaused)
        {
            return false;
        }

        currentHP = Mathf.Max(0f, currentHP - damage);
        NotifyHealthChanged();

        if (currentHP <= 0f)
        {
            HandleDeath();
            return true;
        }

        if (invincibilityRoutine != null)
        {
            StopCoroutine(invincibilityRoutine);
        }

        invincibilityRoutine = StartCoroutine(InvincibilityEffect());
        return true;
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || isDead)
        {
            return;
        }

        float healedHP = Mathf.Min(maxHP, currentHP + amount);
        if (Mathf.Approximately(healedHP, currentHP))
        {
            return;
        }

        currentHP = healedHP;
        NotifyHealthChanged();
    }

    /// <summary>
    /// Increases maximum health. By default, the gained capacity is also healed.
    /// </summary>
    public void IncreaseMaxHP(float amount, bool healIncrease = true)
    {
        if (amount <= 0f)
        {
            return;
        }

        maxHP += amount;
        if (healIncrease && !isDead)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        NotifyHealthChanged();
    }

    private IEnumerator InvincibilityEffect()
    {
        isInvincible = true;
        float elapsed = 0f;

        while (elapsed < invincibleTime)
        {
            if (playerSprite != null)
            {
                playerSprite.enabled = !playerSprite.enabled;
            }

            float waitTime = Mathf.Min(blinkInterval, invincibleTime - elapsed);
            yield return new WaitForSeconds(waitTime);
            elapsed += waitTime;
        }

        if (playerSprite != null)
        {
            playerSprite.enabled = true;
        }

        isInvincible = false;
        invincibilityRoutine = null;
    }

    private void NotifyHealthChanged()
    {
        UpdateHealthUI();
        HealthChanged?.Invoke(currentHP, maxHP);
    }

    private void UpdateHealthUI()
    {
        if (hpImage != null)
        {
            hpImage.fillAmount = maxHP > 0f ? currentHP / maxHP : 0f;
        }
    }

    private void HandleDeath()
    {
        isDead = true;
        isInvincible = false;
        Died?.Invoke();

        // TODO: Connect the game-over UI/manager here when it is implemented.
    }

    private void OnDisable()
    {
        if (invincibilityRoutine != null)
        {
            StopCoroutine(invincibilityRoutine);
            invincibilityRoutine = null;
        }

        isInvincible = false;
        if (playerSprite != null)
        {
            playerSprite.enabled = true;
        }
    }

    private void OnValidate()
    {
        maxHP = Mathf.Max(1f, maxHP);
        invincibleTime = Mathf.Max(0f, invincibleTime);
        blinkInterval = Mathf.Max(0.02f, blinkInterval);

        if (Application.isPlaying)
        {
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
            UpdateHealthUI();
        }
    }
}
