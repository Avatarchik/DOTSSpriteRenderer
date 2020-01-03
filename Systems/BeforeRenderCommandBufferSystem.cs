using Unity.Entities;
using UnityEngine;

namespace DOTSSpriteRenderer.Systems {
    [ExecuteAlways]
    [UpdateAfter(typeof(BeforeAnimationCommandBufferSystem))]
    [UpdateBefore(typeof(RenderSystem))]
    public class BeforeRenderCommandBufferSystem : EntityCommandBufferSystem { }
}
