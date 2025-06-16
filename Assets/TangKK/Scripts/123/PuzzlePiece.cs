using UnityEngine;

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
            // ✅ 记录原来组中的所有拼图块，用于后续恢复
            var originalGroupPieces = new System.Collections.Generic.List<PuzzlePiece>(currentGroup.pieces);

            currentGroup.RemovePiece(this);
            currentGroup = null;

            // ✅ 检查剩余拼图块是否仍然在同一组中（更可靠的方法）
            if (originalGroupPieces.Count > 1)
            {
                var remainingPieces = new System.Collections.Generic.List<PuzzlePiece>();
                foreach (var piece in originalGroupPieces)
                {
                    if (piece != this && piece.currentGroup != null)
                    {
                        remainingPieces.Add(piece);
                    }
                }

                // ✅ 使用组关系检测，而不是物理距离
                if (remainingPieces.Count > 0)
                {
                    // 如果剩余拼图还在同一个组中，说明它们仍然连接
                    var firstPieceGroup = remainingPieces[0].currentGroup;
                    bool allInSameGroup = true;

                    foreach (var piece in remainingPieces)
                    {
                        if (piece.currentGroup != firstPieceGroup)
                        {
                            allInSameGroup = false;
                            break;
                        }
                    }

                    // 如果不是所有拼图都在同一组中，说明有分离，需要重新处理各个组
                    if (!allInSameGroup)
                    {
                        var processedGroups = new System.Collections.Generic.HashSet<PuzzleGroup>();

                        foreach (var piece in remainingPieces)
                        {
                            var group = piece.currentGroup;
                            if (group != null && !processedGroups.Contains(group))
                            {
                                processedGroups.Add(group);

                                // 如果组中只有一个拼图，恢复原组号
                                if (group.pieces.Count == 1)
                                {
                                    piece.initialGroupID = piece.originalGroupID;
                                    Debug.Log($"[拼图分离] 单独拼图 {piece.name} 恢复原组号：{piece.originalGroupID}");
                                }
                            }
                        }
                    }
                }
            }

            RefreshPath();
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

        bool isConnected = horizontalConnect || verticalConnect;

        if (isConnected)
        {
            Debug.Log($"[物理连接检测] {a.name} 与 {b.name} 仍然连接");
        }

        return isConnected;
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

        // ✅ 重置吸附状态 - 但不要过早重置，等检查完所有拼图后再决定
        bool foundSnap = false;

        PuzzlePiece[] allPieces = FindObjectsOfType<PuzzlePiece>();
        Debug.Log($"[OnMouseUp] 找到 {allPieces.Length} 个拼图块进行检查");

        foreach (var other in allPieces)
        {
            if (other == this) continue;

            Debug.Log($"[OnMouseUp] 检查与 {other.name} 的吸附可能性");
            if (TrySnapTo(other))
            {
                // ✅ 吸附成功，isSnapSuccessful已经在TrySnapTo中设置为true
                foundSnap = true;
                Debug.Log($"[吸附成功] {name} 成功吸附到 {other.name}");

                CombineWith(other);

                // ✅ 吸附成功后强制更新组号和路径
                ForceUpdateAfterSnap();
                return;
            }
        }

        // ✅ 如果没有找到吸附，才设置为false
        if (!foundSnap)
        {
            isSnapSuccessful = false;
            Debug.Log($"[吸附失败] {name} 没有找到可吸附的拼图");
        }

        // ✅ 拖动后未拼接 → 确保拼图有组
        if (currentGroup == null)
        {
            GameObject g = new GameObject("PuzzleGroup");
            var newGroup = g.AddComponent<PuzzleGroup>();
            newGroup.AddPiece(this);

            // ✅ 恢复原始组 ID
            this.initialGroupID = this.originalGroupID;

            Debug.Log($"[拼图独立] 拼图 {name} 创建独立组，组 ID：{initialGroupID}");
        }

        RefreshPath();
    }

    /// <summary>
    /// 吸附成功后强制更新组号和路径
    /// </summary>
    private void ForceUpdateAfterSnap()
    {
        var pathMover = FindObjectOfType<PathMover>();
        if (pathMover != null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var playerPiece = player.GetComponentInParent<PuzzlePiece>();
                if (playerPiece == this || (currentGroup != null && currentGroup.pieces.Contains(playerPiece)))
                {
                    // 强制更新PathMover的组号
                    pathMover.ForceUpdateGroupID(this.initialGroupID);
                    Debug.Log($"[吸附后更新] 强制更新PathMover组号为：{this.initialGroupID}");
                }
            }

            // 刷新路径
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

        Debug.Log($"[TrySnapTo] {name} → {other.name}: 偏移={offset}, 期望距离X={expectedX}, Y={expectedY}, 容差={snapTolerance}");

        bool snapped = false;

        // 检查水平连接（左右相邻）
        float xDiff = Mathf.Abs(Mathf.Abs(offset.x) - expectedX);
        float yDiff = Mathf.Abs(offset.y);
        bool horizontalCheck = xDiff < snapTolerance && yDiff < snapTolerance;

        Debug.Log($"[TrySnapTo] 水平检查: X差值={xDiff}, Y差值={yDiff}, 水平条件={horizontalCheck}");

        if (horizontalCheck)
        {
            float newX = otherPos.x + Mathf.Sign(offset.x) * expectedX;
            transform.position = new Vector3(newX, otherPos.y, myPos.z);
            snapped = true;
            Debug.Log($"[TrySnapTo] 水平吸附成功！新位置: {transform.position}");
        }
        else
        {
            // 检查垂直连接（上下相邻）
            float yDiff2 = Mathf.Abs(Mathf.Abs(offset.y) - expectedY);
            float xDiff2 = Mathf.Abs(offset.x);
            bool verticalCheck = yDiff2 < snapTolerance && xDiff2 < snapTolerance;

            Debug.Log($"[TrySnapTo] 垂直检查: Y差值={yDiff2}, X差值={xDiff2}, 垂直条件={verticalCheck}");

            if (verticalCheck)
            {
                float newY = otherPos.y + Mathf.Sign(offset.y) * expectedY;
                transform.position = new Vector3(otherPos.x, newY, myPos.z);
                snapped = true;
                Debug.Log($"[TrySnapTo] 垂直吸附成功！新位置: {transform.position}");
            }
        }

        // ✅ 直接在这里设置吸附成功状态
        if (snapped)
        {
            isSnapSuccessful = true;
            Debug.Log($"[TrySnapTo] 设置 isSnapSuccessful = true");
            RefreshPath();
        }
        else
        {
            Debug.Log($"[TrySnapTo] 吸附失败，所有条件都不满足");
        }

        return snapped;
    }

    private void CombineWith(PuzzlePiece other)
    {
        PuzzleGroup groupA = this.currentGroup;
        PuzzleGroup groupB = other.currentGroup;
        PuzzleGroup playerGroup = DetectPlayerGroup();
        PuzzleGroup targetGroup = null;

        // 1. 创建新组或吸收已有组
        if (groupA == null && groupB == null)
        {
            GameObject g = new GameObject("PuzzleGroup");
            targetGroup = g.AddComponent<PuzzleGroup>();
            targetGroup.AddPiece(this);
            targetGroup.AddPiece(other);
        }
        else if (groupA == null && groupB != null)
        {
            groupB.AddPiece(this);
            targetGroup = groupB;
        }
        else if (groupA != null && groupB == null)
        {
            groupA.AddPiece(other);
            targetGroup = groupA;
        }
        else if (groupA != null && groupB != null)
        {
            if (groupA != groupB)
            {
                // 优先合并到玩家所在组
                if (playerGroup == groupA)
                {
                    groupA.AbsorbGroup(groupB);
                    targetGroup = groupA;
                }
                else if (playerGroup == groupB)
                {
                    groupB.AbsorbGroup(groupA);
                    targetGroup = groupB;
                }
                else
                {
                    groupA.AbsorbGroup(groupB);
                    targetGroup = groupA;
                }
            }
            else
            {
                targetGroup = groupA;
            }
        }

        // 2. 吸附到最近点
        if (targetGroup != null)
        {
            Vector3 closestPos = targetGroup.FindClosestSnapPosition(this);
            transform.localPosition = closestPos;

            // 3. 为整个组分配统一的新组 ID
            int newGroupID = targetGroup.GetInstanceID();

            foreach (var p in targetGroup.pieces)
            {
                if (p.originalGroupID == 0)
                    p.originalGroupID = p.initialGroupID;

                p.initialGroupID = newGroupID;
            }

            Debug.Log($"[拼图合并] 成功合并为新组：{newGroupID}，拼图数：{targetGroup.pieces.Count}");

            // 4. 强制刷新 PathMover 的组号（如果玩家在这组）
            var pathMover = FindObjectOfType<PathMover>();
            if (pathMover != null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    var playerPiece = player.GetComponentInParent<PuzzlePiece>();
                    if (playerPiece != null && targetGroup.pieces.Contains(playerPiece))
                    {
                        pathMover.ForceUpdateGroupID(newGroupID);
                        Debug.Log($"[强制更新] PathMover组号更新为：{newGroupID}");
                    }
                }
            }
        }

        RefreshPath();
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

    private void RefreshPath()
    {
        var mover = FindObjectOfType<PathMover>();
        if (mover != null)
        {
            mover.RefreshPaths();
        }
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