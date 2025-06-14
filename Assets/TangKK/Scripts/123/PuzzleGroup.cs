using System.Collections.Generic;
using UnityEngine;

public class PuzzleGroup : MonoBehaviour
{
    public List<PuzzlePiece> pieces = new List<PuzzlePiece>();

    public void AddPiece(PuzzlePiece piece)
    {
        pieces.Add(piece);
        piece.currentGroup = this;
        piece.transform.SetParent(this.transform);

        // 自动吸附对齐（局部坐标对齐）
        Vector3 closestPos = FindClosestSnapPosition(piece);
        piece.transform.localPosition = closestPos;
    }

    public void RemovePiece(PuzzlePiece piece)
    {
        pieces.Remove(piece);
        piece.transform.SetParent(null);
    }

    // 吸附到最近整数网格
    Vector3 FindClosestSnapPosition(PuzzlePiece piece)
    {
        Vector3 pos = piece.transform.localPosition;
        return new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), 0f);
    }
}