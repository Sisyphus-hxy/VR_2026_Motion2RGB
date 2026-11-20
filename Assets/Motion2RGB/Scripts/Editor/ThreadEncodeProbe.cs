using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Motion2RGB.EditorTools
{
    /// <summary>One-off check: is ImageConversion.EncodeArrayToPNG callable off the main thread?</summary>
    public static class ThreadEncodeProbe
    {
        public static void Run()
        {
            const int w = 64, h = 48;
            var raw = new byte[w * h * 4];
            for (int i = 0; i < raw.Length; i++) raw[i] = (byte)(i * 7);

            string mainResult = "n/a";
            try
            {
                var png = ImageConversion.EncodeArrayToPNG(raw, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)w, (uint)h);
                mainResult = "main ok, " + png.Length + " bytes";
            }
            catch (Exception e)
            {
                mainResult = "main FAILED: " + e.GetType().Name + " " + e.Message;
            }
            Debug.Log("[Probe] " + mainResult);

            string workerResult = "not run";
            var thread = new Thread(() =>
            {
                try
                {
                    var png = ImageConversion.EncodeArrayToPNG(raw, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)w, (uint)h);
                    workerResult = "worker ok, " + png.Length + " bytes";
                }
                catch (Exception e)
                {
                    workerResult = "worker FAILED: " + e.GetType().Name + " " + e.Message;
                }
            });
            thread.IsBackground = true;
            thread.Start();
            thread.Join(5000);
            Debug.Log("[Probe] " + workerResult);
            EditorApplication.Exit(workerResult.StartsWith("worker ok") ? 0 : 1);
        }
    }
}
