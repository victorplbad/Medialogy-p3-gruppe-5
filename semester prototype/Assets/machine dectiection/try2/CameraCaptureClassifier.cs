using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;

/// <summary>
/// Webcam/phone capture -> freeze for one second -> classify using an ONNX model.
/// Attach to a scene GameObject and assign the Inspector references.
/// </summary>
public class CameraCaptureClassifier : MonoBehaviour
{

    ////////////////////////////////////////////////////////////////////////
    public CollectRessourses collectRessourses;





    /////////////////////////////////////////////////////////////
    [Header("Model")]
    [SerializeField] private ModelAsset modelAsset;
    [SerializeField] private TextAsset labelsFile;
    [SerializeField] private BackendType backend = BackendType.GPUCompute;

    [Header("Model input")]
    [SerializeField] private int imageWidth = 224;
    [SerializeField] private int imageHeight = 224;
    [SerializeField] private InputNormalization normalization = InputNormalization.MinusOneToOne;

    [Header("Camera")]
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;
    [SerializeField] private int requestedFPS = 30;
    [SerializeField] private string preferredCameraName = "";

    [Header("UI")]
    [SerializeField] private RawImage livePreview;
    [SerializeField] private RawImage frozenPreview;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button analyzeButton;
    [SerializeField] private Button retakeButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text predictionText;
    [SerializeField] private TMP_Text confidenceText;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float freezeSeconds = 1f;

    private WebCamTexture webcam;
    private Texture2D capturedTexture;
    private Worker worker;
    private string[] labels = Array.Empty<string>();
    private bool isBusy;

    private enum InputNormalization
    {
        RawZeroTo255,
        ZeroToOne,
        MinusOneToOne
    }

    private void Start()
    {
        LoadLabels();

        if (modelAsset == null)
        {
            SetStatus("Assign model.onnx in the Inspector.");
            enabled = false;
            return;
        }

        try
        {
            Model runtimeModel = ModelLoader.Load(modelAsset);
            worker = new Worker(runtimeModel, backend);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetStatus("Could not load ONNX model. Check Console and model import.");
            enabled = false;
            return;
        }

        if (captureButton != null) captureButton.onClick.AddListener(Capture);
        if (analyzeButton != null) analyzeButton.onClick.AddListener(AnalyzeCapturedImage);
        if (retakeButton != null) retakeButton.onClick.AddListener(Retake);

        if (analyzeButton != null) analyzeButton.interactable = false;
        if (retakeButton != null) retakeButton.interactable = false;
        if (frozenPreview != null) frozenPreview.gameObject.SetActive(false);

        StartCoroutine(StartCamera());
    }
        
    private IEnumerator StartCamera()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            SetStatus("Camera permission was not granted.");
            yield break;
        }
