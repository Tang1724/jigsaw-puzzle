using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 节点连接信息
/// </summary>
[System.Serializable]
public class NodeConnection
{
    public Transform startNode;
    public Transform endNode;
    public LineRenderer lineRenderer;
    
    public NodeConnection(Transform start, Transform end)
    {
        startNode = start;
        endNode = end;
    }
}

/// <summary>
/// 线条控制器 - 管理节点之间的独立连接
/// </summary>
public class LineController : MonoBehaviour
{
    [Header("线条设置")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField] private bool useWorldSpace = true;
    
    [Header("连接管理")]
    [SerializeField] private List<NodeConnection> connections = new List<NodeConnection>();
    
    // 用于存储所有线条渲染器的容器
    private Transform lineContainer;
    
    private void Awake()
    {
        InitializeLineContainer();
    }
    
    /// <summary>
    /// 初始化线条容器
    /// </summary>
    private void InitializeLineContainer()
    {
        GameObject container = new GameObject("LineConnections");
        container.transform.SetParent(transform);
        lineContainer = container.transform;
    }
    
    /// <summary>
    /// 设置节点连接（替代原来的SetUpLine方法）
    /// </summary>
    public void SetUpNodeConnections(Transform[] nodes, int[,] connectionMatrix)
    {
        ClearAllConnections();
        
        // 根据连接矩阵创建连接
        for (int i = 0; i < nodes.Length; i++)
        {
            for (int j = i + 1; j < nodes.Length; j++) // j = i + 1 避免重复连接
            {
                if (connectionMatrix[i, j] == 1) // 1表示连接，0表示不连接
                {
                    CreateConnection(nodes[i], nodes[j]);
                }
            }
        }
    }
    
    /// <summary>
    /// 简化版本：连接所有相邻节点（基于距离）
    /// </summary>
    public void SetUpNodeConnectionsByDistance(Transform[] nodes, float maxConnectionDistance = 2f)
    {
        ClearAllConnections();
        
        for (int i = 0; i < nodes.Length; i++)
        {
            for (int j = i + 1; j < nodes.Length; j++)
            {
                float distance = Vector3.Distance(nodes[i].position, nodes[j].position);
                if (distance <= maxConnectionDistance)
                {
                    CreateConnection(nodes[i], nodes[j]);
                }
            }
        }
    }
    
    /// <summary>
    /// 手动添加特定连接
    /// </summary>
    public void AddConnection(Transform startNode, Transform endNode)
    {
        // 检查连接是否已存在
        if (ConnectionExists(startNode, endNode))
        {
            Debug.LogWarning($"连接已存在: {startNode.name} <-> {endNode.name}");
            return;
        }
        
        CreateConnection(startNode, endNode);
    }
    
    /// <summary>
    /// 移除特定连接
    /// </summary>
    public void RemoveConnection(Transform startNode, Transform endNode)
    {
        NodeConnection connectionToRemove = null;
        
        foreach (NodeConnection connection in connections)
        {
            if ((connection.startNode == startNode && connection.endNode == endNode) ||
                (connection.startNode == endNode && connection.endNode == startNode))
            {
                connectionToRemove = connection;
                break;
            }
        }
        
        if (connectionToRemove != null)
        {
            DestroyConnection(connectionToRemove);
        }
    }
    
    /// <summary>
    /// 创建两个节点之间的连接
    /// </summary>
    private void CreateConnection(Transform startNode, Transform endNode)
    {
        // 创建新的GameObject来承载LineRenderer
        GameObject lineObj = new GameObject($"Line_{startNode.name}_to_{endNode.name}");
        lineObj.transform.SetParent(lineContainer);
        
        // 添加LineRenderer组件
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        
        // 配置LineRenderer
        ConfigureLineRenderer(lr);
        
        // 创建连接对象
        NodeConnection newConnection = new NodeConnection(startNode, endNode)
        {
            lineRenderer = lr
        };
        
        connections.Add(newConnection);
        
        // 立即更新线条位置
        UpdateConnectionPosition(newConnection);
    }
    
