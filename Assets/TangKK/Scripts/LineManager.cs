using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 连接定义 - 用于在Inspector中定义节点连接
/// </summary>
[System.Serializable]
public class ConnectionDefinition
{
    [Tooltip("起始节点索引")]
    public int startNodeIndex;
    [Tooltip("结束节点索引")]
    public int endNodeIndex;
    
    public ConnectionDefinition(int start, int end)
    {
        startNodeIndex = start;
        endNodeIndex = end;
    }
}

/// <summary>
/// 线条管理器 - 管理节点和它们之间的连接
/// </summary>
public class LineManager : MonoBehaviour
{
    [Header("节点设置")]
    [Tooltip("所有路径节点")]
    public Transform[] points;
    
    [Header("连接设置")]
    [Tooltip("连接方式")]
    public ConnectionMode connectionMode = ConnectionMode.Manual;
    
    [Tooltip("手动定义的连接")]
    public List<ConnectionDefinition> manualConnections = new List<ConnectionDefinition>();
    
    [Tooltip("自动连接的最大距离")]
    public float autoConnectionDistance = 2f;
    
    [Header("组件引用")]
    public LineController lineController;
    
    [Header("调试工具")]
    [SerializeField] private bool showNodeIndices = true;
    [SerializeField] private bool showConnectionGizmos = true;
    
    public enum ConnectionMode
    {
        Manual,        // 手动定义连接
        AutoDistance,  // 基于距离自动连接
        Grid,          // 网格连接模式
        Custom         // 自定义连接矩阵
    }
    
    private void Start()
    {
        SetupConnections();
    }
    
    /// <summary>
    /// 设置节点连接
    /// </summary>
    public void SetupConnections()
    {
        if (lineController == null)
        {
            Debug.LogError("LineController 未分配！");
            return;
        }
        
        if (points == null || points.Length == 0)
        {
            Debug.LogError("没有定义节点！");
            return;
        }
        
        switch (connectionMode)
        {
            case ConnectionMode.Manual:
                SetupManualConnections();
                break;
            case ConnectionMode.AutoDistance:
                SetupAutoDistanceConnections();
                break;
            case ConnectionMode.Grid:
                SetupGridConnections();
                break;
            case ConnectionMode.Custom:
                SetupCustomConnections();
                break;
        }
    }
    
    /// <summary>
    /// 设置手动定义的连接
    /// </summary>
    private void SetupManualConnections()
    {
        lineController.ClearAllConnections();
        
        foreach (ConnectionDefinition connection in manualConnections)
        {
            if (IsValidConnection(connection))
            {
                lineController.AddConnection(
                    points[connection.startNodeIndex], 
                    points[connection.endNodeIndex]
                );
            }
        }
        
        Debug.Log($"创建了 {manualConnections.Count} 个手动连接");
    }
    
    /// <summary>
    /// 基于距离自动创建连接
    /// </summary>
    private void SetupAutoDistanceConnections()
    {
        lineController.SetUpNodeConnectionsByDistance(points, autoConnectionDistance);
        Debug.Log($"基于距离 {autoConnectionDistance} 创建了 {lineController.GetConnectionCount()} 个连接");
    }
    
    /// <summary>
    /// 设置网格连接（假设节点按网格排列）
    /// </summary>
    private void SetupGridConnections()
    {
        lineController.ClearAllConnections();
        
        // 假设节点按正方形网格排列
        int gridSize = Mathf.RoundToInt(Mathf.Sqrt(points.Length));
        
        for (int i = 0; i < points.Length; i++)
        {
            int x = i % gridSize;
            int y = i / gridSize;
            
            // 连接右边的节点
            if (x < gridSize - 1)
            {
                int rightIndex = y * gridSize + (x + 1);
                if (rightIndex < points.Length)
                {
                    lineController.AddConnection(points[i], points[rightIndex]);
                }
            }
            
            // 连接下面的节点
            if (y < gridSize - 1)
            {
                int downIndex = (y + 1) * gridSize + x;
                if (downIndex < points.Length)
                {
                    lineController.AddConnection(points[i], points[downIndex]);
                }
            }
        }
        
        Debug.Log($"创建了网格连接，网格大小: {gridSize}x{gridSize}");
    }
    
    /// <summary>
    /// 自定义连接矩阵
    /// </summary>
    private void SetupCustomConnections()
    {
        // 创建一个示例连接矩阵
        int nodeCount = points.Length;
        int[,] connectionMatrix = new int[nodeCount, nodeCount];
        
        // 这里可以根据需要自定义连接规则
        // 例如：创建一个星形连接（所有节点连接到中心节点）
        int centerIndex = 0; // 假设第一个节点是中心
        for (int i = 1; i < nodeCount; i++)
        {
            connectionMatrix[centerIndex, i] = 1;
        }
        
        lineController.SetUpNodeConnections(points, connectionMatrix);
        Debug.Log("创建了自定义连接");
    }
    