#endif

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            SetStatus("No camera found. Connect a webcam or check phone permissions.");
            yield break;
        }

        string selectedName = devices[0].name;
        if (!string.IsNullOrWhiteSpace(preferredCameraName))
        {
            foreach (var device in devices)
            {
                if (device.name.IndexOf(preferredCameraName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selectedName = device.name;
                    break;
                }
            }
        }

        webcam = new WebCamTexture(selectedName, requestedWidth, requestedHeight, requestedFPS);
        if (livePreview != null) livePreview.texture = webcam;
        webcam.Play();

        float timeout = 10f;
        while (timeout > 0f && !webcam.didUpdateThisFrame)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (webcam.width <= 16 || webcam.height <= 16)
        {
            SetStatus("Camera did not start. Check permissions/device selection.");
            yield break;
        }

        SetStatus("Camera ready. Press Capture.");
    }

    private void Capture()
    {
        if (isBusy || webcam == null || !webcam.isPlaying || webcam.width <= 16)
        {
            SetStatus("Camera is not ready yet.");
            return;
        }

        if (capturedTexture != null) Destroy(capturedTexture);

        capturedTexture = new Texture2D(webcam.width, webcam.height, TextureFormat.RGBA32, false);
        capturedTexture.SetPixels32(webcam.GetPixels32());
        capturedTexture.Apply(false, false);

        if (frozenPreview != null)
        {
            frozenPreview.texture = capturedTexture;
            frozenPreview.gameObject.SetActive(true);
        }
        if (livePreview != null) livePreview.gameObject.SetActive(false);

        if (captureButton != null) captureButton.interactable = false;
        if (analyzeButton != null) analyzeButton.interactable = false;
        if (retakeButton != null) retakeButton.interactable = true;

        predictionText?.SetText("Analyzing soon…");
        confidenceText?.SetText("");
        StartCoroutine(FreezeThenAnalyze());
    }

    private IEnumerator FreezeThenAnalyze()
    {
        SetStatus($"Image frozen for {freezeSeconds:0.0} second(s)...");
        yield return new WaitForSeconds(freezeSeconds);
        AnalyzeCapturedImage();
    }

    private void AnalyzeCapturedImage()
    {
        if (isBusy || capturedTexture == null || worker == null) return;
        StartCoroutine(RunClassification());
    }

    private IEnumerator RunClassification()
    {
        isBusy = true;
        SetStatus("Running image recognition...");
        if (captureButton != null) captureButton.interactable = false;
        if (analyzeButton != null) analyzeButton.interactable = false;

        // Resize the captured frame to the model's expected 224x224 input.
        Texture2D resized = ResizeTexture(capturedTexture, imageWidth, imageHeight);
        if (resized == null)
        {
            SetStatus("Could not resize captured image.");
            isBusy = false;
            yield break;
        }

        Color32[] pixels = resized.GetPixels32();
        float[] inputData = new float[imageWidth * imageHeight * 3];

        // ONNX input is expected as NHWC: [1, height, width, RGB].
        for (int i = 0; i < pixels.Length; i++)
        {
            inputData[i * 3 + 0] = Normalize(pixels[i].r);
            inputData[i * 3 + 1] = Normalize(pixels[i].g);
            inputData[i * 3 + 2] = Normalize(pixels[i].b);
        }

        Destroy(resized);

        try
        {
            using (var inputTensor = new Tensor<float>(
                new TensorShape(1, imageHeight, imageWidth, 3), inputData))
            {
                worker.Schedule(inputTensor);
                Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
                if (outputTensor == null)
                    throw new Exception("Model output is not a float tensor.");

                // DownloadToArray waits for inference to finish and returns a CPU copy.
                float[] scores = outputTensor.DownloadToArray();
                DisplayPrediction(scores);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            SetStatus("Inference failed. Check Console for the model/backend error.");
        }

        isBusy = false;
        if (captureButton != null) captureButton.interactable = true;
        if (analyzeButton != null) analyzeButton.interactable = true;
    }

    private float Normalize(byte value)
    {
        switch (normalization)
        {
            case InputNormalization.RawZeroTo255:
                return value;
            case InputNormalization.ZeroToOne:
                return value / 255f;
            default:
                return (value / 127.5f) - 1f;
        }
    }

    private void DisplayPrediction(float[] scores)
    {
        if (scores == null || scores.Length == 0)
        {
            SetStatus("The model returned no prediction values.");
            return;
        }

        if (labels.Length != scores.Length)
        {
            SetStatus($"Model returned {scores.Length} scores but labels file has {labels.Length} labels. Check label order/count.");
            Debug.LogError($"Output count={scores.Length}, label count={labels.Length}. Scores: {string.Join(", ", scores)}");
            return;
        }

        int bestIndex = 0;
        for (int i = 1; i < scores.Length; i++)
            if (scores[i] > scores[bestIndex]) bestIndex = i;

        float confidencePercent = scores[bestIndex] * 100f;
        string label = labels[bestIndex];

        if (predictionText != null) predictionText.text = label;
        if (confidenceText != null) confidenceText.text = $"{confidencePercent:0.00}%";
        SetStatus("Prediction complete.");

        // Log every class score for debugging.
        for (int i = 0; i < scores.Length; i++)
            Debug.Log($"{labels[i]}: {scores[i] * 100f:0.00}%");

        /////////////////////////////////////////////////////////////////
        //Debug.Log(label);
        //Debug.Log(confidencePercent);

        

        if (label == "bike")
        {
            collectRessourses.ironRes();
        }
        else if (label == "bottle")
        {
            collectRessourses.plastRes();
        }
        else if (label == "phone")
        {
            collectRessourses.electronicRes();
        }

        /////////////////////////////////////////////////////////////////
    }

    private void LoadLabels()
    {
        if (labelsFile == null)
        {
            SetStatus("Assign labels.txt in the Inspector.");
            return;
        }

        var parsed = new SortedDictionary<int, string>();
        string[] lines = labelsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            int separator = line.IndexOf(' ');
            if (separator < 0) separator = line.IndexOf('\t');
            if (separator <= 0) continue;

            if (int.TryParse(line.Substring(0, separator).Trim(), out int index))
                parsed[index] = line.Substring(separator + 1).Trim();
        }

        if (parsed.Count == 0)
        {
            labels = lines.Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        }
        else
        {
            labels = parsed.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
        }
    }

    private Texture2D ResizeTexture(Texture source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply(false, false);
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private void Retake()
    {
        if (isBusy) return;

        if (capturedTexture != null)
        {
            Destroy(capturedTexture);
            capturedTexture = null;
        }

        if (frozenPreview != null) frozenPreview.gameObject.SetActive(false);
        if (livePreview != null) livePreview.gameObject.SetActive(true);

        if (captureButton != null) captureButton.interactable = true;
        if (analyzeButton != null) analyzeButton.interactable = false;
        if (retakeButton != null) retakeButton.interactable = false;

        if (predictionText != null) predictionText.text = "Waiting for image";
        if (confidenceText != null) confidenceText.text = "";
        SetStatus("Camera ready. Press Capture.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log(message);
    }

    private void OnDestroy()
    {
        if (captureButton != null) captureButton.onClick.RemoveListener(Capture);
        if (analyzeButton != null) analyzeButton.onClick.RemoveListener(AnalyzeCapturedImage);
        if (retakeButton != null) retakeButton.onClick.RemoveListener(Retake);

        if (webcam != null && webcam.isPlaying) webcam.Stop();
        worker?.Dispose();

        if (capturedTexture != null) Destroy(capturedTexture);
    }
}
