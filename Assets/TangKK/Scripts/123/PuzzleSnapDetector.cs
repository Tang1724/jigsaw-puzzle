using UnityEngine;
using System.Collections.Generic;

public class PuzzleSnapDetector : MonoBehaviour
{
    [Header("吸附设置")]
    public float snapDistance = 2.5f;
    public float snapAngle = 15f;
    
    [Header("调试设置")]
    public bool showSnapDebug = true;
    
    private PuzzlePiece thisPuzzle;
    private PuzzlePiece currentSnapTarget;
    private ConnectionPoint currentMyPoint;
    private ConnectionPoint currentTargetPoint;
    private Vector3 currentSnapPosition;
    
    void Start()
    {
        thisPuzzle = GetComponent<PuzzlePiece>();
    }
    
    public void CheckForSnap()
    {
        if (thisPuzzle == null) return;
        
        ClearCurrentSnap();
        
        var allPuzzles = FindObjectsOfType<PuzzlePiece>();
        float closestDistance = float.MaxValue;
        PuzzlePiece bestTarget = null;
        ConnectionPoint bestMyPoint = null;
        ConnectionPoint bestTargetPoint = null;
        Vector3 bestSnapPosition = Vector3.zero;
        
        foreach (var otherPuzzle in allPuzzles)
        {
            if (otherPuzzle == thisPuzzle) continue;
            
            var result = FindBestConnectionWith(otherPuzzle);
            if (result.canConnect && result.distance < closestDistance && result.distance <= snapDistance)
            {
                closestDistance = result.distance;
                bestTarget = otherPuzzle;
                bestMyPoint = result.myPoint;
                bestTargetPoint = result.targetPoint;
                bestSnapPosition = result.snapPosition;
            }
        }
        
        if (bestTarget != null)
        {
            ShowSnapPreview(bestTarget, bestMyPoint, bestTargetPoint, bestSnapPosition);
        }
    }
    
    ConnectionResult FindBestConnectionWith(PuzzlePiece otherPuzzle)
    {
        var result = new ConnectionResult { canConnect = false, distance = float.MaxValue };
        
        var myAvailablePoints = thisPuzzle.GetAvailableConnectionPoints();
        var targetAvailablePoints = otherPuzzle.GetAvailableConnectionPoints();
        
        foreach (var myPoint in myAvailablePoints)
        {
            foreach (var targetPoint in targetAvailablePoints)
            {
                if (CanPointsConnect(myPoint, targetPoint))
                {
                    Vector3 myWorldPos = transform.position + (Vector3)myPoint.localPosition;
                    Vector3 targetWorldPos = otherPuzzle.transform.position + (Vector3)targetPoint.localPosition;
                    float distance = Vector3.Distance(myWorldPos, targetWorldPos);
                    
                    if (distance < result.distance)
                    {
                        result.canConnect = true;
                        result.distance = distance;
                        result.myPoint = myPoint;
                        result.targetPoint = targetPoint;
                        result.snapPosition = CalculateSnapPosition(myPoint, targetPoint, otherPuzzle);
                    }
                }
            }
        }
        
        return result;
    }
    
    bool CanPointsConnect(ConnectionPoint point1, ConnectionPoint point2)
    {
        return AreOppositeDirections(point1.direction, point2.direction);
    }
    
    bool AreOppositeDirections(ConnectionDirection dir1, ConnectionDirection dir2)
    {
        return (dir1 == ConnectionDirection.Up && dir2 == ConnectionDirection.Down) ||
               (dir1 == ConnectionDirection.Down && dir2 == ConnectionDirection.Up) ||
               (dir1 == ConnectionDirection.Left && dir2 == ConnectionDirection.Right) ||
               (dir1 == ConnectionDirection.Right && dir2 == ConnectionDirection.Left);
    }
    
    Vector3 CalculateSnapPosition(ConnectionPoint myPoint, ConnectionPoint targetPoint, PuzzlePiece targetPuzzle)
    {
        Vector3 targetPointWorldPos = targetPuzzle.transform.position + (Vector3)targetPoint.localPosition;
        Vector3 myPointLocalPos = myPoint.localPosition;
        
        return targetPointWorldPos - myPointLocalPos;
    }
    
    void ShowSnapPreview(PuzzlePiece target, ConnectionPoint myPoint, ConnectionPoint targetPoint, Vector3 snapPosition)
    {
        currentSnapTarget = target;
        currentMyPoint = myPoint;
        currentTargetPoint = targetPoint;
        currentSnapPosition = snapPosition;
        
        thisPuzzle.SetSnapPreview(true);
        target.SetSnapPreview(true);
    }
    
    void ClearCurrentSnap()
    {
        if (currentSnapTarget != null)
        {
            thisPuzzle.SetSnapPreview(false);
            currentSnapTarget.SetSnapPreview(false);
            currentSnapTarget = null;
            currentMyPoint = null;
            currentTargetPoint = null;
        }
    }
    
    public void TryAutoConnect()
    {
        if (currentSnapTarget != null && currentMyPoint != null && currentTargetPoint != null)
        {
            if (showSnapDebug)
            {
                Debug.Log($"🔧 开始自动吸附连接: {thisPuzzle.puzzleID} -> {currentSnapTarget.puzzleID}");
            }
            
            // 计算移动距离
            Vector3 oldPosition = transform.position;
            Vector3 newPosition = currentSnapPosition;
            Vector3 movement = newPosition - oldPosition;
            
            if (showSnapDebug)
            {
                Debug.Log($"📍 拼图移动: {oldPosition} -> {newPosition}");
                Debug.Log($"📏 移动距离: {movement}");
            }
            
            // 🔑 关键：直接使用现有的拖拽跟随信息，而不是重新记录
            // 因为此时玩家跟随信息还在 playersFollowInfo 中
            
            // 移动拼图到吸附位置
            transform.position = newPosition;
            
            // 立即同步更新跟随的玩家位置
            thisPuzzle.UpdateFollowingPlayersWithMovement(movement);
            
            // 建立连接
            bool connected = thisPuzzle.ConnectTo(currentSnapTarget, currentMyPoint, currentTargetPoint);
            
            if (showSnapDebug)
            {
                if (connected)
                {
                    Debug.Log($"✅ 自动连接成功: {thisPuzzle.puzzleID} <-> {currentSnapTarget.puzzleID}");
                }
                else
                {
                    Debug.Log($"❌ 自动连接失败: {thisPuzzle.puzzleID} <-> {currentSnapTarget.puzzleID}");
                }
            }
        }
        
        ClearCurrentSnap();
    }
}