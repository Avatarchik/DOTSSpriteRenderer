using System;
using System.Collections.Generic;
using System.Linq;
using DOTSSpriteRenderer.Components;
using DOTSSpriteRenderer.Types;
using JetBrains.Annotations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DOTSSpriteRenderer.Utils {
    public static class SpriteUtils {
        /// <summary>
        /// Components required for simple sprite entity archetype
        /// </summary>
        public static readonly ComponentType[] SpriteComponents = {
                typeof(Translation),
                typeof(SpriteTexture),
                typeof(SpriteIndex),
        };

        /// <summary>
        /// Components required for animated sprite entity archetype
        /// </summary>
        public static readonly ComponentType[] AnimatedSpriteComponents = SpriteComponents.Concat(new ComponentType[] {
                typeof(SpriteSequence),
                typeof(SpriteAnimation),
                typeof(SpriteAnimationNextFrame),
        }).ToArray();

        /// <summary>
        /// Components required for frame-rotation sprite
        /// </summary>
        public static readonly ComponentType[] FrameRotationSpriteComponents = AnimatedSpriteComponents.Concat(new ComponentType[] {
                typeof(Rotation),
                typeof(SpriteFrameRotation)
        }).ToArray();

        /// <summary>
        /// Adds or sets sprite data components to the entity. Sprite is cached automatically.  
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AddSprite([NotNull] EntityManager em,
                                     Entity                  entity,
                                     [NotNull] Sprite        sprite) {
            #region Checks
            if (sprite == null) {
                throw new ArgumentNullException(nameof(sprite));
            }
            #endregion

            var spriteIndex = SpriteCache.Cache(sprite);
            AddSprite(em, entity, spriteIndex);
        }

        /// <summary>
        /// Adds or sets sprite data components to the entity.  
        /// </summary>
        public static void AddSprite([NotNull] EntityManager em,
                                     Entity                  entity,
                                     CachedSprite            cachedSprite) {
            #region Checks
            if (em == null) {
                throw new ArgumentNullException(nameof(em));
            }

            if (entity == Entity.Null) {
                throw new ArgumentNullException(nameof(entity));
            }
            #endregion

            if (!em.HasComponent<Translation>(entity)) {
                em.AddComponentData(entity, new Translation());
            }

            if (!em.HasComponent<Rotation>(entity)) {
                em.AddComponentData(entity, new Rotation {Value = quaternion.identity});
            }

            var textureIndex = SpriteCache.CachedSpriteInfo[cachedSprite.Index].TextureIndex;
            em.AddSharedComponentData(entity, new SpriteTexture {Index = textureIndex});
            em.AddComponentData(entity, new SpriteIndex {
                    Value        = cachedSprite.Index,
                    TextureIndex = textureIndex,
            });
        }

        /// <summary>
        /// Adds or sets animated sprite data components to the entity.
        /// Frame sprites are cached automatically.
        /// Frames are changed one by one based on animation FPS and repeat mode.
        /// </summary>
        public static void AddSpriteAnimationToEntity([NotNull] EntityManager       em,
                                                      Entity                        entity,
                                                      [NotNull] IEnumerable<Sprite> sprites,
                                                      int                           fps        = 60,
                                                      RepeatMode                    repeatMode = RepeatMode.Loop) {
            #region Checks
            if (sprites == null) {
                throw new ArgumentNullException(nameof(sprites));
            }
            #endregion

            var cachedSprites = sprites.Select(SpriteCache.Cache).ToArray();
            AddSpriteAnimationToEntity(em, entity, cachedSprites, fps, repeatMode);
        }

        /// <summary>
        /// Adds or sets animated sprite data components to the entity.
        /// Frames are changed one by one based on animation FPS and repeat mode.
        /// </summary>
        public static void AddSpriteAnimationToEntity([NotNull] EntityManager             em,
                                                      Entity                              entity,
                                                      [NotNull] IEnumerable<CachedSprite> cachedSprites,
                                                      int                                 fps        = 60,
                                                      RepeatMode                          repeatMode = RepeatMode.Loop) {
            #region Checks
            if (em == null) {
                throw new ArgumentNullException(nameof(em));
            }

            if (entity == Entity.Null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (cachedSprites == null) {
                throw new ArgumentNullException(nameof(cachedSprites));
            }

            if (fps < 0) {
                throw new ArgumentOutOfRangeException(nameof(fps), "frames per seconds cannot be negative");
            }
            #endregion

            var cachedSpriteSequence = cachedSprites
                                      .Select(s => new SpriteSequence {SpriteIndex = s.Index})
                                      .ToArray();

            if (cachedSpriteSequence.Length == 0) {
                throw new ArgumentOutOfRangeException(nameof(cachedSprites), "Empty sprite enumerable");
            }

            if (!em.HasComponent<Translation>(entity)) {
                em.AddComponentData(entity, new Translation());
            }

            if (!em.HasComponent<Rotation>(entity)) {
                em.AddComponentData(entity, new Rotation {Value = quaternion.identity});
            }

            var spriteSequence = em.AddBuffer<SpriteSequence>(entity);
            spriteSequence.CopyFrom(cachedSpriteSequence);

            em.AddSharedComponentData(entity, new SpriteTexture {
                    Index = SpriteCache.CachedSpriteInfo[cachedSpriteSequence[0].SpriteIndex].TextureIndex
            });

            var spriteIndex = new SpriteIndex {
                    Value        = cachedSpriteSequence[0].SpriteIndex,
                    TextureIndex = SpriteCache.CachedSpriteInfo[cachedSpriteSequence[0].SpriteIndex].TextureIndex,
            };
            if (em.HasComponent<SpriteIndex>(entity)) {
                em.SetComponentData(entity, spriteIndex);
            }
            else {
                em.AddComponentData(entity, spriteIndex);
            }

            var spriteAnimation = new SpriteAnimation {
                    Frame               = -1,
                    RepeatMode          = repeatMode,
                    FrameDelay          = fps == 0 ? 0 : 1f / fps,
                    NextFrameTime       = -1,
                    SequenceFramesCount = cachedSpriteSequence.Length,
            };
            if (em.HasComponent<SpriteAnimation>(entity)) {
                em.SetComponentData(entity, spriteAnimation);
            }
            else {
                em.AddComponentData(entity, spriteAnimation);
            }

            var spriteAnimationNextFrame = new SpriteAnimationNextFrame {Value = 0};
            if (em.HasComponent<SpriteAnimationNextFrame>(entity)) {
                em.SetComponentData(entity, spriteAnimationNextFrame);
            }
            else {
                em.AddComponentData(entity, spriteAnimationNextFrame);
            }
        }

        /// <summary>
        /// Adds or sets frame-rotated sprite data components to the entity.
        /// Frame is shown based on Z angle of entity <see cref="Rotation"/> component data.
        /// Sprite frames should start with 0 degree (right direction) and sequence counter-clockwise.
        /// Its recommended to keep frames number multiple of 4.
        /// Frame sprites are cached automatically.
        /// </summary>
        public static void AddSpriteFrameRotation([NotNull] EntityManager       em,
                                                  Entity                        entity,
                                                  [NotNull] IEnumerable<Sprite> sprites360) {
            AddSpriteAnimationToEntity(em, entity, sprites360, 0);
            em.AddComponentData(entity, new SpriteFrameRotation());
        }

        /// <summary>
        /// Adds or sets frame-rotated sprite data components to the entity.
        /// Frame is shown based on Z angle of entity <see cref="Rotation"/> component data.
        /// Sprite frames should start with 0 degree (right direction) and sequence counter-clockwise.
        /// Its recommended to keep frames number multiple of 4. 
        /// </summary>
        public static void AddSpriteFrameRotation([NotNull] EntityManager             em,
                                                  Entity                              entity,
                                                  [NotNull] IEnumerable<CachedSprite> cachedSprites360) {
            AddSpriteAnimationToEntity(em, entity, cachedSprites360, 0);
            if (!em.HasComponent<SpriteFrameRotation>(entity)) {
                em.AddComponentData(entity, new SpriteFrameRotation());
            }
        }

        /// <summary>
        /// Converts clockwise frame sequence started with XII hours to math-friendly counter-clockwise sequence accepted with <see cref="AddSpriteFrameRotation(Unity.Entities.EntityManager,Unity.Entities.Entity,System.Collections.Generic.IEnumerable{UnityEngine.Sprite})"/>
        /// </summary>
        /// <param name="clockwiseSequence"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public static Sprite[] SequenceTo360([NotNull] Sprite[] clockwiseSequence) {
            if (clockwiseSequence == null) {
                throw new ArgumentNullException(nameof(clockwiseSequence));
            }

            if ((clockwiseSequence.Length % 4) != 0) {
                throw new ArgumentException("Length should be multiplier of 4", nameof(clockwiseSequence));
            }

            var startFrame = clockwiseSequence.Length / 4;
            var sequence   = new Sprite[clockwiseSequence.Length];
            var index      = 0;

            for (var i = startFrame; i >= 0; i--) {
                sequence[index++] = clockwiseSequence[i];
            }

            for (var i = clockwiseSequence.Length - 1; i > startFrame; i--) {
                sequence[index++] = clockwiseSequence[i];
            }

            return sequence;
        }
    }
}
