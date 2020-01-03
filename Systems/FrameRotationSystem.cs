using DOTSSpriteRenderer.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTSSpriteRenderer.Systems {
    [UpdateAfter(typeof(BeforeAnimationCommandBufferSystem))]
    [UpdateBefore(typeof(FrameChangeSystem))]
    public class FrameRotationSystem : JobComponentSystem {
        private EntityQuery _sprites;

        #region Overrides of ComponentSystemBase
        protected override void OnCreate() {
            base.OnCreate();
            _sprites = GetEntityQuery(ComponentType.ReadOnly<SpriteFrameRotation>(),
                                      ComponentType.ReadOnly<SpriteAnimation>(),
                                      ComponentType.ReadOnly<Rotation>(),
                                      ComponentType.ReadWrite<SpriteAnimationNextFrame>());
        }
        #endregion

        #region UpdateSpriteIndexJob
        [BurstCompile]
        private struct UpdateSpriteIndexJob : IJobForEach<SpriteAnimation, Rotation, SpriteAnimationNextFrame> {
            #region Implementation of IJobForEach_CCC<SpriteAnimation,SpriteIndex,SpriteAnimationNextFrame>
            public void Execute([ReadOnly] ref SpriteAnimation          spriteAnimation,
                                [ReadOnly] ref Rotation                 rotation,
                                ref            SpriteAnimationNextFrame nextFrame) {
                const float pi2        = 2f * math.PI;
                var         frameCount = spriteAnimation.SequenceFramesCount;
                var         v          = math.rotate(rotation.Value, new float3(1, 0, 0));
                var         angle      = math.atan2(v.y, v.x);
                angle = angle < 0 ? pi2 + angle : angle;
                var p = (int)math.round(angle / pi2 * frameCount);
                p               = (p >= frameCount) ? (p - frameCount) : p;
                nextFrame.Value = p;
            }
            #endregion
        }
        #endregion

        #region Overrides of JobComponentSystem
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            return new UpdateSpriteIndexJob().Schedule(_sprites, inputDeps);
        }
        #endregion
    }
}
