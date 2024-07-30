
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace net.ts7m.udon_magazine.script.udon
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual), RequireComponent(typeof(Animator))]
    public class UdonMagazine : UdonSharpBehaviour
    {
        private const int DELAY_FRAMES = 2;
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

        private Animator _animator = null;
        private bool _animating = false;
        private int _maxPageIndex = 0;

        [UdonSynced] private bool _closedSynced = true;
        [UdonSynced] private int _pageIndexSynced = 0;
        
        private bool _closed = true;
        private int _pageIndex = 0;
        private bool _refreshedOnce = false;

        private bool _isOwner() => Networking.IsOwner(Networking.LocalPlayer, this.gameObject);

        private void _takeOwnership()
        {
            if (_isOwner()) return;
            Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        }

        private void _distribute(bool takeOwnership)
        {
            if(takeOwnership) _takeOwnership();
            if (!_isOwner()) return;

            _closedSynced = _closed;
            _pageIndexSynced = _pageIndex;
            RequestSerialization();
        }

        private void _syncPageDisplay()
        {
            page1.texture = pageTextures[_pageIndex * 2];
            page4.texture = pageTextures[_pageIndex * 2 + 1];

            var pageIndex = doublePageCount ? _pageIndex * 2 + 1 : _pageIndex + 1;
            currentPageText.text = _closed ? "-" : pageIndex.ToString();
        }

        private void _open(int pageIndex, bool backwards)
        {
            if (!_closed) return;
            if (_animating) return;
            _animating = true;

            _closed = false;
            _pageIndex = pageIndex;
            _syncPageDisplay();
            _animator.SetBool(_animatorParamOpenCloseBackwards, backwards);
            _animator.SetBool(_animatorParamOpened, true);
            
            _distribute(false);
        }

        private void _close(bool backwards)
        {
            if (_closed) return;
            if (_animating) return;
            _animating = true;

            _closed = true;
            _syncPageDisplay();
            _animator.SetBool(_animatorParamOpenCloseBackwards, backwards);
            _animator.SetBool(_animatorParamOpened, false);
            
            _distribute(false);
        }

        private void _sendPage(int pageIndex)
        {
            if (_closed) return;
            if (_animating) return;
            _animating = true;

            var fromPage = _pageIndex;
            var toPage = pageIndex;
            _pageIndex = toPage;
            
            var forward = toPage > fromPage;
            if (forward)
            {
                page1.texture = pageTextures[fromPage * 2];
                page2.texture = pageTextures[fromPage * 2 + 1];
                page3.texture = pageTextures[toPage * 2];
                page4.texture = pageTextures[toPage * 2 + 1];
            }
            else
            {
                page1.texture = pageTextures[toPage * 2];
                page2.texture = pageTextures[toPage * 2 + 1];
                page3.texture = pageTextures[fromPage * 2];
                page4.texture = pageTextures[fromPage * 2 + 1];
            }
            var displayPageIndex = doublePageCount ? _pageIndex * 2 + 1 : _pageIndex + 1;
            currentPageText.text = _closed ? "-" : displayPageIndex.ToString();
           
            _animator.SetBool(forward ? _animatorParamForward : _animatorParamBackward, true);
            SendCustomEventDelayedFrames(nameof(StartPageSendAnimation), DELAY_FRAMES);
            
            _distribute(false);
        }

        public void StartPageSendAnimation()
        {
            _animator.SetBool(_animatorParamForward, false);
            _animator.SetBool(_animatorParamBackward, false);
        }

        public void OnSendPageAnimationEnd()
        {
            _syncPageDisplay();
            OnAnimationEnd();
        }

        public void OnAnimationEnd()
        {
            _animating = false;
        }

        public void Start()
        {
            Debug.Log("[UdonMagazine] Start");
            
            _animator = this.GetComponent<Animator>();

            if (pageTextures.Length % 2 != 0)
            {
                Debug.LogError("[UdonMagazine] Number of page must be even.");
            }

            _maxPageIndex = pageTextures.Length / 2 - 1;
            var maxPageIndex = doublePageCount ? _maxPageIndex * 2 + 2 : _maxPageIndex + 1;
            maxPageText.text = maxPageIndex.ToString();
            _animating = false;
            _closed = true;
            _pageIndex = 0;
            _refreshedOnce = false;
            _distribute(false);
        }

        public override void OnDeserialization()
        {
            // refresh ONLY first time (i.e. when local player joined)
            // usually, states are synced in a procedural way via SendCustomNetworkEvent
            if (_refreshedOnce) return;
            _refreshedOnce = true;
            
            Refresh();
        }

        private void OnDistributeRequest()
        {
            _distribute(false);
        }

        public override void OnPlayerJoined(VRCPlayerApi _)
        {
            _distribute(false);
        }

        public void OnPickupUseDownLocal()
        {
            if (_closed) _open(0, false);
            else _close(true);
        }

        public void ForwardLocal()
        {
            if(_closed) _open(0, false);
            else if(_pageIndex < _maxPageIndex) _sendPage(_pageIndex + 1);
            else _close(false);
        }

        public void BackwardLocal()
        {
            if(_closed) _open(_maxPageIndex, true);
            else if(_pageIndex > 0) _sendPage(_pageIndex - 1);
            else _close(true);
        }

        public override void OnPickupUseDown()
        {
            _takeOwnership();
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnPickupUseDownLocal));
        }

        public void Forward()
        {
            _takeOwnership();
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ForwardLocal));
        }

        public void Backward()
        {
            _takeOwnership();
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(BackwardLocal));
        }

        public void Refresh()
        {
            Debug.Log("[UdonMagazine] Refreshing");
            
            _animating = false;
            _closed = _closedSynced;
            _pageIndex = _pageIndexSynced;
            _animator.SetBool(_animatorParamOpened, !_closed);
            _syncPageDisplay();
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnDistributeRequest));
        }

        public void ClearPageTextures(int numPages, Texture2D defaultTexture)
        {
            this.pageTextures = new Texture2D[numPages];
            for (var i = 0; i < numPages; i++)
            {
                pageTextures[i] = defaultTexture;
            }
            
            _maxPageIndex = pageTextures.Length / 2 - 1;
            var maxPageIndex = doublePageCount ? _maxPageIndex * 2 + 2 : _maxPageIndex + 1;
            maxPageText.text = maxPageIndex.ToString();
            _animating = false;
            _closed = true;
            _pageIndex = 0;
            _refreshedOnce = false;
            _distribute(false);
        }

        public void SetPageTexture(int index, Texture2D texture)
        {
            this.pageTextures[index] = texture;
        }
    }
}
