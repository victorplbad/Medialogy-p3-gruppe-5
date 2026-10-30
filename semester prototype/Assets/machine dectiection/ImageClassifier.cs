
using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

public class ImageClassifier : MonoBehaviour
{
    [Header("Python Configuration")]
    public string pythonPath =
        @"C:\YourPythonEnvironment\Scripts\python.exe";

    public string pythonScript =
        @"C:\YourUnityProject\Python\classify.py";

    [Header("Image")]
    public string imagePath =
        @"C:\YourUnityProject\Assets\Images\bike.jpg";

    public async void ClassifyImage()
    {
        if (!File.Exists(imagePath))
        {
            Debug.LogError("Image not found: " + imagePath);
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();

        startInfo.FileName = pythonPath;

        startInfo.Arguments =
            "\"" + pythonScript + "\" \"" + imagePath + "\"";

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;

            process.Start();

            Task<string> outputTask =
                process.StandardOutput.ReadToEndAsync();

            Task<string> errorTask =
                process.StandardError.ReadToEndAsync();

            await Task.Run(() => process.WaitForExit());

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                Debug.LogError("Python error: " + error);
                return;
            }

            string[] result = output.Trim().Split('|');

            if (result.Length == 2)
            {
                string detectedClass = result[0];

                float confidence =
                    float.Parse(
                        result[1],
                        System.Globalization.CultureInfo.InvariantCulture
                    );

                Debug.Log("Detected: " + detectedClass);

                Debug.Log(
                    "Confidence: " +
                    (confidence * 100f).ToString("F2") + "%"
                );

                // Connect your game logic here
            }
            else
            {
                Debug.LogError("Invalid Python response: " + output);
            }
        }
    }
}
