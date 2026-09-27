using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

public static class ConfigurePhotonSdk
{
    public static object configure() //기존 App ID를 건드리지 않고 구형 Android 플러그인 메타 갱신
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var pluginRoot = "Assets/Photon/PhotonVoice/PhotonVoiceLibs/Android/libs/"; //Voice 네이티브 플러그인 위치
        var updated = 0; //실제로 변환한 메타 수
        foreach (var name in new[] { "libopus_egpv.so.meta", "libwebrtc-audio.so.meta" }) //확인할 두 플러그인 이름
        {
            var targetPath = pluginRoot + "armeabi-v7a/" + name; //기존 ARMv7 메타
            var templatePath = pluginRoot + "arm64-v8a/" + name; //같은 SDK의 최신 Importer 형식
            if (!File.Exists(targetPath) || !File.Exists(templatePath))
                throw new FileNotFoundException("Install Voice 2.63 Realtime5 before configuring the SDK.");
            var original = File.ReadAllText(targetPath); //기존 메타 내용
            if (!Regex.IsMatch(original, @"(?m)^  serializedVersion: 1\r?$")) continue;
            var guid = Regex.Match(original, @"(?m)^guid: ([0-9a-f]+)\r?$").Groups[1].Value; //참조를 보존할 기존 GUID
            if (guid.Length != 32) throw new InvalidDataException("Unexpected plugin GUID.");
            var template = File.ReadAllText(templatePath); //현재 Importer 형식의 메타
            if (!Regex.IsMatch(template, @"(?m)^  serializedVersion: 2\r?$"))
                throw new InvalidDataException("Unexpected plugin importer version.");
            var converted = Regex.Replace(template, @"(?m)^guid: [0-9a-f]+\r?$", "guid: " + guid).Replace("CPU: ARM64", "CPU: ARMv7"); //GUID와 CPU를 보존한 변환 결과
            File.WriteAllText(targetPath, converted);
            updated++;
        }
        if (updated > 0) AssetDatabase.Refresh();
        return new { importerMetadataUpdated = updated, appSettingsUnchanged = true };
    }
}
