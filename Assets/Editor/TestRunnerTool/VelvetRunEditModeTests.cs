using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Corre los tests EditMode y escribe el resultado en Logs/velvet_tests.txt.</summary>
public static class VelvetRunEditModeTests
{
    [MenuItem("Tools/Velvet/Correr tests EditMode")]
    public static void Run()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
    }

    class Callbacks : ICallbacks
    {
        readonly StringBuilder sb = new StringBuilder();
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
                sb.AppendLine($"{result.ResultState,-10} {result.Test.FullName} {result.Message}");
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            sb.Insert(0, $"Pasaron {result.PassCount}, fallaron {result.FailCount}, omitidos {result.SkipCount}\n");
            System.IO.File.WriteAllText("Logs/velvet_tests.txt", sb.ToString());
            Debug.Log("[Tests] " + sb);
        }
    }
}
