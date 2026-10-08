using UnityEngine;

namespace AmazonExpedition.Player
{
    public sealed class PlayerInputSource : MonoBehaviour
    {
        [Header("Legacy Input Axes")]
        [SerializeField] private string horizontalAxis = "Horizontal";
        [SerializeField] private string verticalAxis = "Vertical";
        [SerializeField] private string jumpButton = "Jump";
        [SerializeField] private string sprintButton = "Sprint";
        [SerializeField] private string interactButton = "Interact";

        [Header("Look")]
        [SerializeField] private bool useMouse = true;
        [SerializeField] private string lookXAxis = "Mouse X";
        [SerializeField] private string lookYAxis = "Mouse Y";
        [SerializeField] private float mouseScale = 0.08f;

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool Jump { get; private set; }
        public bool Sprint { get; private set; }
        public bool Interact { get; private set; }
        public bool AnyInput { get; private set; }

        private void Update()
        {
            Move = new Vector2(Input.GetAxisRaw(horizontalAxis), Input.GetAxisRaw(verticalAxis));
            Jump = Input.GetButtonDown(jumpButton) || Input.GetKeyDown(KeyCode.Space);
            Sprint = Input.GetButton(sprintButton) || Input.GetKey(KeyCode.LeftShift);
            Interact = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.R);

            LookDelta = useMouse
                ? new Vector2(Input.GetAxis(lookXAxis) * mouseScale, Input.GetAxis(lookYAxis) * mouseScale)
                : Vector2.zero;

            AnyInput = Move.sqrMagnitude > 0.001f || Jump || Sprint || Interact;
        }
    }
}
