using System.Linq;
using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class LocalizationStatusUI : MonoBehaviour
{
    [SerializeField] private float lostAfterSeconds = 10f;

    private Localizer localizer;
    private ImmersalSDK sdk;
    private bool sdkReady;
    private int attempts;
    private int successes;
    private int failures;
    private string lastMapName;
    private float lastSuccessTime = -1f;
    private GUIStyle style;

    private void Start()
    {
        sdk = ImmersalSDK.Instance;
        localizer = FindFirstObjectByType<Localizer>();

        if (sdk != null)
        {
            sdkReady = sdk.IsReady;
            sdk.OnInitializationComplete.AddListener(() => sdkReady = true);
        }

        if (localizer != null)
        {
            localizer.OnLocalizationResult.AddListener(_ => attempts++);
            localizer.OnSuccessfulLocalizations.AddListener(OnSuccess);
            localizer.OnFailedLocalizations.AddListener(() => failures++);
        }
    }

    private void OnSuccess(int[] mapIds)
    {
        successes++;
        lastSuccessTime = Time.time;
        var map = FindObjectsByType<XRMap>(FindObjectsSortMode.None).FirstOrDefault(m => mapIds.Contains(m.mapId));
        lastMapName = map != null ? $"{map.mapName} ({map.mapId})" : string.Join(", ", mapIds);
    }

    private string StatusLine()
    {
        if (sdk == null || localizer == null) return "<color=red>Immersal introuvable dans la scène</color>";
        if (!sdkReady) return "Initialisation d'Immersal…";
        if (ARSession.state != ARSessionState.SessionTracking) return $"Suivi AR : {ARSession.state}\nBouge doucement le téléphone";
        if (lastSuccessTime < 0f) return "<color=yellow>Recherche…</color>\nVise les zones scannées";

        float since = Time.time - lastSuccessTime;
        return since > lostAfterSeconds
            ? $"<color=orange>Perdu, recherche…</color>\nDernière localisation il y a {since:0}s"
            : $"<color=lime>Localisé ✓</color>\n{lastMapName}";
    }

    private void OnGUI()
    {
        style ??= new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.025f),
            alignment = TextAnchor.UpperLeft,
            richText = true,
            wordWrap = true,
            padding = new RectOffset(24, 24, 18, 18)
        };

        var safe = Screen.safeArea;
        var rect = new Rect(safe.x + 20, Screen.height - safe.yMax + 20, safe.width - 40, style.fontSize * 6.5f);
        GUI.Box(rect, $"{StatusLine()}\n<size={style.fontSize * 3 / 4}>Tentatives {attempts} · Réussites {successes} · Échecs {failures}</size>", style);
    }
}
