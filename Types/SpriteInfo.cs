using System;
using Unity.Mathematics;

namespace DOTSSpriteRenderer.Types {
    public struct SpriteInfo : IEquatable<SpriteInfo> {
        public float4 UV;
        public float2 Size;
        public float2 Offset;
        public int    TextureIndex;

        #region Equality members
        public bool Equals(SpriteInfo other) {
            return UV.Equals(other.UV) && Size.Equals(other.Size) && Offset.Equals(other.Offset) && TextureIndex == other.TextureIndex;
        }

        public override bool Equals(object obj) {
            return obj is SpriteInfo other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = UV.GetHashCode();
                hashCode = (hashCode * 397) ^ Size.GetHashCode();
                hashCode = (hashCode * 397) ^ Offset.GetHashCode();
                hashCode = (hashCode * 397) ^ TextureIndex;
                return hashCode;
            }
        }
        #endregion
    }
}
