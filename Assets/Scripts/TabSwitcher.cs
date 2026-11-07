using UnityEngine;

public class TabSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject[] tabs;   // 0=VideoPanel, 1=AudioPanel, 2=ControlsPanel
    [SerializeField] private GameObject[] tabButtons;

    void OnEnable() => ShowTab(0);

    public void ShowTab(int index)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = (i == index);
            tabs[i].SetActive(active);
            if (i < tabButtons.Length) MarkButton(i, active);
        }
    }

    // Visual affordance: selected tab stands out
    void MarkButton(int i, bool active)
    {
        var b = tabButtons[i].GetComponent<UnityEngine.UI.Image>();
        if (b != null) b.color = active
            ? new Color(1f, 0.85f, 0.4f)      // accent for the active tab
            : Color.white;                     // inactive
    }
}