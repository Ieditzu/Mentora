using UnityEngine;
using UnityEngine.UI;
using Oculus.Interaction;

/// <summary>
/// Adds VR ray pointer interaction to the PauseMenu canvas.
/// Shows a cursor when the controller ray hits the menu, similar to Meta's VR menu.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class PauseMenuVrPointer : MonoBehaviour
{
    [Header("Cursor Visual")]
    [SerializeField] private GameObject cursorPrefab;
    [SerializeField] private float cursorScale = 0.01f;
    [SerializeField] private float cursorOffset = 0.005f;
    
    [Header("Cursor Colors")]
    [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color hoverColor = new Color(0.35f, 0.72f, 0.95f, 0.95f);
    [SerializeField] private Color selectColor = new Color(0.18f, 0.62f, 0.32f, 0.95f);
    
    [Header("Ray Visual")]
    [SerializeField] private bool showRayWhenPointing = true;
    [SerializeField] private LineRenderer rayLineRenderer;
    [SerializeField] private Color rayDefaultColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color rayHoverColor = new Color(0.35f, 0.72f, 0.95f, 0.5f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip hoverEnterSound;
    [SerializeField] private AudioClip hoverExitSound;
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private float audioVolume = 0.5f;
    
    private Canvas canvas;
    private PointableCanvas pointableCanvas;
    private GameObject cursorInstance;
    private Renderer cursorRenderer;
    private AudioSource audioSource;
    private bool wasHovering;
    
    // Shader property IDs for cursor material
    private static readonly int ShaderColor = Shader.PropertyToID("_Color");
    private static readonly int ShaderOutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int ShaderRadialScale = Shader.PropertyToID("_RadialGradientScale");
    
    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        
        // Ensure canvas has GraphicRaycaster for PointableCanvas
        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        
        // Add PointableCanvas component
        pointableCanvas = gameObject.GetComponent<PointableCanvas>();
        if (pointableCanvas == null)
        {
            pointableCanvas = gameObject.AddComponent<PointableCanvas>();
        }
        
        // Setup PointableCanvas reference
        pointableCanvas.InjectCanvas(canvas);
        
        // Add audio source for feedback
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio
        }
    }
    
    private void Start()
    {
        EnsurePointableCanvasModule();
        CreateCursor();
        CreateRayVisual();
        
        // Subscribe to PointableCanvas events
        if (pointableCanvas != null)
        {
            pointableCanvas.WhenPointerEventRaised += OnPointerEvent;
        }
    }
    
    private void OnDestroy()
    {
        if (pointableCanvas != null)
        {
            pointableCanvas.WhenPointerEventRaised -= OnPointerEvent;
        }
    }
    
    /// <summary>
/// Ensures a PointableCanvasModule exists in the scene for VR pointer support.
/// </summary>
private void EnsurePointableCanvasModule()
{
    // Check for existing module
    PointableCanvasModule existingModule = FindObjectOfType<PointableCanvasModule>();
    if (existingModule != null) return;

    // Find or create EventSystem
    var eventSystem = UnityEngine.EventSystems.EventSystem.current;
    if (eventSystem == null)
    {
        eventSystem = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
    }

    GameObject targetObject;
    if (eventSystem != null)
    {
        targetObject = eventSystem.gameObject;
    }
    else
    {
        targetObject = new GameObject("EventSystem");
        targetObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
        targetObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    // Add PointableCanvasModule
    targetObject.AddComponent<PointableCanvasModule>();
}
    
    private void CreateCursor()
    {
        // Create cursor if no prefab assigned
        if (cursorPrefab == null)
        {
            // Create a simple ring cursor
            GameObject cursorGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(cursorGo.GetComponent<Collider>());
            
            cursorRenderer = cursorGo.GetComponent<Renderer>();
            cursorRenderer.material = new Material(Shader.Find("Interaction/OculusHandCursor"));
            
            // Configure cursor material
            cursorRenderer.material.SetColor(ShaderColor, defaultColor);
            cursorRenderer.material.SetColor(ShaderOutlineColor, defaultColor);
            cursorRenderer.material.SetFloat(ShaderRadialScale, 0.2f);
            
            cursorInstance = cursorGo;
        }
        else
        {
            cursorInstance = Instantiate(cursorPrefab);
            cursorRenderer = cursorInstance.GetComponent<Renderer>();
        }
        
        cursorInstance.name = "PauseMenuVrCursor";
        cursorInstance.transform.localScale = Vector3.one * cursorScale;
        cursorInstance.SetActive(false);
    }
    
    private void CreateRayVisual()
    {
        if (!showRayWhenPointing) return;
        
        if (rayLineRenderer == null)
        {
            GameObject rayGo = new GameObject("PauseMenuVrRay");
            rayGo.transform.SetParent(transform);
            
            rayLineRenderer = rayGo.AddComponent<LineRenderer>();
            rayLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            rayLineRenderer.startWidth = 0.005f;
            rayLineRenderer.endWidth = 0.002f;
            rayLineRenderer.useWorldSpace = true;
            rayLineRenderer.positionCount = 2;
        }
        
        rayLineRenderer.enabled = false;
    }
    
    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                OnHover(evt.Pose.position, evt.Pose.rotation);
                break;
                
            case PointerEventType.Unhover:
                OnUnhover();
                break;
                
            case PointerEventType.Select:
                OnSelect();
                break;
                
            case PointerEventType.Unselect:
                OnUnselect();
                break;
        }
    }
    
    private void OnHover(Vector3 position, Quaternion rotation)
    {
        // Show cursor
        if (cursorInstance != null)
        {
            cursorInstance.SetActive(true);
            cursorInstance.transform.position = position + cursorInstance.transform.forward * cursorOffset;
            cursorInstance.transform.rotation = rotation;
            
            // Update color
            if (cursorRenderer != null)
            {
                cursorRenderer.material.SetColor(ShaderColor, hoverColor);
            }
        }
        
        // Update ray visual
        if (rayLineRenderer != null && showRayWhenPointing)
        {
            rayLineRenderer.enabled = true;
            rayLineRenderer.SetPosition(0, position - cursorInstance.transform.forward * 2f); // Ray origin
            rayLineRenderer.SetPosition(1, position);
            rayLineRenderer.startColor = rayHoverColor;
            rayLineRenderer.endColor = rayHoverColor;
        }
        
        // Play hover sound
        if (!wasHovering && audioSource != null)
        {
            if (hoverEnterSound != null)
            {
                audioSource.PlayOneShot(hoverEnterSound, audioVolume);
            }
            else
            {
                AudioManager.PlayHover();
            }
        }
        
        wasHovering = true;
    }
    
    private void OnUnhover()
    {
        // Hide cursor
        if (cursorInstance != null)
        {
            cursorInstance.SetActive(false);
        }
        
        // Hide ray
        if (rayLineRenderer != null)
        {
            rayLineRenderer.enabled = false;
        }
        
        // Play hover exit sound
        if (wasHovering && hoverExitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hoverExitSound, audioVolume * 0.5f);
        }
        
        wasHovering = false;
    }
    
    private void OnSelect()
    {
        // Update cursor to select color
        if (cursorRenderer != null)
        {
            cursorRenderer.material.SetColor(ShaderColor, selectColor);
            cursorRenderer.material.SetFloat(ShaderRadialScale, 0.1f); // Shrink on select
        }
        
        // Play select sound
        if (audioSource != null)
        {
            if (selectSound != null)
            {
                audioSource.PlayOneShot(selectSound, audioVolume);
            }
            else
            {
                AudioManager.Play(MenSfx.ButtonClick);
            }
        }
        
    }
    
    private void OnUnselect()
    {
        // Return to hover color
        if (cursorRenderer != null && wasHovering)
        {
            cursorRenderer.material.SetColor(ShaderColor, hoverColor);
            cursorRenderer.material.SetFloat(ShaderRadialScale, 0.2f);
        }
        
    }
    
    /// <summary>
    /// Updates the cursor position during hover (called from external ray interactor)
    /// </summary>
    public void UpdateCursorPosition(Vector3 position, Quaternion rotation)
    {
        if (cursorInstance != null && cursorInstance.activeSelf)
        {
            cursorInstance.transform.position = position + rotation * Vector3.forward * cursorOffset;
            cursorInstance.transform.rotation = rotation;
            
            // Update ray line
            if (rayLineRenderer != null && rayLineRenderer.enabled)
            {
                rayLineRenderer.SetPosition(1, position);
            }
        }
    }
    
    /// <summary>
    /// Sets the ray origin position for visual
    /// </summary>
    public void SetRayOrigin(Vector3 origin)
    {
        if (rayLineRenderer != null && rayLineRenderer.enabled)
        {
            rayLineRenderer.SetPosition(0, origin);
        }
    }
}
