using DOTSSpriteRenderer.Components;
using DOTSSpriteRenderer.Types;
using DOTSSpriteRenderer.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace DOTSSpriteRenderer.Systems {
    [UpdateAfter(typeof(BeforeAnimationCommandBufferSystem))]
    public class FrameChangeSystem : JobComponentSystem {
        private EntityQuery               _animations;
        private EntityCommandBufferSystem _commandBufferSystem;

        #region Overrides of ComponentSystemBase
        protected override void OnCreate() {
            base.OnCreate();
            _animations = GetEntityQuery(ComponentType.ReadWrite<SpriteAnimation>(),
                                         ComponentType.ReadOnly<SpriteAnimationNextFrame>(),
                                         ComponentType.ReadWrite<SpriteIndex>());

            _commandBufferSystem = World.GetOrCreateSystem<BeforeRenderCommandBufferSystem>();
        }
        #endregion

        #region UpdateSpriteJob
        [BurstCompile]
        private struct UpdateSpriteIndexJob : IJobChunk {
            [ReadOnly] public                            BufferFromEntity<SpriteSequence>                      SpriteSequences;
            [ReadOnly] public                            NativeList<SpriteInfo>                                SpriteInfoArr;
            [ReadOnly] public                            ArchetypeChunkEntityType                              EntityType;
            [ReadOnly] public                            ArchetypeChunkComponentType<SpriteAnimationNextFrame> SpriteAnimationNextFrameType;
            public                                       ArchetypeChunkComponentType<SpriteAnimation>          SpriteAnimationType;
            public                                       ArchetypeChunkComponentType<SpriteIndex>              SpriteIndexType;
            [NativeDisableParallelForRestriction] public EntityCommandBuffer.Concurrent                        CommandBuffer;

            #region Implementation of IJobChunk
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
                var entities      = chunk.GetNativeArray(EntityType);
                var animations    = chunk.GetNativeArray(SpriteAnimationType);
                var nextFrames    = chunk.GetNativeArray(SpriteAnimationNextFrameType);
                var spriteIndices = chunk.GetNativeArray(SpriteIndexType);

                for (var i = 0; i < chunk.Count; i++) {
                    var entity      = entities[i];
                    var nextFrame   = nextFrames[i];
                    var animation   = animations[i];
                    var spriteIndex = spriteIndices[i];

                    if (nextFrame.Value != animation.Frame) {
                        animation.Frame = nextFrame.Value;
                        var sequence        = SpriteSequences[entity];
                        var nextSpriteIndex = sequence[nextFrame.Value].SpriteIndex;
                        spriteIndex.Value = nextSpriteIndex;
                        var spriteInfo = SpriteInfoArr[nextSpriteIndex];

                        if (spriteIndex.TextureIndex != spriteInfo.TextureIndex) {
                            spriteIndex.TextureIndex = spriteInfo.TextureIndex;
                            CommandBuffer.SetSharedComponent(chunkIndex, entity, new SpriteTexture {
                                    Index = spriteInfo.TextureIndex,
                            });
                        }

                        spriteIndices[i] = spriteIndex;
                    }
                }
            }
            #endregion
        }
        #endregion

        #region Overrides of JobComponentSystem
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            inputDeps = new UpdateSpriteIndexJob {
                    SpriteSequences              = GetBufferFromEntity<SpriteSequence>(true),
                    SpriteInfoArr                = SpriteCache.CachedSpriteInfo,
                    CommandBuffer                = _commandBufferSystem.CreateCommandBuffer().ToConcurrent(),
                    EntityType                   = GetArchetypeChunkEntityType(),
                    SpriteAnimationType          = GetArchetypeChunkComponentType<SpriteAnimation>(),
                    SpriteIndexType              = GetArchetypeChunkComponentType<SpriteIndex>(),
                    SpriteAnimationNextFrameType = GetArchetypeChunkComponentType<SpriteAnimationNextFrame>(),
            }.Schedule(_animations, inputDeps);

            _commandBufferSystem.AddJobHandleForProducer(inputDeps);
            return inputDeps;
        }
        #endregion
    }
}
