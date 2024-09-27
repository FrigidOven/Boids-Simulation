using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BoidsProject
{
    public class BoidManager : MonoBehaviour
    {
        [SerializeField] private GameObject boidPrefab;

        [SerializeField] private float minimumBoidSpeed;
        [SerializeField] private float defaultBoidSpeed;
        [SerializeField] private float maximumBoidSpeed;

        [SerializeField][Range(0, 5000)] private int boidCount;

        [SerializeField] private Vector3 simulationSize;

        [SerializeField][Range(0.01f, 10f)] private float minBoidScale;
        [SerializeField][Range(0.01f, 10f)] private float maxBoidScale;

        [SerializeField] private float visualDistance;
        [SerializeField] private float avoidanceDistance;
        [SerializeField] private float obstacleAvoidanceDistance;

        [SerializeField][Range(0, 1)] private float adherenceFactor;
        [SerializeField][Range(0, 1)] private float avoidanceFactor;
        [SerializeField][Range(0, 1)] private float alignmentFactor;
        [SerializeField][Range(0, 1)] private float boundaryAvoidanceFactor;

        [SerializeField][Range(0, 100)] private int treeScale;
        [SerializeField] private int treeBucketCapacity;
        [SerializeField] private int treeMaxDepth;
        [SerializeField] private int treeInsertionScale;

        [SerializeField] private bool showTree;

        private Vector3 simulationCenter;

        private Octree<int> tree;
        private int currentStep;

        private List<GameObject> boids;
        private List<Rigidbody> boidBodies;
        private List<Bounds> boidBounds;

        private List<Vector3> boidPositions;
        private List<Vector3> boidVelocities;

        private float turnFactor = Mathf.PI * (Mathf.Sqrt(5f) - 1f);

        private void Start()
        {
            simulationCenter = transform.position;
            tree = new(new Bounds(simulationCenter, simulationSize * 2 * treeScale), treeBucketCapacity, treeMaxDepth);
            currentStep = 0;

            boids = new();
            boidBodies = new();
            boidBounds = new();
            boidPositions = new();
            boidVelocities = new();
        }
        private void Update()
        {
            UpdateBeforeSimulation();
            ApplyRules();
            AvoidObstacles();
            UpdateAfterSimulation();
            CreateBoids();
            DeleteBoids();
            UpdateTree();

            if (showTree)
                tree.Draw();

            currentStep = currentStep == 0 ? 1 : 0;
        }
        private void UpdateBeforeSimulation()
        {
            for (int boid = currentStep; boid < boids.Count; boid++)
            {
                boidPositions[boid] = boids[boid].transform.position;
                boidVelocities[boid] = boidBodies[boid].velocity;
            }
        }
        private void UpdateAfterSimulation()
        {
            for (int boid = currentStep; boid < boids.Count; boid+=2)
            {
                boidBodies[boid].velocity = boidVelocities[boid];
                boids[boid].transform.forward = boidBodies[boid].velocity;
            }
        }
        private void ApplyRules()
        {
            Parallel.For(0, (boids.Count / 2) + 1, (i) =>
            {
                int boid = i * 2 + currentStep;
                if (boid < boids.Count)
                {
                    List<int> otherBoids = tree.Query(new Bounds(boidPositions[boid], Vector3.one * visualDistance));
                    Vector3 acceleration1 = Adhere(boid, otherBoids);
                    Vector3 acceleration2 = Avoid(boid, otherBoids);
                    Vector3 acceleration3 = Align(boid, otherBoids);
                    Vector3 acceleration4 = AvoidBoundaries(boid);

                    boidVelocities[boid] += acceleration1 + acceleration2 + acceleration3 + acceleration4;
                    EnforceSpeedRestrictions(boid);
                }
            });
        }
        private Vector3 Adhere(int boid1, List<int> otherBoids)
        {
            Vector3 acceleration = new();
            int nearCount = 0;

            foreach (int boid2 in otherBoids)
            {
                if (boid1 == boid2)
                    continue;


                Vector3 distance = boidPositions[boid1] - boidPositions[boid2];
                if (distance.magnitude < visualDistance)
                {
                    acceleration += boidPositions[boid2];
                    nearCount++;
                }
            }
            if (nearCount > 0)
            {
                acceleration /= nearCount;
                acceleration -= boidPositions[boid1];
            }
            return acceleration * adherenceFactor;
        }
        private Vector3 Avoid(int boid1, List<int> otherBoids)
        {
            Vector3 acceleration = new();

            foreach (int boid2 in otherBoids)
            {
                if (boid1 == boid2)
                    continue;

                Vector3 distance = boidPositions[boid1] - boidPositions[boid2];
                float trueDistance = distance.magnitude;
                if (trueDistance < avoidanceDistance)
                {
                    acceleration += distance * ((avoidanceDistance - trueDistance) / avoidanceDistance);
                }
            }
            return acceleration * avoidanceFactor;
        }
        private Vector3 Align(int boid1, List<int> otherBoids)
        {
            Vector3 acceleration = new();
            int nearCount = 0;

            foreach (int boid2 in otherBoids)
            {
                if (boid1 == boid2)
                    continue;

                Vector3 distance = boidPositions[boid1] - boidPositions[boid2];
                if (distance.magnitude < visualDistance)
                {
                    acceleration += boidVelocities[boid2];
                    nearCount++;
                }
            }
            if (nearCount > 0)
            {
                acceleration /= nearCount;
                acceleration -= boidVelocities[boid1];
            }
            return acceleration * alignmentFactor;
        }
        private void AvoidObstacles()
        {
            for (int boid = 0; boid < boids.Count; boid++)
            {
                float theta;
                float phi;
                int rayCount = 100;
                bool redirect = false;

                Ray direction = new Ray(boidPositions[boid], boidVelocities[boid]);

                for (int i = 0; i < rayCount && Physics.Raycast(direction, obstacleAvoidanceDistance); i++)
                {
                    float index = i;

                    phi = Mathf.Acos(1 - 2 * index / rayCount);
                    theta = Mathf.PI * (1 + Mathf.Sqrt(5)) * index;

                    float x = Mathf.Cos(theta) * Mathf.Sin(phi);
                    float y = Mathf.Sin(theta) * Mathf.Sin(phi);
                    float z = Mathf.Cos(phi);

                    direction = new Ray(boidPositions[boid], boids[boid].transform.TransformDirection(new Vector3(x, y, z)));
                    redirect = true;
                }
                if (redirect)
                    boidVelocities[boid] = direction.direction.normalized * boidVelocities[boid].magnitude;
            }
        }
        private Ray[] GenerateRays(int boid, int rayCount)
        {
            Ray[] rays = new Ray[rayCount];
            float theta;
            float phi;
            for (int i = 0; i < rayCount;i++)
            {
                float index = i;

                phi = Mathf.Acos(1 - 2 * index / rayCount);
                theta = Mathf.PI * (1 + Mathf.Sqrt(5)) * index;

                float x = Mathf.Cos(theta) * Mathf.Sin(phi);
                float y = Mathf.Sin(theta) * Mathf.Sin(phi);
                float z = Mathf.Cos(phi);

                rays[i] = new Ray(boidPositions[boid], new Vector3(x, y, z).normalized);
            }
            return rays;
        }
        private Vector3 AvoidBoundaries(int boid)
        {
            Vector3 acceleration = new();

            if (boidPositions[boid].x < simulationCenter.x - simulationSize.x)
            {
                acceleration.x = minimumBoidSpeed;
            }
            else if (boidPositions[boid].x > simulationCenter.x + simulationSize.x)
            {
                acceleration.x = -minimumBoidSpeed;
            }

            if (boidPositions[boid].y < simulationCenter.y - simulationSize.y)
            {
                acceleration.y = minimumBoidSpeed;
            }
            else if (boidPositions[boid].y > simulationCenter.y + simulationSize.y)
            {
                acceleration.y = -minimumBoidSpeed;
            }

            if (boidPositions[boid].z < simulationCenter.z - simulationSize.z)
            {
                acceleration.z = minimumBoidSpeed;
            }
            else if (boidPositions[boid].z > simulationCenter.z + simulationSize.z)
            {
                acceleration.z = -minimumBoidSpeed;
            }

            return acceleration * boundaryAvoidanceFactor;
        }
        private void EnforceSpeedRestrictions(int boid)
        {
            if (boidVelocities[boid].magnitude < minimumBoidSpeed)
            {
                boidVelocities[boid] = boidVelocities[boid].normalized * minimumBoidSpeed;
            }
            else if (boidVelocities[boid].magnitude > maximumBoidSpeed)
            {
                boidVelocities[boid] = boidVelocities[boid].normalized * maximumBoidSpeed;
            }
        }
        private void UpdateTree()
        {
            List<int> toInsert = new();
            for (int i = currentStep; i < boids.Count; i+=2)
            {
                if (!boidBounds[i].Contains(boids[i].transform.position))
                {
                    tree.Remove(i, boidBounds[i]);
                    boidBounds[i] = new Bounds(boids[i].transform.position, boids[i].GetComponent<MeshRenderer>().bounds.size * treeInsertionScale);
                    toInsert.Add(i);
                }
            }
            tree.Update();
            for (int i = 0; i < toInsert.Count; i++)
            {
                tree.Insert(toInsert[i], boidBounds[toInsert[i]]);
            }
        }
        private void CreateBoids()
        {
            int i = boids.Count;
            while (i < boidCount)
            {
                Vector3 position = new Vector3(Random.Range(simulationCenter.x - simulationSize.x, simulationCenter.x + simulationSize.x),
                               Random.Range(simulationCenter.y - simulationSize.y, simulationCenter.y + simulationSize.y),
                               Random.Range(simulationCenter.z - simulationSize.z, simulationCenter.z + simulationSize.z));
                Vector3 direction = new Vector3(Random.Range(-360f, 360f),
                                Random.Range(-360f, 360f),
                                Random.Range(-360f, 360f)).normalized * defaultBoidSpeed;
                Vector3 scale = Vector3.one * Random.Range(minBoidScale, maxBoidScale);

                boids.Add(Instantiate(boidPrefab, position, Quaternion.identity, transform));
                boids[i].transform.localScale = scale;
                boidBodies.Add(boids[i].GetComponent<Rigidbody>());
                boidBodies[i].velocity = direction;
                boids[i].transform.forward = direction;
                boidBounds.Add(new Bounds(boids[i].transform.position, boids[i].GetComponent<MeshRenderer>().bounds.size * treeInsertionScale));
                boids[i].name = "boid " + (i + 1).ToString();
                boidPositions.Add(position);
                boidVelocities.Add(direction);

                tree.Insert(i, boidBounds[i]);
                i++;
            }
        }
        private void DeleteBoids()
        {
            while (boids.Count > boidCount)
            {
                tree.Remove(boids.Count - 1, boidBounds[boids.Count - 1]);

                GameObject boid = boids[boids.Count - 1];

                boidPositions.RemoveAt(boids.Count - 1);
                boidVelocities.RemoveAt(boids.Count - 1);

                boidBodies.RemoveAt(boids.Count - 1);
                boidBounds.RemoveAt(boids.Count - 1);

                boids.RemoveAt(boids.Count - 1);

                Destroy(boid);
            }
        }
    }
}
