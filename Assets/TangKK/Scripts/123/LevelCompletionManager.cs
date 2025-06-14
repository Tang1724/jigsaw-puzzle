using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LevelCompletionManager : MonoBehaviour
{
    [Header("终点节点设置")]
    public List<PathNode> finishNodes = new List<PathNode>();

    [Header("过关设置")]
    public string nextSceneName = "NextLevel";
    public bool useSceneIndex = false;
    public int nextSceneIndex = 1;

    [Header("分组设置")]
    public bool requireSameGroup = true; // 是否要求同一组

    [Header("调试信息")]
    public bool showDebugInfo = true;

    private HashSet<PathNode> triggeredNodes = new HashSet<PathNode>();
    private bool levelCompleted = false;

    void Start()
    {
        // 为所有终点节点添加触发器
        SetupFinishNodes();
    }

    /// <summary>
    /// 设置终点节点的碰撞器
    /// </summary>
    void SetupFinishNodes()
    {
        foreach (var node in finishNodes)
        {
            if (node == null) continue;

            // 确保节点有碰撞器
            Collider2D collider = node.GetComponent<Collider2D>();
            if (collider == null)
            {
                collider = node.gameObject.AddComponent<CircleCollider2D>();
                ((CircleCollider2D)collider).radius = 0.3f;
            }
            
            // 设置为触发器
            collider.isTrigger = true;

            // 添加终点触发组件
            FinishNodeTrigger trigger = node.GetComponent<FinishNodeTrigger>();
            if (trigger == null)
            {
                trigger = node.gameObject.AddComponent<FinishNodeTrigger>();
            }
            
            // 设置回调
            trigger.Initialize(this, node);

            if (showDebugInfo)
            {
                Debug.Log($"[LevelCompletion] 设置终点节点: {node.name} (组ID: {node.GroupID})");
            }
        }
    }

    /// <summary>
    /// 当玩家进入终点节点
    /// </summary>
    public void OnPlayerEnterFinish(PathNode node)
    {
        if (levelCompleted) return;

        triggeredNodes.Add(node);
        
        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 玩家进入终点: {node.name} 组ID: {node.GroupID} ({triggeredNodes.Count}/{finishNodes.Count})");
        }

        CheckLevelCompletion();
    }

    /// <summary>
    /// 当玩家离开终点节点
    /// </summary>
    public void OnPlayerExitFinish(PathNode node)
    {
        if (levelCompleted) return;

        triggeredNodes.Remove(node);
        
        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 玩家离开终点: {node.name} 组ID: {node.GroupID} ({triggeredNodes.Count}/{finishNodes.Count})");
        }
        
        // ✅ 当玩家离开终点时，重新检查完成状态（可能从完成变为未完成）
        CheckLevelCompletion();
    }

    /// <summary>
    /// 检查是否完成关卡
    /// </summary>
    void CheckLevelCompletion()
    {
        if (finishNodes.Count == 0) return;

        bool allTriggered = false;

        if (requireSameGroup)
        {
            // 按组检查完成条件
            allTriggered = CheckGroupCompletion();
        }
        else
        {
            // 检查是否所有终点都被触发（原逻辑）
            allTriggered = finishNodes.All(node => node != null && triggeredNodes.Contains(node));
        }

        if (allTriggered && !levelCompleted)
        {
            levelCompleted = true;
            OnLevelCompleted();
        }
    }

    /// <summary>
    /// 检查分组完成条件
    /// </summary>
    bool CheckGroupCompletion()
    {
        // 获取所有有效的终点节点
        var validFinishNodes = finishNodes.Where(node => node != null).ToList();
        if (validFinishNodes.Count == 0) return false;

        // 按组ID分组终点节点
        var nodesByGroup = validFinishNodes.GroupBy(node => node.GroupID).ToList();

        if (showDebugInfo)
        {
            Debug.Log($"[LevelCompletion] 检查分组完成条件，共 {nodesByGroup.Count} 个组");
            foreach (var group in nodesByGroup)
            {
                Debug.Log($"[LevelCompletion] 组 {group.Key}: {group.Count()} 个节点");
            }
        }

        // 检查是否有任何一个组的所有终点都被触发
        foreach (var group in nodesByGroup)
        {
            var groupNodes = group.ToList();
            bool groupComplete = groupNodes.All(node => triggeredNodes.Contains(node));
            
            if (showDebugInfo)
            {
                int triggeredInGroup = groupNodes.Count(node => triggeredNodes.Contains(node));
                Debug.Log($"[LevelCompletion] 组 {group.Key}: {triggeredInGroup}/{groupNodes.Count} 个节点被触发，完成状态: {groupComplete}");
            }

            if (groupComplete)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[LevelCompletion] ✅ 组 {group.Key} 的所有终点都被触发！");
                }
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 关卡完成时调用
    /// </summary>
    void OnLevelCompleted()
    {
        if (showDebugInfo)
        {
            if (requireSameGroup)
            {
                var completedGroup = GetCompletedGroup();
                Debug.Log($"[LevelCompletion] 🎉 关卡完成！组 {completedGroup} 的所有终点都被触发，准备进入下一关...");
            }
            else
            {
                Debug.Log("[LevelCompletion] 🎉 关卡完成！所有终点都被触发，准备进入下一关...");
            }
        }

        // 可以在这里添加音效、特效等
        
        // 延迟加载下一关，给玩家反应时间
        Invoke(nameof(LoadNextLevel), 1f);
    }

    /// <summary>
    /// 获取已完成的组ID
    /// </summary>
    int GetCompletedGroup()
    {
        var validFinishNodes = finishNodes.Where(node => node != null).ToList();
        var nodesByGroup = validFinishNodes.GroupBy(node => node.GroupID);

        foreach (var group in nodesByGroup)
        {
            var groupNodes = group.ToList();
            if (groupNodes.All(node => triggeredNodes.Contains(node)))
            {
                return group.Key;
            }
        }

        return -1;
    }

    /// <summary>
    /// 加载下一关
    /// </summary>
    void LoadNextLevel()
    {
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
    /// 手动重置关卡状态
    /// </summary>
    public void ResetLevel()
    {
        triggeredNodes.Clear();
        levelCompleted = false;
        
        if (showDebugInfo)
        {
            Debug.Log("[LevelCompletion] 关卡状态已重置");
        }
    }

    /// <summary>
    /// 添加终点节点
    /// </summary>
    public void AddFinishNode(PathNode node)
    {
        if (node != null && !finishNodes.Contains(node))
        {
            finishNodes.Add(node);
            SetupFinishNodes();
        }
    }

    /// <summary>
    /// 移除终点节点
    /// </summary>
    public void RemoveFinishNode(PathNode node)
    {
        if (finishNodes.Contains(node))
        {
            finishNodes.Remove(node);
            triggeredNodes.Remove(node);
        }
    }

    void OnDrawGizmos()
    {
        // 在Scene视图中显示终点节点
        foreach (var node in finishNodes)
        {
            if (node == null) continue;

            // 根据是否被触发和组状态选择颜色
            bool isTriggered = triggeredNodes.Contains(node);
            
            if (requireSameGroup)
            {
                // 分组模式：根据组的完成状态显示颜色
                bool groupComplete = IsGroupComplete(node.GroupID);
                if (groupComplete)
                {
                    Gizmos.color = Color.yellow; // 整个组完成
                }
                else if (isTriggered)
                {
                    Gizmos.color = Color.green; // 该节点被触发但组未完成
                }
                else
                {
                    Gizmos.color = Color.red; // 未触发
                }
            }
            else
            {
                // 原模式：单独节点状态
                Gizmos.color = isTriggered ? Color.green : Color.red;
            }
            
            // 绘制终点标记
            Gizmos.DrawWireSphere(node.transform.position, 0.3f);
            Gizmos.DrawSphere(node.transform.position, 0.1f);
        }

        // 显示完成状态
        if (levelCompleted)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = Vector3.zero;
            if (finishNodes.Count > 0)
            {
                center = finishNodes.Where(n => n != null)
                                  .Aggregate(Vector3.zero, (sum, node) => sum + node.transform.position) / finishNodes.Count;
            }
            Gizmos.DrawWireSphere(center, 1f);
        }
    }

    /// <summary>
    /// 检查指定组是否完成
    /// </summary>
    bool IsGroupComplete(int groupID)
    {
        var groupNodes = finishNodes.Where(node => node != null && node.GroupID == groupID).ToList();
        return groupNodes.Count > 0 && groupNodes.All(node => triggeredNodes.Contains(node));
    }
}