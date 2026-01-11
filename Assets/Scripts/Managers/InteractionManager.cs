using UnityEngine;

public class InteractionManager : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private float rayDistance = 200f;
    [SerializeField] private LayerMask placementMask = ~0;

    [Header("Fallback Targeting")]
    [SerializeField] private float fallbackDepth = 7f;

    [Header("Movement")]
    [SerializeField] private float baseFollowSpeed = 20f;
    [SerializeField] private float catchUpMultiplier = 12f;
    [SerializeField] private float snapEpsilon = 0.02f;

    [Header("Rotation")]
    [SerializeField] private float rotateDuration = 0.12f;

    [Header("Held Glow Feedback")]
    [SerializeField] private Color validGlowColor = new Color(0.1f, 1.0f, 0.1f, 1f);
    [SerializeField] private Color invalidGlowColor = new Color(1.0f, 0.15f, 0.15f, 1f);
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float validGlowMin = 0.10f;
    [SerializeField] private float validGlowMax = 0.45f;
    [SerializeField] private float invalidGlowMin = 0.20f;
    [SerializeField] private float invalidGlowMax = 0.70f;

    [Header("Sounds")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip placeSound;

    private Camera playerCamera;
    private GridOccupancy grid;

    private PolycubeInstance hoveredShape;


    private int lastPickupFrame = -999;

    public int LastPickupFrame
    {
        get { return lastPickupFrame; }
    }
    private struct HoldSession
    {
        public PolycubeInstance shape;

        public Vector3Int pickupPivotCell;
        public Quaternion pickupRotation;

        public Vector3Int targetPivotCell;
        public Quaternion targetRotation;

        public bool isRotating;
        public Quaternion rotateFrom;
        public Quaternion rotateTo;
        public float rotateElapsed;

        public bool lastPlacementValid;

        public Renderer[] cachedRenderers;

        public bool IsActive()
        {
            return shape != null;
        }
    }

    private HoldSession holdSession;

    private MaterialPropertyBlock materialProperty;
    private float pulseT;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowStrengthId = Shader.PropertyToID("_GlowStrength");

    public bool IsHolding
    {
        get { return holdSession.IsActive(); }
    }

    public bool LastPlacementValid
    {
        get
        {
            if (!holdSession.IsActive()) return false;
            return holdSession.lastPlacementValid;
        }
    }

    private void Awake()
    {
        materialProperty = new MaterialPropertyBlock();
        ResolveDependencies();
    }

    private void Update()
    {
        ResolveDependencies();
        if (grid == null || playerCamera == null) return;

        if (!holdSession.IsActive())
        {
            UpdateIdle();
        }
        else
        {
            UpdateHolding();
        }
    }

    private void ResolveDependencies()
    {
        if (playerCamera != null && grid != null) return;

        if (WorldManager.Instance == null) return;

        playerCamera = WorldManager.Instance.playerCamera;
        grid = WorldManager.Instance.occupancy;
    }

    #region Idle State

    private void UpdateIdle()
    {
        hoveredShape = RaycastForPolycube();

        if (Input.GetMouseButtonDown(0) && hoveredShape != null)
        {
            BeginHold(hoveredShape);
        }
    }

    private PolycubeInstance RaycastForPolycube()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, rayDistance, placementMask, QueryTriggerInteraction.Ignore);
        if (!didHit) return null;

        if (hit.collider == null) return null;

        PolycubeInstance found = hit.collider.GetComponentInParent<PolycubeInstance>();
        return found;
    }

    #endregion

    #region Holding State

    private void UpdateHolding()
    {
        if (holdSession.shape == null)
        {
            EndHoldCleanup();
            return;
        }

        if (holdSession.shape.GetDefinition() == null)
        {
            EndHoldCleanup();
            return;
        }

        HandleRotationInput();

        Vector3Int desiredPivotCell;
        ComputeTargetPivotCell(out desiredPivotCell);

        holdSession.targetPivotCell = grid.ClampPivotToBounds(
            holdSession.shape.GetDefinition(),
            desiredPivotCell,
            holdSession.targetRotation
        );

        holdSession.lastPlacementValid = grid.CanPlace(
            holdSession.shape.GetDefinition(),
            holdSession.targetPivotCell,
            holdSession.targetRotation
        );

        UpdateHeldTransform();
        UpdateHeldGlow(holdSession.lastPlacementValid);

        if (!holdSession.isRotating)
        {
            if (Input.GetMouseButtonDown(0)) TryPlaceHeld();
            if (Input.GetMouseButtonDown(1)) TryCancelHeld();
        }
    }

    private void ComputeTargetPivotCell(out Vector3Int desiredPivotCell)
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, rayDistance, placementMask, QueryTriggerInteraction.Ignore);

        if (didHit)
        {
            Vector3Int anchorCell = WorldToCell(hit.point);
            Vector3Int normalOffset = NormalToOffset(hit.normal);
            desiredPivotCell = anchorCell + normalOffset;
            return;
        }

        Vector3 fallbackPoint = GetPointOnCameraPlane(fallbackDepth);
        desiredPivotCell = WorldToCell(fallbackPoint);
    }

    private void UpdateHeldTransform()
    {
        Vector3 targetWorldPos = CellToWorld(holdSession.targetPivotCell);

        Vector3 current = holdSession.shape.transform.position;
        float dist = Vector3.Distance(current, targetWorldPos);

        float speed = dist * catchUpMultiplier;
        if (speed < baseFollowSpeed) speed = baseFollowSpeed;

        holdSession.shape.transform.position = Vector3.MoveTowards(current, targetWorldPos, speed * Time.deltaTime);

        if (dist <= snapEpsilon)
        {
            holdSession.shape.transform.position = targetWorldPos;
        }

        if (holdSession.isRotating)
        {
            holdSession.rotateElapsed += Time.deltaTime;

            float denom = rotateDuration;
            if (denom < 0.0001f) denom = 0.0001f;

            float t = holdSession.rotateElapsed / denom;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            t = t * t * (3f - 2f * t);

            holdSession.shape.transform.rotation = Quaternion.Slerp(holdSession.rotateFrom, holdSession.rotateTo, t);

            if (holdSession.rotateElapsed >= rotateDuration)
            {
                holdSession.isRotating = false;
                holdSession.shape.transform.rotation = holdSession.rotateTo;
            }
        }
        else
        {
            holdSession.shape.transform.rotation = holdSession.targetRotation;
        }
    }

    #endregion

    #region Placement

    private void BeginHold(PolycubeInstance shape)
    {
        lastPickupFrame = Time.frameCount;

        holdSession = default;
        holdSession.shape = shape;

        holdSession.pickupPivotCell = shape.GetPivotCell();
        holdSession.pickupRotation = SnapToRightAngles(shape.transform.rotation);

        holdSession.targetPivotCell = holdSession.pickupPivotCell;
        holdSession.targetRotation = holdSession.pickupRotation;

        grid.UnregisterPlaced(shape.GetDefinition(), holdSession.pickupPivotCell, holdSession.pickupRotation);

        shape.SetCollidersEnabled(false);

        holdSession.cachedRenderers = shape.GetComponentsInChildren<Renderer>(true);

        Vector3 startPoint = GetPointOnCameraPlane(fallbackDepth);
        Vector3Int startPivotCell = WorldToCell(startPoint);

        holdSession.targetPivotCell = grid.ClampPivotToBounds(shape.GetDefinition(), startPivotCell, holdSession.targetRotation);
        shape.ApplyState(holdSession.targetPivotCell, holdSession.targetRotation);

        holdSession.lastPlacementValid = grid.CanPlace( holdSession.shape.GetDefinition(), holdSession.targetPivotCell, holdSession.targetRotation);

        PlayOneShot(pickupSound);
    }

    private void TryPlaceHeld()
    {
        if (!holdSession.lastPlacementValid) return;

        grid.RegisterPlaced(holdSession.shape.GetDefinition(), holdSession.targetPivotCell, holdSession.targetRotation);

        holdSession.shape.SetCollidersEnabled(true);
        holdSession.shape.ApplyState(holdSession.targetPivotCell, holdSession.targetRotation);

        PlayOneShot(placeSound);
        EndHoldCleanup();
    }

    private void TryCancelHeld()
    {
        bool canReturn = grid.CanPlace(holdSession.shape.GetDefinition(), holdSession.pickupPivotCell, holdSession.pickupRotation);
        if (!canReturn) return;

        grid.RegisterPlaced(holdSession.shape.GetDefinition(), holdSession.pickupPivotCell, holdSession.pickupRotation);

        holdSession.shape.SetCollidersEnabled(true);
        holdSession.shape.ApplyState(holdSession.pickupPivotCell, holdSession.pickupRotation);

        EndHoldCleanup();
    }

    private void EndHoldCleanup()
    {
        if (holdSession.shape != null)
        {
            ClearHeldGlow();
        }

        holdSession = default;
        hoveredShape = null;
        pulseT = 0f;
    }

    #endregion

    #region Rotation

    private void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            RequestRotate(Vector3.right, 90f);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            RequestRotate(Vector3.up, 90f);
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            RequestRotate(Vector3.forward, 90f);
        }
    }

    private void RequestRotate(Vector3 axis, float degrees)
    {
        if (!holdSession.IsActive()) return;
        if (holdSession.isRotating) return;

        Quaternion next = Quaternion.AngleAxis(degrees, axis) * holdSession.targetRotation;
        holdSession.targetRotation = SnapToRightAngles(next);

        holdSession.isRotating = true;
        holdSession.rotateFrom = holdSession.shape.transform.rotation;
        holdSession.rotateTo = holdSession.targetRotation;
        holdSession.rotateElapsed = 0f;
    }

    #endregion

    #region Glow

    private void UpdateHeldGlow(bool isValid)
    {
        if (holdSession.cachedRenderers == null) return;
        if (holdSession.cachedRenderers.Length == 0) return;

        pulseT += Time.deltaTime * pulseSpeed;
        float s = (Mathf.Sin(pulseT) * 0.5f) + 0.5f;

        Color glowColor;
        float glowStrength;

        if (isValid)
        {
            glowColor = validGlowColor;
            glowStrength = Mathf.Lerp(validGlowMin, validGlowMax, s);
        }
        else
        {
            glowColor = invalidGlowColor;
            glowStrength = Mathf.Lerp(invalidGlowMin, invalidGlowMax, s);
        }

        for (int i = 0; i < holdSession.cachedRenderers.Length; i++)
        {
            Renderer r = holdSession.cachedRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(materialProperty);

            materialProperty.SetColor(GlowColorId, glowColor);
            materialProperty.SetFloat(GlowStrengthId, glowStrength);

            r.SetPropertyBlock(materialProperty);
        }
    }

    private void ClearHeldGlow()
    {
        if (holdSession.cachedRenderers == null) return;
        if (holdSession.cachedRenderers.Length == 0) return;

        for (int i = 0; i < holdSession.cachedRenderers.Length; i++)
        {
            Renderer r = holdSession.cachedRenderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(materialProperty);

            materialProperty.SetColor(GlowColorId, Color.black);
            materialProperty.SetFloat(GlowStrengthId, 0f);

            r.SetPropertyBlock(materialProperty);
        }
    }

    #endregion

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource == null) return;
        if (clip == null) return;

        audioSource.PlayOneShot(clip);
    }

    private Vector3 GetPointOnCameraPlane(float distance)
    {
        return playerCamera.transform.position + playerCamera.transform.forward * distance;
    }

    private static Vector3Int WorldToCell(Vector3 world)
    {
        return new Vector3Int(
            Mathf.FloorToInt(world.x),
            Mathf.FloorToInt(world.y),
            Mathf.FloorToInt(world.z)
        );
    }

    private static Vector3 CellToWorld(Vector3Int cell)
    {
        return cell + Vector3.one * 0.5f;
    }

    private static Vector3Int NormalToOffset(Vector3 n)
    {
        n = n.normalized;

        if (Mathf.Abs(n.x) > 0.9f) return new Vector3Int((int)Mathf.Sign(n.x), 0, 0);
        if (Mathf.Abs(n.y) > 0.9f) return new Vector3Int(0, (int)Mathf.Sign(n.y), 0);
        if (Mathf.Abs(n.z) > 0.9f) return new Vector3Int(0, 0, (int)Mathf.Sign(n.z));

        return Vector3Int.up;
    }

    private static Quaternion SnapToRightAngles(Quaternion q)
    {
        Vector3 e = q.eulerAngles;

        e.x = Mathf.Round(e.x / 90f) * 90f;
        e.y = Mathf.Round(e.y / 90f) * 90f;
        e.z = Mathf.Round(e.z / 90f) * 90f;

        return Quaternion.Euler(e);
    }
}
