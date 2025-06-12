using System.Collections.Generic;
using UnityEngine;

// 节点定义
[System.Serializable]
public class NetworkNode
{
    public string nodeName = ""; // 节点名称，如"A", "B", "C"等
    public Vector2 position;
    public List<NodeConnection> connections = new List<NodeConnection>(); // 连接到其他节点
    public Color nodeColor = Color.green;
}

// 节点连接定义
[System.Serializable]
public class NodeConnection
{
    public string targetNodeName; // 目标节点名称
    public PathType pathType = PathType.Auto; // 路径类型
    public List<Vector2> waypoints = new List<Vector2>(); // 路径上的中间点（用于弯曲路径）
    
    // 获取完整路径（包括起点、中间点、终点）
    public List<Vector2> GetFullPath(Vector2 startPos, Vector2 endPos)
    {
        List<Vector2> fullPath = new List<Vector2>();
        fullPath.Add(startPos);
        fullPath.AddRange(waypoints);
        fullPath.Add(endPos);
        return fullPath;
    }
    
    // 获取路径总长度
    public float GetPathLength(Vector2 startPos, Vector2 endPos)
    {
        List<Vector2> path = GetFullPath(startPos, endPos);
        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector2.Distance(path[i], path[i + 1]);
        }
        return length;
    }
    
    // 获取路径上指定比例的位置
    public Vector2 GetPositionAtRatio(float ratio, Vector2 startPos, Vector2 endPos)
    {
        List<Vector2> path = GetFullPath(startPos, endPos);
        if (path.Count < 2) return startPos;
        
        ratio = Mathf.Clamp01(ratio);
        float totalLength = GetPathLength(startPos, endPos);
        float targetDistance = totalLength * ratio;
        float currentDistance = 0f;
        
        for (int i = 0; i < path.Count - 1; i++)
        {
            float segmentLength = Vector2.Distance(path[i], path[i + 1]);
            
            if (currentDistance + segmentLength >= targetDistance)
            {
                float segmentRatio = (targetDistance - currentDistance) / segmentLength;
                return Vector2.Lerp(path[i], path[i + 1], segmentRatio);
            }
            
            currentDistance += segmentLength;
        }
        
        return endPos;
    }
}

public enum PathType
{
    Auto,       // 自动判断方向
    Horizontal, // 水平路径，AD控制
    Vertical,   // 垂直路径，WS控制
    Free        // 自由路径，WASD都可以
}

public class FlexibleNodeNetwork : MonoBehaviour
{
    [Header("角色设置")]
    public float moveSpeed = 5f;
    public Transform playerTransform;
    
    [Header("节点网络")]
    public List<NetworkNode> nodes = new List<NetworkNode>();
    public string startingNodeName = "A"; // 起始节点名称
    
    [Header("显示设置")]
    public bool showNodeNames = true;
    public bool showConnections = true;
    public Color connectionLineColor = Color.blue;
    public Color currentPathColor = Color.yellow;
    public float nodeRadius = 0.3f;
    
    // 当前状态
    private string currentNodeName = "";
    private string targetNodeName = "";
    private NodeConnection currentConnection = null;
    private float pathProgress = 0f; // 0-1，在当前路径上的进度
    private bool isMoving = false;
    
    // 节点查找字典（优化性能）
    private Dictionary<string, NetworkNode> nodeDict = new Dictionary<string, NetworkNode>();
    
    void Start()
    {
        BuildNodeDictionary();
        
        if (!string.IsNullOrEmpty(startingNodeName) && nodeDict.ContainsKey(startingNodeName))
        {
            SetPlayerToNode(startingNodeName);
        }
    }
    
    void Update()
    {
        HandleInput();
        UpdatePlayerPosition();
    }
    
    void BuildNodeDictionary()
    {
        nodeDict.Clear();
        foreach (var node in nodes)
        {
            if (!string.IsNullOrEmpty(node.nodeName))
            {
                nodeDict[node.nodeName] = node;
            }
        }
    }
    
