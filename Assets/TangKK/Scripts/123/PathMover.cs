using UnityEngine;
using System.Collections.Generic;

public class PathMover : MonoBehaviour
{
    [Header("起始节点（人物初始位置）")]
    public PathNode startNode;

    [Header("移动速度")]
    public float moveSpeed = 2f;

    private List<(Vector3 a, Vector3 b)> allPathSegments = new List<(Vector3, Vector3)>();

    void Awake()
    {
        if (startNode != null)
        {
            transform.position = startNode.transform.position;
        }

        RefreshPaths();
    }

    void Update()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 moveDir = input.normalized;
            Vector3 move = moveDir * moveSpeed * Time.deltaTime;
            Vector3 newPos = transform.position + move;

            if (IsOnAnyPath(newPos, out Vector3 segA, out Vector3 segB))
            {
                transform.position = newPos;

                // ✅ 切换 parent 到拼图块
                Transform correctParent = GetPuzzlePieceParentFromSegment(segA, segB);
                if (correctParent != null && transform.parent != correctParent)
                {
                    transform.SetParent(correctParent);
                }
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
            foreach (var path in node.connectedPaths)
            {
                if (path == null || path.targetNode == null) continue;

                Vector3 a = node.transform.position;
                Vector3 b = path.targetNode.transform.position;

                if (!allPathSegments.Exists(p =>
                    (p.a == a && p.b == b) || (p.a == b && p.b == a)))
                {
                    allPathSegments.Add((a, b));
                }
            }
        }
    }

    bool IsOnAnyPath(Vector3 point, out Vector3 a, out Vector3 b)
    {
        foreach (var seg in allPathSegments)
        {
            if (IsPointOnSegment(point, seg.a, seg.b, 0.1f))
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
        float proj = Vector3.Dot(ap, ab.normalized);

        if (proj < 0 || proj > abLen)
            return false;

        float distance = Vector3.Magnitude(ap - ab.normalized * proj);
        return distance < tolerance;
    }

    /// <summary>
    /// 返回路径段归属的拼图块（PuzzlePiece.transform）
    /// </summary>
    Transform GetPuzzlePieceParentFromSegment(Vector3 a, Vector3 b)
    {
        PathNode[] allNodes = FindObjectsOfType<PathNode>(true);

        foreach (var node in allNodes)
        {
            if (node == null || node.parentPiece == null) continue;

            foreach (var path in node.connectedPaths)
            {
                if (path == null || path.targetNode == null) continue;

                Vector3 posA = node.transform.position;
                Vector3 posB = path.targetNode.transform.position;

                bool isSame = (a == posA && b == posB) || (a == posB && b == posA);
                if (isSame)
                {
                    return node.parentPiece.transform; // ✅ 只返回拼图块
                }
            }
        }

        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.1f);
    }
}