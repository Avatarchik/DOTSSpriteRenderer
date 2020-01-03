using Unity.Entities;

namespace DOTSSpriteRenderer.Components {
    [InternalBufferCapacity(64)]
    public struct SpriteSequence : IBufferElementData {
        public int SpriteIndex;
    }
}
