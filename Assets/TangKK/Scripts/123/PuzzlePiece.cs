using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Collider2D))]
public class PuzzlePiece : MonoBehaviour
{
    public PuzzleGroup currentGroup;
    public Vector3 offsetFromMouse;
    public bool isDragging = false;

    [Header("是否冻结（拖动时）")]
    public bool isFrozen = false;

    [Header("拼图组 ID（可手动设置）")]
    public int initialGroupID;

    [HideInInspector]
    public int originalGroupID;

    /// <summary>
    /// 当前拼图所属的组 ID（用于路径判断）
    /// </summary>
    public int GroupID => initialGroupID;

    private void Awake()
    {
        // ✅ 初始化原始组 ID
        originalGroupID = initialGroupID;
    }

    [Header("物理连接检测容差")]
    public float physicalConnectionTolerance = 0.1f;

    [Header("吸附设置")]
    public float snapTolerance = 0.3f; // 吸附容差距离
    public bool isSnapSuccessful = false; // 用于观察吸附是否成功

    private void OnMouseDown()
    {
        isDragging = true;
        SetFrozen(true);
        offsetFromMouse = transform.position - GetMouseWorldPos();

        if (currentGroup != null)
        {
            // 记录分离前的组状态
            var originalGroup = currentGroup;
            var originalPieces = new List<PuzzlePiece>(originalGroup.pieces);

            Debug.Log($"[OnMouseDown] 开始分离 {name}，原组有 {originalPieces.Count} 个拼图");

            // 从组中移除当前拼图
            originalGroup.RemovePiece(this);
            currentGroup = null;

            // ✅ 立即为当前拼图创建独立组
            CreateIndependentGroup();

            // ✅ 立即重新评估剩余拼图的连接性
            if (originalPieces.Count > 1)
            {
                var remainingPieces = originalPieces.Where(p => p != this).ToList();
                Debug.Log($"[OnMouseDown] 立即重新评估 {remainingPieces.Count} 个剩余拼图");
                
                // 强制立即重新分组
                ForceImmediateRegroup(remainingPieces);
            }

            // ✅ 立即刷新路径和UI
            ForceImmediateRefresh();
        }
    }

    /// <summary>
    /// ✅ 为当前拼图立即创建独立组
    /// </summary>
    private void CreateIndependentGroup()
    {
        GameObject g = new GameObject("PuzzleGroup");
        var newGroup = g.AddComponent<PuzzleGroup>();
        this.initialGroupID = this.originalGroupID;
        newGroup.AddPiece(this);
        Debug.Log($"[立即分离] {name} 创建独立组，组ID：{initialGroupID}");
    }

