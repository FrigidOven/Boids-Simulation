# Boids Simulation
 
A boids simulation accelerated with the use of a loose octree for spatial partitioning.
Contains tunable parameters that effect the boids behaviors:

# Results
![image](https://github.com/user-attachments/assets/08ec2226-80bf-4a5a-9b20-05fedc68e48d)
![image](https://github.com/user-attachments/assets/d974fb55-f983-4d1d-a4ca-7a0be06dd844)

AvoidanceDistance: the distance at which boids will begin to avoid eachother.
VisualDistance: the distance at which boids can recognize other boids.
ObstacleAvoidanceDistance: the distance at which boids will begin to avoid obstacles in their line of sight.

AdherenceFactor: the strength at which boids will try to fly towards the center of nearby boids.
AlignmentFactor: the strength at which boids will try to align with nearby boids.
AvoidanceFactor: the strength at which boids will try to avoid other boids.
BoundaryAvoidanceFactor: the strength at which boids will be steered back into bounds if they leave their bounding box.

As mentioned, a loose octree (and helping freelist class) were also created for this project.
The tree is allows the simulation to run much faster than the naive solution, allowing 1,000 boids to be simulated at
roughly 80 fps on my machine.

Errors may arise if you change the bounds of the simulation without also changing the
physical bounding objects size.
