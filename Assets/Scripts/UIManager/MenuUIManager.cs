
using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [SerializeField] private GameObject titleCanvas;
    [SerializeField] private GameObject optionCanvas;
    [SerializeField] private GameObject howToPlayCanvas;

    private GameFlowManager flow;

    private void Start()
    {
        flow = GameFlowManager.Instance;

        if (flow == null)
        {
            Debug.LogError("GameFlowManager が見つかりません");
            return;
        }

        flow.OnStateChanged += HandleStateChanged;

        // 現在の状態に合わせて初期表示を設定
        HandleStateChanged(flow.State, flow.State);
    }

    private void OnDestroy()
    {
        if (flow != null)
        {
            flow.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(GameState from, GameState to)
    {
        Debug.Log($"[MenuUIManager] {from} -> {to}");

        // 別シーンへ遷移するときは、現在のUIをそのまま残す
        if (to != GameState.Title &&
            to != GameState.Option &&
            to != GameState.OperationGuide &&
            to != GameState.Settings)
        {
            return;
        }

        titleCanvas.SetActive(to == GameState.Title);
        optionCanvas.SetActive(to == GameState.Option);
        howToPlayCanvas.SetActive(to == GameState.OperationGuide);
    }
}
