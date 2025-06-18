using UnityEngine;
using System.Collections.Generic;

public class SmoothPathMover : MonoBehaviour
{
    [Header("起始节点（人物初始位置）")]
    public PathNode startNode;

    [Header("移动速度")]
    public float moveSpeed = 2f;

    [Header("路径智能移动设置")]
    [SerializeField] private float pathDetectionRadius = 0.2f;
    [SerializeField] private float directionThreshold = 0.3f;
    [SerializeField] private bool enableSmartMovement = true;
    [SerializeField] private bool showDebugInfo = false;

    private List<(Vector3 a, Vector3 b)> allPathSegments = new List<(Vector3, Vector3)>();
    private int currentGroupID = -1;

    private Vector3 currentSegmentStart;
    private Vector3 currentSegmentEnd;
    private bool isOnPath = false;
    private Vector3 lastValidDirection = Vector3.zero;

    public void ForceUpdateGroupID(int newGroupID)
    {
        if (newGroupID != currentGroupID)
        {
            currentGroupID = newGroupID;
            Debug.Log($"[PathMover] 强制更新组号为：{currentGroupID}");
            RefreshPaths();
        }
    }

    void Awake()
    {
        if (startNode != null)
        {
            transform.position = startNode.transform.position;

            var piece = startNode.parentPiece;
            if (piece != null)
            {
                currentGroupID = piece.GroupID;
                Debug.Log($"[Init] 起始拼图组 ID：{currentGroupID}");
            }
        }

        RefreshPaths();
    }

