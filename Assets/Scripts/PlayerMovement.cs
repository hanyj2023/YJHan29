using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public sealed class PlayerMovement : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("moveX");
    private static readonly int MoveYHash = Animator.StringToHash("moveY");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");

    [SerializeField, Min(0f)]
    private float moveSpeed = 5f;

    private Rigidbody2D body;
    private Animator animator;
    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.down;

    public Vector2 FacingDirection => facingDirection;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        ApplyFacingParameters();
        animator.SetBool(IsMovingHash, false);
    }

    private void Update()
    {
        if (LevelUpPanelController.IsPaused)
        {
            moveInput = Vector2.zero;
            animator.SetBool(IsMovingHash, false);
            return;
        }

        moveInput = ReadKeyboardInput();

        bool isMoving = moveInput.sqrMagnitude > 0f;
        if (isMoving)
        {
            UpdateFacingDirection(moveInput);
        }

        ApplyFacingParameters();
        animator.SetBool(IsMovingHash, isMoving);
    }

    private void FixedUpdate()
    {
        if (moveInput.sqrMagnitude == 0f)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        // MovePosition uses the physics timestep, so speed is stable at every render framerate.
        Vector2 nextPosition = body.position + moveInput * (moveSpeed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
    }

    private void OnDisable()
    {
        moveInput = Vector2.zero;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, false);
        }
    }

    private static Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        bool down = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        bool up = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;

        float x = left == right ? 0f : left ? -1f : 1f;
        float y = down == up ? 0f : down ? -1f : 1f;

        Vector2 input = new Vector2(x, y);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void UpdateFacingDirection(Vector2 input)
    {
        bool hasHorizontalInput = !Mathf.Approximately(input.x, 0f);
        bool hasVerticalInput = !Mathf.Approximately(input.y, 0f);

        if (!hasHorizontalInput)
        {
            facingDirection = input.y > 0f ? Vector2.up : Vector2.down;
            return;
        }

        if (!hasVerticalInput)
        {
            facingDirection = input.x > 0f ? Vector2.right : Vector2.left;
            return;
        }

        // Keep a valid facing axis during diagonal movement so directional
        // Any State transitions never compete and make the sprite flicker.
        if (Mathf.Abs(facingDirection.x) > 0.5f
            && Mathf.Sign(facingDirection.x) == Mathf.Sign(input.x))
        {
            return;
        }

        if (Mathf.Abs(facingDirection.y) > 0.5f
            && Mathf.Sign(facingDirection.y) == Mathf.Sign(input.y))
        {
            return;
        }

        facingDirection = input.y > 0f ? Vector2.up : Vector2.down;
    }

    private void ApplyFacingParameters()
    {
        animator.SetFloat(MoveXHash, facingDirection.x);
        animator.SetFloat(MoveYHash, facingDirection.y);
    }
}
