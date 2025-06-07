using UnityEngine;
using System.Collections.Generic;

public class PuzzlePiece : MonoBehaviour
{
    [Header("拼图基本信息")]
    public string puzzleID = "Puzzle_A";
    public Vector2 puzzleSize = new Vector2(8, 6);
    public bool canDrag = true;
    
    [Header("碰撞体设置")]
    public Vector2 mainColliderSize = new Vector2(8, 6);
    public bool isMainColliderTrigger = true;
    
    [Header("连接设置")]
    public List<ConnectionPoint> connectionPoints = new List<ConnectionPoint>();
    
    [Header("视觉组件")]
    public GameObject puzzleFrame;
    public SpriteRenderer backgroundRenderer;
    
    [Header("调试设置")]
    public bool showPlayerFollow = false;
    public bool showBounds = false;
    public bool showMainColliderGizmo = true;
    
    // 内部状态
    private bool isDragging = false;
    private Vector3 dragOffset;
    private List<PuzzlePiece> connectedPuzzles = new List<PuzzlePiece>();
    private PuzzleSnapDetector snapDetector;
    private BoxCollider2D mainCollider;
    
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
    
    void InitializePuzzle()
    {
        CleanupOldComponents();
        
        if (connectionPoints.Count == 0)
        {
            CreateDefaultConnectionPoints();
        }
        else
        {
            UpdateConnectionPointPositions();
        }
        
        SetupMainCollider();
        SetupVisualComponents();
        CreateBoundaryWalls();
    }
    
    void CleanupOldComponents()
    {
        var oldColliders = GetComponents<Collider2D>();
        for (int i = oldColliders.Length - 1; i >= 0; i--)
        {
            if (oldColliders[i] != null)
            {
                DestroyImmediate(oldColliders[i]);
            }
        }
        
        Transform oldWalls = transform.Find("BoundaryWalls");
        if (oldWalls != null)
        {
            DestroyImmediate(oldWalls.gameObject);
        }
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
    
    void UpdateConnectionPointPositions()
    {
        float halfWidth = puzzleSize.x * 0.5f;
        float halfHeight = puzzleSize.y * 0.5f;
        
        foreach (var point in connectionPoints)
        {
            switch (point.direction)
            {
                case ConnectionDirection.Up:
                    point.localPosition = new Vector2(0, halfHeight);
                    break;
                case ConnectionDirection.Down:
                    point.localPosition = new Vector2(0, -halfHeight);
                    break;
                case ConnectionDirection.Left:
                    point.localPosition = new Vector2(-halfWidth, 0);
                    break;
                case ConnectionDirection.Right:
                    point.localPosition = new Vector2(halfWidth, 0);
                    break;
            }
        }
    }
    
    void SetupMainCollider()
    {
        mainCollider = gameObject.AddComponent<BoxCollider2D>();
        mainCollider.size = mainColliderSize;
        mainCollider.isTrigger = isMainColliderTrigger;
        
        Debug.Log($"设置拼图 {puzzleID} 主碰撞体: 尺寸{mainColliderSize}, 触发器={isMainColliderTrigger}");
    }
    
    void SetupVisualComponents()
    {
        if (backgroundRenderer != null)
        {
            backgroundRenderer.transform.localPosition = Vector3.zero;
            backgroundRenderer.transform.localScale = new Vector3(puzzleSize.x * 0.9f, puzzleSize.y * 0.9f, 1);
            backgroundRenderer.sortingOrder = 0;
        }
        
        SetupPuzzleFrame();
    }
    
    void SetupPuzzleFrame()
    {
        if (puzzleFrame == null)
        {
            CreatePuzzleFrame();
        }
        
        puzzleFrame.transform.localPosition = Vector3.zero;
        puzzleFrame.transform.localScale = new Vector3(puzzleSize.x, puzzleSize.y, 1);
        
        var renderer = puzzleFrame.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = puzzleFrame.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFrameSprite();
        }
        renderer.sortingOrder = 2;
    }
    
