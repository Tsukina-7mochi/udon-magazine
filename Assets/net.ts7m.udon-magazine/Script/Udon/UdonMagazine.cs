using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace net.ts7m.udon_magazine.script.udon {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), RequireComponent(typeof(Animator))]
    public class UdonMagazine : UdonSharpBehaviour {
        private const int DelayFrames = 2;
        private readonly int _animatorParamOpened = Animator.StringToHash("isOpen");
        private readonly int _animatorParamForward = Animator.StringToHash("Forward");
        private readonly int _animatorParamBackward = Animator.StringToHash("Backward");
        private readonly int _animatorParamOpenCloseBackwards = Animator.StringToHash("OpenCloseBackwards");

        [SerializeField] [HideInInspector] private int version = 1;
        [SerializeField] private Texture2D[] pageTextures;
        [SerializeField] private bool doublePageCount;
        [SerializeField] private RawImage page1;
        [SerializeField] private RawImage page2;
        [SerializeField] private RawImage page3;
        [SerializeField] private RawImage page4;
        [SerializeField] private Text currentPageText;
        [SerializeField] private Text maxPageText;

        private Animator _animator;
        private bool _animating;
        private int _maxPageIndex;

        [UdonSynced] private bool _closedSynced = true;
        [UdonSynced] private int _pageIndexSynced;

        private bool _closed = true;
        private int _pageIndex;
        private bool _refreshedOnce;

        private bool _isOwner() => Networking.IsOwner(Networking.LocalPlayer, this.gameObject);

        private void _takeOwnership() {
            if (this._isOwner()) return;
            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        }

        private void _distribute(bool takeOwnership) {
            if (takeOwnership) this._takeOwnership();
            if (!this._isOwner()) return;

            this._closedSynced = this._closed;
            this._pageIndexSynced = this._pageIndex;
            this.RequestSerialization();
        }

        private void _syncPageDisplay() {
            this.page1.texture = this.pageTextures[this._pageIndex * 2];
            this.page4.texture = this.pageTextures[this._pageIndex * 2 + 1];

            var pageIndex = this.doublePageCount ? this._pageIndex * 2 + 1 : this._pageIndex + 1;
            this.currentPageText.text = this._closed ? "-" : pageIndex.ToString();
        }

        private void _open(int pageIndex, bool backwards) {
            if (!this._closed) return;
            if (this._animating) return;
            this._animating = true;

            this._closed = false;
            this._pageIndex = pageIndex;
            this._syncPageDisplay();
            this._animator.SetBool(this._animatorParamOpenCloseBackwards, backwards);
            this._animator.SetBool(this._animatorParamOpened, true);

            this._distribute(false);
        }

        private void _close(bool backwards) {
            if (this._closed) return;
            if (this._animating) return;
            this._animating = true;

            this._closed = true;
            this._syncPageDisplay();
            this._animator.SetBool(this._animatorParamOpenCloseBackwards, backwards);
            this._animator.SetBool(this._animatorParamOpened, false);

            this._distribute(false);
        }

        private void _sendPage(int pageIndex) {
            if (this._closed) return;
            if (this._animating) return;
            this._animating = true;

            var fromPage = this._pageIndex;
            var toPage = pageIndex;
            this._pageIndex = toPage;

            var forward = toPage > fromPage;
            if (forward) {
                this.page1.texture = this.pageTextures[fromPage * 2];
                this.page2.texture = this.pageTextures[fromPage * 2 + 1];
                this.page3.texture = this.pageTextures[toPage * 2];
                this.page4.texture = this.pageTextures[toPage * 2 + 1];
            }
            else {
                this.page1.texture = this.pageTextures[toPage * 2];
                this.page2.texture = this.pageTextures[toPage * 2 + 1];
                this.page3.texture = this.pageTextures[fromPage * 2];
                this.page4.texture = this.pageTextures[fromPage * 2 + 1];
            }

            var displayPageIndex = this.doublePageCount ? this._pageIndex * 2 + 1 : this._pageIndex + 1;
            this.currentPageText.text = this._closed ? "-" : displayPageIndex.ToString();

            this._animator.SetBool(forward ? this._animatorParamForward : this._animatorParamBackward, true);
            this.SendCustomEventDelayedFrames(nameof(UdonMagazine.StartPageSendAnimation), DelayFrames);

            this._distribute(false);
        }

        public void StartPageSendAnimation() {
            this._animator.SetBool(this._animatorParamForward, false);
            this._animator.SetBool(this._animatorParamBackward, false);
        }

        public void OnSendPageAnimationEnd() {
            this._syncPageDisplay();
            this.OnAnimationEnd();
        }

        public void OnAnimationEnd() {
            this._animating = false;
        }

        public void Start() {
            Debug.Log("[UdonMagazine] Start");

            this._animator = this.GetComponent<Animator>();

            if (this.pageTextures.Length % 2 != 0) {
                Debug.LogError("[UdonMagazine] Number of page must be even.");
            }

            this._maxPageIndex = this.pageTextures.Length / 2 - 1;
            var maxPageIndex = this.doublePageCount ? this._maxPageIndex * 2 + 2 : this._maxPageIndex + 1;
            this.maxPageText.text = maxPageIndex.ToString();
            this._animating = false;
            this._closed = true;
            this._pageIndex = 0;
            this._refreshedOnce = false;
            this._distribute(false);
        }

        public override void OnDeserialization() {
            // refresh ONLY first time (i.e. when local player joined)
            // usually, states are synced in a procedural way via SendCustomNetworkEvent
            if (this._refreshedOnce) return;
            this._refreshedOnce = true;

            this.Refresh();
        }

        private void OnDistributeRequest() {
            this._distribute(false);
        }

        public override void OnPlayerJoined(VRCPlayerApi _) {
            this._distribute(false);
        }

        public void OnPickupUseDownLocal() {
            if (this._closed)
                this._open(0, false);
            else
                this._close(true);
        }

        public void ForwardLocal() {
            if (this._closed)
                this._open(0, false);
            else if (this._pageIndex < this._maxPageIndex)
                this._sendPage(this._pageIndex + 1);
            else
                this._close(false);
        }

        public void BackwardLocal() {
            if (this._closed)
                this._open(this._maxPageIndex, true);
            else if (this._pageIndex > 0)
                this._sendPage(this._pageIndex - 1);
            else
                this._close(true);
        }

        public override void OnPickupUseDown() {
            this._takeOwnership();
            this.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(UdonMagazine.OnPickupUseDownLocal));
        }

        public void Forward() {
            this._takeOwnership();
            this.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(UdonMagazine.ForwardLocal));
        }

        public void Backward() {
            this._takeOwnership();
            this.SendCustomNetworkEvent(NetworkEventTarget.All, nameof(UdonMagazine.BackwardLocal));
        }

        public void Refresh() {
            Debug.Log("[UdonMagazine] Refreshing");

            this._animating = false;
            this._closed = this._closedSynced;
            this._pageIndex = this._pageIndexSynced;
            this._animator.SetBool(this._animatorParamOpened, !this._closed);
            this._syncPageDisplay();
            this.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(UdonMagazine.OnDistributeRequest));
        }

        public void ClearPageTextures(int numPages, Texture2D defaultTexture) {
            this.pageTextures = new Texture2D[numPages];
            for (var i = 0; i < numPages; i++) {
                this.pageTextures[i] = defaultTexture;
            }

            this._maxPageIndex = this.pageTextures.Length / 2 - 1;
            var maxPageIndex = this.doublePageCount ? this._maxPageIndex * 2 + 2 : this._maxPageIndex + 1;
            this.maxPageText.text = maxPageIndex.ToString();
            this._animating = false;
            this._closed = true;
            this._pageIndex = 0;
            this._refreshedOnce = false;
            this._distribute(false);
        }

        public void SetPageTexture(int index, Texture2D texture) {
            this.pageTextures[index] = texture;
        }
    }
}