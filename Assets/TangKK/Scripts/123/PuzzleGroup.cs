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

        // ✅ 保留世界位置，避免节点层级被改变影响 parentPiece
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

    public void AbsorbGroup(PuzzleGroup other)
    {
        foreach (var piece in other.pieces)
        {
            this.AddPiece(piece);
        }

        // ✅ 设置新组 ID
        int newGroupID = this.GetInstanceID();
        foreach (var p in this.pieces)
        {
            if (p.originalGroupID == 0)
                p.originalGroupID = p.initialGroupID;

            p.initialGroupID = newGroupID;
        }

        Destroy(other.gameObject);
    }
    public void RotateGroup(float angleDegrees = 90f)
    {
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
}