using Unity.Entities;

namespace DOTSSpriteRenderer.Components {
    public struct SpriteFlip : IComponentData {
        public bool FlipX;
        public bool FlipY;
    }
}
