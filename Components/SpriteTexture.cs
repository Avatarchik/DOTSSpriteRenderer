using System;
using Unity.Entities;

namespace DOTSSpriteRenderer.Components {
    public struct SpriteTexture : ISharedComponentData, IEquatable<SpriteTexture> {
        public int Index;

        #region Equality members
        public bool Equals(SpriteTexture other) {
            return Index == other.Index;
        }

        public override int GetHashCode() {
            return Index;
        }
        #endregion
    }
}
