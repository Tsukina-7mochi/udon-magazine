using UdonSharp;
using UnityEngine;

namespace net.ts7m.udon_magazine.script.udon {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class UdonMagazineBook : UdonSharpBehaviour {
        [SerializeField] [HideInInspector] private int version = 2;
        [SerializeField] private string title;
        [SerializeField] private string author;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private Texture2D coverTexture;
        [SerializeField] private Texture2D[] pageTextures;

        public string Title => this.title;
        public string Author => this.author;
        public string Description => this.description;
        public Texture2D CoverTexture => this.coverTexture;
        public Texture2D[] PageTextures => this.pageTextures;
    }
}