    void Update()
    {
        UpdateCurrentGroupID();

        Vector2 rawInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (rawInput.sqrMagnitude > 0.01f)
        {
            Vector3 desiredMoveDirection = enableSmartMovement
                ? CalculateSmartMoveDirection(rawInput)
                : rawInput.normalized;

            if (desiredMoveDirection != Vector3.zero)
            {
                Vector3 move = desiredMoveDirection * moveSpeed * Time.deltaTime;
                Vector3 newPos = transform.position + move;

                if (IsOnAnyPath(newPos, out Vector3 segA, out Vector3 segB))
                {
                    transform.position = newPos;
                    currentSegmentStart = segA;
                    currentSegmentEnd = segB;
                    isOnPath = true;

                    Transform correctParent = GetPuzzlePieceParentFromSegment(segA, segB, out int groupID);
                    if (correctParent != null && transform.parent != correctParent)
                    {
                        transform.SetParent(correctParent);
                        currentGroupID = groupID;
                        Debug.Log($"[PathMover] 切换拼图组 ID：{currentGroupID}，新 Parent: {correctParent.name}");
                    }
                }
                else
                {
                    isOnPath = false;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ReportPathStatus();
        }
    }

    Vector3 CalculateSmartMoveDirection(Vector2 rawInput)
    {
        if (!isOnPath)
            return rawInput.normalized;

        Vector3 pathDirection = (currentSegmentEnd - currentSegmentStart).normalized;
        Vector3 primaryInputDirection = GetPrimaryInputDirection(rawInput);

        float forwardDot = Vector3.Dot(primaryInputDirection, pathDirection);
        float backwardDot = Vector3.Dot(primaryInputDirection, -pathDirection);

        if (showDebugInfo)
        {
            Debug.Log($"路径方向: {pathDirection}, 输入方向: {primaryInputDirection}, 正向匹配: {forwardDot:F2}, 反向匹配: {backwardDot:F2}");
        }

        if (forwardDot > directionThreshold)
        {
            lastValidDirection = pathDirection;
            return pathDirection;
        }
        else if (backwardDot > directionThreshold)
        {
            lastValidDirection = -pathDirection;
            return -pathDirection;
        }

        Vector3 switchDirection = CheckForPathSwitch(rawInput);
        if (switchDirection != Vector3.zero)
        {
            return switchDirection;
        }

        if (lastValidDirection != Vector3.zero)
        {
            float lastDirectionDot = Vector3.Dot(primaryInputDirection, lastValidDirection);
            if (lastDirectionDot > 0.1f)
            {
                return lastValidDirection;
            }
        }

        return pathDirection;
    }

    Vector3 GetPrimaryInputDirection(Vector2 rawInput)
    {
        if (rawInput.magnitude < 0.1f)
            return Vector3.zero;

        if (Mathf.Abs(rawInput.x) > Mathf.Abs(rawInput.y))
            return rawInput.x > 0 ? Vector3.right : Vector3.left;
        else
            return rawInput.y > 0 ? Vector3.up : Vector3.down;
    }

    Vector3 CheckForPathSwitch(Vector2 rawInput)
    {
        Vector3 currentPos = transform.position;
        Vector3 inputDirection = rawInput.normalized;

        foreach (var segment in allPathSegments)
        {
            if (!IsPathInCurrentGroup(segment.a, segment.b)) continue;

            // ✅ 过滤灰色路径段
            if (!IsSegmentActive(segment.a, segment.b)) continue;

            if ((segment.a == currentSegmentStart && segment.b == currentSegmentEnd) ||
                (segment.a == currentSegmentEnd && segment.b == currentSegmentStart))
                continue;

            float distToA = Vector3.Distance(currentPos, segment.a);
            float distToB = Vector3.Distance(currentPos, segment.b);

            if (distToA < pathDetectionRadius || distToB < pathDetectionRadius)
            {
                Vector3 dir = (segment.b - segment.a).normalized;
                float forwardMatch = Vector3.Dot(inputDirection, dir);
                float backwardMatch = Vector3.Dot(inputDirection, -dir);

                if (forwardMatch > directionThreshold)
                    return dir;
                else if (backwardMatch > directionThreshold)
                    return -dir;
            }
        }

        return Vector3.zero;
    }

    /// ✅ 新增：判断路径段是否激活
    private bool IsSegmentActive(Vector3 a, Vector3 b)
    {
        PathNode nodeA = FindClosestNode(a);
        PathNode nodeB = FindClosestNode(b);
        if (nodeA == null || nodeB == null) return false;

        foreach (var path in nodeA.connectedPaths)
        {
            if (path.targetNode == nodeB)
            {
                return path.isActive;
            }
        }

        return false;
    }

    void UpdateCurrentGroupID()
    {
        var currentPiece = GetComponentInParent<PuzzlePiece>();
        if (currentPiece != null)
        {
            int newGroupID = currentPiece.GroupID;
            if (newGroupID != currentGroupID)
            {
                currentGroupID = newGroupID;
                Debug.Log($"[PathMover] 检测到组号变化：{currentGroupID}");
                RefreshPaths();
            }
        }
    }

    public void RefreshPaths()
    {
        CacheAllPathSegments();
    }

    void CacheAllPathSegments()
    {
        allPathSegments.Clear();

        PathNode[] allNodes = FindObjectsOfType<PathNode>(true);

        foreach (var node in allNodes)
        {
            if (node.parentPiece != null && node.parentPiece.isFrozen)
                continue;

            foreach (var path in node.connectedPaths)
            {
                if (path == null || path.targetNode == null) continue;
                if (!path.isActive) continue;
                if (path.targetNode.parentPiece != null && path.targetNode.parentPiece.isFrozen)
                    continue;

                Vector3 a = node.transform.position;
                Vector3 b = path.targetNode.transform.position;

                if (!allPathSegments.Exists(p => (p.a == a && p.b == b) || (p.a == b && p.b == a)))
                {
                    allPathSegments.Add((a, b));
                    if (showDebugInfo)
                    {
                        Debug.Log($"[路径缓存] 添加路径段：{a} ↔ {b}");
                    }
                }
            }
        }

        Debug.Log($"[路径缓存] 总路径段数：{allPathSegments.Count}");
    }

    /// ✅ 修复点：过滤灰色路径段
    bool IsOnAnyPath(Vector3 point, out Vector3 a, out Vector3 b)
    {
        foreach (var seg in allPathSegments)
        {
            if (!IsPathInCurrentGroup(seg.a, seg.b)) continue;

            if (!IsSegmentActive(seg.a, seg.b)) continue; // ✅ 关键过滤点

            if (IsPointOnSegment(point, seg.a, seg.b, pathDetectionRadius))
            {
                a = seg.a;
                b = seg.b;
                return true;
            }
        }

        a = b = Vector3.zero;
        return false;
    }

    bool IsPointOnSegment(Vector3 point, Vector3 a, Vector3 b, float tolerance = 0.1f)
    {
        Vector3 ab = b - a;
        Vector3 ap = point - a;

        float abLen = ab.magnitude;
        if (abLen < 0.001f) return false;

        float proj = Vector3.Dot(ap, ab.normalized);
        float extendedLength = abLen + tolerance * 2;

        if (proj < -tolerance || proj > extendedLength)
            return false;

        Vector3 closestPointOnLine = a + ab.normalized * Mathf.Clamp(proj, 0, abLen);
        float distance = Vector3.Distance(point, closestPointOnLine);

        return distance <= tolerance;
    }

    bool IsPathInCurrentGroup(Vector3 posA, Vector3 posB)
    {
        PathNode nodeA = FindClosestNode(posA);
        PathNode nodeB = FindClosestNode(posB);

        if (nodeA == null || nodeB == null) return false;

        var pieceA = nodeA.parentPiece;
        var pieceB = nodeB.parentPiece;

        if (pieceA == null || pieceB == null) return false;

        bool sameGroup = pieceA.initialGroupID == currentGroupID && pieceB.initialGroupID == currentGroupID;

        if (!sameGroup && showDebugInfo)
        {
            Debug.Log($"[过滤路径] 不属于当前组：{pieceA.initialGroupID} / {pieceB.initialGroupID} ≠ 当前 {currentGroupID}");
        }

        return sameGroup;
    }

    Transform GetPuzzlePieceParentFromSegment(Vector3 a, Vector3 b, out int groupID)
    {
        PathNode[] allNodes = FindObjectsOfType<PathNode>(true);
        groupID = -1;

        foreach (var node in allNodes)
        {
            if (node == null || node.parentPiece == null) continue;
            if (node.parentPiece.isFrozen) continue;

            foreach (var path in node.connectedPaths)
            {
                if (path == null || path.targetNode == null) continue;
                if (!path.isActive) continue;

                Vector3 posA = node.transform.position;
                Vector3 posB = path.targetNode.transform.position;

                bool isSame = (Vector3.Distance(a, posA) < 0.01f && Vector3.Distance(b, posB) < 0.01f) ||
                              (Vector3.Distance(a, posB) < 0.01f && Vector3.Distance(b, posA) < 0.01f);

                if (isSame)
                {
                    groupID = node.parentPiece.GroupID;
                    return node.parentPiece.transform;
                }
            }
        }

        return null;
    }

    PathNode FindClosestNode(Vector3 position)
    {
        PathNode closest = null;
        float minDist = float.MaxValue;

        foreach (var node in FindObjectsOfType<PathNode>())
        {
            float dist = Vector3.Distance(position, node.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = node;
            }
        }

        return closest;
    }

    void ReportPathStatus()
    {
        Debug.Log($"[PathDebug] 当前组 ID：{currentGroupID}，路径段数：{allPathSegments.Count}");
        Debug.Log($"[PathDebug] 在路径上：{isOnPath}，智能移动：{enableSmartMovement}");
        if (isOnPath)
        {
            Debug.Log($"[PathDebug] 当前路径段：{currentSegmentStart} → {currentSegmentEnd}");
        }
    }
}