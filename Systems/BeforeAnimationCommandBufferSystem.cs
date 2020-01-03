using Unity.Entities;
using UnityEngine;

namespace DOTSSpriteRenderer.Systems {
    [ExecuteAlways]
    [UpdateBefore(typeof(RenderSystem))]
    public class BeforeAnimationCommandBufferSystem : EntityCommandBufferSystem { }
}
