using UnityEngine;
using UnityEngine.UI;

public class SliderScript : MonoBehaviour
{
    public Slider slider;

    public void SliderUpdate(int amount)
    {
        slider.value = amount;
    }
}