    void CreateBoundaryWalls()
    {
        GameObject wallsContainer = new GameObject("BoundaryWalls");
        wallsContainer.transform.SetParent(transform);
        wallsContainer.transform.localPosition = Vector3.zero;
        
        float wallThickness = 0.1f;
        float halfWidth = puzzleSize.x * 0.5f;
        float halfHeight = puzzleSize.y * 0.5f;
        float connectionGap = 1.2f;
        
        // 上墙 - 分成两段，中间留缺口
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(-halfWidth * 0.5f, halfHeight, 0), 
            new Vector2(halfWidth - connectionGap, wallThickness));
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(halfWidth * 0.5f, halfHeight, 0), 
            new Vector2(halfWidth - connectionGap, wallThickness));
        
        // 下墙
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(-halfWidth * 0.5f, -halfHeight, 0), 
            new Vector2(halfWidth - connectionGap, wallThickness));
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(halfWidth * 0.5f, -halfHeight, 0), 
            new Vector2(halfWidth - connectionGap, wallThickness));
        
        // 左墙 - 分成两段
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(-halfWidth, halfHeight * 0.5f, 0), 
            new Vector2(wallThickness, halfHeight - connectionGap));
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(-halfWidth, -halfHeight * 0.5f, 0), 
            new Vector2(wallThickness, halfHeight - connectionGap));
        
        // 右墙
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(halfWidth, halfHeight * 0.5f, 0), 
            new Vector2(wallThickness, halfHeight - connectionGap));
        CreateWallSegment(wallsContainer.transform, 
            new Vector3(halfWidth, -halfHeight * 0.5f, 0), 
            new Vector2(wallThickness, halfHeight - connectionGap));
    }
    
    void CreateWallSegment(Transform parent, Vector3 localPos, Vector2 size)
    {
        GameObject wall = new GameObject("WallSegment");
        wall.transform.SetParent(parent);
        wall.transform.localPosition = localPos;
        wall.layer = 9; // Walls层
        
        var collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = false;
        
        if (showBounds)
        {
            var renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateSimpleSprite(Color.red);
            renderer.sortingOrder = 3;
            wall.transform.localScale = new Vector3(size.x, size.y, 1);
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
        
        // 🔑 关键：在尝试自动连接前，不要清空玩家跟随信息
        if (snapDetector != null)
        {
            snapDetector.TryAutoConnect();
        }
        
        // 连接完成后再更新玩家状态和清空跟随信息
        UpdatePlayersCurrentPuzzle();
        
        // 延迟清空，确保吸附完成
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
    
    // 🔑 新方法：使用移动距离直接更新玩家位置
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
        Vector2 halfSize = mainColliderSize * 0.5f;
        
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
        if (puzzleFrame != null)
        {
            var renderer = puzzleFrame.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = isDragging ? Color.yellow : Color.white;
            }
        }
    }
    
    public void SetSnapPreview(bool showPreview)
    {
        if (puzzleFrame != null)
        {
            var renderer = puzzleFrame.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = showPreview ? Color.green : Color.white;
            }
        }
    }
    
    void CreatePuzzleFrame()
    {
        GameObject frame = new GameObject("PuzzleFrame");
        frame.transform.SetParent(transform);
        frame.transform.localPosition = Vector3.zero;
        
        var renderer = frame.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateFrameSprite();
        renderer.sortingOrder = 2;
        
        puzzleFrame = frame;
    }
    
    Sprite CreateFrameSprite()
    {
        int size = 64;
        int borderWidth = 2;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                bool isBorder = x < borderWidth || x >= size - borderWidth || 
                               y < borderWidth || y >= size - borderWidth;
                
                if (isBorder)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    Sprite CreateSimpleSprite(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }
    
    #endregion
    
    #region 编辑器方法
    
    [ContextMenu("重新初始化拼图")]
    public void ReinitializePuzzle()
    {
        InitializePuzzle();
        Debug.Log($"拼图 {puzzleID} 重新初始化完成");
        Debug.Log($"  拼图显示尺寸: {puzzleSize}");
        Debug.Log($"  主碰撞体尺寸: {mainColliderSize}");
        Debug.Log($"  主碰撞体触发器: {isMainColliderTrigger}");
    }
    
    [ContextMenu("更新拼图尺寸")]
    public void UpdatePuzzleSize()
    {
        CleanupOldComponents();
        InitializePuzzle();
        Debug.Log($"拼图 {puzzleID} 尺寸更新完成");
    }
    
    [ContextMenu("更新主碰撞体")]
    public void UpdateMainCollider()
    {
        if (mainCollider != null)
        {
            mainCollider.size = mainColliderSize;
            mainCollider.isTrigger = isMainColliderTrigger;
            Debug.Log($"主碰撞体已更新: 尺寸{mainColliderSize}, 触发器={isMainColliderTrigger}");
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
        // 绘制拼图显示边界
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, puzzleSize);
        
        // 绘制主碰撞体边界
        if (showMainColliderGizmo)
        {
            Gizmos.color = isMainColliderTrigger ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position, mainColliderSize);
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