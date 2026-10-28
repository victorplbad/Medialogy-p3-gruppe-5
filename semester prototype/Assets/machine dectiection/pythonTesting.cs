using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;

public class pythonTesting : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public class EnsureNaming
    {
        [MenuItem("Python/Ensure Naming")]
        static void RunEnsureNaming()
        {
            PythonRunner.RunFile($"{Application.dataPath}/ensure_naming.py");
        }
    }

}
