using UnityEngine;

public class HowToPlayUIManager : BaseUIManager
{
    private ButtonManager button;

    public GameObject HowToPlayUI;
    public GameObject OptionUI;

    //private UIInputDirection previousDirection = UIInputDirection.None;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();
        HowToPlayUI.SetActive(false);
    }

    private void Update()
    {
        if (button == null) return;

        if (GetButtonDown())
        {
            HowToPlayUI.SetActive(false);
            OptionUI.SetActive(true);

            button.OnOptionClicked();
        }
    }
}