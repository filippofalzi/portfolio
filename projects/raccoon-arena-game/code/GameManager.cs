using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameEvent
{
    PlayerDamage,
    GameEnd
}

public class GameManager : Subject<GameEvent>, IObserver<GameEvent>
{
    public static GameManager Instance;

    [SerializeField] private RacoonHero player;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private GameObject gameOverText;
    [SerializeField] private GameObject youWinText;
    [SerializeField] private EvilRat evilRat;

    [HideInInspector] public bool gameEnded;
    [HideInInspector] public bool playerWins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        gameEnded = false;
        playerWins = false;

        if (endPanel != null)
            endPanel.SetActive(false);

        if (gameOverText != null)
            gameOverText.SetActive(false);

        if (youWinText != null)
            youWinText.SetActive(false);

        if (player != null)
        {
            player.AddObserver(this);
            AddObserver(player);
        }

        if (evilRat != null)
        {
            evilRat.AddObserver(this);
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.RemoveObserver(this);
            RemoveObserver(player);
        }

        if (evilRat != null)
        {
            evilRat.RemoveObserver(this);
        }

        if (Instance == this)
            Instance = null;
    }

    public void EndGame(bool win)
    {
        if (gameEnded)
            return;

        gameEnded = true;
        playerWins = win;

        if (endPanel != null)
            endPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.SetActive(!win);

        if (youWinText != null)
            youWinText.SetActive(win);

        Notify(GameEvent.GameEnd, win);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnNotify(GameEvent gameEvent, object data)
    {
        switch (gameEvent)
        {
            case GameEvent.PlayerDamage:
                if (data is float[] health)
                    Debug.Log("Raccoon health: " + health[0]);
                break;

            case GameEvent.GameEnd:
                if (data is bool win)
                    EndGame(win);
                break;
        }
    }
}