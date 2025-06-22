using UnityEngine;
using System.Collections.Generic;

public class Dabu_PlayerController : MonoBehaviour
{
    public float moveSpeed = 2f;
    private int currentSegmentIndex = 0;
    private float t = 0f;
    private Vector2 moveInput;
    private bool isFollowingPuzzle = false;
    private Transform followedPuzzleTransform;
    private Vector2 localOffset;
    private bool isTransitioning = false;
    private Vector2 targetPosition;
    private float transitionSpeed = 5f;

    private Dabu_PuzzleManager manager => Dabu_PuzzleManager.Instance;

    void Start()
    {
        if (TryFindStartingSegment(out int segIndex, out float segT))
        {
            currentSegmentIndex = segIndex;
            t = segT;
            UpdatePosition();
        }
        else
        {
            Debug.LogWarning("Player not placed on any segment!");
        }
    }

    void Update()
    {
        if (manager == null || manager.mergedSegments.Count == 0) return;

        // 如果正在跟随拼图块，直接跟随移动
        if (isFollowingPuzzle && followedPuzzleTransform != null)
        {
            transform.position = followedPuzzleTransform.TransformPoint(localOffset);
            return;
        }

        // 如果正在过渡中，平滑移动到目标位置
        if (isTransitioning)
        {
            Vector2 currentPos = transform.position;
            Vector2 newPos = Vector2.MoveTowards(currentPos, targetPosition, transitionSpeed * Time.deltaTime);
            transform.position = newPos;
            
            if (Vector2.Distance(newPos, targetPosition) < 0.01f)
            {
                isTransitioning = false;
            }
            return;
        }

        moveInput = new Vector2(
            Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1 :
            Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? -1 : 0,

            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1 :
            Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? -1 : 0
        );

        if (moveInput == Vector2.zero) return;

        Vector2 start = manager.mergedNodePositions[manager.mergedSegments[currentSegmentIndex].startNodeIndex];
        Vector2 end = manager.mergedNodePositions[manager.mergedSegments[currentSegmentIndex].endNodeIndex];
        Vector2 dir = (end - start).normalized;
        
        // 检查是否有垂直方向的输入
        bool hasVerticalInput = Mathf.Abs(moveInput.y) > 0.1f;
        bool hasHorizontalInput = Mathf.Abs(moveInput.x) > 0.1f;
        
        // 如果当前路径是水平的，且有垂直输入，尝试切换
        if (Mathf.Abs(dir.x) > 0.7f && hasVerticalInput)
        {
            Debug.Log($"尝试从水平路径切换到垂直方向，输入: {moveInput}");
            if (TrySwitchToPerpendicularDirection())
            {
                Debug.Log("成功切换到垂直路径");
                return;
            }
            // 如果无法在节点切换，尝试从路径中间切换
            else if (TrySwitchFromPathMiddle())
            {
                Debug.Log("从路径中间成功切换到垂直路径");
                return;
            }
        }
        
        // 如果当前路径是垂直的，且有水平输入，尝试切换
        if (Mathf.Abs(dir.y) > 0.7f && hasHorizontalInput)
        {
            Debug.Log($"尝试从垂直路径切换到水平方向，输入: {moveInput}");
            if (TrySwitchToPerpendicularDirection())
            {
                Debug.Log("成功切换到水平路径");
                return;
            }
            // 如果无法在节点切换，尝试从路径中间切换
            else if (TrySwitchFromPathMiddle())
            {
                Debug.Log("从路径中间成功切换到水平路径");
                return;
            }
        }

        Vector2 desired = moveInput.normalized;
        float dot = Vector2.Dot(dir, desired);

        // 正常移动逻辑
        if (dot > 0.3f)
        {
            t += moveSpeed * Time.deltaTime;
        }
        else if (dot < -0.3f)
        {
            t -= moveSpeed * Time.deltaTime;
        }
        else
        {
            TrySwitchDirection();
            return;
        }

        if (t < 0f || t > 1f)
        {
            // 确保t值在有效范围内，防止跳跃
            t = Mathf.Clamp01(t);
            TrySwitchDirection();
        }
        else
        {
            UpdatePosition();
        }
    }

    void UpdatePosition()
    {
        var seg = manager.mergedSegments[currentSegmentIndex];
        Vector2 a = manager.mergedNodePositions[seg.startNodeIndex];
        Vector2 b = manager.mergedNodePositions[seg.endNodeIndex];
        transform.position = Vector2.Lerp(a, b, t);
    }

