# Sprite renderer for Unity DOTS

Simple sprite renderer for Unity DOTS. Supports:

* Static sprite images
* Frame-based animated sprites
* Rotation-based static images picked from sequence

Native sprite atlases are supported.

## Usage
Use `SpriteUtils` to add sprite to the entity. 
```c#
public class SpriteTest: MonoBehaviour {
    [SerializeField] private Sprite _spriteImage;
    
    private void Satrt() {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var entity = em.CreateEntity();
        em.AddComponentData(entity, new Translation {
            Value = new float3(0, 0, 10),
        });
        SpriteUtils.AddSprite(em, entity, _spriteImage);
    }
}
```

## Caching
Due to performance reasons its preferable to use `SpriteUtls` method overrides that accepts `CachedSprite` instead of `Sprite`. 
```c#
public class SpriteTest: MonoBehaviour {
    [SerializeField] private Sprite[] _spriteAnimation;

    private void Satrt() {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var frames = _spriteAnimation.Select(SpriteCache.Cache).ToArray();
        for (var i = 0; i < 100; i++) {
                var entity = em.CreateEntity();
                em.AddComponentData(entity, new Translation {
                    Value = new float3(i, 0, 10),
                });
                SpriteUtils.AddSpriteAnimationToEntity(em, entity, frames);
        }
    }
}
```
