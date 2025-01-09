using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlockAgent : MonoBehaviour
{
    
    private Vector3 velocity;
    private List<FlockAgent> agents;  // List of all flocking agents

    bool turning = false;

    void Start()
    {
        // Initialize velocity randomly for each agent
        velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * FlockManager.FM.speed;
        agents = new List<FlockAgent>(FindObjectsOfType<FlockAgent>());  // Get all flocking agents in the scene
    }

    void Update()
    {
        Vector3 alignment = ComputeAlignment();
        Vector3 cohesion = ComputeCohesion();
        Vector3 separation = ComputeSeparation();
        

        velocity = Vector3.Lerp(velocity, velocity + alignment * FlockManager.FM.alignmentWeight +
    cohesion * FlockManager.FM.cohesionWeight + separation * FlockManager.FM.separationWeight, Time.deltaTime * FlockManager.FM.smoothFactor);

        velocity = Vector3.ClampMagnitude(velocity, FlockManager.FM.speed);

        // Move the agent
        transform.position += velocity * Time.deltaTime;
        transform.forward = velocity.normalized;  // Face the direction of movement

        Bounds bounds = new Bounds(FlockManager.FM.transform.position, FlockManager.FM.swimLimits * 2.0f);
        if (!bounds.Contains(transform.position))
        {
            turning = true;
        }
        else
        {
            turning = false;
        }

        if (turning)
        {
            Vector3 direction = bounds.ClosestPoint(transform.position) - transform.position; // Get direction to the closest point in bounds
            direction.Normalize(); // Normalize to get a direction vector

            // Rotate towards the direction to turn away from the bounds
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                FlockManager.FM.rotationSpeed * Time.deltaTime
            );

            // Optionally adjust the velocity to steer away from bounds
            velocity += direction * FlockManager.FM.rotationSpeed * Time.deltaTime; // Apply a force to move away from bounds
        }
    }

    Vector3 ComputeAlignment()
    {
        Vector3 alignment = Vector3.zero;
        int neighborCount = 0;

        foreach (var agent in agents)
        {
            if (agent != this && Vector3.Distance(transform.position, agent.transform.position) < FlockManager.FM.neighborRadius)
            {
                alignment += agent.velocity;
                neighborCount++;
            }
        }

        if (neighborCount == 0)
        {
            return alignment;
        }

        if (neighborCount > 0)
        {
            alignment /= neighborCount;  // Average out the velocities
            alignment = alignment.normalized;  // Normalize to get direction
        }

        return alignment;
    }

    Vector3 ComputeCohesion()
    {
        Vector3 cohesion = Vector3.zero;
        int neighborCount = 0;

        foreach (var agent in agents)
        {
            if (agent != this && Vector3.Distance(transform.position, agent.transform.position) < FlockManager.FM.neighborRadius)
            {
                cohesion += agent.transform.position;
                neighborCount++;
            }
        }

        if (neighborCount == 0)
        {
            return cohesion;
        }

        cohesion /= neighborCount;  // Get the average position of neighbors
        Vector3 directionToCohesion = cohesion - transform.position;  // Direction to center of mass

        // Check if agents are too close to center of mass, reduce cohesion influence
        float distanceToCohesion = directionToCohesion.magnitude;
        if (distanceToCohesion < FlockManager.FM.separationRadius)
        {
            directionToCohesion *= (distanceToCohesion / FlockManager.FM.separationRadius); // Reduce the cohesion force
        }

        return directionToCohesion.normalized;
    }

    Vector3 ComputeSeparation()
    {
        Vector3 separation = Vector3.zero;
        int neighborCount = 0;

        foreach (var agent in agents)
        {
            float distance = Vector3.Distance(transform.position, agent.transform.position);
            if (agent != this && distance < FlockManager.FM.neighborRadius)
            {
                // Add separation force that increases as agents get closer
                Vector3 separationForce = transform.position - agent.transform.position;
                separationForce /= distance;  // Makes the force stronger as distance decreases
                separation += separationForce;
                neighborCount++;
            }
        }

        if (neighborCount == 0)
        {
            return separation;
        }

        return separation.normalized;
    }

    void OnDrawGizmos()
    {
        // Draw the neighborhood radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, FlockManager.FM.separationRadius);

        
    }
}