    /// <summary>
    /// ✅ 强制立即重新分组，确保分离效果立即可见
    /// </summary>
    private void ForceImmediateRegroup(List<PuzzlePiece> pieces)
    {
        // 清除所有剩余拼图的组关系，重新开始
        foreach (var piece in pieces)
        {
            if (piece.currentGroup != null)
            {
                var oldGroup = piece.currentGroup;
                oldGroup.RemovePiece(piece);
                // 注意：组为空时会自动销毁
            }
        }

        // 使用图搜索重新建立正确的组关系
        var unprocessed = new List<PuzzlePiece>(pieces);
        var processed = new HashSet<PuzzlePiece>();

        while (unprocessed.Count > 0)
        {
            // ✅ 修复：找到第一个未处理的拼图
            var startPiece = unprocessed.FirstOrDefault(p => !processed.Contains(p));
            
            // ✅ 如果没有找到未处理的拼图，说明所有都处理完了
            if (startPiece == null)
            {
                Debug.Log("[ForceImmediateRegroup] 所有拼图已处理完成");
                break;
            }

            var connectedGroup = new List<PuzzlePiece>();
            var visited = new HashSet<PuzzlePiece>();
            
            // 找到所有与startPiece连接的拼图
            FindConnectedPieces(startPiece, pieces, visited, connectedGroup);

            // 为这个连接组创建新组
            if (connectedGroup.Count == 1)
            {
                // 单独拼图：恢复原始组ID
                var piece = connectedGroup[0];
                GameObject g = new GameObject("PuzzleGroup");
                var newGroup = g.AddComponent<PuzzleGroup>();
                piece.initialGroupID = piece.originalGroupID;
                newGroup.AddPiece(piece);
                Debug.Log($"[立即重组] 创建单独组：{piece.name}，组ID：{piece.initialGroupID}");
            }
            else
            {
                // 多个连接拼图：创建新组
                GameObject g = new GameObject("PuzzleGroup");
                var newGroup = g.AddComponent<PuzzleGroup>();
                int newGroupID = newGroup.GetInstanceID();

                foreach (var piece in connectedGroup)
                {
                    if (piece.originalGroupID == 0)
                        piece.originalGroupID = piece.initialGroupID;
                    piece.initialGroupID = newGroupID;
                    newGroup.AddPiece(piece);
                }
                Debug.Log($"[立即重组] 创建连接组，包含 {connectedGroup.Count} 个拼图，组ID：{newGroupID}");
            }

            // ✅ 标记为已处理，并从未处理列表中移除
            foreach (var piece in connectedGroup)
            {
                processed.Add(piece);
                unprocessed.Remove(piece);
            }

            Debug.Log($"[ForceImmediateRegroup] 已处理 {connectedGroup.Count} 个拼图，剩余 {unprocessed.Count} 个");
        }
    }

    /// <summary>
    /// ✅ 强制立即刷新所有相关系统（支持多玩家）
    /// </summary>
    private void ForceImmediateRefresh()
    {
        // 刷新路径系统
        RefreshPath();

        // ✅ 更新所有玩家的PathMover状态
        UpdateAllPlayersPathMover();

        Debug.Log($"[立即刷新] 完成所有系统刷新");
    }

    /// <summary>
    /// ✅ 新方法：更新所有玩家的PathMover状态
    /// </summary>
    private void UpdateAllPlayersPathMover()
    {
        string[] playerTags = { "Player", "Player1", "Player2", "Character", "Hero" };
        
        foreach (var tag in playerTags)
        {
            try
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag(tag);
                foreach (var player in players)
                {
                    var playerPiece = player.GetComponentInParent<PuzzlePiece>();
                    if (playerPiece != null)
                    {
                        UpdatePlayerPathMover(playerPiece, player.name);
                    }
                }
            }
            catch (UnityException)
            {
                // 标签不存在，跳过
                continue;
            }
        }

