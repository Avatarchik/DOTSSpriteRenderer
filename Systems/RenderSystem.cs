using System.Collections.Generic;
using DOTSSpriteRenderer.Components;
using DOTSSpriteRenderer.Types;
using DOTSSpriteRenderer.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;

namespace DOTSSpriteRenderer.Systems {
    [UpdateAfter(typeof(BeforeAnimationCommandBufferSystem))]
    public class RenderSystem : ComponentSystem {
        private readonly List<SpriteTexture> _allTextures    = new List<SpriteTexture>();
        private readonly List<ComputeBuffer> _argsBuffers    = new List<ComputeBuffer>();
        private readonly List<ComputeBuffer> _dataBuffers    = new List<ComputeBuffer>();
        private readonly List<Material>      _materials      = new List<Material>();
        private readonly uint[]              _argsBufferData = {6, 0, 0, 0, 0};

        private EntityQuery _sprites;
        private Mesh        _mesh;
        private Shader      _shader;
        private int         _dataBufferPropertyName;

        #region Overrides of ComponentSystemBase
        protected override void OnCreate() {
            base.OnCreate();
            _mesh                   = CreateMesh();
            _dataBufferPropertyName = Shader.PropertyToID("dataBuffer");
            _sprites = GetEntityQuery(new EntityQueryDesc {
                    All = new[] {
                            ComponentType.ReadOnly<SpriteIndex>(),
                            ComponentType.ReadOnly<SpriteTexture>(),
                    },
                    None = new ComponentType[] {typeof(Prefab)}
            });

            _shader = Shader.Find("Instanced/ECSSprite");
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            _allTextures.Clear();

            foreach (var computeBuffer in _argsBuffers) {
                computeBuffer.Dispose();
            }

            _argsBuffers.Clear();

            foreach (var computeBuffer in _dataBuffers) {
                computeBuffer.Dispose();
            }

            _dataBuffers.Clear();
        }
        #endregion

        #region CollectSpriteDataJob
        [BurstCompile]
        private struct CollectSpriteDataJob : IJobChunk {
            [ReadOnly] public NativeList<SpriteInfo>                           SpriteInfoArr;
            public            NativeArray<float4x4>                            Data;
            [ReadOnly] public ArchetypeChunkComponentType<Translation>         TranslationType;
            [ReadOnly] public ArchetypeChunkComponentType<Scale>               ScaleType;
            [ReadOnly] public ArchetypeChunkComponentType<Rotation>            RotationType;
            [ReadOnly] public ArchetypeChunkComponentType<SpriteFrameRotation> SpriteFrameRotationType;
            [ReadOnly] public ArchetypeChunkComponentType<SpriteIndex>         SpriteIndexType;
            [ReadOnly] public ArchetypeChunkComponentType<SpriteFlip>          SpriteFlipType;

            #region Implementation of IJobChunk
            public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
                var translations  = chunk.GetNativeArray(TranslationType);
                var spriteIndices = chunk.GetNativeArray(SpriteIndexType);

                NativeArray<Scale> scales = default;
                if (chunk.Has(ScaleType)) {
                    scales = chunk.GetNativeArray(ScaleType);
                }

                NativeArray<Rotation> spriteRotations = default;
                if (chunk.Has(RotationType) && !chunk.Has(SpriteFrameRotationType)) {
                    spriteRotations = chunk.GetNativeArray(RotationType);
                }

                NativeArray<SpriteFlip> spriteFlips = default;
                if (chunk.Has(SpriteFlipType)) {
                    spriteFlips = chunk.GetNativeArray(SpriteFlipType);
                }

                for (var i = 0; i < chunk.Count; i++) {
                    var info = SpriteInfoArr[spriteIndices[i].Value];
                    var ts   = translations[i].Value;
                    var scal = scales.IsCreated ? scales[i].Value : 1;
                    var flpx = 1;
                    var flpy = 1;
                    var uv   = info.UV;
                    var sz   = info.Size;
                    var of   = info.Offset;
                    var zero = 0f;
                    var rota = 0f;
                    if (spriteRotations.IsCreated) {
                        var v = math.rotate(spriteRotations[i].Value, new float3(1, 0, 0));
                        rota = math.atan2(v.y, v.x);
                    }

                    if (spriteFlips.IsCreated) {
                        var spriteFlip = spriteFlips[i];
                        if (spriteFlip.FlipX) {
                            flpx = -1;
                        }

                        if (spriteFlip.FlipY) {
                            flpy = -1;
                        }
                    }

                    Data[firstEntityIndex + i] = new float4x4(ts.x, ts.y, ts.z, rota,
                                                              uv.x, uv.y, uv.z, uv.w,
                                                              sz.x, sz.y, of.x, of.y,
                                                              flpx, flpy, scal, zero);
                }
            }
            #endregion
        }
        #endregion

