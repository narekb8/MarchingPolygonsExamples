# MarchingPolygonsExamples

A small collection of my previous and newer work-in-progress Marching Cubes and Marching Polygons algorithms. The original compute shaders were written for a final game I'm working on, hence why the data and test script is based on a noise algorithm. Will soon implement a script to render some of the examples from the Chapel Hill Volume Rendering Test Data Set.

### CURRENTLY DONE
- Marching Cubes - GPU Compute Shader (smooth and flat shaded normals in GPU)
- Marching Tetrahedra - GPU Compute Shader (smooth and flat shaded normals in GPU)

### CURRENT GOAL
- Implement Volume Rendering Test Data Set scripts for better examples
- Convert GPU compute shaders to Unity's LLVM Burst compiler for a CPU-only version (nice for server-sided generation to generate colliders where we don't have access to a graphics card).