    void HandleInput()
    {
        Vector2 inputDir = Vector2.zero;
        
        // 获取持续输入方向（不是GetKeyDown，而是GetKey）
        if (Input.GetKey(KeyCode.W)) inputDir += Vector2.up;
        if (Input.GetKey(KeyCode.S)) inputDir += Vector2.down;
        if (Input.GetKey(KeyCode.A)) inputDir += Vector2.left;
        if (Input.GetKey(KeyCode.D)) inputDir += Vector2.right;
        
        // 标准化输入方向
        inputDir = inputDir.normalized;
        
        if (inputDir != Vector2.zero)
        {
            if (!isMoving)
            {
                // 在节点上，尝试开始移动
                TryStartMovement(inputDir);
            }
            else
            {
                // 已在移动中，继续移动
                HandlePathMovement(inputDir);
            }
        }
    }
    
    void TryStartMovement(Vector2 inputDirection)
    {
        if (string.IsNullOrEmpty(currentNodeName) || !nodeDict.ContainsKey(currentNodeName))
            return;
            
        NetworkNode currentNode = nodeDict[currentNodeName];
        
        // 查找最匹配输入方向的连接
        NodeConnection bestConnection = null;
        float bestAlignment = -1f;
        string bestTargetNode = "";
        
        foreach (var connection in currentNode.connections)
        {
            if (!nodeDict.ContainsKey(connection.targetNodeName))
                continue;
                
            NetworkNode targetNode = nodeDict[connection.targetNodeName];
            
            // 计算从当前节点到目标节点的实际方向（不考虑路径设置）
            Vector2 directionToTarget = (targetNode.position - currentNode.position).normalized;
            
            // 如果有中间路径点，使用第一个路径点的方向
            if (connection.waypoints.Count > 0)
            {
                directionToTarget = (connection.waypoints[0] - currentNode.position).normalized;
            }
            
            // 计算方向匹配度 - 这里是关键改进
            float alignment = Vector2.Dot(inputDirection, directionToTarget);
            
            if (alignment > bestAlignment && alignment > 0.3f) // 阈值防止意外触发
            {
                bestAlignment = alignment;
                bestConnection = connection;
                bestTargetNode = connection.targetNodeName;
            }
        }
        
        // 开始移动到最匹配的目标节点
        if (bestConnection != null)
        {
            StartMovementToNode(bestTargetNode, bestConnection);
        }
        else
        {
            Debug.Log($"节点 {currentNodeName} 在该方向没有连接");
        }
    }
    
    void StartMovementToNode(string targetNode, NodeConnection connection)
    {
        targetNodeName = targetNode;
        currentConnection = connection;
        pathProgress = 0f;
        isMoving = true;
        
        Debug.Log($"开始从节点 {currentNodeName} 移动到节点 {targetNodeName}");
    }
    
