using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // URP kullanmiyorsan bu satiri ve RenderTo() icindeki URP blogunu sil

/// <summary>
/// Play mode'da bir tusa basinca ekran cozunurlugunden bagimsiz,
/// yuksek cozunurluklu PNG alir. Unity Recorder'a gerek yok.
/// Yeni Input System surumu. Kameranin ustune veya bos bir GameObject'e ekle.
/// </summary>
[AddComponentMenu("Tools/Hi-Res Screenshot")]
public class HiResScreenshot : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Yakalama tusu. Input System'in Key enum'i.")]
    // Alan adi captureKey -> screenshotKey olarak degisti: eski KeyCode serialize edilmis
    // ham int deger (F12 = 293) yeni Key enum'ina tasinip gecersiz kalmasin diye.
    public Key screenshotKey = Key.F9;

    [Header("Mode")]
    [Tooltip("ScreenComposite: ekranda gorunenin birebir aynisi (Overlay UI dahil), multiplier kadar buyutulmus. " +
             "CameraRender: sadece kamerayi izole render eder (UI yok), saydam arka plan destegi var.")]
    public CaptureMode captureMode = CaptureMode.ScreenComposite;

    public enum CaptureMode { ScreenComposite, CameraRender }

    [Header("Capture")]
    [Tooltip("Bos birakilirsa Camera.main kullanilir. Sadece CameraRender modunda kullanilir.")]
    public Camera targetCamera;

    [Tooltip("Ekran cozunurlugunun kac kati alinacak")]
    [Range(1, 8)] public int multiplier = 4;

    [Tooltip("0'dan buyukse multiplier yerine bu sabit cozunurluk kullanilir")]
    public int fixedWidth = 0;
    public int fixedHeight = 0;

    [Tooltip("Kenar yumusatma. 1 = kapali")]
    [Range(1, 8)] public int msaaSamples = 8;

    [Tooltip("Arka plani saydam yapar (store/marketing gorselleri icin). Sadece CameraRender modunda.")]
    public bool transparentBackground = false;

    [Header("Output")]
    public string folderName = "Screenshots";
    public string filePrefix = "shot";

    void Reset()
    {
        targetCamera = GetComponent<Camera>();
    }

    void OnValidate()
    {
        // Gecersiz/serialize'dan tasinmis deger gelirse guvenli varsayilana don.
        if (!Enum.IsDefined(typeof(Key), screenshotKey))
            screenshotKey = Key.F9;
    }

    void Awake()
    {
        // Reset() sadece editorde, component eklenirken calisir.
        // Build'de ve Inspector alani bos kalmissa burasi devreye girer.
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

void Update()
{
    var kb = Keyboard.current;
    if (kb == null) return;

    // Serialize edilmis gecersiz deger korumasi (Key enum'inda Count uyesi yok)
    if (screenshotKey == Key.None || !Enum.IsDefined(typeof(Key), screenshotKey)) return;

    if (kb[screenshotKey].wasPressedThisFrame)
        Capture();
}

    [ContextMenu("Capture Now")]
    public void Capture()
    {
        if (captureMode == CaptureMode.ScreenComposite)
        {
            // Kamera/RenderTexture gerekmez: ekranin composited karesini alacagiz.
            StartCoroutine(ScreenCompositeRoutine());
            return;
        }

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("[HiResScreenshot] Kamera bulunamadi.");
            return;
        }

        int w = fixedWidth  > 0 ? fixedWidth  : Screen.width  * multiplier;
        int h = fixedHeight > 0 ? fixedHeight : Screen.height * multiplier;

        // GPU texture limitini asma
        int max = SystemInfo.maxTextureSize;
        if (w > max || h > max)
        {
            float s = Mathf.Min(max / (float)w, max / (float)h);
            w = Mathf.Max(1, Mathf.FloorToInt(w * s));
            h = Mathf.Max(1, Mathf.FloorToInt(h * s));
            Debug.LogWarning($"[HiResScreenshot] Cozunurluk {max}px limitine gore {w}x{h} olarak kirpildi.");
        }

        StartCoroutine(CaptureRoutine(cam, w, h));
    }

    /// <summary>
    /// Ekranda gorunenin birebir aynisini (tum kameralar + Screen Space Overlay UI dahil)
    /// multiplier kadar buyuterek yakalar. WYSIWYG marketing gorselleri icin.
    /// </summary>
    IEnumerator ScreenCompositeRoutine()
    {
        // Frame tam bitsin ki UI/post-processing dahil composited kare hazir olsun.
        yield return new WaitForEndOfFrame();

        int sup = Mathf.Max(1, multiplier);
        var shot = ScreenCapture.CaptureScreenshotAsTexture(sup);
        int w = shot.width, h = shot.height;

        string path = BuildPath(w, h);
        byte[] png = shot.EncodeToPNG();  // format-guvenli; tek kare icin ana thread'de sorun degil
        Destroy(shot);

        WritePngAsync(png, path);
    }

    IEnumerator CaptureRoutine(Camera cam, int w, int h)
    {
        // Frame tam bitsin ki sahne/post-processing eksik yakalanmasin
        yield return new WaitForEndOfFrame();

        var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = Mathf.Max(1, msaaSamples),
            sRGB = true,
            useMipMap = false,
            autoGenerateMips = false
        };

        var msaaRT = new RenderTexture(desc);
        msaaRT.Create();

        var prevTarget = cam.targetTexture;
        var prevClear  = cam.clearFlags;
        var prevBg     = cam.backgroundColor;

        if (transparentBackground)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        RenderTo(cam, msaaRT);

        cam.targetTexture   = prevTarget;
        cam.clearFlags      = prevClear;
        cam.backgroundColor = prevBg;

        // MSAA'yi resolve et (async readback MSAA target'tan okuyamaz)
        RenderTexture readRT = msaaRT;
        if (desc.msaaSamples > 1)
        {
            var rdesc = desc;
            rdesc.msaaSamples = 1;
            rdesc.depthBufferBits = 0;
            readRT = new RenderTexture(rdesc);
            readRT.Create();
            Graphics.Blit(msaaRT, readRT);
        }

        string path = BuildPath(w, h);

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            var a = msaaRT;
            var b = readRT;
            AsyncGPUReadback.Request(readRT, 0, TextureFormat.RGBA32, request =>
            {
                if (!request.hasError)
                    SaveAsync(request.GetData<byte>().ToArray(), w, h, path);
                else
                    Debug.LogError("[HiResScreenshot] GPU readback basarisiz.");

                Cleanup(a, b);
            });
        }
        else
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = readRT;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prevActive;

            var bytes = tex.GetRawTextureData<byte>().ToArray();
            Destroy(tex);
            Cleanup(msaaRT, readRT);

            SaveAsync(bytes, w, h, path);
        }
    }

    static void RenderTo(Camera cam, RenderTexture rt)
    {
        // Unity 6 + URP: resmi yol. SubmitRenderRequest post-processing dahil dogru calisir.
        if (RenderPipelineManager.currentPipeline != null)
        {
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req))
            {
                RenderPipeline.SubmitRenderRequest(cam, req);
                return;
            }
        }

        // Built-in RP veya fallback
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
    }

    static void Cleanup(RenderTexture a, RenderTexture b)
    {
        if (b != null && b != a) { b.Release(); Destroy(b); }
        if (a != null) { a.Release(); Destroy(a); }
    }

    string BuildPath(int w, int h)
    {
#if UNITY_EDITOR
        string root = Path.Combine(Application.dataPath, "..", folderName);
#else
        string root = Path.Combine(Application.persistentDataPath, folderName);
#endif
        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        string name = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{w}x{h}.png";
        return Path.Combine(root, name);
    }

    /// <summary>Hazir PNG byte'larini ana thread disinda diske yazar.</summary>
    static void WritePngAsync(byte[] png, string path)
    {
        Task.Run(() =>
        {
            try
            {
                File.WriteAllBytes(path, png);
                Debug.Log($"[HiResScreenshot] Kaydedildi: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HiResScreenshot] Kaydetme hatasi: {e}");
            }
        });
    }

    /// <summary>PNG encode + disk yazma isini ana thread disinda yapar, oyunda takilma olmaz.</summary>
    static void SaveAsync(byte[] rgba, int w, int h, string path)
    {
        Task.Run(() =>
        {
            try
            {
                byte[] png = ImageConversion.EncodeArrayToPNG(
                    rgba, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h);
                File.WriteAllBytes(path, png);
                Debug.Log($"[HiResScreenshot] Kaydedildi: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HiResScreenshot] Kaydetme hatasi: {e}");
            }
        });
    }
}
