using UnityEngine;

/// <summary>
/// 简化版终点触发器 - 只要有Player进入就激活
/// </summary>
public class FinishNodeTrigger : MonoBehaviour
{
    [Header("触发范围设置")]
    [Tooltip("终点的触发范围半径")]
    public float triggerRadius = 0.1f;
    
    [Header("调试设置")]
    [Tooltip("是否显示触发范围")]
    public bool showTriggerRange = true;
    
    private LevelCompletionManager manager;
    private PathNode thisNode;
    private bool isActivated = false;

    public void Initialize(LevelCompletionManager completionManager, PathNode node)
    {
        manager = completionManager;
        thisNode = node;
        
        // ✅ 初始化时更新碰撞器范围
        UpdateColliderSize();
    }

    void Start()
    {
        // 确保碰撞器范围正确
        UpdateColliderSize();
    }

    /// <summary>
    /// ✅ 更新碰撞器大小
    /// </summary>
    public void UpdateColliderSize()
    {
        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        if (collider != null)
        {
            collider.radius = triggerRadius;
            collider.isTrigger = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ✅ 只检查Player标签
        if (!other.CompareTag("Player")) return;
        if (manager == null || thisNode == null) return;

        // ✅ 如果还没激活，就激活这个终点
        if (!isActivated)
        {
            isActivated = true;
            // 使用旧方法名来兼容
            manager.OnPlayerEnterFinish(thisNode);
            Debug.Log($"[FinishTrigger] ✅ 终点 {thisNode.name} 被Player {other.name} 激活！(触发范围: {triggerRadius})");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // ✅ 当Player离开时，检查是否还有其他Player在内部
        if (!other.CompareTag("Player")) return;
        if (!isActivated) return;

        // ✅ 使用可调节的触发范围检查
        Collider2D[] playersInside = Physics2D.OverlapCircleAll(transform.position, triggerRadius);
        bool hasPlayerInside = false;

        foreach (var collider in playersInside)
        {
            if (collider.CompareTag("Player") && collider != other)
            {
                hasPlayerInside = true;
                break;
            }
        }

        // ✅ 如果没有Player了，取消激活
        if (!hasPlayerInside)
        {
            isActivated = false;
            // 使用旧方法名来兼容
            manager.OnPlayerExitFinish(thisNode);
            Debug.Log($"[FinishTrigger] ❌ 终点 {thisNode.name} 失活，Player {other.name} 离开且无其他Player");
        }
    }

    /// <summary>
    /// 获取激活状态
    /// </summary>
    public bool IsActivated => isActivated;

    /// <summary>
    /// 手动重置状态
    /// </summary>
    public void ResetActivation()
    {
        isActivated = false;
    }

    /// <summary>
    /// ✅ 在编辑器中显示触发范围
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showTriggerRange) return;

        // 根据激活状态选择颜色
        if (Application.isPlaying)
        {
            Gizmos.color = isActivated ? Color.green : Color.red;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }

        // 绘制触发范围
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        
        // 绘制填充圆圈（透明）
        Color fillColor = Gizmos.color;
        fillColor.a = 0.2f;
        Gizmos.color = fillColor;
        Gizmos.DrawSphere(transform.position, triggerRadius);
    }

    /// <summary>
    /// ✅ 编辑器中值改变时自动更新
    /// </summary>
    void OnValidate()
    {
        // 确保触发范围不为负数
        if (triggerRadius < 0.1f)
        {
            triggerRadius = 0.1f;
        }

        // 实时更新碰撞器大小（仅在编辑模式下）
        if (!Application.isPlaying)
        {
            UpdateColliderSize();
        }
    }
}