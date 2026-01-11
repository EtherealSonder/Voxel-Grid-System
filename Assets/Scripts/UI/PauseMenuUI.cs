using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PauseMenuUI : MonoBehaviour
{

    [Header("UI Roots")]
    [SerializeField] private GameObject pauseRoot;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject savePanel;
    [SerializeField] private GameObject loadPanel;

    [Header("Main Buttons")]
    [SerializeField] private Button openSaveButton;
    [SerializeField] private Button openLoadButton;

    [Header("Save Panel")]
    [SerializeField] private Button backFromSaveButton;
    [SerializeField] private Button[] saveSlotButtons;
    [SerializeField] private TMP_Text[] saveSlotLabels;

    [Header("Load Panel")]
    [SerializeField] private Button backFromLoadButton;
    [SerializeField] private Button[] loadSlotButtons;
    [SerializeField] private TMP_Text[] loadSlotLabels;

    private SaveLoadManager saveLoad;
    private MonoBehaviour flyingCameraScript;
    private InteractionManager interactionManager;
    private MonoBehaviour interactionFeedbackUI;


    private bool isPaused;
    private float previousTimeScale = 1f;


    [Header("Rebuild Controls")]
    [SerializeField] private Slider maxShapesSlider;
    [SerializeField] private TMP_Text maxShapesValueText;
    [SerializeField] private Toggle proceduralShapesToggle;
    [SerializeField] private Button rebuildButton;

    [SerializeField] private int minShapes = 5;
    private void Awake()
    {
        CacheReferencesFromWorldManager();
        BindUiEvents();
        BindRebuildControls();

        SetUnpausedStateImmediate();
    }

    private void BindRebuildControls()
    {
        if (maxShapesSlider != null)
        {
            maxShapesSlider.wholeNumbers = true;

            maxShapesSlider.onValueChanged.AddListener(OnMaxShapesSliderChanged);
            OnMaxShapesSliderChanged(maxShapesSlider.value);
        }

        if (rebuildButton != null)
        {
            rebuildButton.onClick.AddListener(OnRebuildClicked);
        }
    }

    private void OnMaxShapesSliderChanged(float value)
    {
        int v = Mathf.RoundToInt(value);

        if (maxShapesValueText != null)
            maxShapesValueText.text = v.ToString();
    }

    private void OnRebuildClicked()
    {
        if (!EnsureGameplayRefs())
            return;

        PolycubeSpawner spawner = WorldManager.Instance.polycubeSpawner;
        if (spawner == null)
        {
            Debug.LogError("PauseMenuUI: WorldManager is missing polycubeSpawner reference.");
            return;
        }

        int maxCount = minShapes;
        if (maxShapesSlider != null)
            maxCount = Mathf.RoundToInt(maxShapesSlider.value);

        PolycubeSpawner.SpawnMode mode = PolycubeSpawner.SpawnMode.Predefined;
        if (proceduralShapesToggle != null && proceduralShapesToggle.isOn)
            mode = PolycubeSpawner.SpawnMode.RandomProcedural;

        spawner.Rebuild(mode, minShapes, maxCount);
        Resume();

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }
    }


    #region Pause And Resume

    private void Pause()
    {
        if (isPaused)
            return;

        if (!EnsureGameplayRefs())
            return;

        isPaused = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SetGameplayEnabled(false);
        SetCursorForPause(true);

        if (pauseRoot != null)
            pauseRoot.SetActive(true);

        ShowMainPanel();
        RefreshSlotUi();
    }

    private void Resume()
    {
        if (!isPaused)
            return;

        if (!EnsureGameplayRefs())
            return;

        isPaused = false;

        Time.timeScale = previousTimeScale;
        if (Time.timeScale <= 0f)
            Time.timeScale = 1f;

        SetGameplayEnabled(true);
        SetCursorForPause(false);

        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        ShowMainPanel();
    }

    private void SetGameplayEnabled(bool enabled)
    {
        if (flyingCameraScript != null)
            flyingCameraScript.enabled = enabled;

        if (interactionManager != null)
            interactionManager.enabled = enabled;

        if (interactionFeedbackUI != null)
            interactionFeedbackUI.enabled = enabled;

    }

    private static void SetCursorForPause(bool paused)
    {
        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    #endregion

    #region Panel Navigation

    private void ShowMainPanel()
    {
        SetPanelState(showMain: true, showSave: false, showLoad: false);
    }

    private void ShowSavePanel()
    {
        if (!isPaused)
            return;

        SetPanelState(showMain: false, showSave: true, showLoad: false);
        RefreshSlotUi();
    }

    private void ShowLoadPanel()
    {
        if (!isPaused)
            return;

        SetPanelState(showMain: false, showSave: false, showLoad: true);
        RefreshSlotUi();
    }

    private void SetPanelState(bool showMain, bool showSave, bool showLoad)
    {
        if (mainPanel != null)
            mainPanel.SetActive(showMain);

        if (savePanel != null)
            savePanel.SetActive(showSave);

        if (loadPanel != null)
            loadPanel.SetActive(showLoad);
    }

    #endregion

    #region UI Binding

    private void BindUiEvents()
    {
        if (openSaveButton != null)
            openSaveButton.onClick.AddListener(ShowSavePanel);

        if (openLoadButton != null)
            openLoadButton.onClick.AddListener(ShowLoadPanel);

        if (backFromSaveButton != null)
            backFromSaveButton.onClick.AddListener(ShowMainPanel);

        if (backFromLoadButton != null)
            backFromLoadButton.onClick.AddListener(ShowMainPanel);

        BindSlotButtons(saveSlotButtons, OnSaveSlotClicked);
        BindSlotButtons(loadSlotButtons, OnLoadSlotClicked);
    }

    private void BindSlotButtons(Button[] buttons, System.Action<int> onClickSlot)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null)
                continue;

            int slot = i + 1;
            b.onClick.AddListener(() => onClickSlot(slot));
        }
    }

    #endregion

    #region Slot Actions

    private void OnSaveSlotClicked(int slot)
    {
        if (!EnsureGameplayRefs())
            return;

        saveLoad.SaveToSlot(slot);
        RefreshSlotUi();
    }

    private void OnLoadSlotClicked(int slot)
    {
        if (!EnsureGameplayRefs())
            return;

        if (!saveLoad.DoesSlotExist(slot))
            return;

        Resume();
        saveLoad.LoadFromSlot(slot);
    }

    #endregion

    private void RefreshSlotUi()
    {
        if (!EnsureGameplayRefs())
            return;

        int saveCount = saveSlotButtons != null ? saveSlotButtons.Length : 0;
        int loadCount = loadSlotButtons != null ? loadSlotButtons.Length : 0;
        int slotCount = Mathf.Max(saveCount, loadCount);

        if (slotCount <= 0)
            return;

        for (int i = 0; i < slotCount; i++)
        {
            int slot = i + 1;

            bool exists = saveLoad.DoesSlotExist(slot);

            string timestamp;
            bool hasTimestamp = saveLoad.TryGetSlotTimestamp(slot, out timestamp);

            string line2 = (exists && hasTimestamp) ? timestamp : "Empty";
            string labelText = "Slot " + slot + "\n" + line2;

            if (saveSlotLabels != null && i < saveSlotLabels.Length && saveSlotLabels[i] != null)
                saveSlotLabels[i].text = labelText;

            if (loadSlotLabels != null && i < loadSlotLabels.Length && loadSlotLabels[i] != null)
                loadSlotLabels[i].text = labelText;

            if (loadSlotButtons != null && i < loadSlotButtons.Length && loadSlotButtons[i] != null)
                loadSlotButtons[i].interactable = exists;
        }
    }

    private void CacheReferencesFromWorldManager()
    {
        WorldManager wm = WorldManager.Instance;
        if (wm == null)
        {
            Debug.LogError("PauseMenuUI: WorldManager.Instance is missing.");
            return;
        }

        saveLoad = wm.saveLoadManager;
        flyingCameraScript = wm.flyingCamera;
        interactionManager = wm.interactionManager;
        interactionFeedbackUI = wm.interactionFeedbackUI;
    }

    private bool EnsureGameplayRefs()
    {
        if (saveLoad == null || flyingCameraScript == null || interactionManager == null || interactionFeedbackUI == null)
        {
            CacheReferencesFromWorldManager();
        }

        if (saveLoad == null)
        {
            Debug.LogError("PauseMenuUI: Missing SaveLoadManager reference on WorldManager.");
            return false;
        }

        if (flyingCameraScript == null)
        {
            Debug.LogError("PauseMenuUI: Missing flying camera script reference on WorldManager.");
            return false;
        }

        if (interactionManager == null)
        {
            Debug.LogError("PauseMenuUI: Missing InteractionManager reference on WorldManager.");
            return false;
        }

        if (interactionFeedbackUI == null)
        {
            Debug.LogError("PauseMenuUI: Missing crosshair UI script reference on WorldManager.");
            return false;
        }

        return true;
    }

    private void SetUnpausedStateImmediate()
    {
        isPaused = false;

        previousTimeScale = 1f;
        Time.timeScale = 1f;

        SetCursorForPause(false);

        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        ShowMainPanel();

        if (EnsureGameplayRefs())
            SetGameplayEnabled(true);
    }

}
