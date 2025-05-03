using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace net.ts7m.udon_magazine.script.udon {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class UdonMagazine : UdonSharpBehaviour {
        [SerializeField] [HideInInspector] private int version = 2;

        [Header("Contents")]
        [SerializeField] private string title;
        [SerializeField] private string author;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private Texture2D[] pageTextures;

        [Header("Behaviors")]
        [SerializeField] private bool doublePageCount;
        [SerializeField] private bool debug;

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private RawImage page1;
        [SerializeField] private RawImage page2;
        [SerializeField] private RawImage page3;
        [SerializeField] private RawImage page4;
        [SerializeField] private Text currentPageText;
        [SerializeField] private Text maxPageText;

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
         * * Which page is opened.
         * 
         * * When the value is -1, magazine is closed.
         * * When the value is -2, magazine is closed and flipped.
         */
        private int _displayPageIndex = -1;
        [UdonSynced] private int _pageIndex = -1;

        public string Title => this.title;
        public string Author => this.author;
        public string Description => this.description;

        public int PageIndex {
            get => this._pageIndex;
            set => this._setPageIndex(value);
        }


        private void _debugLog(string message) {
            Debug.Log("[UdonMagazine]" + message);
        }

        /**
         * Returns true if the local player has ownership of this object.
         */
        private bool _isOwner() {
            return Networking.IsOwner(Networking.LocalPlayer, this.gameObject);
        }

        /**
         * Takes ownership of this object. Returns true if the local player has ownership.
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

            if (this._isOwner()) {
                this.RequestSerialization();
                return;
            }

            this.SendCustomNetworkEvent(
                NetworkEventTarget.Owner,
                nameof(this.RequestSerialization)
            );
        }

        private void _invokeSendPageAnimation(int oldPageIndex, int newPageIndex) {
            if (this.debug) {
                this._debugLog(
                    $"{nameof(this._invokeSendPageAnimation)}({oldPageIndex}, {newPageIndex})");
            }

            if (oldPageIndex == newPageIndex) return;
            if (this._animating) return;
            this._animating = true;

            if (newPageIndex < 0) {
                this.animator.SetBool(this._animatorParamOpened, false);
                this.animator.SetBool(this._animatorParamFlipped, newPageIndex == -2);
                return;
            }

            if (oldPageIndex < 0) {
                this.page1.texture = this.pageTextures[newPageIndex * 2];
                this.page4.texture = this.pageTextures[newPageIndex * 2 + 1];
                this.animator.SetBool(this._animatorParamOpened, true);
                return;
            }

            if (oldPageIndex < newPageIndex) {
                this.page1.texture = this.pageTextures[oldPageIndex * 2];
                this.page2.texture = this.pageTextures[oldPageIndex * 2 + 1];
                this.animator.SetBool(this._animatorParamForward, true);

                this.SendCustomEventDelayedFrames(
                    nameof(this.SendPageAnimationEventForward),
                    this._sendPageAnimationDelay
                );
            } else {
                this.page3.texture = this.pageTextures[oldPageIndex * 2];
                this.page4.texture = this.pageTextures[oldPageIndex * 2 + 1];
                this.animator.SetBool(this._animatorParamBackward, true);

                this.SendCustomEventDelayedFrames(
                    nameof(this.SendPageAnimationEventBackward),
                    this._sendPageAnimationDelay
                );
            }
        }

        private bool _setPageIndex(int pageIndex) {
            if (this.debug) this._debugLog($"{nameof(this._setPageIndex)}({pageIndex})");

            if (pageIndex < -2) return false;
            if (pageIndex > (this.pageTextures.Length - 1) / 2) return false;
            if (!this._takeOwnership()) return false;

            this._pageIndex = pageIndex;
            this.RequestSerialization();
            this.OnDeserialization();

            return true;
        }

        #region Event

        public void SendPageAnimationEventForward() {
            if (this.debug) Debug.Log($"{nameof(this.SendPageAnimationEventForward)}()");

            this.page3.texture = this.pageTextures[this._displayPageIndex * 2];
            this.page4.texture = this.pageTextures[this._displayPageIndex * 2 + 1];
            this.animator.SetBool(this._animatorParamForward, false);
        }

        public void SendPageAnimationEventBackward() {
            if (this.debug) Debug.Log($"{nameof(this.SendPageAnimationEventBackward)}()");

            this.page1.texture = this.pageTextures[this._displayPageIndex * 2];
            this.page2.texture = this.pageTextures[this._displayPageIndex * 2 + 1];
            this.animator.SetBool(this._animatorParamBackward, false);
        }

        public void OnAnimationEnd() {
            if (this.debug) this._debugLog($"{nameof(this.OnAnimationEnd)}()");

            this._animating = false;

            if (this._displayPageIndex >= 0) {
                this.page1.texture = this.pageTextures[this._displayPageIndex * 2];
                this.page4.texture = this.pageTextures[this._displayPageIndex * 2 + 1];
            }

            if (this._displayPageIndex != this._pageIndex) {
                this.SendCustomEventDelayedFrames(
                    nameof(this.InvokeSendPageAnimationFromState),
                    this._sendPageAnimationDelay
                );
            }
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

            var maxPage = this.pageTextures.Length;
            if (!this.doublePageCount) maxPage /= 2;
            this.maxPageText.text = maxPage.ToString();

            this.RestoreStates();
            this._requestSerializationToOwner();
        }

        public override void OnPlayerJoined(VRCPlayerApi _) {
            if (this.debug) this._debugLog($"{nameof(this.OnPlayerJoined)}()");

            if (this._isOwner()) this.RequestSerialization();
        }

        public override void OnDeserialization() {
            if (this.debug) this._debugLog($"{nameof(this.OnDeserialization)}()");

            if (this._pageIndex < 0) {
                this.currentPageText.text = "-";
            } else {
                var pageIndex = (this.doublePageCount ? this._pageIndex * 2 : this._pageIndex) + 1;
                this.currentPageText.text = pageIndex.ToString();
            }

            if (!this._animating) this.InvokeSendPageAnimationFromState();
        }

        public void Forward() {
            if (this.debug) this._debugLog($"{nameof(this.Forward)}()");

            if (this._animating) return;

            var pageIndex = this._pageIndex + 1;
            if (pageIndex > (this.pageTextures.Length - 1) / 2) pageIndex = -2;

            this.PageIndex = pageIndex;
        }

        public void Backward() {
            if (this.debug) this._debugLog($"{nameof(this.Backward)}()");

            if (this._animating) return;

            var pageIndex = this._pageIndex - 1;
            if (pageIndex < -1) pageIndex = (this.pageTextures.Length - 1) / 2;

            this.PageIndex = pageIndex;
        }

        public void RestoreStates() {
            if (this.debug) this._debugLog($"{nameof(this.RestoreStates)}()");

            this._animating = false;
            this.animator.SetBool(this._animatorParamOpened, this._displayPageIndex >= 0);
            this.animator.SetBool(this._animatorParamFlipped, this._displayPageIndex == -2);
            if (this._displayPageIndex >= 0) {
                this.page1.texture = this.pageTextures[this._displayPageIndex * 2];
                this.page4.texture = this.pageTextures[this._displayPageIndex * 2 + 1];
            }
        }

        #endregion
    }
}
