using System.Collections.Generic;
using UnityEngine;

public class PuzzleGroup : MonoBehaviour
{
    [Header("组 ID 设置")]
    public bool useCustomID = false;
    public int customID = 0;

    [HideInInspector] public int groupID;

    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    private void Awake()
    {
        groupID = useCustomID ? customID : GetInstanceID();
    }

    public void AddPiece(PuzzlePiece piece)
    {
        if (piece == null || pieces.Contains(piece)) return;

        pieces.Add(piece);
        piece.currentGroup = this;

        // 保留世界位置，避免节点层级被改变影响 parentPiece
        piece.transform.SetParent(this.transform, worldPositionStays: true);

        Vector3 closestPos = FindClosestSnapPosition(piece);
        piece.transform.localPosition = closestPos;

        Debug.Log($"[PuzzleGroup] 添加 {piece.name} 到组 {groupID}");
    }

    public void RemovePiece(PuzzlePiece piece)
    {
        if (piece == null || !pieces.Contains(piece)) return;

        pieces.Remove(piece);
        piece.transform.SetParent(null, worldPositionStays: true);
        piece.currentGroup = null;

        Debug.Log($"[PuzzleGroup] 从组 {groupID} 移除 {piece.name}");

        // ✅ 如果组为空，销毁该组
        if (pieces.Count == 0)
        {
            Destroy(gameObject);
            Debug.Log($"[PuzzleGroup] 组 {groupID} 已清空并销毁");
        }
    }

    public Vector3 FindClosestSnapPosition(PuzzlePiece piece)
    {
        Vector3 pos = piece.transform.localPosition;
        return new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), 0f);
    }

    /// <summary>
    /// ✅ 简化的组合并方法 - 现在主要由PuzzlePiece的ReorganizeConnectedPuzzles处理
    /// </summary>
    public void AbsorbGroup(PuzzleGroup other)
    {
        if (other == null || other == this) return;

        // 将另一个组的所有拼图加入当前组
        var otherPieces = new List<PuzzlePiece>(other.pieces); // 创建副本避免修改原列表时出错
        
        foreach (var piece in otherPieces)
        {
            this.AddPiece(piece);
        }

        // 销毁空组
        if (other.pieces.Count == 0)
        {
            Destroy(other.gameObject);
        }

        Debug.Log($"[PuzzleGroup] 组 {other.groupID} 被吸收到组 {groupID}");
    }

    public void RotateGroup(float angleDegrees = 90f)
    {
        if (pieces.Count == 0) return;

        Vector3 center = Vector3.zero;
        foreach (var piece in pieces)
        {
            center += piece.transform.position;
        }
        center /= pieces.Count;

        foreach (var piece in pieces)
        {
            Vector3 dir = piece.transform.position - center;
            dir = Quaternion.Euler(0, 0, angleDegrees) * dir;
            piece.transform.position = center + dir;
            piece.transform.Rotate(0, 0, angleDegrees);
        }

        Debug.Log($"[PuzzleGroup] 组 {groupID} 旋转 {angleDegrees} 度");
    }

    /// <summary>
    /// ✅ 新增：检查组内所有拼图是否物理连接
    /// </summary>
    public bool AreAllPiecesConnected()
    {
        if (pieces.Count <= 1) return true;

        // 使用第一个拼图作为起点，检查是否能通过物理连接到达所有拼图
        var visited = new HashSet<PuzzlePiece>();
        var toVisit = new Queue<PuzzlePiece>();
        
        toVisit.Enqueue(pieces[0]);
        visited.Add(pieces[0]);

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            
            foreach (var other in pieces)
            {
                if (!visited.Contains(other) && current.IsPhysicallyConnected(current, other))
                {
                    visited.Add(other);
                    toVisit.Enqueue(other);
                }
            }
        }

        bool allConnected = visited.Count == pieces.Count;
        if (!allConnected)
        {
            Debug.LogWarning($"[PuzzleGroup] 组 {groupID} 中有 {pieces.Count - visited.Count} 个拼图未连接");
        }

        return allConnected;
    }
}