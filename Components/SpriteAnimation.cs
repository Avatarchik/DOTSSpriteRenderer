using DOTSSpriteRenderer.Types;
using Unity.Entities;

namespace DOTSSpriteRenderer.Components {
    public struct SpriteAnimation : IComponentData {
        public int        Frame;
        public int        SequenceFramesCount;
        public float      FrameDelay;
        public double     NextFrameTime;
        public RepeatMode RepeatMode;
    }
}