    /// <summary>
    /// 配置LineRenderer的属性（使用Gradient方式）
    /// </summary>
    private void ConfigureLineRenderer(LineRenderer lr)
    {
        lr.material = lineMaterial;
        
        // 🔥 更好的解决方案：使用Gradient设置颜色
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(lineColor, 0.0f), new GradientColorKey(lineColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(lineColor.a, 0.0f), new GradientAlphaKey(lineColor.a, 1.0f) }
        );
        lr.colorGradient = gradient;
        
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;
        lr.useWorldSpace = useWorldSpace;
        
        // 设置其他渲染属性
        lr.sortingLayerName = "Default";
        lr.sortingOrder = 0;
    }
    
    /// <summary>
    /// 更新单个连接的位置
    /// </summary>
    private void UpdateConnectionPosition(NodeConnection connection)
    {
        if (connection.lineRenderer != null && 
            connection.startNode != null && 
            connection.endNode != null)
        {
            connection.lineRenderer.SetPosition(0, connection.startNode.position);
            connection.lineRenderer.SetPosition(1, connection.endNode.position);
        }
    }
    
    /// <summary>
    /// 检查连接是否已存在
    /// </summary>
    private bool ConnectionExists(Transform startNode, Transform endNode)
    {
        foreach (NodeConnection connection in connections)
        {
            if ((connection.startNode == startNode && connection.endNode == endNode) ||
                (connection.startNode == endNode && connection.endNode == startNode))
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 销毁连接
    /// </summary>
    private void DestroyConnection(NodeConnection connection)
    {
        if (connection.lineRenderer != null)
        {
            DestroyImmediate(connection.lineRenderer.gameObject);
        }
        connections.Remove(connection);
    }
    
    /// <summary>
    /// 清除所有连接
    /// </summary>
    public void ClearAllConnections()
    {
        for (int i = connections.Count - 1; i >= 0; i--)
        {
            DestroyConnection(connections[i]);
        }
        connections.Clear();
    }
    
    /// <summary>
    /// 更新所有连接位置（在Update中调用）
    /// </summary>
    private void Update()
    {
        // 更新所有连接的位置
        foreach (NodeConnection connection in connections)
        {
            UpdateConnectionPosition(connection);
        }
    }
    
    /// <summary>
    /// 设置线条颜色（使用Gradient方式）
    /// </summary>
    public void SetLineColor(Color color)
    {
        lineColor = color;
        foreach (NodeConnection connection in connections)
        {
            if (connection.lineRenderer != null)
            {
                // 🔥 使用Gradient设置颜色（推荐方式）
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0.0f), new GradientAlphaKey(color.a, 1.0f) }
                );
                connection.lineRenderer.colorGradient = gradient;
            }
        }
    }
    
    /// <summary>
    /// 设置线条宽度
    /// </summary>
    public void SetLineWidth(float width)
    {
        lineWidth = width;
        foreach (NodeConnection connection in connections)
        {
            if (connection.lineRenderer != null)
            {
                connection.lineRenderer.startWidth = width;
                connection.lineRenderer.endWidth = width;
            }
        }
    }
    
    /// <summary>
    /// 获取当前连接数量
    /// </summary>
    public int GetConnectionCount()
    {
        return connections.Count;
    }
    
    /// <summary>
    /// 获取指定节点的所有连接
    /// </summary>
    public List<Transform> GetConnectedNodes(Transform node)
    {
        List<Transform> connectedNodes = new List<Transform>();
        
        foreach (NodeConnection connection in connections)
        {
            if (connection.startNode == node)
            {
                connectedNodes.Add(connection.endNode);
            }
            else if (connection.endNode == node)
            {
                connectedNodes.Add(connection.startNode);
            }
        }
        
        return connectedNodes;
    }
    
    /// <summary>
    /// 调试：在Scene视图中显示连接信息
    /// </summary>
    private void OnDrawGizmos()
    {
        if (connections == null) return;
        
        Gizmos.color = Color.yellow;
        foreach (NodeConnection connection in connections)
        {
            if (connection.startNode != null && connection.endNode != null)
            {
                Vector3 midPoint = (connection.startNode.position + connection.endNode.position) * 0.5f;
                Gizmos.DrawWireSphere(midPoint, 0.1f);
            }
        }
    }
}