using UdonSharp;
using UnityEngine;

namespace net.ts7m.udon_magazine.script.udon {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UdonMagazineHandle : UdonSharpBehaviour {
        [SerializeField] private UdonMagazine magazine;
        [SerializeField] private bool backward;

        public void Start() {
            if (magazine == null) {
                Debug.LogError("[UdonMagazineHandle] Magazine is not set.");
            }
        }

        public override void Interact() {
            if (magazine == null) return;

            if (backward) {
                magazine.Backward();
            }
            else {
                magazine.Forward();
            }
        }
    }
}