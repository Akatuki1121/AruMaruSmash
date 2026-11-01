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
    }

    private void Update()
    {
        if (button == null) return;

        if (GetConfirmButtonDown())
        {
            HowToPlayUI.SetActive(false);
            OptionUI.SetActive(true);

            button.OnOptionClicked();
        }
    }
}