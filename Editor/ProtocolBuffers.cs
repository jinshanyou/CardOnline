using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ProtocolBuffers
{

    private static string PROTO_PATH = Application.dataPath + "/Proto";
    private static string PROTO_OUT_PATH = Application.dataPath + "/Scripts/NetWork/ProtoGenerated";
    private static string PROTO_EXE_PATH = Application.dataPath + "/Tools/protoc/protoc.exe";

    [MenuItem("protoBufTool/生成cs文件")]
    private static void GenerateCSharp()
    {
        DirectoryInfo directoryInfos = Directory.CreateDirectory(PROTO_PATH);
        FileInfo[] files = directoryInfos.GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Extension == ".proto")
            {
                Process cmd = new Process();
                cmd.StartInfo.FileName = PROTO_EXE_PATH;
                cmd.StartInfo.Arguments = $"-I\"{PROTO_PATH}\" --csharp_out=\"{PROTO_OUT_PATH}\" \"{files[i].FullName}\"";
                //cmd.StartInfo.Arguments = $"-I{PROTO_PATH} --csharp_out={PROTO_OUT_PATH} {files[i]}";
                // 3. 配置进程窗口相关表现
                cmd.StartInfo.UseShellExecute = false;         // 必须为false，才能使用后面的重定向拦截报错信息
                cmd.StartInfo.CreateNoWindow = true;           // 不显示原本黑色的 CMD 窗口，静默执行
                cmd.StartInfo.RedirectStandardError = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.Start();
                string error = cmd.StandardError.ReadToEnd();
                string output = cmd.StandardOutput.ReadToEnd();
                cmd.WaitForExit();

                if (cmd.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"protobuf 生成失败:\n{error}");
                }
                else
                {
                    UnityEngine.Debug.Log($"protobuf 生成成功:\n{output}");
                    AssetDatabase.Refresh();
                }

            }
        }
    }

}
