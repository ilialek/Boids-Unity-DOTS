using System;
using Unity.Entities;
//using Unity.Transforms;
using UnityEngine;

public class BoidAuthoring : MonoBehaviour
{
    public float SeparationWeight = 1.0f;
    public float AlignmentWeight = 1.0f;
    public float CohesionWeight = 1.0f;
    public float MoveSpeed = 25.0f;
    public float CellRadius;
    public float MaxSpeed;
    public float BoundsWeight;
    public float SmoothFactor;
    public float RotationSmoothFactor;

    class Baker : Baker<BoidAuthoring>
    {
        public override void Bake(BoidAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Renderable | TransformUsageFlags.WorldSpace);
            AddSharedComponent(entity, new Boid
            {
                SeparationWeight = authoring.SeparationWeight,
                AlignmentWeight = authoring.AlignmentWeight,
                CohesionWeight = authoring.CohesionWeight,
                MoveSpeed = authoring.MoveSpeed,
                CellRadius = authoring.CellRadius,
                MaxSpeed = authoring.MaxSpeed,
                BoundsWeight = authoring.BoundsWeight,
                SmoothFactor = authoring.SmoothFactor,
                RotationSmoothFactor = authoring.RotationSmoothFactor
            });
        }
    }

}

[Serializable]
public struct Boid : ISharedComponentData
{
    public float SeparationWeight;
    public float AlignmentWeight;
    public float CohesionWeight;
    public float MoveSpeed;
    public float CellRadius;
    public float MaxSpeed;
    public float BoundsWeight;
    public float SmoothFactor;
    public float RotationSmoothFactor;
}
