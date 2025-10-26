using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameOfLifeManager : MonoBehaviour
{
    [Header("Grid")]
    public int width = 50;
    public int height = 30;
    public float cellSize = 0.2f;
    public GameObject cellPrefab;

    [Header("Simulation")]
    public bool wrapEdges = false;
    public float stepDelay = 0.2f;

    [Header("UI")]
    public Button startPauseButton;
    public Button stepButton;
    public Button randomizeButton;
    public Button clearButton;
    public Button restartButton;
    public Slider speedSlider;
    public Text statusText;
    public Text scoreText;

    public Button classicModeButton;
    public Button pvpModeButton;


    [Header("PvP Settings")]
    public bool pvpMode = true;
    public int startingPiecesPerPlayer = 20;
    public Color playerAColor = Color.white;
    public Color playerBColor = Color.black;
    public GameObject piecesInputPanel;
    public InputField piecesInputField; 


    // Internal grid
    private Cell[,] cells;
    private bool[,] nextState;
    private int[,] ownerBuffer;
    private Coroutine simCoroutine;
    private bool isRunning = false;

    // PvP state
    private enum Phase { Установка, Процессинг, Окончено, Idle }
    private Phase phase = Phase.Idle;
    private int currentPlayer = 0;
    private int[] placedPieces = new int[2];
    private int[] scores = new int[2];

    // Mouse drawing
    private Vector2 gridOriginWorld;
    private bool isMouseDrawing = false;
    private bool mouseDrawTargetState = true;
    private int mouseDrawTargetOwner = -1;

    public Dropdown patternDropdown;
    public PatternData[] availablePatterns;

    private PatternData selectedPattern;


    void Start()
    {
        CreateGrid();
        HookupUI();

        if (speedSlider != null)
        {
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
            speedSlider.value = stepDelay;
        }
        if (patternDropdown != null)
        {
            patternDropdown.ClearOptions();
            patternDropdown.AddOptions(new System.Collections.Generic.List<string>(
                System.Array.ConvertAll(availablePatterns, p => p.patternName)
            ));
            patternDropdown.onValueChanged.AddListener(i => selectedPattern = availablePatterns[i]);
            selectedPattern = availablePatterns[0];
        }
        // Initialize phase
        if (pvpMode)
        {
            phase = Phase.Установка;
            currentPlayer = 0;
            placedPieces[0] = placedPieces[1] = 0;
            scores[0] = scores[1] = 0;
            UpdateStatus($"PvP Setup: Player A place {startingPiecesPerPlayer} pieces");
        }
        else
        {
            phase = Phase.Idle;
            UpdateStatus("Classic Life. Place cells manually or Randomize.");
        }
        if (piecesInputField != null)
        {
            piecesInputField.text = startingPiecesPerPlayer.ToString();
            piecesInputField.onEndEdit.AddListener(OnPiecesChanged);
        }
        if (piecesInputPanel != null)
        {
            piecesInputPanel.SetActive(pvpMode);
        }
        
        UpdateScoreUI();
    }

    public void OnPiecesChanged(string value)
    {
        int newVal;
        if (int.TryParse(value, out newVal))
        {
            startingPiecesPerPlayer = Mathf.Max(1, newVal);
            UpdateStatus($"Теперь у каждого игрока по {startingPiecesPerPlayer} фигур");
        }
        else
        {
            if (piecesInputField != null)
                piecesInputField.text = startingPiecesPerPlayer.ToString();
        }
    }


    void CreateGrid()
    {
        cells = new Cell[width, height];
        nextState = new bool[width, height];
        ownerBuffer = new int[width, height];

        Vector2 originLocal = new Vector2(-(width * cellSize) / 2f + cellSize / 2f, -(height * cellSize) / 2f + cellSize / 2f);
        gridOriginWorld = (Vector2)transform.position + originLocal;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 pos = gridOriginWorld + new Vector2(x * cellSize, y * cellSize);
                GameObject go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                go.transform.localScale = Vector3.one * cellSize;
                Cell c = go.GetComponent<Cell>();
                // set the cell colors for PvP to match manager config
                c.playerAColor = playerAColor;
                c.playerBColor = playerBColor;
                c.aliveNeutralColor = Color.white;
                c.deadColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                c.Init(this, x, y, false, -1);
                cells[x, y] = c;
                ownerBuffer[x, y] = -1;
            }
        }
    }

    void HookupUI()
    {
        if (classicModeButton != null) classicModeButton.onClick.AddListener(() => SetMode(false));
        if (pvpModeButton != null) pvpModeButton.onClick.AddListener(() => SetMode(true));
        if (startPauseButton != null) startPauseButton.onClick.AddListener(ToggleStartPause);
        if (stepButton != null) stepButton.onClick.AddListener(StepOnce);
        if (randomizeButton != null) randomizeButton.onClick.AddListener(Randomize);
        if (clearButton != null) clearButton.onClick.AddListener(Clear);
        if (restartButton != null) restartButton.onClick.AddListener(Restart);
    }

    public void SetMode(bool isPvp)
    {
        Pause();
        pvpMode = isPvp;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y].SetState(false, -1);

        placedPieces[0] = placedPieces[1] = 0;
        scores[0] = scores[1] = 0;
        currentPlayer = 0;

        if (pvpMode)
        {
            phase = Phase.Установка;
            UpdateStatus($"PvP Setup: Player A place {startingPiecesPerPlayer} pieces");
        }
        else
        {
            phase = Phase.Idle;
            UpdateStatus("Classic Life. Place cells manually or Randomize.");
        }
        if (piecesInputField != null)
        {
            piecesInputField.text = startingPiecesPerPlayer.ToString();
        }
        if (piecesInputPanel != null)
        {
            piecesInputPanel.SetActive(isPvp);
        }
        UpdateScoreUI();
    }


    void OnSpeedChanged(float value)
    {
        stepDelay = Mathf.Max(0.01f, value);
        UpdateStatus();
    }

    public void ToggleStartPause()
    {
        if (isRunning) Pause();
        else StartSim();
    }

    public void StartSim()
    {
        if (isRunning) return;

        if (pvpMode && phase == Phase.Установка)
        {
            if (placedPieces[0] < startingPiecesPerPlayer || placedPieces[1] < startingPiecesPerPlayer)
            {
                UpdateStatus($"Finish placing pieces first: A {placedPieces[0]}/{startingPiecesPerPlayer}, B {placedPieces[1]}/{startingPiecesPerPlayer}");
                return;
            }
            phase = Phase.Процессинг;
            scores[0] = scores[1] = 0;
        }

        isRunning = true;
        simCoroutine = StartCoroutine(RunSimulation());
        UpdateStatus("Running");
    }

    public void Pause()
    {
        if (!isRunning) return;
        isRunning = false;
        if (simCoroutine != null) StopCoroutine(simCoroutine);
        simCoroutine = null;
        UpdateStatus("Paused");
    }

    public void StepOnce()
    {
        if (isRunning) return;

        if (pvpMode && phase == Phase.Установка)
        {
            UpdateStatus("Finish setup first (place pieces) before stepping.");
            return;
        }

        ComputeNextGeneration();
        ApplyNextState();
        UpdateStatus("Stepped");
    }

    IEnumerator RunSimulation()
    {
        while (true)
        {
            ComputeNextGeneration();
            ApplyNextState();
            yield return new WaitForSeconds(stepDelay);
        }
    }

    void ComputeNextGeneration()
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                ownerBuffer[i, j] = -1;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int alive = 0;
                int countA = 0;
                int countB = 0;

                for (int ox = -1; ox <= 1; ox++)
                {
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        int nx = x + ox;
                        int ny = y + oy;

                        if (wrapEdges)
                        {
                            nx = (nx + width) % width;
                            ny = (ny + height) % height;
                        }
                        else
                        {
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        }

                        if (cells[nx, ny].IsAlive)
                        {
                            alive++;
                            if (pvpMode)
                            {
                                if (cells[nx, ny].Owner == 0) countA++;
                                else if (cells[nx, ny].Owner == 1) countB++;
                            }
                        }
                    }
                }

                bool cur = cells[x, y].IsAlive;
                int curOwner = cells[x, y].Owner;
                bool nextAlive = cur;
                int nextOwner = -1;

                if (!pvpMode)
                {
                    if (cur)
                    {
                        nextAlive = (alive == 2 || alive == 3);
                        nextOwner = nextAlive ? -1 : -1;
                    }
                    else
                    {
                        nextAlive = (alive == 3);
                        nextOwner = -1;
                    }
                }
                else
                {
                    if (cur)
                    {
                        nextAlive = (alive == 2 || alive == 3);
                        nextOwner = nextAlive ? curOwner : -1;
                    }
                    else
                    {
                        if (alive == 3)
                        {
                            nextAlive = true;
                            if (countA > countB) nextOwner = 0;
                            else if (countB > countA) nextOwner = 1;
                            else nextOwner = (Random.value < 0.5f) ? 0 : 1;
                            scores[nextOwner]++;
                        }
                        else
                        {
                            nextAlive = false;
                            nextOwner = -1;
                        }
                    }
                }

                nextState[x, y] = nextAlive;
                ownerBuffer[x, y] = nextOwner;
            }
        }
    }

    void ApplyNextState()
    {
        int aliveCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int ownerToApply = ownerBuffer[x, y];
                if (!nextState[x, y]) ownerToApply = -1;
                cells[x, y].SetState(nextState[x, y], ownerToApply);
                if (cells[x, y].IsAlive) aliveCount++;
            }
        }

        UpdateScoreUI();

        // End condition: no living cells -> game finished (pause)
        if (aliveCount == 0)
        {
            if (isRunning) Pause();
            phase = Phase.Окончено;
            string winner;
            if (!pvpMode)
            {
                winner = "No cells alive — simulation ended.";
            }
            else
            {
                if (scores[0] > scores[1]) winner = $"Player A wins ({scores[0]} : {scores[1]})";
                else if (scores[1] > scores[0]) winner = $"Player B wins ({scores[1]} : {scores[0]})";
                else winner = $"Draw ({scores[0]} : {scores[1]})";
            }
            UpdateStatus($"Finished. {winner}");
        }
    }

    // Randomize grid. In PvP mode randomly assign owners for alive cells.
    public void Randomize()
    {
        Pause();
        System.Random r = new System.Random();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                bool alive = r.NextDouble() < 0.25;
                if (pvpMode)
                {
                    int owner = -1;
                    if (alive) owner = (r.NextDouble() < 0.5) ? 0 : 1;
                    cells[x, y].SetState(alive, owner);
                }
                else
                {
                    cells[x, y].SetState(alive, -1);
                }
            }
        }

        // If PvP, randomize implies skipping setup and starting running immediately
        if (pvpMode)
        {
            // reset placed / scores
            placedPieces[0] = placedPieces[1] = 0;
            scores[0] = scores[1] = 0;
            phase = Phase.Процессинг;
        }

        UpdateScoreUI();
        UpdateStatus("Randomized");
    }

    public void Clear()
    {
        Pause();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y].SetState(false, -1);

        // reset PvP state if enabled
        if (pvpMode)
        {
            phase = Phase.Установка;
            placedPieces[0] = placedPieces[1] = 0;
            scores[0] = scores[1] = 0;
            currentPlayer = 0;
            UpdateStatus($"Cleared. PvP Setup: Player A place {startingPiecesPerPlayer} pieces");
        }
        else
        {
            phase = Phase.Idle;
            UpdateStatus("Cleared.");
        }

        UpdateScoreUI();
    }

    // Full restart — clears everything and resets PvP flags
    public void Restart()
    {
        Clear();
        // keep pvpMode as is, but reset everything
        if (pvpMode)
        {
            phase = Phase.Установка;
            placedPieces[0] = placedPieces[1] = 0;
            scores[0] = scores[1] = 0;
            currentPlayer = 0;
            UpdateStatus($"Restarted. PvP Setup: Player A place {startingPiecesPerPlayer} pieces");
        }
        else
        {
            phase = Phase.Idle;
            UpdateStatus("Restarted.");
        }
    }

    public void UpdateStatus(string extra = "")
    {
        if (statusText == null) return;
        string mode = isRunning ? "Процессинг" : "Пауза";
        string phaseStr = pvpMode ? phase.ToString() : "Классика";
        string info = $"Фаза: {phaseStr}\nРежим: {mode}\nЗадержка: {stepDelay:F2}s";
        if (!string.IsNullOrEmpty(extra)) info += $"\n{extra}";
        statusText.text = info;
    }

    void UpdateScoreUI()
    {
        if (scoreText == null) return;
        if (pvpMode)
            scoreText.text = $"Игрок A: {scores[0]}   |   Игрок B: {scores[1]}\nPlaced A: {placedPieces[0]}/{startingPiecesPerPlayer} B: {placedPieces[1]}/{startingPiecesPerPlayer}";
        else
            scoreText.text = "Классический Режим";
    }

    // Called from Cell when clicked
    public void HandleCellClick(Cell cell)
    {
        // Allowed to edit in two cases:
        // - Classic: when not running (paused or idle)
        // - PvP: during Setup phase or when paused (we allow manual edit while paused too)
        if (!CanEdit()) return;

        // Calculate new state depending on context:
        if (pvpMode && phase == Phase.Установка)
        {
            // During Setup players place pieces for themselves; they cannot click on already alive cell.
            if (cell.IsAlive) return;
            if (placedPieces[currentPlayer] >= startingPiecesPerPlayer)
            {
                // if current player finished, ignore (shouldn't normally happen; we advance automatically)
                return;
            }

            cell.SetState(true, currentPlayer);
            placedPieces[currentPlayer]++;

            // If both players have finished placing -> move to Running phase and optionally start
            if (placedPieces[currentPlayer] >= startingPiecesPerPlayer)
            {
                if (currentPlayer == 0)
                {
                    currentPlayer = 1;
                    UpdateStatus($"PvP Setup: Player B place {startingPiecesPerPlayer} pieces");
                }
                else
                {
                    // both finished
                    phase = Phase.Процессинг;
                    // You may want to auto-start or wait for "Start" — here we auto-start
                    StartSim();
                    UpdateStatus("PvP Setup finished — Simulation started");
                }
            }

            UpdateScoreUI();
            return;
        }

        // Otherwise (classic or paused), clicking toggles alive/dead.
        bool newState = !cell.IsAlive;
        int ownerToSet = -1;
        if (pvpMode)
        {
            // When paused in PvP (not during Setup), let user toggle to neutral alive or to current player's color?
            // We'll set newly alive cells to currentPlayer if in paused PvP; otherwise set owner -1.
            if (newState)
            {
                // If phase is Running but paused, do not allow changing owners (require pause). We are here only when CanEdit==true
                ownerToSet = currentPlayer; // makes it easier to place additional pieces for currentPlayer
            }
            else
            {
                ownerToSet = -1;
            }
        }
        else
        {
            ownerToSet = -1;
        }

        cell.SetState(newState, ownerToSet);
        UpdateScoreUI();
    }

    // Determine whether user can edit with mouse
    public bool CanEdit()
    {
        // allow edit in Setup (PvP) or whenever not running (paused/idle)
        if (pvpMode && phase == Phase.Установка) return true;
        return !isRunning;
    }

    // Update loop handles "drag painting" like original code
    void Update()
    {
        // Only allow painting when CanEdit() == true
        if (!CanEdit())
        {
            isMouseDrawing = false;
            return;
        }

        // Handle mouse down (left = draw/toggle, right = erase)
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            Vector3 mp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 worldPos = new Vector2(mp.x, mp.y);
            int ix = Mathf.FloorToInt((worldPos.x - gridOriginWorld.x) / cellSize);
            int iy = Mathf.FloorToInt((worldPos.y - gridOriginWorld.y) / cellSize);

            if (ix >= 0 && ix < width && iy >= 0 && iy < height)
            {
                Cell c = cells[ix, iy];
                if (Input.GetMouseButtonDown(0))
                {
                    // left click: toggle (or set to alive for current player in PvP Setup)
                    if (pvpMode && phase == Phase.Установка)
                    {
                        // in setup, clicking places for current player (cannot toggle dead->alive owner differently)
                        if (!c.IsAlive && placedPieces[currentPlayer] < startingPiecesPerPlayer)
                        {
                            c.SetState(true, currentPlayer);
                            placedPieces[currentPlayer]++;
                            if (placedPieces[currentPlayer] >= startingPiecesPerPlayer)
                            {
                                if (currentPlayer == 0) currentPlayer = 1;
                                else
                                {
                                    phase = Phase.Процессинг;
                                    StartSim();
                                }
                            }
                        }
                    }
                    else
                    {
                        // paused classic or paused PvP (not setup) — toggle and set owner to currentPlayer if pvp
                        bool newState = !c.IsAlive;
                        int owner = -1;
                        if (pvpMode && newState) owner = currentPlayer;
                        c.SetState(newState, owner);
                    }

                    // start drawing mode
                    isMouseDrawing = true;
                    mouseDrawTargetState = c.IsAlive;
                    mouseDrawTargetOwner = c.Owner;
                }
                else
                {
                    // right click: erase
                    c.SetState(false, -1);
                    isMouseDrawing = true;
                    mouseDrawTargetState = false;
                    mouseDrawTargetOwner = -1;
                }

                UpdateScoreUI();
            }
        }

        // While holding the button — paint
        if (isMouseDrawing && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
        {
            Vector3 mp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 worldPos = new Vector2(mp.x, mp.y);
            int ix = Mathf.FloorToInt((worldPos.x - gridOriginWorld.x) / cellSize);
            int iy = Mathf.FloorToInt((worldPos.y - gridOriginWorld.y) / cellSize);

            if (ix >= 0 && ix < width && iy >= 0 && iy < height)
            {
                Cell c = cells[ix, iy];
                if (c.IsAlive != mouseDrawTargetState || (mouseDrawTargetState && c.Owner != mouseDrawTargetOwner))
                {
                    // If we are in Setup, restrict painting so players cannot place more than allowed
                    if (pvpMode && phase == Phase.Установка && mouseDrawTargetState)
                    {
                        if (placedPieces[currentPlayer] >= startingPiecesPerPlayer) return;
                        if (!c.IsAlive)
                        {
                            c.SetState(true, currentPlayer);
                            placedPieces[currentPlayer]++;
                            if (placedPieces[currentPlayer] >= startingPiecesPerPlayer)
                            {
                                if (currentPlayer == 0) currentPlayer = 1;
                                else
                                {
                                    phase = Phase.Процессинг;
                                    StartSim();
                                }
                            }
                        }
                    }
                    else
                    {
                        c.SetState(mouseDrawTargetState, mouseDrawTargetOwner);
                    }
                }
            }
        }

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
        {
            isMouseDrawing = false;
        }
    }
}
