using UnityEngine;
using System.Collections.Generic;

public class PuzzlePiece : MonoBehaviour
{
    [Header("拼图基本信息")]
    public string puzzleID = "Puzzle_A";
    public Vector2 puzzleSize = new Vector2(8, 6);
    public bool canDrag = true;
    
    [Header("碰撞体设置 - 手动控制")]
    public bool useCustomCollider = true;
    public Vector2 customColliderSize = new Vector2(8, 6);
    public Vector2 colliderOffset = Vector2.zero;
    public bool isColliderTrigger = true;
    
    [Header("边框设置 - 可自定义")]
    public bool showBorder = true;
    public Color borderColor = Color.white;
    public float borderWidth = 0.1f;
    public bool animatedBorder = true;
    [Range(0.1f, 5f)]
    public float animationSpeed = 1f;
    public Material borderMaterial;  // 可以指定自定义材质
    
    [Header("连接设置")]
    public List<ConnectionPoint> connectionPoints = new List<ConnectionPoint>();
    
    [Header("调试设置")]
    public bool showPlayerFollow = false;
    public bool showGizmos = true;
    
    // 内部状态
    private bool isDragging = false;
    private Vector3 dragOffset;
    private List<PuzzlePiece> connectedPuzzles = new List<PuzzlePiece>();
    private PuzzleSnapDetector snapDetector;
    private BoxCollider2D mainCollider;
    
    // 边框相关
    private GameObject borderContainer;
    private List<LineRenderer> borderLines = new List<LineRenderer>();
    private float animationOffset = 0f;
    
    // 玩家跟随相关
    private List<PlayerFollowInfo> playersFollowInfo = new List<PlayerFollowInfo>();
    
    [System.Serializable]
    private class PlayerFollowInfo
    {
        public PlayerMoveControl player;
        public Vector3 relativePosition;
        
        public PlayerFollowInfo(PlayerMoveControl p, Vector3 puzzlePos)
        {
            player = p;
            relativePosition = p.transform.position - puzzlePos;
        }
    }
    
    void Start()
    {
        InitializePuzzle();
        snapDetector = GetComponent<PuzzleSnapDetector>();
        if (snapDetector == null)
        {
            snapDetector = gameObject.AddComponent<PuzzleSnapDetector>();
        }
    }
    
    void Update()
    {
        if (animatedBorder && showBorder)
        {
            UpdateBorderAnimation();
        }
    }
    
    void InitializePuzzle()
    {
        // 只在需要时清理旧组件
        if (useCustomCollider)
        {
            CleanupOldColliders();
            SetupCustomCollider();
        }
        
        // 设置连接点（如果为空才创建默认的）
        if (connectionPoints.Count == 0)
        {
            CreateDefaultConnectionPoints();
        }
        
        // 设置边框
        if (showBorder)
        {
            CreateCustomBorder();
        }
    }
    
    void CleanupOldColliders()
    {
        var oldColliders = GetComponents<Collider2D>();
        for (int i = oldColliders.Length - 1; i >= 0; i--)
        {
            if (oldColliders[i] != null)
            {
                DestroyImmediate(oldColliders[i]);
            }
        }
    }
    
    void SetupCustomCollider()
    {
        if (!useCustomCollider) return;
        
        mainCollider = gameObject.AddComponent<BoxCollider2D>();
        mainCollider.size = customColliderSize;
        mainCollider.offset = colliderOffset;
        mainCollider.isTrigger = isColliderTrigger;
        
        Debug.Log($"设置拼图 {puzzleID} 自定义碰撞体: 尺寸{customColliderSize}, 偏移{colliderOffset}, 触发器={isColliderTrigger}");
    }
    
    void CreateDefaultConnectionPoints()
    {
        connectionPoints.Clear();
        
        float halfWidth = puzzleSize.x * 0.5f;
        float halfHeight = puzzleSize.y * 0.5f;
        
        connectionPoints.Add(new ConnectionPoint
        {
            direction = ConnectionDirection.Up,
            localPosition = new Vector2(0, halfHeight),
            connectionType = ConnectionType.Normal
        });
        
        connectionPoints.Add(new ConnectionPoint
        {
            direction = ConnectionDirection.Down,
            localPosition = new Vector2(0, -halfHeight),
            connectionType = ConnectionType.Normal
        });
        
        connectionPoints.Add(new ConnectionPoint
        {
            direction = ConnectionDirection.Left,
            localPosition = new Vector2(-halfWidth, 0),
            connectionType = ConnectionType.Normal
        });
        
        connectionPoints.Add(new ConnectionPoint
        {
            direction = ConnectionDirection.Right,
            localPosition = new Vector2(halfWidth, 0),
            connectionType = ConnectionType.Normal
        });
    }
    
