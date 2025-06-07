using UnityEngine;

public class QuickSetupHelper : MonoBehaviour
{
    [Header("快速设置工具")]
    [Space]
    [TextArea(2, 3)]
    public string instructions = "勾选下方复选框执行操作，或右键点击组件标题选择菜单项";
    
    [Space]
    [Header("一键操作")]
    public bool createCompleteScene = false;
    public bool clearScene = false;
    
    [Space]
    [Header("单独创建")]
    public bool createPlayer = false;
    public bool createPuzzleA = false;
    public bool createPuzzleB = false;
    
    [Space]
    [Header("设置")]
    public bool showLayerInfo = false;
    
    void Update()
    {
        if (createCompleteScene)
        {
            createCompleteScene = false;
            CreateCompleteTestScene();
        }
        
        if (clearScene)
        {
            clearScene = false;
            ClearTestScene();
        }
        
        if (createPlayer)
        {
            createPlayer = false;
            CreateTestPlayer();
        }
        
        if (createPuzzleA)
        {
            createPuzzleA = false;
            CreateTestPuzzleA();
        }
        
        if (createPuzzleB)
        {
            createPuzzleB = false;
            CreateTestPuzzleB();
        }
        
        if (showLayerInfo)
        {
            showLayerInfo = false;
            SetupLayersAndSorting();
        }
    }
    
    [ContextMenu("创建完整测试场景")]
    public void CreateCompleteTestScene()
    {
        CreateTestPlayer();
        CreateTestPuzzleA();
        CreateTestPuzzleB();
        Debug.Log("✅ 测试场景创建完成！");
    }
    
    [ContextMenu("创建测试玩家")]
    public void CreateTestPlayer()
    {
        if (FindObjectOfType<PlayerMoveControl>() != null)
        {
            Debug.Log("⚠️ 玩家已存在！");
            return;
        }
        
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(-3, 0, 0);
        player.layer = 8;
        
        var moveControl = player.AddComponent<PlayerMoveControl>();
        moveControl.moveSpeed = 5f;
        moveControl.checkRadius = 0.2f;
        moveControl.wallLayer = 1 << 9; // Walls层
        
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.freezeRotation = true;
        
        var collider = player.AddComponent<CircleCollider2D>();
        collider.radius = 0.3f;
        
        var renderer = player.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSimpleSprite(Color.red, 32);
        renderer.sortingOrder = 5;
        
        Debug.Log("✅ 玩家创建完成！");
    }
    
    [ContextMenu("创建拼图A")]
    public void CreateTestPuzzleA()
    {
        CreateTestPuzzle("PuzzlePiece_A", "Room_A", new Vector3(-5, 0, 0), Color.cyan);
    }
    
    [ContextMenu("创建拼图B")]
    public void CreateTestPuzzleB()
    {
        CreateTestPuzzle("PuzzlePiece_B", "Room_B", new Vector3(5, 0, 0), Color.yellow);
    }
    
    [ContextMenu("清除测试场景")]
    public void ClearTestScene()
    {
        int deletedCount = 0;
        
        var player = FindObjectOfType<PlayerMoveControl>();
        if (player != null) 
        {
            DestroyImmediate(player.gameObject);
            deletedCount++;
        }
        
        var puzzles = FindObjectsOfType<PuzzlePiece>();
        foreach (var puzzle in puzzles)
        {
            DestroyImmediate(puzzle.gameObject);
            deletedCount++;
        }
        
        Debug.Log($"✅ 场景已清除！删除了 {deletedCount} 个对象");
    }
    
    [ContextMenu("显示层级设置信息")]
    public void SetupLayersAndSorting()
    {
        Debug.Log("=== 🛠️ 层级设置信息 ===");
        Debug.Log("Layers:");
        Debug.Log("  Layer 8: Player");
        Debug.Log("  Layer 9: Walls");
        Debug.Log("  Layer 10: Puzzles");
        Debug.Log("");
        Debug.Log("Sorting Orders:");
        Debug.Log("  0: Background");
        Debug.Log("  1: Passages");
        Debug.Log("  2: Puzzle Frame");
        Debug.Log("  3: Walls (可选)");
        Debug.Log("  5: Player");
        Debug.Log("");
        Debug.Log("物理层级碰撞设置:");
        Debug.Log("  Player层(8) 与 Walls层(9) 碰撞");
        Debug.Log("  Puzzles层(10) 与 Default层(0) 碰撞");
    }
    
    void CreateTestPuzzle(string objectName, string puzzleID, Vector3 position, Color bgColor)
    {
        GameObject puzzle = new GameObject(objectName);
        puzzle.transform.position = position;
        puzzle.layer = 10;
        
        // 添加拼图组件
        var puzzlePiece = puzzle.AddComponent<PuzzlePiece>();
        puzzlePiece.puzzleID = puzzleID;
        puzzlePiece.puzzleSize = new Vector2(8, 6);
        puzzlePiece.mainColliderSize = new Vector2(8, 6);
        puzzlePiece.isMainColliderTrigger = true;
        puzzlePiece.canDrag = true;
        puzzlePiece.showBounds = false;
        puzzlePiece.showPlayerFollow = false; // 可以设为true来调试
        
        // 添加吸附检测器
        var snapDetector = puzzle.AddComponent<PuzzleSnapDetector>();
        snapDetector.snapDistance = 2.5f;
        
        // 创建背景
        GameObject background = new GameObject("Background");
        background.transform.SetParent(puzzle.transform);
        background.transform.localPosition = Vector3.zero;
        
        var bgRenderer = background.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = CreateSimpleSprite(bgColor, 256);
        bgRenderer.sortingOrder = 0;
        
        puzzlePiece.backgroundRenderer = bgRenderer;
        
        // 初始化拼图（这会自动设置所有其他组件）
        puzzlePiece.ReinitializePuzzle();
        
        Debug.Log($"✅ 拼图 {puzzleID} 创建完成！");
    }
    
    Sprite CreateSimpleSprite(Color color, int size)
    {
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}