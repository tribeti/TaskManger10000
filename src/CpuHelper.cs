using System.Runtime.InteropServices;

namespace src;

public class CpuHelper
{
    // Source - https://stackoverflow.com/a/63744912
    // Posted by Jorma Rebane
    // Retrieved 2026-02-16, License - CC BY-SA 4.0

    [StructLayout(LayoutKind.Sequential)]
    struct CACHE_DESCRIPTOR
    {
        public byte Level;
        public byte Associativity;
        public ushort LineSize;
        public uint Size;
        public uint Type;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION
    {
        [FieldOffset(0)] public byte ProcessorCore;
        [FieldOffset(0)] public uint NumaNode;
        [FieldOffset(0)] public CACHE_DESCRIPTOR Cache;
        [FieldOffset(0)] private UInt64 Reserved1;
        [FieldOffset(8)] private UInt64 Reserved2;
    }

    public enum LOGICAL_PROCESSOR_RELATIONSHIP
    {
        RelationProcessorCore,
        RelationNumaNode,
        RelationCache,
        RelationProcessorPackage,
        RelationGroup,
        RelationAll = 0xffff
    }

    struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
    {
        public UIntPtr ProcessorMask;
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION ProcessorInformation;
    }

    [DllImport("kernel32.dll")]
    static extern unsafe bool GetLogicalProcessorInformation(SYSTEM_LOGICAL_PROCESSOR_INFORMATION* buffer, out int bufferSize);

    static unsafe int GetProcessorCoreCount()
    {
        GetLogicalProcessorInformation(null, out int bufferSize);
        int numEntries = bufferSize / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
        var coreInfo = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[numEntries];

        fixed (SYSTEM_LOGICAL_PROCESSOR_INFORMATION* pCoreInfo = coreInfo)
        {
            GetLogicalProcessorInformation(pCoreInfo, out bufferSize);
            int cores = 0;
            for (int i = 0; i < numEntries; ++i)
            {
                ref SYSTEM_LOGICAL_PROCESSOR_INFORMATION info = ref pCoreInfo[i];
                if (info.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                    ++cores;
            }
            return cores > 0 ? cores : 1;
        }
    }

    public static readonly int NumPhysicalCores = GetProcessorCoreCount();
}