    void TrySwitchDirection()
    {
        var currentSeg = manager.mergedSegments[currentSegmentIndex];
        int atNodeIndex = (t < 0.5f) ? currentSeg.startNodeIndex : currentSeg.endNodeIndex;
        Vector2 currentPos = manager.mergedNodePositions[atNodeIndex];

        if (!manager.nodeToSegments.TryGetValue(atNodeIndex, out var connectedSegments)) return;

        // 检查玩家是否接近当前节点
        float distanceToNode = Vector2.Distance(transform.position, currentPos);
        float switchThreshold = 0.3f; // 距离阈值，只要在这个范围内就可以切换

        if (distanceToNode > switchThreshold) return;

        foreach (int nextSegIndex in connectedSegments)
        {
            if (nextSegIndex == currentSegmentIndex) continue;

            var seg = manager.mergedSegments[nextSegIndex];
            if (!seg.isActive) continue;

            int otherNode = (seg.startNodeIndex == atNodeIndex) ? seg.endNodeIndex : seg.startNodeIndex;
            Vector2 otherPos = manager.mergedNodePositions[otherNode];
            Vector2 direction = (otherPos - currentPos).normalized;

            // 降低方向匹配的阈值，让切换更容易
            if (Vector2.Dot(direction, moveInput.normalized) > 0.3f)
            {
                currentSegmentIndex = nextSegIndex;
                
                // 计算在新路径段上的正确位置
                Vector2 newSegStart = manager.mergedNodePositions[seg.startNodeIndex];
                Vector2 newSegEnd = manager.mergedNodePositions[seg.endNodeIndex];
                
                // 将玩家当前位置投影到新路径段上
                Vector2 projected = ProjectPointOnLineSegment(newSegStart, newSegEnd, transform.position, out float newT);
                t = newT;
                t = Mathf.Clamp01(t);
                
                // 使用平滑过渡
                targetPosition = projected;
                isTransitioning = true;
                
                return;
            }
        }

        t = Mathf.Clamp01(t);
        UpdatePosition();
    }

    public void FollowPuzzle(Transform puzzleTransform)
    {
        followedPuzzleTransform = puzzleTransform;
        localOffset = puzzleTransform.InverseTransformPoint(transform.position);
        isFollowingPuzzle = true;
    }

    public void StopFollowingPuzzle()
    {
        isFollowingPuzzle = false;
        followedPuzzleTransform = null;
    }

    private Vector2 ProjectPointOnLineSegment(Vector2 a, Vector2 b, Vector2 p, out float t)
    {
        Vector2 ab = b - a;
        t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }

    private bool TryFindStartingSegment(out int foundIndex, out float foundT)
    {
        Vector2 playerPos = transform.position;
        float closestDist = float.MaxValue;
        int bestIndex = -1;
        float bestT = 0f;

        for (int i = 0; i < manager.mergedSegments.Count; i++)
        {
            var seg = manager.mergedSegments[i];
            Vector2 a = manager.mergedNodePositions[seg.startNodeIndex];
            Vector2 b = manager.mergedNodePositions[seg.endNodeIndex];

            Vector2 projected = ProjectPointOnLineSegment(a, b, playerPos, out float tOnLine);
            float dist = Vector2.Distance(playerPos, projected);

            if (dist < 0.1f && dist < closestDist)
            {
                closestDist = dist;
                bestIndex = i;
                bestT = tOnLine;
            }
        }

        foundIndex = bestIndex;
        foundT = bestT;
        return bestIndex != -1;
    }

    public void UpdatePositionOnGraph()
    {
        if (TryFindStartingSegment(out int segIndex, out float segT))
        {
            currentSegmentIndex = segIndex;
            t = segT;
            UpdatePosition();
        }
        else
        {
            Debug.LogWarning("Player could not be placed on any segment after graph rebuild!");
        }
    }

    public bool IsOnThisPuzzle(Transform puzzle)
    {
        var manager = Dabu_PuzzleManager.Instance;
        if (manager == null) return false;

        // 获取当前路径段上靠近的节点
        var segment = manager.mergedSegments[currentSegmentIndex];
        int nodeIndex = (t < 0.5f) ? segment.startNodeIndex : segment.endNodeIndex;

        Vector2 nodeWorldPos = manager.mergedNodePositions[nodeIndex];

        // 将世界坐标转回拼图的 local 空间
        Vector2 localPos = puzzle.InverseTransformPoint(nodeWorldPos);

        var graph = puzzle.GetComponent<Dabu_PuzzleGraph>();
        foreach (var node in graph.nodes)
        {
            if (Vector2.Distance(node.position, localPos) < 0.01f)
                return true;
        }

        return false;
    }

    private bool TrySwitchToPerpendicularDirection()
    {
        var currentSeg = manager.mergedSegments[currentSegmentIndex];
        
        Debug.Log($"当前段 {currentSegmentIndex}: 从节点{currentSeg.startNodeIndex}到节点{currentSeg.endNodeIndex}");
        Debug.Log($"所有可用段数量: {manager.mergedSegments.Count}");
        
        // 检查当前路径的两个端点
        int startNodeIndex = currentSeg.startNodeIndex;
        int endNodeIndex = currentSeg.endNodeIndex;
        
        // 尝试从起点切换到垂直方向
        if (TrySwitchAtNode(startNodeIndex))
        {
            return true;
        }
        
        // 尝试从终点切换到垂直方向
        if (TrySwitchAtNode(endNodeIndex))
        {
            return true;
        }
        
        return false;
    }
    
