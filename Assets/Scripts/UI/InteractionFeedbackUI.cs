using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractionFeedbackUI : MonoBehaviour
{
    private Camera cam;
    private InteractionManager interactionManager;

    public float hoverDistance = 8f;
    public LayerMask raycastMask = ~0;

    private string invalidPlacementText = "Invalid position";
    private bool preferDefinitionId = true;

    public Image crosshairImage;
    public TMP_Text hoverLabel;

    public Sprite normalCrosshairSprite;
    public Sprite grabCrosshairSprite;
    public Sprite placeCrosshairSprite;
    public Sprite invalidCrosshairSprite;

    public AudioSource audioSource;
    public AudioClip invalidSound;

    private PolycubeInstance currentHover;
    private bool showStickyInvalidMessage;

    private void Awake()
    {
        ResolveWorldReferences();
        ResolveLocalReferences();
        ApplyIdleUI();
    }

    private void Update()
    {
        ResolveWorldReferences();

        if (!IsUiWired())
            return;

        if (interactionManager != null && interactionManager.IsHolding)
        {
            UpdateHoldingUi();
        }
        else
        {
            showStickyInvalidMessage = false;
            UpdateHoverIdleUi();
        }
    }

    private void ResolveWorldReferences()
    {
        if (WorldManager.Instance == null)
            return;

        if (cam == null)
            cam = WorldManager.Instance.playerCamera;

        if (interactionManager == null)
            interactionManager = WorldManager.Instance.interactionManager;
    }

    private void ResolveLocalReferences()
    {
        if (crosshairImage == null)
            crosshairImage = GetComponentInChildren<Image>(true);

        if (hoverLabel == null)
            hoverLabel = GetComponentInChildren<TMP_Text>(true);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
  }

    #region Holding State

    private void UpdateHoldingUi()
    {
        currentHover = null;

        bool canPlace = false;
        if (interactionManager != null)
            canPlace = interactionManager.LastPlacementValid;

        if (canPlace)
        {
            ApplyPlaceValidUi();
            return;
        }

        ApplyPlaceInvalidUi();

        if (Input.GetMouseButtonDown(0))
        {
            if (interactionManager != null && Time.frameCount == interactionManager.LastPickupFrame)
                return;

            showStickyInvalidMessage = true;
            PlayInvalidSound();
        }

        if (hoverLabel != null)
        {
            hoverLabel.gameObject.SetActive(showStickyInvalidMessage);
            if (showStickyInvalidMessage)
                hoverLabel.text = invalidPlacementText;
        }
    }

    private void ApplyPlaceValidUi()
    {
        if (crosshairImage != null && placeCrosshairSprite != null)
            crosshairImage.sprite = placeCrosshairSprite;

        if (hoverLabel != null)
            hoverLabel.gameObject.SetActive(false);

        showStickyInvalidMessage = false;
    }

    private void ApplyPlaceInvalidUi()
    {
        if (crosshairImage != null && invalidCrosshairSprite != null)
            crosshairImage.sprite = invalidCrosshairSprite;
    }

    #endregion

    #region Hover/Idle State

    private void UpdateHoverIdleUi()
    {
        PolycubeInstance hoveredShape = RaycastForHoverShape();

        if (hoveredShape != null)
        {
            currentHover = hoveredShape;
            ApplyHoverUi(hoveredShape);
            return;
        }

        currentHover = null;
        ApplyIdleUI();
    }

    private PolycubeInstance RaycastForHoverShape()
    {
        if (cam == null)
            return null;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, hoverDistance, raycastMask, QueryTriggerInteraction.Ignore);
        if (!didHit) return null;

        if (hit.collider == null) return null;

        return hit.collider.GetComponentInParent<PolycubeInstance>();
    }

    private void ApplyHoverUi(PolycubeInstance shape)
    {
        if (crosshairImage != null && grabCrosshairSprite != null)
            crosshairImage.sprite = grabCrosshairSprite;

        if (hoverLabel != null)
        {
            hoverLabel.text = BuildHoverLabel(shape);
            hoverLabel.gameObject.SetActive(true);
        }
    }

    private void ApplyIdleUI()
    {
        if (crosshairImage != null && normalCrosshairSprite != null)
            crosshairImage.sprite = normalCrosshairSprite;

        if (hoverLabel != null)
            hoverLabel.gameObject.SetActive(false);
    }

    private string BuildHoverLabel(PolycubeInstance shape)
    {
        if (shape == null) return string.Empty;

        if (!preferDefinitionId)
            return shape.gameObject.name;

        PolycubeDefinition def = shape.GetDefinition();
        if (def == null)
            return shape.gameObject.name;

        return def.GetId();
    }

    #endregion

    private void PlayInvalidSound()
    {
        if (audioSource == null) return;
        if (invalidSound == null) return;

        audioSource.PlayOneShot(invalidSound);
    }

    private bool IsUiWired()
    {
        if (cam == null) return false;
        if (crosshairImage == null) return false;
        if (hoverLabel == null) return false;
        return true;
    }
}
