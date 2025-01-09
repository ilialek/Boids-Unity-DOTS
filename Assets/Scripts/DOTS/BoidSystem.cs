using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

namespace Boids
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct BoidSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityQuery boidQuery = SystemAPI.QueryBuilder().WithAll<Boid>().WithAllRW<LocalToWorld>().Build();

            var world = state.WorldUnmanaged;
            state.EntityManager.GetAllUniqueSharedComponents(out NativeList<Boid> uniqueBoidTypes, world.UpdateAllocator.ToAllocator);
            float dt = math.min(0.05f, SystemAPI.Time.DeltaTime);

            float3 boundsCenter = new float3(0f, 0f, 0f);
            float3 boundsSize = new float3(5, 5, 5);

            foreach (Boid boidSettings in uniqueBoidTypes)
            {
                boidQuery.AddSharedComponentFilter(boidSettings);

                int boidCount = boidQuery.CalculateEntityCount();
                if (boidCount == 0)
                {
                    boidQuery.ResetFilter();
                    continue;
                }

                NativeArray<LocalToWorld> boidPositions = boidQuery.ToComponentDataArray<LocalToWorld>(Allocator.TempJob);
                NativeParallelMultiHashMap<int3, int> spatialGrid = new NativeParallelMultiHashMap<int3, int>(boidCount, Allocator.TempJob);

                var populateGridJob = new PopulateSpatialGridJob
                {
                    BoidPositions = boidPositions,
                    CellSize = boidSettings.CellRadius,
                    SpatialGrid = spatialGrid.AsParallelWriter()
                };
                populateGridJob.Schedule(boidCount, 64).Complete();

                var flockingJob = new FlockingJob
                {
                    BoidSettings = boidSettings,
                    DeltaTime = dt,
                    BoundsCenter = boundsCenter,
                    BoundsSize = boundsSize,
                    BoidPositions = boidPositions,
                    SpatialGrid = spatialGrid,
                    CellSize = boidSettings.CellRadius,
                };

                flockingJob.ScheduleParallel(boidQuery, state.Dependency).Complete();

                // Cleanup
                spatialGrid.Dispose();
                boidPositions.Dispose();

                boidQuery.ResetFilter();
            }
        }

        [BurstCompile]
        public struct PopulateSpatialGridJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<LocalToWorld> BoidPositions;
            public float CellSize;
            public NativeParallelMultiHashMap<int3, int>.ParallelWriter SpatialGrid;

            public void Execute(int index)
            {
                float3 position = BoidPositions[index].Position;
                int3 cell = SpatialHash(position, CellSize);
                SpatialGrid.Add(cell, index);
            }
        }

        [BurstCompile]
        public partial struct FlockingJob : IJobEntity
        {
            public Boid BoidSettings;
            public float DeltaTime;

            public float3 BoundsCenter;
            public float3 BoundsSize;

            [ReadOnly] public NativeArray<LocalToWorld> BoidPositions;
            [ReadOnly] public NativeParallelMultiHashMap<int3, int> SpatialGrid;
            public float CellSize;

            public void Execute([EntityIndexInQuery] int index, ref LocalToWorld localToWorld)
            {
                float3 position = localToWorld.Position;
                float3 forward = localToWorld.Forward;

                float3 alignment = float3.zero;
                float3 cohesion = float3.zero;
                float3 separation = float3.zero;
                float3 boundsSteering = float3.zero;
                int neighborCount = 0;

                int3 currentCell = SpatialHash(position, CellSize);

                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            int3 neighborCell = currentCell + new int3(x, y, z);

                            if (SpatialGrid.TryGetFirstValue(neighborCell, out int neighborIndex, out var iterator))
                            {
                                do
                                {
                                    if (neighborIndex == index) continue;

                                    float3 otherPosition = BoidPositions[neighborIndex].Position;
                                    float distance = math.distance(position, otherPosition);

                                    if (distance < BoidSettings.CellRadius)
                                    {
                                        alignment += BoidPositions[neighborIndex].Forward;
                                        cohesion += otherPosition;
                                        separation += (position - otherPosition) / (distance * distance);
                                        neighborCount++;
                                    }
                                } while (SpatialGrid.TryGetNextValue(out neighborIndex, ref iterator));
                            }
                        }
                    }
                }

                if (neighborCount > 0)
                {
                    alignment = math.normalize(alignment / neighborCount);
                    cohesion = math.normalize(cohesion / neighborCount - position);
                    separation = math.normalize(separation / neighborCount);
                }

                float3 boundsMin = BoundsCenter - BoundsSize / 2f;
                float3 boundsMax = BoundsCenter + BoundsSize / 2f;

                if (position.x < boundsMin.x || position.x > boundsMax.x ||
                    position.y < boundsMin.y || position.y > boundsMax.y ||
                    position.z < boundsMin.z || position.z > boundsMax.z)
                {
                    float3 directionToCenter = BoundsCenter - position;
                    boundsSteering = math.normalize(directionToCenter) * BoidSettings.MaxSpeed;
                }

                float3 desiredVelocity = forward +
                                         alignment * BoidSettings.AlignmentWeight +
                                         cohesion * BoidSettings.CohesionWeight +
                                         separation * BoidSettings.SeparationWeight +
                                         boundsSteering * BoidSettings.BoundsWeight;

                desiredVelocity = math.normalize(desiredVelocity) * BoidSettings.MaxSpeed;
                float3 smoothedVelocity = math.lerp(forward * BoidSettings.MaxSpeed, desiredVelocity, BoidSettings.SmoothFactor * DeltaTime);

                position += smoothedVelocity * DeltaTime;

                quaternion smoothedRotation = math.slerp(
                    quaternion.LookRotationSafe(forward, math.up()),
                    quaternion.LookRotationSafe(smoothedVelocity, math.up()),
                    BoidSettings.RotationSmoothFactor * DeltaTime
                );

                localToWorld = new LocalToWorld
                {
                    Value = float4x4.TRS(position, smoothedRotation, new float3(1f, 1f, 1f))
                };
            }
        }

        private static int3 SpatialHash(float3 position, float cellSize)
        {
            return new int3(
                (int)math.floor(position.x / cellSize),
                (int)math.floor(position.y / cellSize),
                (int)math.floor(position.z / cellSize)
            );
        }
    }
}
