using UnityEngine;

[RequireComponent(typeof(PathNode))]
[RequireComponent(typeof(Collider2D))]
public class PathToggleSwitch : MonoBehaviour
{
    [Header("连接 A（默认连接）")]
    public PathNode nodeA1;
    public PathNode nodeA2;

    [Header("连接 B（替代连接）")]
    public PathNode nodeB1;
    public PathNode nodeB2;

    private bool isToggled = false;
    private Vector3 enterPosition;
    private bool playerInside = false;

    void Start()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;

        // 初始化路径
        SetConnection(nodeA1, nodeA2, true);
        SetConnection(nodeB1, nodeB2, false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        enterPosition = other.transform.position;
        playerInside = true;

        Debug.Log($"[开关] 玩家进入，位置：{enterPosition}");
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !playerInside) return;

        Vector3 exitPosition = other.transform.position;
        Vector3 center = GetComponent<Collider2D>().bounds.center;

        // 判断是否“穿越”——穿过中心点的任意方向
        Vector3 toEnter = enterPosition - center;
        Vector3 toExit = exitPosition - center;

        float dot = Vector3.Dot(toEnter.normalized, toExit.normalized);
        bool crossedThrough = dot < 0f; // 如果方向相反（夹角大于 90°），说明穿过了中心

        if (crossedThrough)
        {
            Debug.Log("[开关] 玩家完整穿过，触发路径切换");
            TogglePath();
        }
        else
        {
            Debug.Log("[开关] 玩家没有穿过中心，忽略");
        }

        playerInside = false;
    }

    void TogglePath()
    {
        isToggled = !isToggled;

        if (isToggled)
        {
            SetConnection(nodeA1, nodeA2, false);
            SetConnection(nodeB1, nodeB2, true);
        }
        else
        {
            SetConnection(nodeB1, nodeB2, false);
            SetConnection(nodeA1, nodeA2, true);
        }

        var mover = FindObjectOfType<PathMover>();
        if (mover != null)
        {
            mover.RefreshPaths();
        }

        Debug.Log($"[开关] 路径切换完成，现在是 {(isToggled ? "B 路径" : "A 路径")}");
    }

    void SetConnection(PathNode a, PathNode b, bool enable)
    {
        if (a == null || b == null) return;

        if (enable)
        {
            if (!a.IsConnectedTo(b))
            {
                a.ConnectTo(b);
                Debug.Log($"[路径连接] 启用连接：{a.name} <--> {b.name}");
            }
        }
        else
        {
            if (a.IsConnectedTo(b))
            {
                a.DisconnectFrom(b);
                Debug.Log($"[路径连接] 断开连接：{a.name} X {b.name}");
            }
        }
    }
}