    void HandlePathMovement(Vector2 inputDirection)
    {
        if (currentConnection == null) return;
        
        // 预先获取节点，避免重复声明
        NetworkNode startNode = nodeDict[currentNodeName];
        NetworkNode endNode = nodeDict[targetNodeName];
        
        // 根据路径类型检查输入是否有效
        bool canMove = false;
        float moveDirection = 1f; // 默认向前移动
        
        switch (currentConnection.pathType)
        {
            case PathType.Auto:
                // 自动判断主要方向 - 改进：始终以实际路径方向为准
                Vector2 actualPathDirection = (endNode.position - startNode.position).normalized;
                
                if (Mathf.Abs(actualPathDirection.x) > Mathf.Abs(actualPathDirection.y))
                {
                    // 主要是水平方向
                    if (inputDirection.x != 0f)
                    {
                        canMove = true;
                        // 关键改进：移动方向基于输入与实际路径方向的关系
                        moveDirection = Vector2.Dot(inputDirection, actualPathDirection) > 0 ? 1f : -1f;
                    }
                }
                else
                {
                    // 主要是垂直方向
                    if (inputDirection.y != 0f)
                    {
                        canMove = true;
                        // 关键改进：移动方向基于输入与实际路径方向的关系
                        moveDirection = Vector2.Dot(inputDirection, actualPathDirection) > 0 ? 1f : -1f;
                    }
                }
                break;
                
            case PathType.Horizontal:
                if (inputDirection.x != 0f)
                {
                    canMove = true;
                    // 改进：考虑实际路径方向
                    Vector2 pathDir = (endNode.position - startNode.position).normalized;
                    moveDirection = Vector2.Dot(inputDirection, pathDir) > 0 ? 1f : -1f;
                }
                break;
                
            case PathType.Vertical:
                if (inputDirection.y != 0f)
                {
                    canMove = true;
                    // 改进：考虑实际路径方向
                    Vector2 pathDir = (endNode.position - startNode.position).normalized;
                    moveDirection = Vector2.Dot(inputDirection, pathDir) > 0 ? 1f : -1f;
                }
                break;
                
            case PathType.Free:
                // 自由移动，任何方向都可以
                if (inputDirection != Vector2.zero)
                {
                    canMove = true;
                    // 计算输入方向与路径方向的匹配度
                    Vector2 pathDirection = (endNode.position - startNode.position).normalized;
                    moveDirection = Vector2.Dot(inputDirection, pathDirection) > 0 ? 1f : -1f;
                }
                break;
        }
        
        if (canMove)
        {
            // 计算移动步长
            float pathLength = currentConnection.GetPathLength(startNode.position, endNode.position);
            float moveStep = (moveSpeed * Time.deltaTime) / pathLength;
            
            // 应用移动方向
            pathProgress += moveStep * moveDirection;
            
            // 检查边界
            if (pathProgress >= 1f)
            {
                pathProgress = 1f;
                SetPlayerToNode(targetNodeName);
            }
            else if (pathProgress <= 0f)
            {
                pathProgress = 0f;
                // 返回起始节点
                SetPlayerToNode(currentNodeName);
            }
        }
    }
    
    void UpdateMovement()
    {
        // 这个方法现在主要用于更新玩家位置显示
        // 实际的移动逻辑已经移到 HandlePathMovement 中
        UpdatePlayerPosition();
    }
    
    void SetPlayerToNode(string nodeName)
    {
        if (!nodeDict.ContainsKey(nodeName)) return;
        
        currentNodeName = nodeName;
        targetNodeName = "";
        currentConnection = null;
        pathProgress = 0f;
        isMoving = false;
        
        NetworkNode node = nodeDict[nodeName];
        playerTransform.position = new Vector3(node.position.x, node.position.y, playerTransform.position.z);
        
        Debug.Log($"到达节点 {nodeName}");
        
        // 显示可用连接
        if (node.connections.Count > 0)
        {
            string connectionInfo = $"可连接到: ";
            foreach (var conn in node.connections)
            {
                connectionInfo += conn.targetNodeName + " ";
            }
            Debug.Log(connectionInfo);
        }
    }
    
    void UpdatePlayerPosition()
    {
        if (!isMoving || currentConnection == null) return;
        
        NetworkNode startNode = nodeDict[currentNodeName];
        NetworkNode endNode = nodeDict[targetNodeName];
        
        Vector2 currentPos = currentConnection.GetPositionAtRatio(pathProgress, startNode.position, endNode.position);
        playerTransform.position = new Vector3(currentPos.x, currentPos.y, playerTransform.position.z);
    }
    
