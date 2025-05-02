using System.Collections;
using System.Threading.Tasks;
using TMPro;
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
    [SerializeField] public bool useSmoothNormals = true;
    [SerializeField] public bool useCubes = true;
    [SerializeField] TMP_Text normalsText;
    [SerializeField] TMP_Text algoText;
    [SerializeField] TMP_Text vertsText;
    [SerializeField] TMP_Text trisText;
    private float _Dissolve = 2f;
    private GameObject m1;
    private GameObject m2;

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
    [SerializeField] ComputeShader CubesShader;
    [SerializeField] ComputeShader TetraShader;
    [SerializeField] ComputeShader NoiseShader;

    async Awaitable Awake()
    {
        //mapSize *= resolution;
        MarchingShader = useCubes ? CubesShader : TetraShader;
        if(useRand) arenaSeed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(arenaSeed);

        CreateBuffers();
        await GenerateFirstMesh();
    }

    public void UpdateNormals(bool smooth)
    {
        useSmoothNormals = smooth;

    }

    public void UpdateAlgo(float val)
    {
        if(val > 0)
        {
            useCubes = false;
            algoText.text = "Tetrahedra";
        }
        else
        {
            useCubes = true;
            algoText.text = "Cubes";
        }
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
    async Awaitable GenerateFirstMesh()
    {
        arenaSeed = Random.Range(0, 1000000);
        
        Mesh mesh = await ConstructMesh();

        //while(_Dissolve < mapSize.y * resolution)
            //await Awaitable.NextFrameAsync();
        
        m1 = new GameObject();
        m1.AddComponent<MeshFilter>().sharedMesh = mesh;
        m1.AddComponent<MeshRenderer>().materials = gameObject.GetComponent<MeshRenderer>().materials;
        m1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        m1.transform.localScale *= resolution;
    
        m2 = new GameObject();
        m2.AddComponent<MeshFilter>().sharedMesh = mesh;
        m2.AddComponent<MeshRenderer>().materials = gameObject.GetComponent<MeshRenderer>().materials;
        m2.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        m2.transform.localScale = new Vector3(resolution, resolution, -resolution);

        m1.AddComponent<MeshCollider>().sharedMesh = mesh;
        m2.AddComponent<MeshCollider>().sharedMesh = mesh;

        StopAllCoroutines();
        StartCoroutine(DissolveMesh(false));
    }

    async Awaitable GenerateNewMesh()
    {
        ClearBuffers();
        CreateBuffers();

        arenaSeed = Random.Range(0, 1000000);
        
        Mesh mesh = await ConstructMesh();

        while(_Dissolve < mapSize.y * resolution)
            await Awaitable.NextFrameAsync();

        Destroy(m1);
        Destroy(m2);
        
        m1 = new GameObject();
        m1.AddComponent<MeshFilter>().sharedMesh = mesh;
        m1.AddComponent<MeshRenderer>().materials = gameObject.GetComponent<MeshRenderer>().materials;
        m1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        m1.transform.localScale *= resolution;
    
        m2 = new GameObject();
        m2.AddComponent<MeshFilter>().sharedMesh = mesh;
        m2.AddComponent<MeshRenderer>().materials = gameObject.GetComponent<MeshRenderer>().materials;
        m2.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        m2.transform.localScale = new Vector3(resolution, resolution, -resolution);

        m1.AddComponent<MeshCollider>().sharedMesh = mesh;
        m2.AddComponent<MeshCollider>().sharedMesh = mesh;

        StopAllCoroutines();
        StartCoroutine(DissolveMesh(false));
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

    async Awaitable<Mesh> ConstructMesh()
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
	    MarchingShader.SetBool("_SmoothNormals", useSmoothNormals);
        MarchingShader.SetFloat("_ChunkSizeX", mapSize.x);
        MarchingShader.SetFloat("_ChunkSizeY", mapSize.y);
        MarchingShader.SetFloat("_ChunkSizeZ", mapSize.z);
        MarchingShader.SetFloat("_IsoLevel", isoLevel);

        _triBuffer.SetCounterValue(0);

        MarchingShader.Dispatch(0, (int)mapSize.x / 8, (int)mapSize.y / 8, (int)mapSize.z);

        AsyncGPUReadbackRequest readback = AsyncGPUReadback.Request(_triBuffer, _triBuffer.count, 0);

        while(!readback.done)
            await Awaitable.NextFrameAsync();

        Triangle[] triangles = new Triangle[ReadTriangleCount()];
        _triBuffer.GetData(triangles);

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

        vertsText.text = "Vertex Count: " + verts.Length;
        trisText.text = "Tri Count: " + triangles.Length;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0, true);
        return mesh;
    }

    public async void NewMesh()
    {
        MarchingShader = useCubes ? CubesShader : TetraShader;
        StopAllCoroutines();
        StartCoroutine(DissolveMesh(true));

        await GenerateNewMesh();
    }

    public IEnumerator DissolveMesh(bool dissolve)
    {
        MeshRenderer arenaMat = gameObject.GetComponent<MeshRenderer>();
        if(dissolve)
        {
            while(_Dissolve < mapSize.y * resolution)
            {
                _Dissolve += mapSize.y / 3f * Time.deltaTime * resolution;
                arenaMat.sharedMaterial.SetFloat("_DissolveAmount", _Dissolve);
                yield return null;
            }
        }
        else
        {
            while(_Dissolve > -0.8f)
            {
                _Dissolve -= mapSize.y / 3f * Time.deltaTime * resolution;
                arenaMat.sharedMaterial.SetFloat("_DissolveAmount", _Dissolve);
                yield return null;
            }
        }
    }
}
