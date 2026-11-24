using UnityEngine;
using UnityEngine.UI;

public class SliderScript : MonoBehaviour
{
    public Slider slider;

    public float step = 0.05f;

    public void SliderUpdate(float amount)
    {
        slider.value += amount * step;

        AudioManager.Instance.GetBGM();
        AudioManager.Instance.GetSE();
    }

    public void SetValue(float value)
    {
        slider.SetValueWithoutNotify(value);
    }
}
