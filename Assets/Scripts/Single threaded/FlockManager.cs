using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlockManager : MonoBehaviour {

    public static FlockManager FM;
    public GameObject fishPrefab;
    public int numFish = 20;
    public GameObject[] allFish;
    public Vector3 swimLimits = new Vector3(5.0f, 5.0f, 5.0f);

    [Header("Fish Settings")]
    public float neighborRadius;
    public float separationRadius;
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;
    public float separationWeight = 1f;
    public float speed = 5f;  // Maximum speed of an agent
    public float rotationSpeed = 5f;

    public float smoothFactor;

    void Start() {

        allFish = new GameObject[numFish];

        for (int i = 0; i < numFish; ++i) {

            Vector3 pos = this.transform.position + new Vector3(
                Random.Range(-swimLimits.x, swimLimits.x),
                Random.Range(-swimLimits.y, swimLimits.y),
                Random.Range(-swimLimits.z, swimLimits.z));

            allFish[i] = Instantiate(fishPrefab, pos, Quaternion.identity);
        }

        FM = this;
    }


  
}