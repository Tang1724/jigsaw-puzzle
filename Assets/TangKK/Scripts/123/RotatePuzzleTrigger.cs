using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class RotatePuzzleTrigger : MonoBehaviour
{
    [Header("旋转角度")]
    public float rotationAngle = 90f;

    [Header("是否只触发一次")]
    public bool triggerOnce = false;

    [Header("旋转锁定设置")]
    public float rotationDuration = 0.5f; // 旋转动画时长
    public bool useRotationAnimation = true; // 是否使用旋转动画

    private bool hasTriggered = false;
    private bool playerInside = false;
    private Vector3 enterPosition;
    
    // 🔒 旋转状态管理
    private bool isRotating = false;
    private static bool globalRotationLock = false; // 全局旋转锁，防止多个触发器同时旋转

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;
        if (isRotating || globalRotationLock) return; // 🔒 旋转期间禁止新的触发

        enterPosition = other.transform.position;
        playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerInside || (triggerOnce && hasTriggered)) return;
        if (isRotating || globalRotationLock) return; // 🔒 旋转期间禁止触发

        Vector3 exitPosition = other.transform.position;
        Vector3 center = GetComponent<Collider2D>().bounds.center;

        Vector3 toEnter = enterPosition - center;
        Vector3 toExit = exitPosition - center;

        float dot = Vector3.Dot(toEnter.normalized, toExit.normalized);
        bool crossedThrough = dot < 0f;

        if (crossedThrough)
        {
            Debug.Log("[RotateTrigger] 玩家完整穿过触发器，尝试触发旋转");

            var player = other.gameObject;
            var playerPiece = player.GetComponentInParent<PuzzlePiece>();

            if (playerPiece == null)
            {
                Debug.LogWarning("[RotateTrigger] 无法找到玩家关联的 PuzzlePiece");
                return;
            }

            // ✅ 获取玩家所在的组
            var playerGroup = playerPiece.currentGroup;
            if (playerGroup == null)
            {
                Debug.LogWarning("[RotateTrigger] 玩家未处于任何拼图组，无法触发旋转");
                return;
            }

            // ✅ 检查触发器是否处于同一个组的拼图上
            var thisPiece = GetComponentInParent<PuzzlePiece>();
            if (thisPiece != null && thisPiece.currentGroup != playerGroup)
            {
                Debug.Log("[RotateTrigger] 触发器不属于玩家所在的拼图组，忽略旋转");
                return;
            }

            // 🔒 开始旋转流程（异步执行）
            StartCoroutine(PerformRotationSequence(player, playerGroup));
            hasTriggered = true;
        }
        else
        {
            Debug.Log("[RotateTrigger] 玩家未穿越触发器中心，忽略旋转");
        }

        playerInside = false;
    }

    /// <summary>
    /// 🔒 完整的旋转序列：锁定→旋转→解锁
    /// </summary>
    private IEnumerator PerformRotationSequence(GameObject player, PuzzleGroup playerGroup)
    {
        // 步骤1：设置全局锁定状态
        isRotating = true;
        globalRotationLock = true;
        Debug.Log("[旋转序列] 🔒 开始旋转，锁定所有玩家移动");

        // 步骤2：禁用所有玩家的移动能力
        LockAllPlayerMovement(true);

        // 步骤3：记录旋转前状态
        Vector3 playerWorldPos = player.transform.position;
        PuzzlePiece playerPiece = player.GetComponentInParent<PuzzlePiece>();
        Vector3 playerLocalPos = Vector3.zero;
        
        if (playerPiece != null)
        {
            playerLocalPos = playerPiece.transform.InverseTransformPoint(playerWorldPos);
            Debug.Log($"[旋转序列] 记录人物局部位置：{playerLocalPos}");
        }

        // 步骤4：执行旋转（瞬间或动画）
        if (useRotationAnimation)
        {
            yield return StartCoroutine(PerformAnimatedRotation(playerGroup));
        }
        else
        {
            PerformInstantRotation(playerGroup);
        }

        // 步骤5：立即刷新路径系统
        RefreshAllPathsImmediately();

        // 步骤6：更新玩家位置
        if (playerPiece != null)
        {
            Vector3 newPlayerWorldPos = playerPiece.transform.TransformPoint(playerLocalPos);
            player.transform.position = newPlayerWorldPos;
            Debug.Log($"[旋转序列] 更新人物位置：{newPlayerWorldPos}");
        }

        // 步骤7：等待一帧确保物理稳定
        yield return null;

        // 步骤8：重新组织拼图组关系
        ReorganizeAndRefreshAll();

        // 步骤9：恢复玩家路径关系
        yield return StartCoroutine(RestorePlayerRelationship(player));

        // 步骤10：解除锁定，恢复移动
        LockAllPlayerMovement(false);
        isRotating = false;
        globalRotationLock = false;
        
        Debug.Log("[旋转序列] 🔓 旋转完成，解锁所有玩家移动");
    }

    /// <summary>
    /// 🔒 锁定/解锁所有玩家的移动能力
    /// </summary>
    private void LockAllPlayerMovement(bool lockMovement)
    {
        // 方案1：控制PathMover组件
        PathMover[] allMovers = FindObjectsOfType<PathMover>();
        foreach (var mover in allMovers)
        {
            mover.SetMovementLocked(lockMovement);
        }

        // 方案2：控制输入组件（如果有PlayerController的话）
        /*
        PlayerController[] allControllers = FindObjectsOfType<PlayerController>();
        foreach (var controller in allControllers)
        {
            controller.enabled = !lockMovement;
        }
        */

        Debug.Log($"[移动锁定] {(lockMovement ? "🔒 锁定" : "🔓 解锁")}所有玩家移动");
    }

    /// <summary>
    /// 瞬间旋转（原有逻辑）
    /// </summary>
    private void PerformInstantRotation(PuzzleGroup playerGroup)
    {
        foreach (var piece in playerGroup.pieces)
        {
            piece.RotateSelf(rotationAngle);
            Debug.Log($"[瞬间旋转] {piece.name} 完成旋转 {rotationAngle}度");
        }
    }

    /// <summary>
    /// 🎬 动画旋转（平滑旋转效果）
    /// </summary>
    private IEnumerator PerformAnimatedRotation(PuzzleGroup playerGroup)
    {
        // 记录所有拼图的初始旋转
        var initialRotations = new Quaternion[playerGroup.pieces.Count];
        var targetRotations = new Quaternion[playerGroup.pieces.Count];
        
        for (int i = 0; i < playerGroup.pieces.Count; i++)
        {
            var piece = playerGroup.pieces[i];
            initialRotations[i] = piece.transform.rotation;
            targetRotations[i] = piece.transform.rotation * Quaternion.Euler(0, 0, rotationAngle);
        }

        // 执行平滑旋转动画
        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            float t = elapsed / rotationDuration;
            t = Mathf.SmoothStep(0f, 1f, t); // 使用smooth step获得更好的动画效果

            for (int i = 0; i < playerGroup.pieces.Count; i++)
            {
                var piece = playerGroup.pieces[i];
                piece.transform.rotation = Quaternion.Lerp(initialRotations[i], targetRotations[i], t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 确保最终旋转精确
        for (int i = 0; i < playerGroup.pieces.Count; i++)
        {
            var piece = playerGroup.pieces[i];
            piece.transform.rotation = targetRotations[i];
        }

        Debug.Log("[动画旋转] ✅ 动画旋转完成");
    }

    /// <summary>
    /// ✅ 立即刷新所有路径，确保旋转后路径立即可用
    /// </summary>
    private void RefreshAllPathsImmediately()
    {
        // 刷新所有路径节点
        PathNode[] allNodes = FindObjectsOfType<PathNode>();
        foreach (var node in allNodes)
        {
            node.AssignParentPiece(); // 重新分配父拼图
            node.RefreshPathLines();   // 刷新路径线条
        }

        // 强制刷新所有PathMover的路径缓存
        PathMover[] allMovers = FindObjectsOfType<PathMover>();
        foreach (var mover in allMovers)
        {
            mover.RefreshPaths();
        }

        Debug.Log("[立即刷新] ✅ 已立即刷新所有路径节点和PathMover缓存");
    }

    /// <summary>
    /// ✅ 协程：恢复人物的正确父子关系
    /// </summary>
    private IEnumerator RestorePlayerRelationship(GameObject player)
    {
        // 等待一帧，让物理系统稳定
        yield return null;
        
        // 尝试将人物吸附到最近的路径上
        PathMover pathMover = player.GetComponent<PathMover>();
        if (pathMover != null)
        {
            // 强制更新PathMover的组ID和路径
            var playerPiece = player.GetComponentInParent<PuzzlePiece>();
            if (playerPiece != null)
            {
                pathMover.ForceUpdateGroupID(playerPiece.GroupID);
            }
            
            // 手动触发一次路径吸附检查
            Vector3 currentPos = player.transform.position;
            Vector3 snapPos = FindNearestPathPosition(currentPos, pathMover);
            if (snapPos != Vector3.zero)
            {
                player.transform.position = snapPos;
                Debug.Log($"[恢复关系] 人物吸附到最近路径：{snapPos}");
            }
        }
    }

    /// <summary>
    /// ✅ 查找最近的有效路径位置
    /// </summary>
    private Vector3 FindNearestPathPosition(Vector3 playerPos, PathMover pathMover)
    {
        // 使用PathMover的逻辑查找最近的路径段
        if (pathMover.FindClosestPathSegment(playerPos, out Vector3 segA, out Vector3 segB))
        {
            return pathMover.GetClosestPointOnSegment(playerPos, segA, segB);
        }
        return Vector3.zero;
    }

    /// <summary>
    /// ✅ 旋转后重新组织拼图组，并刷新路径（确保逻辑正确）
    /// </summary>
    private void ReorganizeAndRefreshAll()
    {
        PuzzlePiece[] allPieces = FindObjectsOfType<PuzzlePiece>();
        foreach (var piece in allPieces)
        {
            piece.ReorganizeConnectedPuzzles(); // 重新组织组
            piece.RefreshPath();                // 刷新路径
        }

        RefreshAllPaths();
    }

    /// <summary>
    /// 刷新所有路径显示和路径缓存
    /// </summary>
    private void RefreshAllPaths()
    {
        foreach (var node in FindObjectsOfType<PathNode>())
        {
            node.RefreshPathLines();
        }

        // ✅ 更新所有玩家的 PathMover（支持多人）
        PathMover[] allMovers = FindObjectsOfType<PathMover>();
        foreach (var mover in allMovers)
        {
            mover.RefreshPaths();
        }

        Debug.Log("[RotateTrigger] ✅ 已刷新所有路径线段和所有玩家的路径缓存");
    }
}