using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class Cell : MonoBehaviour
{
    private SpriteRenderer sr;
    private GameOfLifeManager manager;

    public int x, y;

    public bool IsAlive { get; private set; } = false;
    public int Owner { get; private set; } = -1;

    public Color aliveNeutralColor = Color.white;
    public Color playerAColor = Color.white;
    public Color playerBColor = Color.black;
    public Color deadColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void Init(GameOfLifeManager manager, int x, int y, bool alive, int owner = -1)
    {
        this.manager = manager;
        this.x = x;
        this.y = y;
        SetState(alive, owner);
    }

    public void SetState(bool alive, int owner)
    {
        IsAlive = alive;
        Owner = alive ? owner : -1;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (sr == null) return;

        if (!IsAlive)
        {
            sr.color = deadColor;
            return;
        }

        if (Owner == -1)
        {
            sr.color = aliveNeutralColor;
        }
        else if (Owner == 0)
        {
            sr.color = playerAColor;
        }
        else
        {
            sr.color = playerBColor;
        }
    }
/*
    void OnMouseDown()
    {
        if (manager == null) return;
        manager.HandleCellClick(this);
    }
*/
}
