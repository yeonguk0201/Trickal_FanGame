using UnityEngine;

namespace TrickalFanGame.Frontend
{
    /// <summary>
    /// The rig-local rectangle that matches the character's still portrait sprite, so a camera framing it
    /// shows the animated rig at exactly the size and place the still art had.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StandingRigFrame : MonoBehaviour
    {
        [SerializeField] private Rect frame = new(-2.5f, -0.25f, 5f, 5f);

        public Rect Frame => frame;

        public void Configure(Rect localFrame) => frame = localFrame;
    }
}
