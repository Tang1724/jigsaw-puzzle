using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 基于线条路径的玩家移动控制器
/// </summary>
public class LinePlayerController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float nodeStopDuration = 0.2f; // 在节点处停顿的时间
    
    [Header("输入设置")]
    [SerializeField] private float inputDeadzone = 0.3f;
    [SerializeField] private bool allowDiagonalOnDiagonalLines = true;
    
    [Header("线条系统引用")]
    [SerializeField] private LineController lineController;
    [SerializeField] private Transform[] allNodes; // 所有节点的引用
    
    [Header("调试")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool enableDebugLogs = false;
    
    // 当前状态
    private Transform currentNode; // 当前所在的节点
    private Transform targetNode;  // 目标节点
    private bool isMoving = false;
    private bool isAtNode = true;
    private bool canMove = true;   // 是否可以移动（用于节点停顿）
    
    // 输入状态
    private Vector2 inputDirection;
    private Vector2 lastValidInput;
    
    // 移动状态
    private Coroutine moveCoroutine;
    private Coroutine nodeStopCoroutine;
    
    public bool IsMoving => isMoving;
    public bool IsAtNode => isAtNode;
    public Transform CurrentNode => currentNode;
    
    private void Start()
    {
        InitializePlayer();
    }
    
    private void Update()
    {
        HandleInput();
        ProcessMovement();
    }
    
    /// <summary>
    /// 初始化玩家
    /// </summary>
    private void InitializePlayer()
    {
        // 如果没有指定线条控制器，尝试自动查找
        if (lineController == null)
        {
            lineController = FindObjectOfType<LineController>();
        }
        
        if (lineController == null)
        {
            Debug.LogError("[LinePlayerController] 未找到 LineController！");
            return;
        }
        
        // 将玩家放置到最近的节点
        SnapToNearestNode();
    }
    
    /// <summary>
    /// 处理输入
    /// </summary>
    private void HandleInput()
    {
        // 获取WASD输入
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        
        inputDirection = new Vector2(horizontal, vertical);
        
        // 应用死区
        if (inputDirection.magnitude > inputDeadzone)
        {
            lastValidInput = inputDirection.normalized;
        }
        else
        {
            inputDirection = Vector2.zero;
        }
    }
    
    /// <summary>
    /// 处理移动逻辑
    /// </summary>
    private void ProcessMovement()
    {
        // 如果正在移动或者不能移动，则不处理新的移动请求
        if (isMoving || !canMove || inputDirection.magnitude < inputDeadzone)
            return;
        
        // 只有在节点处才能开始新的移动
        if (isAtNode && currentNode != null)
        {
            TryMoveFromCurrentNode();
        }
    }
    
    /// <summary>
    /// 尝试从当前节点移动
    /// </summary>
    private void TryMoveFromCurrentNode()
    {
        // 获取当前节点连接的所有节点
        List<Transform> connectedNodes = lineController.GetConnectedNodes(currentNode);
        
        if (connectedNodes.Count == 0)
        {
            if (enableDebugLogs)
                Debug.Log("[LinePlayerController] 当前节点没有连接");
            return;
        }
        
        // 找到最匹配输入方向的连接节点
        Transform bestTarget = FindBestMatchingNode(connectedNodes, inputDirection);
        
        if (bestTarget != null)
        {
            // 检查移动方向是否被线条类型允许
            if (IsMovementAllowedOnLine(currentNode, bestTarget, inputDirection))
            {
                StartMovementTo(bestTarget);
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[LinePlayerController] 线条类型不允许此方向的移动");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[LinePlayerController] 找不到匹配输入方向的节点");
        }
    }
    
    /// <summary>
    /// 找到最匹配输入方向的节点
    /// </summary>
    private Transform FindBestMatchingNode(List<Transform> connectedNodes, Vector2 input)
    {
        Transform bestMatch = null;
        float bestDot = 0.3f; // 最小匹配度阈值
        
        foreach (Transform node in connectedNodes)
        {
            Vector2 directionToNode = ((Vector2)(node.position - currentNode.position)).normalized;
            float dot = Vector2.Dot(input.normalized, directionToNode);
            
            if (dot > bestDot)
            {
                bestDot = dot;
                bestMatch = node;
            }
        }
        
        return bestMatch;
    }
    
    /// <summary>
    /// 检查在指定线条上是否允许此方向的移动
    /// </summary>
    private bool IsMovementAllowedOnLine(Transform startNode, Transform endNode, Vector2 inputDir)
    {
        Vector2 lineDirection = ((Vector2)(endNode.position - startNode.position)).normalized;
        float angle = Vector2.SignedAngle(Vector2.right, lineDirection);
        
        // 标准化角度到 -180 到 180 度
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        
        LineType lineType = DetermineLineType(angle);
        
        return IsInputValidForLineType(inputDir, lineType);
    }
    
    /// <summary>
    /// 确定线条类型
    /// </summary>
    private LineType DetermineLineType(float angle)
    {
        float absAngle = Mathf.Abs(angle);
        
        // 水平线（左右）：-15° 到 15° 或 165° 到 180°
        if (absAngle <= 15f || absAngle >= 165f)
        {
            return LineType.Horizontal;
        }
        // 垂直线（上下）：75° 到 105°
        else if (absAngle >= 75f && absAngle <= 105f)
        {
            return LineType.Vertical;
        }
        // 对角线
        else
        {
            return LineType.Diagonal;
        }
    }
    
    /// <summary>
    /// 检查输入是否对线条类型有效
    /// </summary>
    private bool IsInputValidForLineType(Vector2 input, LineType lineType)
    {
        switch (lineType)
        {
            case LineType.Horizontal:
                // 水平线只允许左右移动（A/D）
                return Mathf.Abs(input.x) > Mathf.Abs(input.y) && Mathf.Abs(input.x) > 0.3f;
                
            case LineType.Vertical:
                // 垂直线只允许上下移动（W/S）
                return Mathf.Abs(input.y) > Mathf.Abs(input.x) && Mathf.Abs(input.y) > 0.3f;
                
            case LineType.Diagonal:
                // 对角线允许对角移动或主方向移动
                if (allowDiagonalOnDiagonalLines)
                {
                    return input.magnitude > 0.3f; // 允许任何方向
                }
                else
                {
                    // 只允许主要的4个方向
                    return (Mathf.Abs(input.x) > 0.3f && Mathf.Abs(input.y) < 0.3f) ||
                           (Mathf.Abs(input.y) > 0.3f && Mathf.Abs(input.x) < 0.3f);
                }
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// 开始移动到目标节点
    /// </summary>
    private void StartMovementTo(Transform target)
    {
        if (target == null || isMoving) return;
        
        targetNode = target;
        
        // 停止之前的移动
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }
        
        // 开始新的移动
        moveCoroutine = StartCoroutine(MoveToNode());
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LinePlayerController] 开始移动：{currentNode.name} → {target.name}");
        }
    }
    
    /// <summary>
    /// 移动到节点的协程
    /// </summary>
    private IEnumerator MoveToNode()
    {
        isMoving = true;
        isAtNode = false;
        
        Vector3 startPos = currentNode.position;
        Vector3 endPos = targetNode.position;
        float distance = Vector3.Distance(startPos, endPos);
        float journeyTime = distance / moveSpeed;
        float elapsedTime = 0f;
        
        // 设置旋转方向
        Vector2 moveDirection = (endPos - startPos).normalized;
        
        while (elapsedTime < journeyTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / journeyTime;
            
            // 平滑移动
            t = Mathf.SmoothStep(0f, 1f, t);
            
            // 更新位置
            transform.position = Vector3.Lerp(startPos, endPos, t);
            
            // 更新旋转
            UpdateRotation(moveDirection);
            
            yield return null;
        }
        
        // 确保精确到达目标位置
        transform.position = endPos;
        
        // 到达节点
        ArrivedAtNode(targetNode);
    }
    
    /// <summary>
    /// 到达节点时的处理
    /// </summary>
    private void ArrivedAtNode(Transform node)
    {
        currentNode = node;
        targetNode = null;
        isMoving = false;
        isAtNode = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LinePlayerController] 到达节点：{node.name}");
        }
        
        // 开始节点停顿
        StartNodeStop();
    }
    
    /// <summary>
    /// 开始节点停顿
    /// </summary>
    private void StartNodeStop()
    {
        if (nodeStopDuration > 0)
        {
            canMove = false;
            
            if (nodeStopCoroutine != null)
            {
                StopCoroutine(nodeStopCoroutine);
            }
            
            nodeStopCoroutine = StartCoroutine(NodeStopCoroutine());
        }
    }
    
    /// <summary>
    /// 节点停顿协程
    /// </summary>
    private IEnumerator NodeStopCoroutine()
    {
        yield return new WaitForSeconds(nodeStopDuration);
        canMove = true;
        
        if (enableDebugLogs)
        {
            Debug.Log("[LinePlayerController] 节点停顿结束，可以继续移动");
        }
    }
    
    /// <summary>
    /// 更新玩家旋转
    /// </summary>
    private void UpdateRotation(Vector2 direction)
    {
        if (direction.magnitude < 0.1f) return;
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
    
    /// <summary>
    /// 将玩家吸附到最近的节点
    /// </summary>
    private void SnapToNearestNode()
    {
        if (allNodes == null || allNodes.Length == 0)
        {
            Debug.LogError("[LinePlayerController] 没有设置节点数组！");
            return;
        }
        
        Transform nearestNode = null;
        float nearestDistance = float.MaxValue;
        
        foreach (Transform node in allNodes)
        {
            if (node == null) continue;
            
            float distance = Vector3.Distance(transform.position, node.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }
        
        if (nearestNode != null)
        {
            transform.position = nearestNode.position;
            currentNode = nearestNode;
            isAtNode = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LinePlayerController] 吸附到节点：{nearestNode.name}");
            }
        }
    }
    
    /// <summary>
    /// 线条类型枚举
    /// </summary>
    private enum LineType
    {
        Horizontal, // 水平线
        Vertical,   // 垂直线
        Diagonal    // 对角线
    }
    
    /// <summary>
    /// 手动设置当前节点（用于调试或特殊情况）
    /// </summary>
    public void SetCurrentNode(Transform node)
    {
        if (node == null) return;
        
        currentNode = node;
        transform.position = node.position;
        isAtNode = true;
        isMoving = false;
        
        // 停止所有移动
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }
    
    /// <summary>
    /// 强制停止移动
    /// </summary>
    public void StopMovement()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        
        isMoving = false;
        canMove = true;
        
        // 吸附到最近的节点
        SnapToNearestNode();
    }
    
    /// <summary>
    /// 调试信息显示
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("=== 玩家移动调试 ===");
        GUILayout.Label($"当前节点: {(currentNode ? currentNode.name : "无")}");
        GUILayout.Label($"目标节点: {(targetNode ? targetNode.name : "无")}");
        GUILayout.Label($"正在移动: {isMoving}");
        GUILayout.Label($"在节点处: {isAtNode}");
        GUILayout.Label($"可以移动: {canMove}");
        GUILayout.Label($"输入方向: {inputDirection}");
        
        if (currentNode != null)
        {
            List<Transform> connected = lineController.GetConnectedNodes(currentNode);
            GUILayout.Label($"连接节点数: {connected.Count}");
        }
        
        GUILayout.EndArea();
    }
    
    /// <summary>
    /// Scene视图调试绘制
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugInfo) return;
        
        // 绘制当前节点
        if (currentNode != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentNode.position, 0.3f);
        }
        
        // 绘制目标节点
        if (targetNode != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetNode.position, 0.25f);
            
            // 绘制移动路径
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetNode.position);
        }
        
        // 绘制输入方向
        if (inputDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.blue;
            Vector3 inputWorldDir = new Vector3(inputDirection.x, inputDirection.y, 0) * 0.5f;
            Gizmos.DrawRay(transform.position, inputWorldDir);
        }
    }
}