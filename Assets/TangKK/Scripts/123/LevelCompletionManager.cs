using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LevelCompletionManager : MonoBehaviour
{
    [Header("终点节点设置")]
    public List<PathNode> finishNodes = new List<PathNode>();
    
    [Header("触发范围设置")]
    [Tooltip("所有终点的默认触发范围")]
    public float defaultTriggerRadius = 0.3f;

    [Header("过关设置")]
    public string nextSceneName = "NextLevel";
    public bool useSceneIndex = false;
    public int nextSceneIndex = 1;

    [Header("调试信息")]
    public bool showDebugInfo = true;

    /// <summary>
    /// 已激活的终点节点
    /// </summary>
    private HashSet<PathNode> activatedNodes = new HashSet<PathNode>();
    private bool levelCompleted = false;

    void Start()
    {
        SetupFinishNodes();
        
        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 🎮 关卡初始化：共 {finishNodes.Count} 个终点节点");
            Debug.Log($"[LevelCompletion] 📋 过关条件：{finishNodes.Count} 个终点都需要有Player激活");
        }
    }

    /// <summary>
    /// 设置终点节点
    /// </summary>
    void SetupFinishNodes()
    {
        foreach (var node in finishNodes)
        {
            if (node == null) continue;

            // 确保有碰撞器
            Collider2D collider = node.GetComponent<Collider2D>();
            if (collider == null)
            {
                collider = node.gameObject.AddComponent<CircleCollider2D>();
            }
            
            // ✅ 设置为触发器并使用可调节的范围
            collider.isTrigger = true;
            if (collider is CircleCollider2D circleCollider)
            {
                circleCollider.radius = defaultTriggerRadius;
            }

            // 添加触发器组件
            FinishNodeTrigger trigger = node.GetComponent<FinishNodeTrigger>();
            if (trigger == null)
            {
                trigger = node.gameObject.AddComponent<FinishNodeTrigger>();
            }
            
            // ✅ 设置触发器的范围
            trigger.triggerRadius = defaultTriggerRadius;
            trigger.Initialize(this, node);

            if (showDebugInfo)
            {
                Debug.Log($"[LevelCompletion] 📍 设置终点: {node.name} (触发范围: {defaultTriggerRadius})");
            }
        }
    }

    /// <summary>
    /// ✅ 兼容旧方法名 - 当玩家进入终点时调用
    /// </summary>
    public void OnPlayerEnterFinish(PathNode node)
    {
        if (levelCompleted) return;

        activatedNodes.Add(node);

        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] ✅ 终点激活: {node.name} ({activatedNodes.Count}/{finishNodes.Count})");
        }

        CheckLevelCompletion();
    }

    /// <summary>
    /// ✅ 兼容旧方法名 - 当玩家离开终点时调用
    /// </summary>
    public void OnPlayerExitFinish(PathNode node)
    {
        if (levelCompleted) return;

        activatedNodes.Remove(node);

        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] ❌ 终点失活: {node.name} ({activatedNodes.Count}/{finishNodes.Count})");
        }
    }

    /// <summary>
    /// ✅ 新方法名 - 当终点被激活时调用
    /// </summary>
    public void OnFinishNodeActivated(PathNode node)
    {
        OnPlayerEnterFinish(node);
    }

    /// <summary>
    /// ✅ 新方法名 - 当终点失活时调用
    /// </summary>
    public void OnFinishNodeDeactivated(PathNode node)
    {
        OnPlayerExitFinish(node);
    }

    /// <summary>
    /// ✅ 检查关卡完成条件 - 超级简单的逻辑
    /// </summary>
    void CheckLevelCompletion()
    {
        // ✅ 只检查：是否所有终点都被激活
        bool allActivated = finishNodes.Count > 0 && 
                           finishNodes.All(node => node != null && activatedNodes.Contains(node));

        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 🔍 完成检查: {activatedNodes.Count}/{finishNodes.Count} 个终点激活，完成状态: {allActivated}");
        }

        if (allActivated && !levelCompleted)
        {
            levelCompleted = true;
            OnLevelCompleted();
        }
    }

    /// <summary>
    /// ✅ 关卡完成
    /// </summary>
    void OnLevelCompleted()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 🎉 关卡完成！所有 {finishNodes.Count} 个终点都被激活，准备进入下一关...");
        }

        // 延迟进入下一关
        Invoke(nameof(LoadNextLevel), 1.5f);
    }

    /// <summary>
    /// 加载下一关
    /// </summary>
    void LoadNextLevel()
    {
        if (showDebugInfo)
        {
            Debug.Log("[LevelCompletion] 🚀 载入下一关...");
        }

        if (useSceneIndex)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
        }
    }

    /// <summary>
    /// 重置关卡状态
    /// </summary>
    [ContextMenu("重置关卡")]
    public void ResetLevel()
    {
        activatedNodes.Clear();
        levelCompleted = false;

        // 重置所有终点触发器
        foreach (var node in finishNodes)
        {
            if (node != null)
            {
                var trigger = node.GetComponent<FinishNodeTrigger>();
                if (trigger != null)
                {
                    trigger.ResetActivation();
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("[LevelCompletion] 🔄 关卡状态已重置");
        }
    }

    /// <summary>
    /// 调试：显示当前状态
    /// </summary>
    [ContextMenu("显示当前状态")]
    public void ShowCurrentStatus()
    {
        Debug.Log("=== 当前关卡状态 ===");
        Debug.Log($"总终点数: {finishNodes.Count}");
        Debug.Log($"已激活终点数: {activatedNodes.Count}");
        Debug.Log($"关卡完成: {levelCompleted}");

        foreach (var node in finishNodes)
        {
            if (node != null)
            {
                bool isActivated = activatedNodes.Contains(node);
                Debug.Log($"  终点 {node.name}: {(isActivated ? "✅ 已激活" : "❌ 未激活")}");
            }
        }
    }

    /// <summary>
    /// ✅ 批量更新所有终点的触发范围
    /// </summary>
    [ContextMenu("更新所有终点触发范围")]
    public void UpdateAllTriggerRanges()
    {
        foreach (var node in finishNodes)
        {
            if (node != null)
            {
                var trigger = node.GetComponent<FinishNodeTrigger>();
                if (trigger != null)
                {
                    trigger.triggerRadius = defaultTriggerRadius;
                    trigger.UpdateColliderSize();
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 🔄 已更新所有终点触发范围为: {defaultTriggerRadius}");
        }
    }

    void Update()
    {
        // 快捷键
        if (Input.GetKeyDown(KeyCode.F5))
        {
            ShowCurrentStatus();
        }
        if (Input.GetKeyDown(KeyCode.F6))
        {
            ResetLevel();
        }
        // ✅ 新增：F7键批量更新触发范围
        if (Input.GetKeyDown(KeyCode.F7))
        {
            UpdateAllTriggerRanges();
        }
    }

    void OnDrawGizmos()
    {
        // 在Scene视图中显示终点状态
        foreach (var node in finishNodes)
        {
            if (node == null) continue;

            // 根据激活状态显示颜色
            bool isActivated = activatedNodes.Contains(node);
            Gizmos.color = isActivated ? Color.green : Color.red;

            // ✅ 使用实际的触发范围绘制
            float actualRadius = defaultTriggerRadius;
            var trigger = node.GetComponent<FinishNodeTrigger>();
            if (trigger != null)
            {
                actualRadius = trigger.triggerRadius;
            }

            // 绘制终点标记
            Gizmos.DrawWireSphere(node.transform.position, actualRadius);
            Gizmos.DrawSphere(node.transform.position, actualRadius * 0.3f);
        }

        // 显示完成状态
        if (levelCompleted)
        {
            Gizmos.color = Color.yellow;
            if (finishNodes.Count > 0)
            {
                Vector3 center = finishNodes.Where(n => n != null)
                                          .Aggregate(Vector3.zero, (sum, node) => sum + node.transform.position) / finishNodes.Count;
                Gizmos.DrawWireSphere(center, 1f);
            }
        }
    }
}