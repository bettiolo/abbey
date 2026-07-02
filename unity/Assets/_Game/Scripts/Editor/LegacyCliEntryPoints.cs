namespace Abbey.Editor
{
    /// <summary>
    /// Legacy -executeMethod entry points. Pre-existing tooling contracts
    /// (tools/capture_unity_screenshot.py, docs/RUNNING_ON_MAC.md) invoke
    /// Abbey.Editor.* while the editor assembly's real namespace is
    /// Abbey.EditorTools — these one-line shims keep both spellings working.
    /// </summary>
    public static class ScreenshotCapture
    {
        public static void CaptureFromCLI()
        {
            EditorTools.ScreenshotCapture.CaptureFromCLI();
        }
    }

    public static class Builds
    {
        public static void BuildMacOS()
        {
            EditorTools.Builds.BuildMacOS();
        }
    }
}
