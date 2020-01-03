using System.Collections.Generic;
using DOTSSpriteRenderer.Components;
using DOTSSpriteRenderer.Types;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DOTSSpriteRenderer.Utils {
    /// <summary>
    /// Keeps sprite info and and sprites textures indexed to make <see cref="SpriteTexture"/> blittable.
    /// </summary>
    public static class SpriteCache {
        /// <summary>
        /// If duplicates are merged it takes less memory but makes caching slower. 
        /// </summary>
        public static bool MergeDuplicates = false;

        private static          NativeList<SpriteInfo>   _cachedSpriteInfo;
        private static readonly Dictionary<Texture, int> CachedTextureIndices = new Dictionary<Texture, int>(64);
        private static readonly List<Texture>            CachedTextures       = new List<Texture>(64);
        private static readonly object                   CacheLock            = new object();

        /// <summary>
        /// Caches sprite and returns <see cref="CachedSprite"/> with its index.
        /// </summary>
        public static CachedSprite Cache(Sprite sprite) {
            lock (CacheLock) {
                var rect = sprite.textureRect;
                var texSize = new float4(sprite.texture.width, sprite.texture.height,
                                         sprite.texture.width, sprite.texture.height);
                if (!CachedTextureIndices.TryGetValue(sprite.texture, out var textureIndex)) {
                    textureIndex = CachedTextures.Count;
                    CachedTextures.Add(sprite.texture);
                    CachedTextureIndices[sprite.texture] = textureIndex;
                }

                if (!_cachedSpriteInfo.IsCreated) {
                    _cachedSpriteInfo = new NativeList<SpriteInfo>(2048, Allocator.Persistent);
                }

                var spriteIndex = _cachedSpriteInfo.Length;

                var spriteInfo = new SpriteInfo {
                        UV           = new float4(rect.xMin, rect.yMin, rect.xMax, rect.yMax) / texSize,
                        Size         = new float2(rect.width, rect.height)                    / sprite.pixelsPerUnit,
                        Offset       = -(sprite.pivot - sprite.textureRectOffset)             / sprite.pixelsPerUnit,
                        TextureIndex = textureIndex,
                };

                if (MergeDuplicates) {
                    var existingIndex = _cachedSpriteInfo.IndexOf(spriteInfo);
                    if (existingIndex >= 0) {
                        return new CachedSprite(existingIndex);
                    }
                }

                _cachedSpriteInfo.Add(spriteInfo);
                return new CachedSprite(spriteIndex);
            }
        }

        /// <summary>
        /// Clears all caches.
        /// Warning: all <see cref="CachedSprite"/> instances created before this call become invalid.
        /// </summary>
        public static void ClearCache() {
            lock (CacheLock) {
                _cachedSpriteInfo.Dispose();
                CachedTextures.Clear();
            }
        }

        internal static NativeList<SpriteInfo> CachedSpriteInfo => _cachedSpriteInfo;

        internal static Texture GetCachedTexture(int index) {
            lock (CacheLock) {
                return CachedTextures[index];
            }
        }
    }
}
