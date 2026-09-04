using UnityEngine;

public class GridNode
{
    public int x;
    public int y;

    public int cost;

    public GridNode(int x, int y)
    {
        this.x = x;
        this.y = y;
    }
}

public class AIMove : MonoBehaviour
{
    public float nodeSize = 1f;
    public Vector2Int size;
    private GridNode[,] nodes;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nodes = new GridNode[size.x, size.y];
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                nodes[x, y] = new GridNode(x, y);
                nodes[x, y].cost = IsWalkable(new Vector3(x * nodeSize, 0, y * nodeSize)) ? 1 : int.MaxValue;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log(WorldToNode(transform.position).x + " " + WorldToNode(transform.position).y);
    }

    public GridNode WorldToNode(Vector3 world)
    {
        Vector3 local = world;
        int x = Mathf.FloorToInt(local.x / nodeSize);
        int y = Mathf.FloorToInt(local.z / nodeSize);

        x = Mathf.Clamp(x, 0, size.x - 1);
        y = Mathf.Clamp(y, 0, size.y - 1);
        return nodes[x, y];
    }

    [SerializeField] LayerMask obstacleMask;
    private float obstacleHeight;

    bool IsWalkable(Vector3 center)
    {
        Vector3 halfExtents = new(
            nodeSize * .45f,
            obstacleHeight * .5f,
            nodeSize * .45f);

        return !Physics.CheckBox(
            center, halfExtents,
            Quaternion.identity,
            obstacleMask);
    }


}