    void CreateCustomBorder()
    {
        // 清理旧边框
        if (borderContainer != null)
        {
            DestroyImmediate(borderContainer);
        }
        
        borderContainer = new GameObject("BorderContainer");
        borderContainer.transform.SetParent(transform);
        borderContainer.transform.localPosition = Vector3.zero;
        
        borderLines.Clear();
        
        float halfWidth = puzzleSize.x * 0.5f;
        float halfHeight = puzzleSize.y * 0.5f;
        
        // 创建四条边框线
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-halfWidth, -halfHeight, 0), // 左下
            new Vector3(halfWidth, -halfHeight, 0),  // 右下
            new Vector3(halfWidth, halfHeight, 0),   // 右上
            new Vector3(-halfWidth, halfHeight, 0)   // 左上
        };
        
        // 创建四条边
        CreateBorderLine(corners[0], corners[1]); // 下边
        CreateBorderLine(corners[1], corners[2]); // 右边
        CreateBorderLine(corners[2], corners[3]); // 上边
        CreateBorderLine(corners[3], corners[0]); // 左边
    }
    
    void CreateBorderLine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("BorderLine");
        lineObj.transform.SetParent(borderContainer.transform);
        lineObj.transform.localPosition = Vector3.zero;
        
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        
        // 设置LineRenderer属性
        if (borderMaterial != null)
        {
            line.material = borderMaterial;
        }
        else
        {
            // 使用默认材质
            line.material = CreateDefaultBorderMaterial();
        }
        
        // 修复：使用startColor和endColor替代color
        line.startColor = borderColor;
        line.endColor = borderColor;
        line.startWidth = borderWidth;
        line.endWidth = borderWidth;
        line.positionCount = 2;
        line.useWorldSpace = false;
        line.sortingOrder = 10; // 确保边框在最上层
        
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        
        borderLines.Add(line);
    }
    
    Material CreateDefaultBorderMaterial()
    {
        // 创建一个简单的虚线材质
        Material mat = new Material(Shader.Find("Sprites/Default"));
        
        // 如果需要虚线效果，可以在这里设置材质属性
        // 或者你可以提供自己的Shader
        
        return mat;
    }
    
    void UpdateBorderAnimation()
    {
        if (borderLines.Count == 0) return;
        
        animationOffset += animationSpeed * Time.deltaTime;
        
        foreach (var line in borderLines)
        {
            if (line != null && line.material != null)
            {
                // 这里可以实现各种动画效果
                // 例如：颜色变化
                float alpha = 0.5f + 0.5f * Mathf.Sin(animationOffset);
                Color animatedColor = borderColor;
                animatedColor.a = alpha;
                
                // 修复：使用startColor和endColor替代color
                line.startColor = animatedColor;
                line.endColor = animatedColor;
                
                // 如果材质支持，也可以设置UV偏移来实现流动效果
                if (line.material.HasProperty("_MainTex"))
                {
                    line.material.SetTextureOffset("_MainTex", new Vector2(animationOffset * 0.1f, 0));
                }
            }
        }
    }
    
    #region 鼠标拖拽控制
    
    void OnMouseDown()
    {
        if (!canDrag) return;
        
        isDragging = true;
        
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        dragOffset = transform.position - mouseWorldPos;
        
        DisconnectAll();
        RecordPlayersInside();
        SetDraggingVisual(true);
        
        if (showPlayerFollow)
        {
            Debug.Log($"开始拖拽拼图 {puzzleID}，记录了 {playersFollowInfo.Count} 个玩家");
        }
    }
    
    void OnMouseDrag()
    {
        if (!isDragging) return;
        
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        Vector3 newPuzzlePosition = mouseWorldPos + dragOffset;
        
        transform.position = newPuzzlePosition;
        UpdateFollowingPlayers();
        
        if (snapDetector != null)
        {
            snapDetector.CheckForSnap();
        }
    }
    
    void OnMouseUp()
    {
        if (!isDragging) return;
        
        isDragging = false;
        SetDraggingVisual(false);
        
        if (showPlayerFollow)
        {
            Debug.Log($"🎯 松开鼠标，当前跟随玩家数量: {playersFollowInfo.Count}");
            foreach (var info in playersFollowInfo)
            {
                if (info.player != null)
                {
                    Debug.Log($"  - 玩家 {info.player.name}: 相对位置 {info.relativePosition}");
                }
            }
        }
        
        if (snapDetector != null)
        {
            snapDetector.TryAutoConnect();
        }
        
        UpdatePlayersCurrentPuzzle();
        Invoke(nameof(ClearFollowInfo), 0.1f);
        
        if (showPlayerFollow)
        {
            Debug.Log($"🏁 拼图 {puzzleID} 拖拽结束");
        }
    }
    
    void RecordPlayersInside()
    {
        playersFollowInfo.Clear();
        var allPlayers = FindObjectsOfType<PlayerMoveControl>();
        Vector3 currentPuzzlePosition = transform.position;
        
        foreach (var player in allPlayers)
        {
            if (IsPositionInside(player.transform.position))
            {
                var followInfo = new PlayerFollowInfo(player, currentPuzzlePosition);
                playersFollowInfo.Add(followInfo);
                
                if (showPlayerFollow)
                {
                    Debug.Log($"记录玩家: 相对位置 {followInfo.relativePosition}");
                }
            }
        }
    }
    
    void UpdateFollowingPlayers()
    {
        Vector3 currentPuzzlePosition = transform.position;
        
        foreach (var followInfo in playersFollowInfo)
        {
            if (followInfo.player != null)
            {
                Vector3 newPlayerPosition = currentPuzzlePosition + followInfo.relativePosition;
                followInfo.player.transform.position = newPlayerPosition;
            }
        }
    }
    
    public void UpdateFollowingPlayersWithMovement(Vector3 movement)
    {
        if (showPlayerFollow)
        {
            Debug.Log($"🚀 使用移动距离更新玩家位置，移动: {movement}，玩家数量: {playersFollowInfo.Count}");
        }
        
        foreach (var followInfo in playersFollowInfo)
        {
            if (followInfo.player != null)
            {
                Vector3 oldPos = followInfo.player.transform.position;
                Vector3 newPos = oldPos + movement;
                followInfo.player.transform.position = newPos;
                
                if (showPlayerFollow)
                {
                    Debug.Log($"  - 玩家 {followInfo.player.name}: {oldPos} -> {newPos}");
                }
            }
        }
    }
    
    void UpdatePlayersCurrentPuzzle()
    {
        foreach (var followInfo in playersFollowInfo)
        {
            if (followInfo.player != null)
            {
                followInfo.player.ForceUpdateCurrentPuzzle();
            }
        }
    }
    
    void ClearFollowInfo()
    {
        if (showPlayerFollow)
        {
            Debug.Log($"🧹 清空跟随信息，原有 {playersFollowInfo.Count} 个玩家");
        }
        playersFollowInfo.Clear();
    }
    
    #endregion
    
    #region 连接管理
    
    public bool ConnectTo(PuzzlePiece otherPuzzle, ConnectionPoint myPoint, ConnectionPoint otherPoint)
    {
        if (IsConnectedTo(otherPuzzle)) return false;
        
        myPoint.isConnected = true;
        otherPoint.isConnected = true;
        myPoint.connectedPoint = otherPoint;
        otherPoint.connectedPoint = myPoint;
        
        connectedPuzzles.Add(otherPuzzle);
        otherPuzzle.connectedPuzzles.Add(this);
        
        CreatePassageBetween(myPoint, otherPoint, otherPuzzle);
        NotifyAllPlayersUpdatePuzzle();
        
        Debug.Log($"拼图连接成功: {puzzleID} <-> {otherPuzzle.puzzleID}");
        return true;
    }
    
    public void DisconnectFrom(PuzzlePiece otherPuzzle)
    {
        if (!IsConnectedTo(otherPuzzle)) return;
        
        foreach (var point in connectionPoints)
        {
            if (point.isConnected && 
                point.connectedPoint != null && 
                otherPuzzle.connectionPoints.Contains(point.connectedPoint))
            {
                point.Disconnect();
            }
        }
        
        connectedPuzzles.Remove(otherPuzzle);
        otherPuzzle.connectedPuzzles.Remove(this);
        
        NotifyAllPlayersUpdatePuzzle();
        Debug.Log($"拼图连接断开: {puzzleID} <-> {otherPuzzle.puzzleID}");
    }
    
    public void DisconnectAll()
    {
        var connectedCopy = new List<PuzzlePiece>(connectedPuzzles);
        foreach (var puzzle in connectedCopy)
        {
            DisconnectFrom(puzzle);
        }
    }
    
    void NotifyAllPlayersUpdatePuzzle()
    {
        var allPlayers = FindObjectsOfType<PlayerMoveControl>();
        foreach (var player in allPlayers)
        {
            player.ForceUpdateCurrentPuzzle();
        }
    }
    
    void CreatePassageBetween(ConnectionPoint myPoint, ConnectionPoint otherPoint, PuzzlePiece otherPuzzle)
    {
        Vector3 myWorldPos = transform.position + (Vector3)myPoint.localPosition;
        Vector3 otherWorldPos = otherPuzzle.transform.position + (Vector3)otherPoint.localPosition;
        Vector3 passageCenter = (myWorldPos + otherWorldPos) * 0.5f;
        
        GameObject passage = new GameObject($"Passage_{puzzleID}_{otherPuzzle.puzzleID}");
        passage.transform.position = passageCenter;
        
        var passageMarker = passage.AddComponent<PassageMarker>();
        passageMarker.Initialize(this, otherPuzzle, myPoint, otherPoint);
        
        myPoint.passageObject = passage;
        otherPoint.passageObject = passage;
    }
    
    #endregion
    
    #region 公共查询方法
    
    public bool IsPositionInside(Vector2 position)
    {
        Vector2 localPos = position - (Vector2)transform.position;
        Vector2 halfSize = puzzleSize * 0.5f;
        
        return Mathf.Abs(localPos.x) <= halfSize.x && Mathf.Abs(localPos.y) <= halfSize.y;
    }
    
    public bool IsConnectedTo(PuzzlePiece otherPuzzle)
    {
        return connectedPuzzles.Contains(otherPuzzle);
    }
    
    public List<PuzzlePiece> GetConnectedPuzzles()
    {
        return new List<PuzzlePiece>(connectedPuzzles);
    }
    
    public List<ConnectionPoint> GetAvailableConnectionPoints()
    {
        var available = new List<ConnectionPoint>();
        foreach (var point in connectionPoints)
        {
            if (!point.isConnected)
            {
                available.Add(point);
            }
        }
        return available;
    }
    
    #endregion
    
    #region 视觉效果
    
    void SetDraggingVisual(bool isDragging)
    {
        if (showBorder && borderLines.Count > 0)
        {
            Color dragColor = isDragging ? Color.yellow : borderColor;
            foreach (var line in borderLines)
            {
                if (line != null)
                {
                    // 修复：使用startColor和endColor替代color
                    line.startColor = dragColor;
                    line.endColor = dragColor;
                }
            }
        }
    }
    
    public void SetSnapPreview(bool showPreview)
    {
        if (showBorder && borderLines.Count > 0)
        {
            Color snapColor = showPreview ? Color.green : borderColor;
            foreach (var line in borderLines)
            {
                if (line != null)
                {
                    // 修复：使用startColor和endColor替代color
                    line.startColor = snapColor;
                    line.endColor = snapColor;
                }
            }
        }
    }
    
    #endregion
    
    #region 编辑器方法
    
    [ContextMenu("重新初始化拼图")]
    public void ReinitializePuzzle()
    {
        InitializePuzzle();
        Debug.Log($"拼图 {puzzleID} 重新初始化完成");
    }
    
    [ContextMenu("更新边框")]
    public void UpdateBorder()
    {
        if (showBorder)
        {
            CreateCustomBorder();
            Debug.Log($"边框已更新");
        }
    }
    
    [ContextMenu("更新碰撞体")]
    public void UpdateCollider()
    {
        if (useCustomCollider)
        {
            CleanupOldColliders();
            SetupCustomCollider();
            Debug.Log($"碰撞体已更新: 尺寸{customColliderSize}, 偏移{colliderOffset}");
        }
    }
    
    [ContextMenu("测试玩家跟随")]
    public void TestPlayerFollow()
    {
        Debug.Log("=== 🧪 测试玩家跟随 ===");
        RecordPlayersInside();
        Vector3 testMovement = new Vector3(2, 1, 0);
        Debug.Log($"模拟移动: {testMovement}");
        UpdateFollowingPlayersWithMovement(testMovement);
        Debug.Log("测试完成");
    }
    
    #endregion
    
    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // 绘制拼图边界
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, puzzleSize);
        
        // 绘制自定义碰撞体边界
        if (useCustomCollider)
        {
            Gizmos.color = isColliderTrigger ? Color.green : Color.red;
            Vector3 colliderCenter = transform.position + (Vector3)colliderOffset;
            Gizmos.DrawWireCube(colliderCenter, customColliderSize);
        }
        
        // 绘制连接点
        foreach (var point in connectionPoints)
        {
            Vector3 worldPos = transform.position + (Vector3)point.localPosition;
            Gizmos.color = point.isConnected ? Color.green : Color.red;
            Gizmos.DrawWireSphere(worldPos, 0.3f);
            
            Vector3 direction = GetDirectionVector(point.direction) * 0.5f;
            Gizmos.DrawLine(worldPos, worldPos + direction);
        }
    }
    
    Vector3 GetDirectionVector(ConnectionDirection direction)
    {
        switch (direction)
        {
            case ConnectionDirection.Up: return Vector3.up;
            case ConnectionDirection.Down: return Vector3.down;
            case ConnectionDirection.Left: return Vector3.left;
            case ConnectionDirection.Right: return Vector3.right;
            default: return Vector3.zero;
        }
    }
}