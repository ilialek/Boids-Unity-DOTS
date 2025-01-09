using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
//using UnityEngine.Profiling;

namespace Boids
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct BoidSchoolSpawnSystem : ISystem
    {
        private EntityQuery _boidQuery;
        public void OnCreate(ref SystemState state)
        {
            _boidQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<Boid, LocalTransform>().Build(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
            ComponentLookup<LocalToWorld> localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            WorldUnmanaged world = state.World.Unmanaged;

            foreach (var (boidSchool, boidSchoolLocalToWorld, entity) in
                     SystemAPI.Query<RefRO<BoidSchool>, RefRO<LocalToWorld>>()
                         .WithEntityAccess())
            {
                NativeArray<Entity> boidEntities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(boidSchool.ValueRO.Count, ref world.UpdateAllocator);

                state.EntityManager.Instantiate(boidSchool.ValueRO.Prefab, boidEntities);

                SetBoidLocalToWorld setBoidLocalToWorldJob = new SetBoidLocalToWorld
                {
                    LocalToWorldFromEntity = localToWorldLookup,
                    Entities = boidEntities,
                    Center = boidSchoolLocalToWorld.ValueRO.Position,
                    Radius = boidSchool.ValueRO.InitialRadius
                };
                state.Dependency = setBoidLocalToWorldJob.Schedule(boidSchool.ValueRO.Count, 64, state.Dependency);
                state.Dependency.Complete();

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            // All Prefabs are currently forced to TransformUsageFlags.Dynamic by default, which means boids get a LocalTransform
            // they don't need. As a workaround, remove the component at spawn-time.
            state.EntityManager.RemoveComponent<LocalTransform>(_boidQuery);
        }
    }

    [BurstCompile]
    struct SetBoidLocalToWorld : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        [NativeDisableParallelForRestriction]
        public ComponentLookup<LocalToWorld> LocalToWorldFromEntity;

        public NativeArray<Entity> Entities;
        public float3 Center;
        public float Radius;

        public void Execute(int i)
        {
            Entity entity = Entities[i];
            Random random = new Random(((uint)(entity.Index + i + 1) * 0x9F6ABC1));
            float3 dir = math.normalizesafe(random.NextFloat3() - new float3(0.5f, 0.5f, 0.5f));
            float3 pos = Center + (dir * Radius);
            LocalToWorld localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(pos, quaternion.LookRotationSafe(dir, math.up()), new float3(1f, 1f, 1f))
            };
            LocalToWorldFromEntity[entity] = localToWorld;
        }
    }
}