        // ✅ 额外方法：直接查找所有PathMover组件并更新相关的
        UpdateAllPathMoversDirectly();
    }

    /// <summary>
    /// ✅ 直接更新所有PathMover组件
    /// </summary>
    private void UpdateAllPathMoversDirectly()
    {
        PathMover[] allPathMovers = FindObjectsOfType<PathMover>();
        
        foreach (var pathMover in allPathMovers)
        {
            // 获取这个PathMover对应的玩家拼图
            var playerPiece = pathMover.GetComponentInParent<PuzzlePiece>();
            if (playerPiece != null)
            {
                // 检查这个玩家是否与当前操作的拼图相关
                bool isPlayerRelevant = false;
                
                if (playerPiece == this)
                {
                    isPlayerRelevant = true;
                }
                else if (currentGroup != null && currentGroup.pieces.Contains(playerPiece))
                {
                    isPlayerRelevant = true;
                }
                else if (IsPhysicallyConnected(this, playerPiece))
                {
                    isPlayerRelevant = true;
                }

                if (isPlayerRelevant)
                {
                    pathMover.ForceUpdateGroupID(this.initialGroupID);
                    Debug.Log($"[直接PathMover更新] 更新PathMover {pathMover.name} 组号为：{this.initialGroupID}");
                }
            }
        }
    }

    /// <summary>
    /// ✅ 为特定玩家更新PathMover
    /// </summary>
    private void UpdatePlayerPathMover(PuzzlePiece playerPiece, string playerName)
    {
        // ✅ 修复：为每个玩家找到其对应的PathMover组件
        GameObject playerObject = playerPiece.transform.root.gameObject;
        PathMover playerPathMover = playerObject.GetComponent<PathMover>();
        
        // 如果玩家对象本身没有PathMover，查找其子对象
        if (playerPathMover == null)
        {
            playerPathMover = playerObject.GetComponentInChildren<PathMover>();
        }
        
        // 如果还是找不到，通过玩家标签找到对应的PathMover
        if (playerPathMover == null)
        {
            string[] playerTags = { "Player", "Player1", "Player2", "Character", "Hero" };
            foreach (var tag in playerTags)
            {
                try
                {
                    GameObject[] players = GameObject.FindGameObjectsWithTag(tag);
                    foreach (var player in players)
                    {
                        if (player.name == playerName || player.GetComponentInParent<PuzzlePiece>() == playerPiece)
                        {
                            playerPathMover = player.GetComponent<PathMover>();
                            if (playerPathMover == null)
                            {
                                playerPathMover = player.GetComponentInChildren<PathMover>();
                            }
                            if (playerPathMover != null) break;
                        }
                    }
                    if (playerPathMover != null) break;
                }
                catch (UnityException)
                {
                    continue;
                }
            }
        }

        if (playerPathMover != null)
        {
            // 检查这个玩家是否与当前操作的拼图相关
            bool isPlayerRelevant = false;
            
            // 情况1：玩家就在当前拼图上
            if (playerPiece == this)
            {
                isPlayerRelevant = true;
            }
            // 情况2：玩家在与当前拼图同组的拼图上
            else if (currentGroup != null && currentGroup.pieces.Contains(playerPiece))
            {
                isPlayerRelevant = true;
            }
            // 情况3：检查玩家是否与当前拼图物理连接
            else if (IsPhysicallyConnected(this, playerPiece))
            {
                isPlayerRelevant = true;
            }

            if (isPlayerRelevant)
            {
                playerPathMover.ForceUpdateGroupID(this.initialGroupID);
                Debug.Log($"[多玩家更新] 为玩家 {playerName} 的PathMover更新组号为：{this.initialGroupID}");
            }
        }
        else
        {
            Debug.LogWarning($"[多玩家更新] 未找到玩家 {playerName} 的PathMover组件！");
        }
    }

    /// <summary>
    /// 使用深度优先搜索找到所有物理连接的拼图
    /// </summary>
    private void FindConnectedPieces(PuzzlePiece startPiece, List<PuzzlePiece> allPieces, 
                                   HashSet<PuzzlePiece> visited, List<PuzzlePiece> connectedComponent)
    {
        visited.Add(startPiece);
        connectedComponent.Add(startPiece);

        foreach (var other in allPieces)
        {
            if (!visited.Contains(other) && IsPhysicallyConnected(startPiece, other))
            {
                FindConnectedPieces(other, allPieces, visited, connectedComponent);
            }
        }
    }

    /// <summary>
    /// 检查两个拼图块是否物理连接（基于位置）
    /// </summary>
    public bool IsPhysicallyConnected(PuzzlePiece a, PuzzlePiece b)
    {
        Vector3 aSize = a.GetComponent<Renderer>().bounds.size;
        Vector3 bSize = b.GetComponent<Renderer>().bounds.size;

        Vector3 aPos = a.transform.position;
        Vector3 bPos = b.transform.position;

        Vector3 offset = aPos - bPos;

        float expectedX = (aSize.x + bSize.x) / 2f;
        float expectedY = (aSize.y + bSize.y) / 2f;

        // 检查水平连接
        bool horizontalConnect = Mathf.Abs(Mathf.Abs(offset.x) - expectedX) < physicalConnectionTolerance &&
                                Mathf.Abs(offset.y) < physicalConnectionTolerance;

        // 检查垂直连接
        bool verticalConnect = Mathf.Abs(Mathf.Abs(offset.y) - expectedY) < physicalConnectionTolerance &&
                              Mathf.Abs(offset.x) < physicalConnectionTolerance;

        return horizontalConnect || verticalConnect;
    }

    private void OnMouseDrag()
    {
        if (isDragging)
        {
            transform.position = GetMouseWorldPos() + offsetFromMouse;
        }
    }

    private void OnMouseUp()
    {
        isDragging = false;
        SetFrozen(false);

        Debug.Log($"[OnMouseUp] {name} 鼠标释放，开始检查吸附");

        bool foundSnap = false;
        PuzzlePiece[] allPieces = FindObjectsOfType<PuzzlePiece>();

        foreach (var other in allPieces)
        {
            if (other == this) continue;

            if (TrySnapTo(other))
            {
                foundSnap = true;
                Debug.Log($"[吸附成功] {name} 成功吸附到 {other.name}");

                // ✅ 吸附后，重新组织所有连接的拼图
                ReorganizeConnectedPuzzles();
                ForceUpdateAfterSnap();
                return;
            }
        }

        if (!foundSnap)
        {
            isSnapSuccessful = false;
            Debug.Log($"[吸附失败] {name} 没有找到可吸附的拼图");
        }

        // ✅ 由于OnMouseDown时已经创建了独立组，这里只需要确认
        if (currentGroup == null)
        {
            Debug.LogWarning($"[OnMouseUp] {name} 没有组，这不应该发生！重新创建独立组");
            CreateIndependentGroup();
        }

        RefreshPath();
    }

    /// <summary>
    /// ✅ 新方法：重新组织所有连接的拼图，确保物理连接的拼图都在同一组（支持多玩家）
    /// </summary>
    public void ReorganizeConnectedPuzzles()
    {
        PuzzlePiece[] allPieces = FindObjectsOfType<PuzzlePiece>();
        var unprocessed = new List<PuzzlePiece>(allPieces);
        var processed = new HashSet<PuzzlePiece>();

        while (unprocessed.Count > 0)
        {
            var startPiece = unprocessed[0];
            if (processed.Contains(startPiece))
            {
                unprocessed.Remove(startPiece);
                continue;
            }

            // 找到所有与startPiece连接的拼图
            var connectedGroup = new List<PuzzlePiece>();
            var visited = new HashSet<PuzzlePiece>();
            FindConnectedPieces(startPiece, allPieces.ToList(), visited, connectedGroup);

            // 将这些拼图放在同一组中
            if (connectedGroup.Count > 1)
            {
                // ✅ 优先使用任何玩家所在的组，支持多玩家
                PuzzleGroup targetGroup = GetPreferredPlayerGroup(connectedGroup);
                
                if (targetGroup == null)
                {
                    targetGroup = connectedGroup[0].currentGroup;
                }

                if (targetGroup == null)
                {
                    GameObject g = new GameObject("PuzzleGroup");
                    targetGroup = g.AddComponent<PuzzleGroup>();
                }

                // 将所有连接的拼图加入目标组
                int newGroupID = targetGroup.GetInstanceID();
                foreach (var piece in connectedGroup)
                {
                    if (piece.currentGroup != targetGroup)
                    {
                        if (piece.currentGroup != null)
                        {
                            piece.currentGroup.RemovePiece(piece);
                        }
                        targetGroup.AddPiece(piece);
                    }

                    if (piece.originalGroupID == 0)
                        piece.originalGroupID = piece.initialGroupID;
                    piece.initialGroupID = newGroupID;
                }

                Debug.Log($"[重新组织] 将 {connectedGroup.Count} 个连接的拼图归入组 {newGroupID}");
                
                // ✅ 更新所有相关玩家的状态
                UpdateAllPlayersForGroup(connectedGroup, newGroupID);
            }

            // 标记这些拼图为已处理
            foreach (var piece in connectedGroup)
            {
                processed.Add(piece);
                unprocessed.Remove(piece);
            }
        }
    }

    /// <summary>
    /// ✅ 获取包含玩家的首选组（支持多玩家）
    /// </summary>
    private PuzzleGroup GetPreferredPlayerGroup(List<PuzzlePiece> connectedGroup)
    {
        string[] playerTags = { "Player", "Player1", "Player2", "Character", "Hero" };
        
        // 检查连接组中是否有玩家
        foreach (var piece in connectedGroup)
        {
            foreach (var tag in playerTags)
            {
                try
                {
                    GameObject[] players = GameObject.FindGameObjectsWithTag(tag);
                    foreach (var player in players)
                    {
                        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
                        if (playerPiece == piece && piece.currentGroup != null)
                        {
                            return piece.currentGroup;
                        }
                    }
                }
                catch (UnityException)
                {
                    continue;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// ✅ 为组中的所有玩家更新状态
    /// </summary>
    private void UpdateAllPlayersForGroup(List<PuzzlePiece> groupPieces, int groupID)
    {
        // ✅ 获取所有PathMover组件，而不是只有第一个
        PathMover[] allPathMovers = FindObjectsOfType<PathMover>();
        string[] playerTags = { "Player", "Player1", "Player2", "Character", "Hero" };
        
        foreach (var pathMover in allPathMovers)
        {
            foreach (var tag in playerTags)
            {
                try
                {
                    GameObject[] players = GameObject.FindGameObjectsWithTag(tag);
                    foreach (var player in players)
                    {
                        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
                        if (playerPiece != null && groupPieces.Contains(playerPiece))
                        {
                            // 检查这个PathMover是否属于当前玩家
                            var playerPathMover = player.GetComponent<PathMover>();
                            if (playerPathMover == null)
                            {
                                playerPathMover = player.GetComponentInChildren<PathMover>();
                            }
                            
                            // 只更新属于当前玩家的PathMover
                            if (playerPathMover == pathMover)
                            {
                                pathMover.ForceUpdateGroupID(groupID);
                                Debug.Log($"[多玩家组更新] 玩家 {player.name} 的PathMover更新组号为：{groupID}");
                            }
                        }
                    }
                }
                catch (UnityException)
                {
                    continue;
                }
            }
            
            // 刷新当前PathMover的路径
            pathMover.RefreshPaths();
        }
    }

    private PuzzlePiece GetPlayerPiece()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;
        return player.GetComponentInParent<PuzzlePiece>();
    }

    /// <summary>
    /// 吸附成功后强制更新组号和路径（支持多玩家）
    /// </summary>
    private void ForceUpdateAfterSnap()
    {
        // ✅ 更新所有PathMover，而不仅仅是第一个
        UpdateAllPlayersAfterSnap();
    }

    /// <summary>
    /// ✅ 吸附后更新所有相关玩家的状态
    /// </summary>
    private void UpdateAllPlayersAfterSnap(PathMover specificPathMover = null)
    {
        // 获取所有可能的玩家标签
        string[] playerTags = { "Player", "Player1", "Player2", "Character", "Hero" };
        
        // ✅ 如果没有指定特定的PathMover，则更新所有PathMover
        PathMover[] pathMoversToUpdate;
        if (specificPathMover != null)
        {
            pathMoversToUpdate = new PathMover[] { specificPathMover };
        }
        else
        {
            pathMoversToUpdate = FindObjectsOfType<PathMover>();
        }
        
        foreach (var pathMover in pathMoversToUpdate)
        {
            foreach (var tag in playerTags)
            {
                try
                {
                    GameObject[] players = GameObject.FindGameObjectsWithTag(tag);
                    foreach (var player in players)
                    {
                        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
                        if (playerPiece != null)
                        {
                            // 检查这个PathMover是否属于当前玩家
                            var playerPathMover = player.GetComponent<PathMover>();
                            if (playerPathMover == null)
                            {
                                playerPathMover = player.GetComponentInChildren<PathMover>();
                            }
                            
                            // 只更新属于当前玩家的PathMover
                            if (playerPathMover == pathMover)
                            {
                                // 检查玩家是否与当前拼图或组相关
                                bool shouldUpdate = false;
                                
                                // 情况1：玩家在当前拼图上
                                if (playerPiece == this)
                                {
                                    shouldUpdate = true;
                                }
                                // 情况2：玩家在当前拼图组中
                                else if (currentGroup != null && currentGroup.pieces.Contains(playerPiece))
                                {
                                    shouldUpdate = true;
                                }
                                
                                if (shouldUpdate)
                                {
                                    pathMover.ForceUpdateGroupID(this.initialGroupID);
                                    Debug.Log($"[吸附后多玩家更新] 为玩家 {player.name} 的PathMover更新组号为：{this.initialGroupID}");
                                }
                            }
                        }
                    }
                }
                catch (UnityException)
                {
                    // 标签不存在，继续下一个
                    continue;
                }
            }
            
            // 刷新当前PathMover的路径
            pathMover.RefreshPaths();
        }
    }

    private bool TrySnapTo(PuzzlePiece other)
    {
        Vector3 mySize = GetComponent<Renderer>().bounds.size;
        Vector3 otherSize = other.GetComponent<Renderer>().bounds.size;

        Vector3 myPos = transform.position;
        Vector3 otherPos = other.transform.position;

        Vector3 offset = myPos - otherPos;

        float expectedX = (mySize.x + otherSize.x) / 2f;
        float expectedY = (mySize.y + otherSize.y) / 2f;

        bool snapped = false;

        // 检查水平连接
        float xDiff = Mathf.Abs(Mathf.Abs(offset.x) - expectedX);
        float yDiff = Mathf.Abs(offset.y);
        bool horizontalCheck = xDiff < snapTolerance && yDiff < snapTolerance;

        if (horizontalCheck)
        {
            float newX = otherPos.x + Mathf.Sign(offset.x) * expectedX;
            transform.position = new Vector3(newX, otherPos.y, myPos.z);
            snapped = true;
        }
        else
        {
            // 检查垂直连接
            float yDiff2 = Mathf.Abs(Mathf.Abs(offset.y) - expectedY);
            float xDiff2 = Mathf.Abs(offset.x);
            bool verticalCheck = yDiff2 < snapTolerance && xDiff2 < snapTolerance;

            if (verticalCheck)
            {
                float newY = otherPos.y + Mathf.Sign(offset.y) * expectedY;
                transform.position = new Vector3(otherPos.x, newY, myPos.z);
                snapped = true;
            }
        }

        if (snapped)
        {
            isSnapSuccessful = true;
            RefreshPath();
        }

        return snapped;
    }

    private PuzzleGroup DetectPlayerGroup()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        var piece = player.GetComponentInParent<PuzzlePiece>();
        return piece != null ? piece.currentGroup : null;
    }

    private bool IsPlayerOnThisPiece()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        return player.transform.IsChildOf(this.transform);
    }

    public void RefreshPath()
    {
        // ✅ 刷新所有PathMover的路径，而不是只有第一个
        PathMover[] allMovers = FindObjectsOfType<PathMover>();
        foreach (var mover in allMovers)
        {
            mover.RefreshPaths();
        }
        
        Debug.Log($"[RefreshPath] 已刷新 {allMovers.Length} 个PathMover的路径");
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 screenPos = Input.mousePosition;
        screenPos.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(screenPos);
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        Debug.Log($"[拼图] {(frozen ? "冻结" : "解冻")}：{gameObject.name}");
    }

    public void RotateSelf(float angleDegrees = 90f)
    {
        transform.Rotate(0, 0, angleDegrees);
        Debug.Log($"[PuzzlePiece] {name} 自身旋转 {angleDegrees} 度");
    }
}