using System;
using System.Runtime.InteropServices;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// P1-6: conservative low-spec detection for Win7 tills. This only changes
    /// rendering cost; it does not change business operations or data rules.
    /// </summary>
    public static class LowSpecMode
    {
        private static readonly bool DetectedLowSpec = DetectLowSpec();

        public static bool IsLowSpec
        {
            get { return DetectedLowSpec; }
        }

        public static void ApplyToAntdUi()
        {
            if (!IsLowSpec)
                return;

            AntdUI.Config.Animation = false;
            AntdUI.Config.TextRenderingHighQuality = false;
            AntdUI.Config.ShadowEnabled = false;
        }

        private static bool DetectLowSpec()
        {
            if (Environment.ProcessorCount <= 2)
                return true;

            var memory = new MemoryStatusEx();
            memory.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            try
            {
                if (GlobalMemoryStatusEx(ref memory) &&
                    memory.TotalPhysicalMemory < 3UL * 1024UL * 1024UL * 1024UL)
                    return true;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            bool compositionEnabled;
            if (TryGetCompositionEnabled(out compositionEnabled) && !compositionEnabled)
                return true;

            return false;
        }

        private static bool TryGetCompositionEnabled(out bool enabled)
        {
            enabled = true;
            try
            {
                return DwmIsCompositionEnabled(out enabled) == 0;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysicalMemory;
            public ulong AvailablePhysicalMemory;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    }
}
