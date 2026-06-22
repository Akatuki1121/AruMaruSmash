using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [HideInInspector] public float deadzone = 0.3f;
    [HideInInspector] public Rigidbody rb;

    private Vector2 moveInput = Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // UnityのPlayer Inputから自動で呼び出される関数
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();

        //Debug.Log($"{gameObject.name} に入力が届いています！ 値: {moveInput}");
    }

    //----- 継続入力 -----//
    public bool GetInputUp() => moveInput.y > deadzone;
    public bool GetInputDown() => moveInput.y < -deadzone;
    public bool GetInputRight() => moveInput.x > deadzone;
    public bool GetInputLeft() => moveInput.x < -deadzone;

    //----- 単発入力 -----//
    public bool GetInputUpNow() => moveInput.y > deadzone;
    public bool GetInputDownNow() => moveInput.y < -deadzone;
    public bool GetInputRightNow() => moveInput.x > deadzone;
    public bool GetInputLeftNow() => moveInput.x < -deadzone;

    public bool GetDetermination() => false;
}