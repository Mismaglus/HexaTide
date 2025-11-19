using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GenericBarController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private Slider slider;
    [SerializeField] private RectTransform marksContainer;
    [SerializeField] private GameObject tickPrefab;
    [SerializeField] private TMP_Text label;

    [Header("Bar Values")]
    [SerializeField, Min(0)] private int maxValue = 10;
    [SerializeField, Min(0)] private int currentValue = 10;

    private int lastMaxValue = -1;

    public int MaxValue
    {
        get => maxValue;
        set
        {
            maxValue = Mathf.Max(1, value);
            RefreshTicks();
            UpdateSlider();
            UpdateLabel();
        }
    }

    public int CurrentValue
    {
        get => currentValue;
        set
        {
            currentValue = Mathf.Clamp(value, 0, maxValue);
            UpdateSlider();
            UpdateLabel();
        }
    }

    private void Awake()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>();
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (marksContainer == null && slider != null)
            marksContainer = slider.transform.Find("Fill Area/Fill/Marks") as RectTransform;

        slider.wholeNumbers = true;
        slider.interactable = false;

        RefreshTicks();
        UpdateSlider();
        UpdateLabel();
    }

    /// <summary>
    /// Rebuild tick marks based on maxValue.
    /// </summary>
    private void RefreshTicks()
    {
        if (tickPrefab == null || marksContainer == null) return;

        // Only rebuild if value changed
        if (maxValue == lastMaxValue) return;
        lastMaxValue = maxValue;

        foreach (Transform child in marksContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < maxValue; i++)
        {
            var tick = Instantiate(tickPrefab, marksContainer);
            tick.name = $"Tick_{i}";
        }

        // Force Layout Group update (if exists)
        var layout = marksContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(marksContainer);
    }

    private void UpdateSlider()
    {
        if (slider == null) return;
        slider.maxValue = maxValue;
        slider.value = currentValue;
    }

    private void UpdateLabel()
    {
        if (label == null) return;
        label.text = $"{currentValue}/{maxValue}";
    }

    // Optional: live preview in Editor
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        MaxValue = maxValue;
        CurrentValue = currentValue;
    }
#endif
}