    /// <summary>
    /// 验证连接是否有效
    /// </summary>
    private bool IsValidConnection(ConnectionDefinition connection)
    {
        return connection.startNodeIndex >= 0 && 
               connection.startNodeIndex < points.Length &&
               connection.endNodeIndex >= 0 && 
               connection.endNodeIndex < points.Length &&
               connection.startNodeIndex != connection.endNodeIndex;
    }
    
    /// <summary>
    /// 添加新连接
    /// </summary>
    public void AddConnection(int startIndex, int endIndex)
    {
        if (startIndex >= 0 && startIndex < points.Length &&
            endIndex >= 0 && endIndex < points.Length &&
            startIndex != endIndex)
        {
            ConnectionDefinition newConnection = new ConnectionDefinition(startIndex, endIndex);
            manualConnections.Add(newConnection);
            
            if (lineController != null)
            {
                lineController.AddConnection(points[startIndex], points[endIndex]);
            }
        }
    }
    
    /// <summary>
    /// 移除连接
    /// </summary>
    public void RemoveConnection(int startIndex, int endIndex)
    {
        manualConnections.RemoveAll(c => 
            (c.startNodeIndex == startIndex && c.endNodeIndex == endIndex) ||
            (c.startNodeIndex == endIndex && c.endNodeIndex == startIndex));
        
        if (lineController != null && startIndex < points.Length && endIndex < points.Length)
        {
            lineController.RemoveConnection(points[startIndex], points[endIndex]);
        }
    }
    
    /// <summary>
    /// 快速创建一些常用的连接模式
    /// </summary>
    [ContextMenu("创建线性连接")]
    public void CreateLinearConnections()
    {
        manualConnections.Clear();
        for (int i = 0; i < points.Length - 1; i++)
        {
            manualConnections.Add(new ConnectionDefinition(i, i + 1));
        }
        SetupConnections();
    }
    
    [ContextMenu("创建环形连接")]
    public void CreateCircularConnections()
    {
        manualConnections.Clear();
        for (int i = 0; i < points.Length; i++)
        {
            int nextIndex = (i + 1) % points.Length;
            manualConnections.Add(new ConnectionDefinition(i, nextIndex));
        }
        SetupConnections();
    }
    
    [ContextMenu("创建星形连接")]
    public void CreateStarConnections()
    {
        manualConnections.Clear();
        if (points.Length > 1)
        {
            for (int i = 1; i < points.Length; i++)
            {
                manualConnections.Add(new ConnectionDefinition(0, i));
            }
        }
        SetupConnections();
    }
    
    [ContextMenu("创建完全连接")]
    public void CreateCompleteConnections()
    {
        manualConnections.Clear();
        for (int i = 0; i < points.Length; i++)
        {
            for (int j = i + 1; j < points.Length; j++)
            {
                manualConnections.Add(new ConnectionDefinition(i, j));
            }
        }
        SetupConnections();
    }
    
    /// <summary>
    /// 清除所有连接
    /// </summary>
    [ContextMenu("清除所有连接")]
    public void ClearAllConnections()
    {
        manualConnections.Clear();
        if (lineController != null)
        {
            lineController.ClearAllConnections();
        }
    }
    
    /// <summary>
    /// 可视化调试
    /// </summary>
    private void OnDrawGizmos()
    {
        if (points == null) return;
        
        // 显示节点索引
        if (showNodeIndices)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(points[i].position + Vector3.up * 0.5f, i.ToString());
#endif
                }
            }
        }
        
        // 显示连接预览
        if (showConnectionGizmos && connectionMode == ConnectionMode.Manual)
        {
            Gizmos.color = Color.cyan;
            foreach (ConnectionDefinition connection in manualConnections)
            {
                if (IsValidConnection(connection))
                {
                    Gizmos.DrawLine(
                        points[connection.startNodeIndex].position,
                        points[connection.endNodeIndex].position
                    );
                }
            }
        }
    }
    
    /// <summary>
    /// 获取节点的所有连接
    /// </summary>
    public List<int> GetConnectedNodeIndices(int nodeIndex)
    {
        List<int> connections = new List<int>();
        
        foreach (ConnectionDefinition connection in manualConnections)
        {
            if (connection.startNodeIndex == nodeIndex)
            {
                connections.Add(connection.endNodeIndex);
            }
            else if (connection.endNodeIndex == nodeIndex)
            {
                connections.Add(connection.startNodeIndex);
            }
        }
        
        return connections;
    }
}