using Unity.Entities;

namespace DOTSSpriteRenderer.Components {
    public struct SpriteIndex : IComponentData {
        public int Value;
        public int TextureIndex;
    }
}
