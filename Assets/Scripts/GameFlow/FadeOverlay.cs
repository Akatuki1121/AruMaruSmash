using System.Collections;
using UnityEngine;

/// <summary>
/// 全画面の黒フェード。フェード色は黒（仕様書に色の指定なし。仮）。
/// 実装は OnGUI なので、UIシーンやCanvasの有無に依存しない
/// </summary>
public class FadeOverlay : MonoBehaviour
{
    private const int DRAW_DEPTH = int.MinValue;   // 最前面
    private const float ALPHA_CLEAR = 0f;
    private const float ALPHA_BLACK = 1f;

    private float m_alpha = ALPHA_CLEAR;

    public IEnumerator FadeOut(float seconds)
    {
        yield return FadeTo(ALPHA_BLACK, seconds);
    }

    public IEnumerator FadeIn(float seconds)
    {
        yield return FadeTo(ALPHA_CLEAR, seconds);
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        float start = m_alpha;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // ポーズ中(timeScale=0)でも進むよう unscaled を使う
            elapsed += Time.unscaledDeltaTime;
            m_alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        m_alpha = target;
    }

    private void OnGUI()
    {
        if (m_alpha <= ALPHA_CLEAR)
        {
            return;
        }
        GUI.depth = DRAW_DEPTH;
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, m_alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
