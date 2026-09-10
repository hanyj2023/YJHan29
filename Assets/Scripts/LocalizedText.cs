using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField] private string stringKey;
    [SerializeField] private TMP_Text target;

    private object[] arguments;

    public string StringKey => stringKey;

    private void Awake()
    {
        target ??= GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.LanguageChanged -= Refresh;
    }

    public void SetKey(string key, params object[] formatArguments)
    {
        stringKey = key;
        arguments = formatArguments;
        Refresh();
    }

    public void SetArguments(params object[] formatArguments)
    {
        arguments = formatArguments;
        Refresh();
    }

    public void Refresh()
    {
        target ??= GetComponent<TMP_Text>();
        if (target != null && !string.IsNullOrWhiteSpace(stringKey))
        {
            target.text = LocalizationManager.Get(stringKey, arguments);
        }
    }
}
