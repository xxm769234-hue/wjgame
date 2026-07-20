using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IdleAdventureBattleHud : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform root;
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI friendlyCountText;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button pauseButton;
    [SerializeField] private TextMeshProUGUI pauseButtonText;
    [SerializeField] private RectTransform friendlySlotsRoot;
    [SerializeField] private RectTransform enemySlotsRoot;

    public Canvas Canvas => canvas != null ? canvas : GetComponent<Canvas>();
    public RectTransform Root => root != null ? root : transform as RectTransform;
    public TextMeshProUGUI StageText => stageText;
    public TextMeshProUGUI TurnText => turnText;
    public TextMeshProUGUI FriendlyCountText => friendlyCountText;
    public TextMeshProUGUI EnemyCountText => enemyCountText;
    public TextMeshProUGUI LogText => logText;
    public TextMeshProUGUI ResultText => resultText;
    public Button PauseButton => pauseButton;
    public TextMeshProUGUI PauseButtonText => pauseButtonText;
    public RectTransform FriendlySlotsRoot => friendlySlotsRoot;
    public RectTransform EnemySlotsRoot => enemySlotsRoot;

    public bool HasBindings =>
        Canvas != null &&
        Root != null &&
        stageText != null &&
        turnText != null &&
        friendlyCountText != null &&
        enemyCountText != null &&
        logText != null &&
        resultText != null &&
        pauseButton != null &&
        pauseButtonText != null &&
        friendlySlotsRoot != null &&
        enemySlotsRoot != null;

    public void Initialize(Canvas canvasComponent, RectTransform rootTransform, TextMeshProUGUI stageLabel, TextMeshProUGUI turnLabel, TextMeshProUGUI friendlyCountLabel, TextMeshProUGUI enemyCountLabel, TextMeshProUGUI logLabel, TextMeshProUGUI resultLabel, Button pauseButtonControl, TextMeshProUGUI pauseButtonLabel, RectTransform friendlySlots, RectTransform enemySlots)
    {
        canvas = canvasComponent;
        root = rootTransform;
        stageText = stageLabel;
        turnText = turnLabel;
        friendlyCountText = friendlyCountLabel;
        enemyCountText = enemyCountLabel;
        logText = logLabel;
        resultText = resultLabel;
        pauseButton = pauseButtonControl;
        pauseButtonText = pauseButtonLabel;
        friendlySlotsRoot = friendlySlots;
        enemySlotsRoot = enemySlots;
    }
}
