using UnityEngine;
using System.Collections.Generic;

public class PathMover : MonoBehaviour
{
    [Header("起始节点（人物初始位置）")]
    public PathNode startNode;

    [Header("移动速度")]
    public float moveSpeed = 2f;

    private List<(Vector3 a, Vector3 b)> allPathSegments = new List<(Vector3, Vector3)>();

    private int currentGroupID = -1;

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

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (input.sqrMagnitude > 0.01f)
        {
            // ✅ 使用 X/Y 平面移动（适用于 2D 横版或俯视图）
            Vector3 moveDir = new Vector3(input.x, input.y, 0).normalized;
            Vector3 tryMove = moveDir * moveSpeed * Time.deltaTime;
            Vector3 candidatePos = transform.position + tryMove;

            if (FindClosestPathSegment(candidatePos, out Vector3 segA, out Vector3 segB))
            {
                // 吸附到路径上
                Vector3 projected = GetClosestPointOnSegment(candidatePos, segA, segB);
                transform.position = projected;

                Transform correctParent = GetPuzzlePieceParentFromSegment(segA, segB, out int groupID);
                if (correctParent != null && transform.parent != correctParent)
                {
                    transform.SetParent(correctParent);
                    currentGroupID = groupID;
                    Debug.Log($"[PathMover] 切换拼图组 ID：{currentGroupID}，新 Parent: {correctParent.name}");
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ReportPathStatus();
        }
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
                    Debug.Log($"[路径缓存] 添加路径段：{a} ↔ {b}");
                }
            }
        }

        Debug.Log($"[路径缓存] 总路径段数：{allPathSegments.Count}");
    }

    bool FindClosestPathSegment(Vector3 point, out Vector3 closestA, out Vector3 closestB)
    {
        float minDist = float.MaxValue;
        closestA = closestB = Vector3.zero;

        foreach (var seg in allPathSegments)
        {
            if (!IsPathInCurrentGroup(seg.a, seg.b)) continue;

            Vector3 projected = GetClosestPointOnSegment(point, seg.a, seg.b);
            float dist = Vector3.Distance(point, projected);

            if (dist < 0.3f && dist < minDist)
            {
                minDist = dist;
                closestA = seg.a;
                closestB = seg.b;
            }
        }

        return minDist < float.MaxValue;
    }

    Vector3 GetClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab.normalized) / ab.magnitude;
        t = Mathf.Clamp01(t);
        return a + ab * t;
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

        if (!sameGroup)
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

                bool isSame = (a == posA && b == posB) || (a == posB && b == posA);
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
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.1f);

        if (allPathSegments != null)
        {
            foreach (var seg in allPathSegments)
            {
                bool belongsToCurrentGroup = IsPathInCurrentGroup(seg.a, seg.b);
                Gizmos.color = belongsToCurrentGroup ? Color.green : new Color(0.5f, 0.5f, 0.5f, 0.3f);
                Gizmos.DrawLine(seg.a, seg.b);
            }
        }
    }
}