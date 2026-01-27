// https://forum.unity.com/threads/build-date-or-version-from-code.59134/
using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class BuildInfoProcessor : IPreprocessBuildWithReport {
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) {
        string code =
            "using System;\n" +
            "using System.Globalization;\n" +
            "public static class BuildInfo {\n" +
                $"public static readonly DateTime BUILD_TIME = DateTime.ParseExact(\"{DateTime.UtcNow:O}\", DateTimeStyles.RoundtripKind);\n" +
            "}";

        File.WriteAllText("Assets/Scripts/BuildInfo.cs", code);
    }
}