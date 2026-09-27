using UnityEngine;

public class Jump : MonoBehaviour
{
    [Header("Jump Designer Numbers")]
    [SerializeField] public float timeToApex = 0.35f;
    [SerializeField] public float timeToFall = 0.25f;
    [SerializeField] public float maxJumpHeight = 5f;
    [SerializeField] public float minJumpHeight = 3f;
    [SerializeField] public float doubleJumpHeight = 2.5f;

    [Header("Horizontal Movement")]
    [SerializeField] private float maxSpeed = 8;
    [SerializeField] private float maxSpeedOnAir = 4;
    [SerializeField] private float timeToReachMax = 0.3f;
    [SerializeField] private float timeToStop = 0.1f;
    [SerializeField] private float turnSpeed = 0.5f;
    [SerializeField] private float turnSpeedScalar = 2;

    [Header("Gravity Switch (Celeste)")]
    [SerializeField] private KeyCode gravitySwitchKey = KeyCode.G;

    [Header("Collision & Layers")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Distance from the center/pivot of your character to their feet (0.5 for a standard Cube, 0 if pivot is at feet)")]
    [SerializeField] private float playerHalfHeight = 0.5f;

    // Horizontal Movement Derived Properties
    float acceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToReachMax;
    float deceleration => (isGrounded ? maxSpeed : maxSpeedOnAir) / timeToStop;
    float turnRate => (maxSpeed * turnSpeedScalar) / turnSpeed;

    // Vertical Physics Derived Properties
    float riseG => 2f * maxJumpHeight / (timeToApex * timeToApex);
    float fallG => 2f * maxJumpHeight / (timeToFall * timeToFall);
    float maxJumpVelocity => 2f * maxJumpHeight / timeToApex;
    float doubleJumpVelocity => Mathf.Sqrt(2f * riseG * doubleJumpHeight);

    Vector3 velocity = Vector3.zero;

    // State Variables
    private int gravityDir = 1; // 1 = normal (falling down), -1 = inverted (falling up)
    private bool isJumping;
    private bool canDoubleJump;
    private float startJumpY;
    private bool isGrounded;

    void Start()
    {
        CheckGrounded();
    }

    void Update()
    {
        HorizontalMovement();
        HandleGravitySwitch();

        // 1. Initial Jump
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            velocity.y = maxJumpVelocity * gravityDir;
            startJumpY = transform.position.y;
            isJumping = true;
            canDoubleJump = true;
            isGrounded = false;
        }
        // 2. Double Jump
        else if (!isGrounded && canDoubleJump && Input.GetButtonDown("Jump"))
        {
            velocity.y = doubleJumpVelocity * gravityDir;
            startJumpY = transform.position.y;
            isJumping = true;
            canDoubleJump = false;
        }

        // 3. Variable Jump Height Cutoff (Triggered when button is released early)
        if (Input.GetButtonUp("Jump") && isJumping)
        {
            bool isRising = (gravityDir == 1 && velocity.y > 0) || (gravityDir == -1 && velocity.y < 0);

            if (isRising)
            {
                float heightReached = Mathf.Abs(transform.position.y - startJumpY);
                float remainingMinHeight = Mathf.Max(0f, minJumpHeight - heightReached);

                float vCut = Mathf.Sqrt(2f * riseG * remainingMinHeight);

                if (Mathf.Abs(velocity.y) > vCut)
                {
                    velocity.y = vCut * gravityDir;
                }
            }
            isJumping = false;
        }

        // 4. Determine current gravity (Rise vs Fall)
        float currentG = 0f;
        if (!isGrounded)
        {
            bool isRising = (gravityDir == 1 && velocity.y > 0) || (gravityDir == -1 && velocity.y < 0);
            currentG = (isRising ? riseG : fallG) * gravityDir;
        }

        // 5. Exact Trapezoidal Step 
        float vyBefore = velocity.y;
        velocity.y -= currentG * Time.deltaTime;
        float dy = (vyBefore + velocity.y) * 0.5f * Time.deltaTime;

        // 6. VERTICAL MOVEMENT SWEEP (Fixes Tunneling through platforms)
        if (Mathf.Abs(dy) > 0.0001f) // Only check if we are actually moving up/down
        {
            float moveDirY = Mathf.Sign(dy);
            Vector3 sweepDir = moveDirY == 1 ? Vector3.up : Vector3.down;
            float sweepDist = Mathf.Abs(dy) + playerHalfHeight;

            // Cast a ray in the exact direction we are moving
            if (Physics.Raycast(transform.position, sweepDir, out RaycastHit hit, sweepDist, groundLayer))
            {
                // Snap our movement exactly to the surface so we don't go through
                dy = (hit.distance - playerHalfHeight - 0.002f) * moveDirY;

                // Kill vertical momentum because we hit a wall/floor/ceiling
                velocity.y = 0f;

                if (moveDirY != gravityDir)
                    isJumping = false;
            }
        }

        // Apply Movement
        transform.position += new Vector3(velocity.x * Time.deltaTime, dy, 0);

        // 7. Check if resting on the ground
        CheckGrounded();
    }

    private void HandleGravitySwitch()
    {
        if (Input.GetKeyDown(gravitySwitchKey))
        {
            gravityDir *= -1; // Flip between 1 (down) and -1 (up)
        }
    }

    private void CheckGrounded()
    {
        // Only cast a tiny ray just past the feet (or head, if inverted) to check if we are resting
        Vector3 origin = transform.position;
        Vector3 direction = gravityDir == 1 ? Vector3.down : Vector3.up;
        float castDistance = playerHalfHeight + 0.05f;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, castDistance, groundLayer))
        {
            isGrounded = true;
            canDoubleJump = true;
            isJumping = false;
        }
        else
        {
            isGrounded = false;
        }
    }

    private void HorizontalMovement()
    {
        float input = Input.GetAxisRaw("Horizontal");
        float targetSpeed = maxSpeed * input;
        float targetAccel = ChooseAccel(input);
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, targetAccel * Time.deltaTime);
    }

    float ChooseAccel(float input)
    {
        bool isTurning = !Mathf.Approximately(Mathf.Sign(input), Mathf.Sign(velocity.x));
        if (Mathf.Abs(input) > 0)
        {
            if (!isTurning) return acceleration;
            return turnRate;
        }
        else
        {
            return deceleration;
        }
    }

    private void OnDrawGizmos()
    {
        // Shift the starting point slightly to the right so it doesn't hide inside the player model
        Vector3 debugOrigin = transform.position + Vector3.right * 0.6f;

        Vector3 direction = gravityDir == 1 ? Vector3.down : Vector3.up;
        float castDistance = playerHalfHeight + 0.05f;

        Gizmos.color = isGrounded ? Color.green : Color.red;

        // Draw the ray and a small box at the start so you can see where it originates
        Gizmos.DrawRay(debugOrigin, direction * castDistance);
        Gizmos.DrawCube(debugOrigin, Vector3.one * 0.1f);

        // Draw a circle where the code thinks your feet are
        Vector3 feet = debugOrigin + direction * playerHalfHeight;
        Gizmos.DrawWireSphere(feet, 0.05f);
    }
}