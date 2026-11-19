using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Runs the EditMode tests and writes the results to Logs/velvet_tests.txt.</summary>
public static class VelvetRunEditModeTests
{
    [MenuItem("Tools/Velvet/Run EditMode Tests")]
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
            sb.Insert(0, $"Passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}\n");
            System.IO.File.WriteAllText("Logs/velvet_tests.txt", sb.ToString());
            Debug.Log("[Tests] " + sb);
        }
    }
}
