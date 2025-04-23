using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace net.ts7m.udon_magazine.script.udon {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class UdonMagazine : UdonSharpBehaviour {
        [SerializeField] [HideInInspector] private int version = 2;
        [SerializeField] private UdonMagazineBook book;

        [SerializeField] private Material coverMaterial;
        [SerializeField] private Animator animator;
        [SerializeField] private RawImage page1;
        [SerializeField] private RawImage page2;
        [SerializeField] private RawImage page3;
        [SerializeField] private RawImage page4;
        [SerializeField] private Text currentPageText;
        [SerializeField] private Text maxPageText;

        [SerializeField] private bool doublePageCount;
        [SerializeField] private bool debug;
        private readonly int _animatorParamBackward = Animator.StringToHash("Backward");
        private readonly int _animatorParamFlipped = Animator.StringToHash("Flipped");
        private readonly int _animatorParamForward = Animator.StringToHash("Forward");
        private readonly int _animatorParamOpened = Animator.StringToHash("Opened");
        private readonly int _sendPageAnimationDelay = 2;

        /**
         * true if when opening, closing, sending page animation is playing.
         */
        private bool _animating;

        /**
         * Which page is opened; When the value is -1, magazine is closed. When the value is -2, magazine is closed and flipped.
         */
        private int _displayPageIndex = -1;

        [UdonSynced] private int _pageIndex = -1;

        private void _debugLog(string message) {
            Debug.Log("[UdonMagazine]" + message);
        }

        private void _loadBook() {
            if (this.debug) this._debugLog($"{nameof(this._loadBook)}");

            this.coverMaterial.mainTexture = this.book.CoverTexture;
        }

        /**
         * Returns true if we have ownership of this object.
         */
        private bool _isOwner() {
            return Networking.IsOwner(Networking.LocalPlayer, this.gameObject);
        }

        /**
         * Takes ownership of this object. Returns true if we have ownership.
         */
        private bool _takeOwnership() {
            if (this.debug) this._debugLog($"{nameof(this._takeOwnership)}()");

            if (this._isOwner()) return true;

            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
            return this._isOwner();
        }

        /**
         * Request object owner to distribute its synced variables
         */
        private void _requestSerializationToOwner() {
            if (this.debug) this._debugLog($"{nameof(this._requestSerializationToOwner)}()");

            if (this._isOwner()) return;
            this.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(this.RequestSerialization));
        }

        private void _invokeSendPageAnimation(int oldPageIndex, int newPageIndex) {
            if (this.debug) this._debugLog($"{nameof(this._invokeSendPageAnimation)}({oldPageIndex}, {newPageIndex})");

            if (oldPageIndex == newPageIndex) return;
            if (this._animating) return;
            this._animating = true;

            if (newPageIndex < 0) {
                this.animator.SetBool(this._animatorParamOpened, false);
                this.animator.SetBool(this._animatorParamFlipped, newPageIndex == -2);
                return;
            }

            if (oldPageIndex < 0) {
                this.page1.texture = this.book.PageTextures[newPageIndex * 2];
                this.page4.texture = this.book.PageTextures[newPageIndex * 2 + 1];
                this.animator.SetBool(this._animatorParamOpened, true);
                return;
            }

            if (oldPageIndex < newPageIndex) {
                this.page1.texture = this.book.PageTextures[oldPageIndex * 2];
                this.page2.texture = this.book.PageTextures[oldPageIndex * 2 + 1];
                this.animator.SetBool(this._animatorParamForward, true);

                this.SendCustomEventDelayedFrames(
                    nameof(this.SendPageAnimationEventForward),
                    this._sendPageAnimationDelay
                );
            } else {
                this.page3.texture = this.book.PageTextures[oldPageIndex * 2];
                this.page4.texture = this.book.PageTextures[oldPageIndex * 2 + 1];
                this.animator.SetBool(this._animatorParamBackward, true);

                this.SendCustomEventDelayedFrames(
                    nameof(this.SendPageAnimationEventBackward),
                    this._sendPageAnimationDelay
                );
            }
        }

        public bool SetPageIndex(int pageIndex) {
            if (this.debug) this._debugLog($"{nameof(this.SetPageIndex)}({pageIndex})");

            if (!this._takeOwnership()) return false;

            this._pageIndex = pageIndex;
            this.RequestSerialization();
            this.OnDeserialization();

            return true;
        }

        #region Event

        public void SendPageAnimationEventForward() {
            if (this.debug) Debug.Log($"{nameof(this.SendPageAnimationEventForward)}()");

            this.page3.texture = this.book.PageTextures[this._displayPageIndex * 2];
            this.page4.texture = this.book.PageTextures[this._displayPageIndex * 2 + 1];
            this.animator.SetBool(this._animatorParamForward, false);
        }

        public void SendPageAnimationEventBackward() {
            if (this.debug) Debug.Log($"{nameof(this.SendPageAnimationEventBackward)}()");

            this.page1.texture = this.book.PageTextures[this._displayPageIndex * 2];
            this.page2.texture = this.book.PageTextures[this._displayPageIndex * 2 + 1];
            this.animator.SetBool(this._animatorParamBackward, false);
        }

        public void OnAnimationEnd() {
            if (this.debug) this._debugLog($"{nameof(this.OnAnimationEnd)}()");

            this._animating = false;

            if (this._displayPageIndex >= 0) {
                this.page1.texture = this.book.PageTextures[this._displayPageIndex * 2];
                this.page4.texture = this.book.PageTextures[this._displayPageIndex * 2 + 1];
            }

            if (this._displayPageIndex != this._pageIndex)
                this.SendCustomEventDelayedFrames(
                    nameof(this.InvokeSendPageAnimationFromState),
                    this._sendPageAnimationDelay
                );
        }

        public void InvokeSendPageAnimationFromState() {
            if (this.debug) this._debugLog($"{nameof(this.InvokeSendPageAnimationFromState)}()");

            var oldPageIndex = this._displayPageIndex;
            var newPageIndex = this._pageIndex;
            this._displayPageIndex = this._pageIndex;
            this._invokeSendPageAnimation(oldPageIndex, newPageIndex);
        }

        public void Start() {
            if (this.debug) this._debugLog($"{nameof(this.Start)}()");

            this._loadBook();

            this.RestoreStates();
            this._requestSerializationToOwner();
        }

        public override void OnPlayerJoined(VRCPlayerApi _) {
            if (this.debug) this._debugLog($"{nameof(this.OnPlayerJoined)}()");

            if (this._isOwner()) this.RequestSerialization();
        }

        public override void OnDeserialization() {
            if (this.debug) this._debugLog($"{nameof(this.OnDeserialization)}()");

            if (this.doublePageCount) {
                this.maxPageText.text = this.book.PageTextures.Length.ToString();
                this.currentPageText.text = this._pageIndex >= 0 ? (this._pageIndex * 2 + 1).ToString() : "-";
            } else {
                this.maxPageText.text = (this.book.PageTextures.Length / 2).ToString();
                this.currentPageText.text = this._pageIndex >= 0 ? (this._pageIndex + 1).ToString() : "-";
            }

            if (!this._animating) this.InvokeSendPageAnimationFromState();
        }

        public void Forward() {
            if (this.debug) this._debugLog($"{nameof(this.Forward)}()");

            var pageIndex = this._pageIndex + 1;
            if (pageIndex > (this.book.PageTextures.Length - 1) / 2) pageIndex = -2;

            this.SetPageIndex(pageIndex);
        }

        public void Backward() {
            if (this.debug) this._debugLog($"{nameof(this.Backward)}()");

            var pageIndex = this._pageIndex - 1;
            if (pageIndex < -1) pageIndex = (this.book.PageTextures.Length - 1) / 2;

            this.SetPageIndex(pageIndex);
        }

        public void RestoreStates() {
            if (this.debug) this._debugLog($"{nameof(this.RestoreStates)}()");

            this._animating = false;
            this.animator.SetBool(this._animatorParamOpened, this._displayPageIndex >= 0);
            this.animator.SetBool(this._animatorParamFlipped, this._displayPageIndex == -2);
            if (this._displayPageIndex >= 0) {
                this.page1.texture = this.book.PageTextures[this._displayPageIndex * 2];
                this.page4.texture = this.book.PageTextures[this._displayPageIndex * 2 + 1];
            }
        }

        #endregion
    }
}
