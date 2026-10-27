
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
            Debug.LogError("GameFlowManager ‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ");
            return;
        }

        flow.OnStateChanged += HandleStateChanged;

        // Œ»Ý‚Ìó‘Ô‚É‡‚í‚¹‚Ä‰Šú•\Ž¦‚ðÝ’è
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

        titleCanvas.SetActive(to == GameState.Title);
        optionCanvas.SetActive(to == GameState.Option);
        howToPlayCanvas.SetActive(to == GameState.OperationGuide);

        Debug.Log(
            $"Title: {titleCanvas.activeSelf}, " +
            $"Option: {optionCanvas.activeSelf}, " +
            $"HowToPlay: {howToPlayCanvas.activeSelf}"
        );
    }
}