        #region Overrides of ComponentSystem
        protected override void OnUpdate() {
            Profiler.BeginSample("Batched sprite collect");
            _allTextures.Clear();
            EntityManager.GetAllUniqueSharedComponentData(_allTextures);

            var passCount     = _allTextures.Count;
            var jobs          = new NativeArray<JobHandle>(passCount, Allocator.TempJob);
            var dataBufferArr = new NativeArray<float4x4>[passCount];

            for (var i = 0; i < passCount; i++) {
                var spriteTexture = _allTextures[i];

                _sprites.SetSharedComponentFilter(spriteTexture);
                var entityCount = _sprites.CalculateEntityCount();

                if (entityCount == 0) {
                    jobs[i] = new JobHandle();
                    continue;
                }

                dataBufferArr[i] = new NativeArray<float4x4>(entityCount, Allocator.TempJob);

                jobs[i] = new CollectSpriteDataJob {
                        SpriteInfoArr           = SpriteCache.CachedSpriteInfo,
                        Data                    = dataBufferArr[i],
                        TranslationType         = GetArchetypeChunkComponentType<Translation>(),
                        ScaleType               = GetArchetypeChunkComponentType<Scale>(),
                        SpriteIndexType         = GetArchetypeChunkComponentType<SpriteIndex>(),
                        RotationType            = GetArchetypeChunkComponentType<Rotation>(),
                        SpriteFrameRotationType = GetArchetypeChunkComponentType<SpriteFrameRotation>(),
                        SpriteFlipType          = GetArchetypeChunkComponentType<SpriteFlip>(),
                }.Schedule(_sprites);
            }

            Profiler.EndSample();

            JobHandle.CompleteAll(jobs);
            jobs.Dispose();

            Profiler.BeginSample("Batched sprite render");
            var bounds = new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f));

            for (var i = 0; i < passCount; i++) {
                if (_argsBuffers.Count <= i) {
                    _argsBuffers.Add(new ComputeBuffer(5, sizeof(uint)));
                }

                var instanceCount = dataBufferArr[i].Length;

                if (_dataBuffers.Count <= i) {
                    _dataBuffers.Add(new ComputeBuffer(instanceCount, sizeof(float) * 16));
                }

                if (_materials.Count <= i) {
                    _materials.Add(new Material(_shader) {
                            enableInstancing = true,
                    });
                }

                if (instanceCount == 0) {
                    continue;
                }

                _argsBufferData[1] = (uint)instanceCount;
                _argsBuffers[i].SetData(_argsBufferData);

                if (_dataBuffers[i].count < instanceCount) {
                    _dataBuffers[i].Dispose();
                    _dataBuffers[i] = new ComputeBuffer(instanceCount, sizeof(float) * 16);
                }

                _dataBuffers[i].SetData(dataBufferArr[i].ToArray());

                _materials[i].mainTexture = SpriteCache.GetCachedTexture(i);
                _materials[i].SetBuffer(_dataBufferPropertyName, _dataBuffers[i]);

                Graphics.DrawMeshInstancedIndirect(_mesh, 0, _materials[i], bounds, _argsBuffers[i]);

                dataBufferArr[i].Dispose();
            }

            Profiler.EndSample();
        }
        #endregion

        private static Mesh CreateMesh() {
            var mesh     = new Mesh();
            var vertices = new Vector3[4];
            vertices[0]   = new Vector3(-.5f, -.5f, 0);
            vertices[1]   = new Vector3(.5f,  -.5f, 0);
            vertices[2]   = new Vector3(-.5f, .5f,  0);
            vertices[3]   = new Vector3(.5f,  .5f,  0);
            mesh.vertices = vertices;

            var tri = new int[6];
            tri[0]         = 0;
            tri[1]         = 2;
            tri[2]         = 1;
            tri[3]         = 2;
            tri[4]         = 3;
            tri[5]         = 1;
            mesh.triangles = tri;

            var uv = new Vector2[4];
            uv[0]   = new Vector2(0, 0);
            uv[1]   = new Vector2(1, 0);
            uv[2]   = new Vector2(0, 1);
            uv[3]   = new Vector2(1, 1);
            mesh.uv = uv;

            return mesh;
        }
    }
}