    void OnDrawGizmos()
    {
        // 绘制节点
        foreach (var node in nodes)
        {
            Vector3 nodePos = new Vector3(node.position.x, node.position.y, 0);
            
            // 节点圆圈
            Gizmos.color = node.nodeColor;
            if (node.nodeName == currentNodeName)
            {
                Gizmos.color = Color.yellow; // 当前节点高亮
            }
            
            Gizmos.DrawWireSphere(nodePos, nodeRadius);
            
            // 节点名称
            if (showNodeNames && !string.IsNullOrEmpty(node.nodeName))
            {
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(nodePos + Vector3.up * (nodeRadius + 0.2f), node.nodeName);
                #endif
            }
        }
        
        // 绘制连接
        if (showConnections)
        {
            foreach (var node in nodes)
            {
                foreach (var connection in node.connections)
                {
                    if (!nodeDict.ContainsKey(connection.targetNodeName))
                        continue;
                        
                    NetworkNode targetNode = nodeDict[connection.targetNodeName];
                    
                    // 设置连接线颜色
                    if (isMoving && node.nodeName == currentNodeName && 
                        connection.targetNodeName == targetNodeName)
                    {
                        Gizmos.color = currentPathColor;
                    }
                    else
                    {
                        Gizmos.color = connectionLineColor;
                    }
                    
                    // 绘制路径
                    List<Vector2> fullPath = connection.GetFullPath(node.position, targetNode.position);
                    for (int i = 0; i < fullPath.Count - 1; i++)
                    {
                        Vector3 start = new Vector3(fullPath[i].x, fullPath[i].y, 0);
                        Vector3 end = new Vector3(fullPath[i + 1].x, fullPath[i + 1].y, 0);
                        Gizmos.DrawLine(start, end);
                    }
                    
                    // 绘制中间路径点
                    Gizmos.color = Color.white;
                    foreach (var waypoint in connection.waypoints)
                    {
                        Gizmos.DrawWireSphere(new Vector3(waypoint.x, waypoint.y, 0), 0.1f);
                    }
                }
            }
        }
        
        // 绘制玩家
        if (Application.isPlaying && playerTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(playerTransform.position, 0.15f);
        }
    }
    
    // 便捷方法：添加节点
    public void AddNode(string name, Vector2 position, Color color = default)
    {
        if (color == default) color = Color.green;
        
        NetworkNode newNode = new NetworkNode
        {
            nodeName = name,
            position = position,
            nodeColor = color
        };
        
        nodes.Add(newNode);
        BuildNodeDictionary();
    }
    
    // 便捷方法：添加连接
    public void AddConnection(string fromNode, string toNode, PathType pathType = PathType.Auto)
    {
        NetworkNode node = nodes.Find(n => n.nodeName == fromNode);
        if (node != null)
        {
            NodeConnection connection = new NodeConnection
            {
                targetNodeName = toNode,
                pathType = pathType
            };
            node.connections.Add(connection);
        }
    }
    
    // 创建示例网络
    [ContextMenu("创建示例网络")]
    public void CreateSampleNetwork()
    {
        nodes.Clear();
        
        // 创建节点
        AddNode("A", new Vector2(-2, 2), Color.green);
        AddNode("B", new Vector2(2, 2), Color.green);
        AddNode("C", new Vector2(-2, -2), Color.green);
        AddNode("D", new Vector2(2, -2), Color.green);
        AddNode("E", new Vector2(0, 0), Color.red); // 中心节点
        
        // 创建连接关系 A-B, A-C, A-E, B-D, B-E, C-E, D-E
        AddConnection("A", "B", PathType.Horizontal);
        AddConnection("A", "C", PathType.Vertical);
        AddConnection("A", "E", PathType.Auto);
        
        AddConnection("B", "A", PathType.Horizontal);
        AddConnection("B", "D", PathType.Vertical);
        AddConnection("B", "E", PathType.Auto);
        
        AddConnection("C", "A", PathType.Vertical);
        AddConnection("C", "D", PathType.Horizontal);
        AddConnection("C", "E", PathType.Auto);
        
        AddConnection("D", "B", PathType.Vertical);
        AddConnection("D", "C", PathType.Horizontal);
        AddConnection("D", "E", PathType.Auto);
        
        AddConnection("E", "A", PathType.Auto);
        AddConnection("E", "B", PathType.Auto);
        AddConnection("E", "C", PathType.Auto);
        AddConnection("E", "D", PathType.Auto);
        
        startingNodeName = "A";
        
        Debug.Log("示例网络创建完成！");
    }
}