    private bool TrySwitchAtNode(int nodeIndex)
    {
        if (!manager.nodeToSegments.TryGetValue(nodeIndex, out var connectedSegments)) 
            return false;

        Vector2 nodePos = manager.mergedNodePositions[nodeIndex];
        float distanceToNode = Vector2.Distance(transform.position, nodePos);
        float switchThreshold = 1.5f; // 增加阈值，让切换更容易

        Debug.Log($"检查节点 {nodeIndex}，距离: {distanceToNode}，阈值: {switchThreshold}");

        if (distanceToNode > switchThreshold) 
            return false;

        Debug.Log($"在节点 {nodeIndex} 附近，检查 {connectedSegments.Count} 个连接");

        foreach (int nextSegIndex in connectedSegments)
        {
            if (nextSegIndex == currentSegmentIndex) continue;

            var seg = manager.mergedSegments[nextSegIndex];
            if (!seg.isActive) continue;

            int otherNode = (seg.startNodeIndex == nodeIndex) ? seg.endNodeIndex : seg.startNodeIndex;
            Vector2 otherPos = manager.mergedNodePositions[otherNode];
            Vector2 direction = (otherPos - nodePos).normalized;

            Debug.Log($"检查段 {nextSegIndex}，方向: {direction}，输入: {moveInput}");
            Debug.Log($"段 {nextSegIndex}: 从节点{seg.startNodeIndex}到节点{seg.endNodeIndex}");

            // 检查输入方向是否与路径方向匹配
            bool inputMatchesDirection = false;
            
            // 检查垂直方向
            if (Mathf.Abs(direction.y) > 0.7f && Mathf.Abs(moveInput.y) > 0.1f)
            {
                inputMatchesDirection = (direction.y > 0 && moveInput.y > 0) || (direction.y < 0 && moveInput.y < 0);
                Debug.Log($"垂直方向检查: direction.y={direction.y}, moveInput.y={moveInput.y}, 匹配={inputMatchesDirection}");
            }
            // 检查水平方向
            else if (Mathf.Abs(direction.x) > 0.7f && Mathf.Abs(moveInput.x) > 0.1f)
            {
                // 修复水平方向匹配逻辑
                bool rightInput = moveInput.x > 0;
                bool rightDirection = direction.x > 0;
                inputMatchesDirection = (rightInput && rightDirection) || (!rightInput && !rightDirection);
                Debug.Log($"水平方向检查: direction.x={direction.x}, moveInput.x={moveInput.x}, 匹配={inputMatchesDirection}");
            }
            else
            {
                Debug.Log($"方向不匹配: 路径方向={direction}, 输入={moveInput}");
            }

            if (inputMatchesDirection)
            {
                Debug.Log($"成功切换到段 {nextSegIndex}");
                currentSegmentIndex = nextSegIndex;
                
                // 计算在新路径段上的正确位置
                Vector2 newSegStart = manager.mergedNodePositions[seg.startNodeIndex];
                Vector2 newSegEnd = manager.mergedNodePositions[seg.endNodeIndex];
                
                // 将玩家当前位置投影到新路径段上
                Vector2 projected = ProjectPointOnLineSegment(newSegStart, newSegEnd, transform.position, out float newT);
                t = newT;
                t = Mathf.Clamp01(t);
                
                // 使用平滑过渡
                targetPosition = projected;
                isTransitioning = true;
                
                return true;
            }
        }
        
        Debug.Log("没有找到匹配的方向");
        return false;
    }

    private bool TrySwitchFromPathMiddle()
    {
        // 检查所有可用的路径段，寻找垂直或水平方向的路径
        for (int i = 0; i < manager.mergedSegments.Count; i++)
        {
            if (i == currentSegmentIndex) continue;
            
            var seg = manager.mergedSegments[i];
            if (!seg.isActive) continue;
            
            Vector2 segStart = manager.mergedNodePositions[seg.startNodeIndex];
            Vector2 segEnd = manager.mergedNodePositions[seg.endNodeIndex];
            Vector2 segDir = (segEnd - segStart).normalized;
            
            // 检查是否是垂直路径且玩家有垂直输入
            bool isVerticalPath = Mathf.Abs(segDir.y) > 0.7f;
            bool hasVerticalInput = Mathf.Abs(moveInput.y) > 0.1f;
            
            // 检查是否是水平路径且玩家有水平输入
            bool isHorizontalPath = Mathf.Abs(segDir.x) > 0.7f;
            bool hasHorizontalInput = Mathf.Abs(moveInput.x) > 0.1f;
            
            if ((isVerticalPath && hasVerticalInput) || (isHorizontalPath && hasHorizontalInput))
            {
                // 检查玩家是否接近这个路径
                Vector2 projected = ProjectPointOnLineSegment(segStart, segEnd, transform.position, out float tOnLine);
                float distance = Vector2.Distance(transform.position, projected);
                
                if (distance < 0.5f)
                {
                    string pathType = isVerticalPath ? "垂直" : "水平";
                    Debug.Log($"找到{pathType}路径 {i}，距离: {distance}");
                    currentSegmentIndex = i;
                    
                    // 使用投影点计算t值，确保平滑过渡
                    t = tOnLine;
                    t = Mathf.Clamp01(t);
                    
                    // 使用平滑过渡
                    targetPosition = projected;
                    isTransitioning = true;
                    
                    return true;
                }
            }
        }
        
        return false;
    }
}
