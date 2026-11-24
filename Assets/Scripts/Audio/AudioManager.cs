using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{

    [SerializeField] private AudioMixer audioMixer;

    private const float DECIBEL_FACTOR = 20f;

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // セット
    public void SetBGM(float volume)
    {
        float db = Mathf.Log10(volume) * DECIBEL_FACTOR;

        bool success = audioMixer.SetFloat("BGMVolume", db);

        Debug.Log($"音量：{volume} / dB：{db} / 成功：{success}");
    }

    public void SetSE(float volume)
    {
        audioMixer.SetFloat("SEVolume", Mathf.Log10(volume) * DECIBEL_FACTOR);

    }

    // 現在の音量の取得
    public float GetBGM()
    {
        audioMixer.GetFloat("BGMVolume", out float db);
        return Mathf.Pow(10f, db / DECIBEL_FACTOR);
    }

    public float GetSE()
    {
        audioMixer.GetFloat("SEVolume", out float db);
        return Mathf.Pow(10f, db / DECIBEL_FACTOR);
    }
}
