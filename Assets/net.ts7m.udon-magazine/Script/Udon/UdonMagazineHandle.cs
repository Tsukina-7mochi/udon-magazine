using UdonSharp;
using UnityEngine;

namespace net.ts7m.udon_magazine.script.udon {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UdonMagazineHandle : UdonSharpBehaviour {
        [SerializeField] private UdonMagazine magazine;
        [SerializeField] private bool backward;

        public void Start() {
            if (this.magazine == null) {
                Debug.LogError("[UdonMagazineHandle] Magazine is not set.");
            }
        }

        public override void Interact() {
            if (this.magazine == null) return;

            if (this.backward) {
                this.magazine.Backward();
            } else {
                this.magazine.Forward();
            }
        }
    }
}
