using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class GenMapShader : MonoBehaviour
{
    [SerializeField] int resolution = 8;
    [SerializeField] int octaves = 3;
    [SerializeField] float isoLevel = .5f;
    [SerializeField] Vector3 mapSize = new Vector3(16, 16, 16);
    [SerializeField] bool useRand = true;
    [SerializeField] int arenaSeed = 10;

    struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Vector3 normA;
        public Vector3 normB;
        public Vector3 normC;
        public static int SizeOf => sizeof(float) * 3 * 3 * 2;
    }

    [SerializeField] MeshFilter meshFilter;
    ComputeBuffer _triBuffer;
    ComputeBuffer _triCountBuffer;
    RenderTexture _weight;

    [SerializeField] ComputeShader MarchingShader;
    [SerializeField] ComputeShader NoiseShader;

    private void Awake()
    {
        //mapSize *= resolution;
        CreateBuffers();
    }

    void CreateBuffers()
    {
        _triBuffer = new ComputeBuffer(5 * (int)mapSize.x * (int)mapSize.y * (int)mapSize.z * 4, Triangle.SizeOf, ComputeBufferType.Append);
        _triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        _weight = new RenderTexture((int)mapSize.x, (int)mapSize.y, 0)
        {
            graphicsFormat = GraphicsFormat.R32_SFloat,
            volumeDepth = (int)mapSize.z,
            enableRandomWrite = true,
            dimension = TextureDimension.Tex3D
        };
        _weight.Create();
        _weight.wrapMode = TextureWrapMode.Repeat;
		_weight.filterMode = FilterMode.Bilinear;
    }
  
    // Start is called before the first frame update
    void Start()
    {
        if(useRand) arenaSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        UnityEngine.Random.InitState(arenaSeed);
        Debug.Log(arenaSeed);
        arenaSeed = UnityEngine.Random.Range(0, 1000000);
        
        Mesh mesh = ConstructMesh();
        
        GameObject m1 = new GameObject();
        m1.AddComponent<MeshFilter>().sharedMesh = mesh;
        m1.AddComponent<MeshRenderer>().materials = gameObject.GetComponent<MeshRenderer>().materials;
        m1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        m1.transform.localScale *= resolution;
    
        GameObject m2 = new GameObject();
        m2.AddComponent<MeshFilter>().sharedMesh = mesh;
        m2.AddComponent<MeshRenderer>().materials = gameObject.GetComponent<MeshRenderer>().materials;
        m2.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        m2.transform.localScale = new Vector3(resolution, resolution, -resolution);

        m1.AddComponent<MeshCollider>().sharedMesh = mesh;
        m2.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    void ClearBuffers()
    {
        ReleaseBuffers();
        CreateBuffers();
    }

    void ReleaseBuffers()
    {
        _triBuffer.Release();
        _triCountBuffer.Release();
        _weight.Release();
    }

    int ReadTriangleCount()
    {
        int[] triCount = { 0 };
        ComputeBuffer.CopyCount(_triBuffer, _triCountBuffer, 0);
        _triCountBuffer.GetData(triCount);
        return triCount[0];
    }

    Mesh ConstructMesh()
    {
        NoiseShader.SetTexture(0, "_Weights", _weight);
        MarchingShader.SetTexture(0, "_Weights", _weight);
        NoiseShader.SetFloat("_IsoLevel", isoLevel);
        NoiseShader.SetInt("_Octaves", octaves);
        NoiseShader.SetFloat("_ChunkSizeX", mapSize.x);
        NoiseShader.SetFloat("_ChunkSizeY", mapSize.y);
        NoiseShader.SetFloat("_ChunkSizeZ", mapSize.z);
        NoiseShader.SetFloat("_Seed", arenaSeed);
        NoiseShader.Dispatch(0, (int)mapSize.x / 8, (int)mapSize.y / 8, (int)mapSize.z / 8);
        
        MarchingShader.SetBuffer(0, "_Triangles", _triBuffer);
        MarchingShader.SetFloat("_ChunkSizeX", mapSize.x);
        MarchingShader.SetFloat("_ChunkSizeY", mapSize.y);
        MarchingShader.SetFloat("_ChunkSizeZ", mapSize.z);
        MarchingShader.SetFloat("_IsoLevel", isoLevel);

        _triBuffer.SetCounterValue(0);

        MarchingShader.Dispatch(0, (int)mapSize.x / 8, (int)mapSize.y / 8, (int)mapSize.z);

        Triangle[] triangles = new Triangle[ReadTriangleCount()];
        _triBuffer.GetData(triangles);

        //SaveRT3DToTexture3DAsset(_weight, "Debug/TestFile");
        return CreateMeshFromTriangles(triangles);
    }

    Mesh CreateMeshFromTriangles(Triangle[] triangles)
    {
        Vector3[] verts = new Vector3[triangles.Length * 3];
        int[] tris = new int[triangles.Length * 3];
        Vector3[] normals = new Vector3[triangles.Length * 3];

        for (int i = 0; i < triangles.Length; i++)
        {
            int startIndex = i * 3; 
            verts[startIndex] = triangles[i].c;
            verts[startIndex + 1] = triangles[i].b;
            verts[startIndex + 2] = triangles[i].a;

            tris[startIndex] = startIndex;
            tris[startIndex + 1] = startIndex + 1;
            tris[startIndex + 2] = startIndex + 2;

            normals[startIndex] = triangles[i].normC;
            normals[startIndex + 1] = triangles[i].normB;
            normals[startIndex + 2] = triangles[i].normA;
        }

        Debug.Log(verts.Length);

        Mesh mesh = new Mesh
        {
            indexFormat = IndexFormat.UInt32
        };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0, true);
        return mesh;
    }    
}
