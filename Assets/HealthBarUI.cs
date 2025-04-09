using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DamageReceiver targetHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;

    void Start()
    {
        // Try to find a slider if not assigned
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }

        // Try to find fill image if not assigned
        if (fillImage == null && healthSlider != null)
        {
            fillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        // Set initial value
        UpdateHealthBar();

        // Subscribe to hit events
        if (targetHealth != null)
        {
            targetHealth.OnHit.AddListener((_) => UpdateHealthBar());
        }
    }

    void Update()
    {
        // Update the health bar every frame (for smooth transitions)
        UpdateHealthBar();

        // Make the health bar face the camera
        if (Camera.main != null)
        {
            transform.forward = Camera.main.transform.forward;
        }
    }

    void UpdateHealthBar()
    {
        if (targetHealth == null || healthSlider == null) return;

        // Get current health percentage
        float healthPercent = targetHealth.GetHealthPercent();

        // Update slider value
        healthSlider.value = healthPercent;

        // Update color based on health
        if (fillImage != null)
        {
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercent);
        }
    }

    // Public method to set the target
    public void SetTarget(DamageReceiver newTarget)
    {
        // Remove listener from old target
        if (targetHealth != null)
        {
            targetHealth.OnHit.RemoveListener((_) => UpdateHealthBar());
        }

        // Set new target
        targetHealth = newTarget;

        // Add listener to new target
        if (targetHealth != null)
        {
            targetHealth.OnHit.AddListener((_) => UpdateHealthBar());
        }

        // Update bar
        UpdateHealthBar();
    }
}