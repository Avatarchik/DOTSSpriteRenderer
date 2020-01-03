using DOTSSpriteRenderer.Components;
using DOTSSpriteRenderer.Types;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace DOTSSpriteRenderer.Systems {
    [UpdateAfter(typeof(BeforeAnimationCommandBufferSystem))]
    [UpdateBefore(typeof(FrameChangeSystem))]
    public class AnimationSystem : JobComponentSystem {
        private EntityQuery _animations;

        #region Overrides of ComponentSystemBase
        protected override void OnCreate() {
            base.OnCreate();
            _animations = GetEntityQuery(ComponentType.ReadOnly<SpriteAnimation>(),
                                         ComponentType.ReadWrite<SpriteAnimationNextFrame>());
        }
        #endregion

        #region UpdateSpriteIndexJob
        [BurstCompile]
        private struct UpdateFrameJob : IJobForEachWithEntity<SpriteAnimation, SpriteAnimationNextFrame> {
            [ReadOnly] public double ElapsedTime;

            #region Implementation of IJobForEachWithEntity_ECC<SpriteAnimation,SpriteIndex>
            public void Execute(Entity                                  entity, int index,
                                [ReadOnly] ref SpriteAnimation          animation,
                                ref            SpriteAnimationNextFrame nextFrame) {
                if (animation.FrameDelay > 0) {
                    if (animation.NextFrameTime < 0) {
                        animation.NextFrameTime = ElapsedTime;
                    }

                    if (ElapsedTime <= animation.NextFrameTime) {
                        animation.NextFrameTime += animation.FrameDelay;
                        nextFrame.Value         =  animation.Frame + 1;

                        if (nextFrame.Value >= animation.SequenceFramesCount) {
                            if (animation.RepeatMode == RepeatMode.Loop) {
                                nextFrame.Value = 0;
                            }
                            else {
                                nextFrame.Value = animation.Frame;
                            }
                        }
                    }
                }
            }
            #endregion
        }
        #endregion

        #region Overrides of JobComponentSystem
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            return new UpdateFrameJob {ElapsedTime = Time.ElapsedTime}.Schedule(_animations, inputDeps);
        }
        #endregion
    }
}
