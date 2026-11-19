using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Copies raw audio files from Assets/StreamingAudio~ into the built player's StreamingAssets/Audio folder
/// so runtime playback works without invoking Unity's AudioImporter (FSBTool64.exe) in the Editor.
/// </summary>
public class CopyStreamingAudioOnBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string sourceDir = Path.Combine(Application.dataPath, "StreamingAudio~");
        if (!Directory.Exists(sourceDir)) return;

        string targetDir = ResolveStreamingAudioTargetDir(report.summary.platform, report.summary.outputPath);
        if (string.IsNullOrEmpty(targetDir)) return;

        Directory.CreateDirectory(targetDir);
        foreach (string srcFile in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(srcFile);
            if (fileName.EndsWith(".meta")) continue;
            File.Copy(srcFile, Path.Combine(targetDir, fileName), overwrite: true);
        }

        Debug.Log($"[CopyStreamingAudioOnBuild] Copied raw audio files to: {targetDir}");
    }

    private static string ResolveStreamingAudioTargetDir(BuildTarget target, string outputPath)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
            case BuildTarget.StandaloneLinux64:
            {
                string dir = Path.GetDirectoryName(outputPath);
                string exeName = Path.GetFileNameWithoutExtension(outputPath);
                return Path.Combine(dir ?? "", exeName + "_Data", "StreamingAssets", "Audio");
            }
            case BuildTarget.StandaloneOSX:
                return Path.Combine(outputPath, "Contents", "Resources", "Data", "StreamingAssets", "Audio");
            case BuildTarget.WebGL:
                return Path.Combine(outputPath, "StreamingAssets", "Audio");
            default:
                return null;
        }
    }
}
